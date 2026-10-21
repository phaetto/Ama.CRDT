namespace Ama.CRDT.IntegrationTests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Decorators;
using Ama.CRDT.Services.Providers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

/// <summary>
/// A centralized manager to store clock skews per replica for simulating network time drifts.
/// </summary>
public sealed class ClockSkewManager
{
    public IDictionary<string, TimeSpan> Skews { get; } = new Dictionary<string, TimeSpan>();
}

/// <summary>
/// A naive custom timestamp provider that allows simulated clock skewing per replica.
/// This perfectly demonstrates the vulnerability of pure wall-clock time in distributed systems.
/// </summary>
public sealed class SkewedTimestampProvider : ICrdtTimestampProvider
{
    private readonly ReplicaContext replicaContext;
    private readonly ClockSkewManager skewManager;

    public SkewedTimestampProvider(ReplicaContext replicaContext, ClockSkewManager skewManager)
    {
        ArgumentNullException.ThrowIfNull(replicaContext);
        ArgumentNullException.ThrowIfNull(skewManager);

        this.replicaContext = replicaContext;
        this.skewManager = skewManager;
    }

    public CrdtTimestamp Now()
    {
        var now = DateTimeOffset.UtcNow;
        if (!string.IsNullOrEmpty(replicaContext.ReplicaId) && skewManager.Skews.TryGetValue(replicaContext.ReplicaId, out var skew))
        {
            now += skew;
        }
        return new CrdtTimestamp(now.ToUnixTimeMilliseconds(), 0, 0, replicaContext.ReplicaId);
    }

    public CrdtTimestamp Create(long value)
    {
        return new CrdtTimestamp(value, 0, 0, replicaContext.ReplicaId);
    }
}

/// <summary>
/// A robust Hybrid Logical Clock (HLC) implementation.
/// It uses physical wall-clock time for Ticks and a logical Counter to resolve sub-millisecond or skewed events.
/// This guarantees timestamps are strictly monotonic and always move forward, safely recovering from skewed clocks.
/// </summary>
public sealed class HlcTimestampProvider : ICrdtTimestampProvider
{
    private readonly ReplicaContext replicaContext;
    private readonly ClockSkewManager skewManager;
    private long maxPhysicalMs = 0;
    private int logicalCounter = 0;
    private readonly object @lock = new object();

    public HlcTimestampProvider(ReplicaContext replicaContext, ClockSkewManager skewManager)
    {
        ArgumentNullException.ThrowIfNull(replicaContext);
        ArgumentNullException.ThrowIfNull(skewManager);

        this.replicaContext = replicaContext;
        this.skewManager = skewManager;
    }

    public CrdtTimestamp Now()
    {
        lock (@lock)
        {
            var wallClock = DateTimeOffset.UtcNow;
            if (!string.IsNullOrEmpty(replicaContext.ReplicaId) && skewManager.Skews.TryGetValue(replicaContext.ReplicaId, out var skew))
            {
                wallClock += skew;
            }
            
            long wallMs = wallClock.ToUnixTimeMilliseconds();

            if (wallMs > maxPhysicalMs)
            {
                maxPhysicalMs = wallMs;
                logicalCounter = 0;
            }
            else
            {
                // Wall clock is behind or equal to our highest seen time. 
                // We advance the logical counter to guarantee strict ordering!
                logicalCounter++;
            }

            return new CrdtTimestamp(maxPhysicalMs, logicalCounter, 0, replicaContext.ReplicaId);
        }
    }

    public CrdtTimestamp Create(long value)
    {
        return new CrdtTimestamp(value, 0, 0, replicaContext.ReplicaId);
    }

    /// <summary>
    /// Observes incoming timestamps from remote replicas and fast-forwards the local clock if necessary.
    /// </summary>
    public void UpdateFromIncoming(CrdtTimestamp incoming)
    {
        lock (@lock)
        {
            if (incoming.Ticks > maxPhysicalMs)
            {
                // We saw a timestamp from the "future" (another node's skewed clock).
                // Fast forward our physical clock to match it.
                maxPhysicalMs = incoming.Ticks;
                logicalCounter = incoming.Counter + 1;
            }
            else if (incoming.Ticks == maxPhysicalMs)
            {
                // Same millisecond, just ensure our counter is strictly greater
                if (incoming.Counter >= logicalCounter)
                {
                    logicalCounter = incoming.Counter + 1;
                }
            }
        }
    }
}

/// <summary>
/// A DI Decorator that intercepts applied patches to feed incoming timestamps to the HLC.
/// </summary>
public sealed class HlcUpdaterApplicatorDecorator : AsyncCrdtApplicatorDecoratorBase
{
    private readonly ICrdtTimestampProvider provider;

    public HlcUpdaterApplicatorDecorator(IAsyncCrdtApplicator inner, DecoratorBehavior behavior, ICrdtTimestampProvider provider) 
        : base(inner, behavior)
    {
        ArgumentNullException.ThrowIfNull(provider);
        this.provider = provider;
    }

    protected override Task OnBeforeApplyAsync<TDoc>(CrdtDocument<TDoc> document, CrdtPatch patch, CancellationToken cancellationToken)
    {
        if (provider is HlcTimestampProvider hlcProvider)
        {
            foreach (var op in patch.Operations)
            {
                hlcProvider.UpdateFromIncoming(op.Timestamp);
            }
        }

        return Task.CompletedTask;
    }
}

public sealed class ClockSkewSimulationTests
{
    [Fact]
    public async Task ClockSkew_LwwResolution_ShouldFailToResolveAndFavorReplicaInTheFuture()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddCrdt()
                .AddCrdtAotContext<NetworkSimulationTestContext>();
        
        services.AddSingleton<ClockSkewManager>();
        services.AddCrdtTimestampProvider<SkewedTimestampProvider>(); // Using the NAIVE provider
        
        var provider = services.BuildServiceProvider();
        var skewManager = provider.GetRequiredService<ClockSkewManager>();
        
        // Replica A's clock is maliciously/accidentally 1 hour in the future.
        skewManager.Skews["A"] = TimeSpan.FromHours(1);
        skewManager.Skews["B"] = TimeSpan.Zero;

        var scopeFactory = provider.GetRequiredService<ICrdtScopeFactory>();
        using var scopeA = scopeFactory.CreateScope("A");
        using var scopeB = scopeFactory.CreateScope("B");

        var docA = InitializeDocument(scopeA);
        var docB = InitializeDocument(scopeB);

        // Act
        // A acts first (in real time) but its clock is +1 hr.
        var resultA = await GenerateSinglePatchAsync(scopeA, docA, d => d with { Title = "Title-From-A" });
        docA = resultA.Document;

        // B acts second (in real time), so normally it would win LWW.
        // But its naive clock is accurate, meaning its timestamp is ~1 hour BEHIND A's timestamp.
        var resultB = await GenerateSinglePatchAsync(scopeB, docB, d => d with { Title = "Title-From-B" });
        docB = resultB.Document;

        // Cross-apply patches to each other
        docA = await ApplyToAsync(scopeA, docA, resultB.Patch);
        docB = await ApplyToAsync(scopeB, docB, resultA.Patch);

        // Assert - The "Poison Pill" effect
        // Both converge mathematically cleanly, BUT A's outdated intent dominated B's newer intent 
        // simply because A's wall-clock was wrong.
        docA.Data.ShouldBe(docB.Data);
        docA.Data!.Title.ShouldBe("Title-From-A");
    }

    [Fact]
    public async Task ClockSkew_HybridLogicalClock_ShouldRecoverFromPoisonPill()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddCrdt()
                .AddCrdtAotContext<NetworkSimulationTestContext>()
                .AddCrdtApplicatorDecorator<HlcUpdaterApplicatorDecorator>(DecoratorBehavior.Before);
        
        services.AddSingleton<ClockSkewManager>();
        services.AddCrdtTimestampProvider<HlcTimestampProvider>(); // Using the ROBUST HLC provider
        
        var provider = services.BuildServiceProvider();
        var skewManager = provider.GetRequiredService<ClockSkewManager>();
        
        // Replica A's clock is maliciously/accidentally 1 hour in the future.
        skewManager.Skews["A"] = TimeSpan.FromHours(1);
        skewManager.Skews["B"] = TimeSpan.Zero;

        var scopeFactory = provider.GetRequiredService<ICrdtScopeFactory>();
        using var scopeA = scopeFactory.CreateScope("A");
        using var scopeB = scopeFactory.CreateScope("B");

        var docA = InitializeDocument(scopeA);
        var docB = InitializeDocument(scopeB);

        // Act
        // 1. Replica A writes with a +1 hour timestamp
        var resultA1 = await GenerateSinglePatchAsync(scopeA, docA, d => d with { Title = "Title-From-A-Skewed" });
        docA = resultA1.Document;

        // 2. Replica B syncs with A.
        // This causes B's Applicator to hit the HlcUpdaterApplicatorDecorator,
        // advancing B's internal HLC clock to +1 hour.
        docB = await ApplyToAsync(scopeB, docB, resultA1.Patch);
        docB.Data!.Title.ShouldBe("Title-From-A-Skewed");

        // 3. Replica B now makes a local edit. 
        // Because of the HLC, B's new timestamp will logically follow A's future time!
        var resultB1 = await GenerateSinglePatchAsync(scopeB, docB, d => d with { Title = "Title-From-B-Causal" });
        docB = resultB1.Document;

        // 4. Replica A syncs with B.
        docA = await ApplyToAsync(scopeA, docA, resultB1.Patch);

        // Assert - Causality Respected!
        // Despite B having a "correct" clock that is 1 hour behind A, B causally followed A.
        // B's update successfully overwrote A's poison pill.
        docA.Data.ShouldBe(docB.Data);
        docA.Data!.Title.ShouldBe("Title-From-B-Causal");
    }

    [Fact]
    public async Task ClockSkew_CausalOperations_ShouldBeImmuneToWallClockSkew()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddCrdt()
                .AddCrdtAotContext<NetworkSimulationTestContext>();
        
        services.AddSingleton<ClockSkewManager>();
        services.AddCrdtTimestampProvider<SkewedTimestampProvider>();
        
        var provider = services.BuildServiceProvider();
        var skewManager = provider.GetRequiredService<ClockSkewManager>();
        
        // A is 1 day in the past, B is 1 day in the future. Total skew of 2 days.
        skewManager.Skews["A"] = TimeSpan.FromDays(-1);
        skewManager.Skews["B"] = TimeSpan.FromDays(1);

        var scopeFactory = provider.GetRequiredService<ICrdtScopeFactory>();
        using var scopeA = scopeFactory.CreateScope("A");
        using var scopeB = scopeFactory.CreateScope("B");

        var docA = InitializeDocument(scopeA);
        var docB = InitializeDocument(scopeB);

        // Act
        // A increments the counter and adds a tag
        var patcherA = scopeA.ServiceProvider.GetRequiredService<IAsyncCrdtPatcher>();
        var opIncA = await patcherA.GenerateOperationAsync(docA, d => d.ViewCount, new IncrementIntent(5));
        var opTagA = await patcherA.GenerateOperationAsync(docA, d => d.Tags, new AddIntent("Tag-A"));
        var patchA = new CrdtPatch(new[] { opIncA, opTagA });
        docA = await ApplyToAsync(scopeA, docA, patchA);

        // B increments the counter and adds a tag
        var patcherB = scopeB.ServiceProvider.GetRequiredService<IAsyncCrdtPatcher>();
        var opIncB = await patcherB.GenerateOperationAsync(docB, d => d.ViewCount, new IncrementIntent(10));
        var opTagB = await patcherB.GenerateOperationAsync(docB, d => d.Tags, new AddIntent("Tag-B"));
        var patchB = new CrdtPatch(new[] { opIncB, opTagB });
        docB = await ApplyToAsync(scopeB, docB, patchB);

        // Cross-apply
        docA = await ApplyToAsync(scopeA, docA, patchB);
        docB = await ApplyToAsync(scopeB, docB, patchA);

        // Assert
        // Convergence should be perfect and causal intent combined accurately
        // because Counter and OR-Set do not rely strictly on wall-clock time for correctness.
        docA.Data.ShouldBe(docB.Data);
        docA.Data!.ViewCount.ShouldBe(15);
        docA.Data.Tags.ShouldContain("Tag-A");
        docA.Data.Tags.ShouldContain("Tag-B");
    }

    private CrdtDocument<SimulationDocument> InitializeDocument(IServiceScope scope)
    {
        var doc = new SimulationDocument();
        var meta = scope.ServiceProvider.GetRequiredService<ICrdtMetadataManager>().Initialize(doc);
        return new CrdtDocument<SimulationDocument>(doc, meta);
    }

    private async Task<(CrdtPatch Patch, CrdtDocument<SimulationDocument> Document)> GenerateSinglePatchAsync(IServiceScope scope, CrdtDocument<SimulationDocument> crdtDoc, Func<SimulationDocument, SimulationDocument> modifier)
    {
        var patcher = scope.ServiceProvider.GetRequiredService<IAsyncCrdtPatcher>();
        var nextDoc = modifier(crdtDoc.Data!);
        var patch = await patcher.GeneratePatchAsync(crdtDoc, nextDoc);
        var newDoc = await ApplyToAsync(scope, crdtDoc, patch);
        return (patch, newDoc);
    }

    private async Task<CrdtDocument<SimulationDocument>> ApplyToAsync(IServiceScope scope, CrdtDocument<SimulationDocument> crdtDoc, CrdtPatch patch)
    {
        var applicator = scope.ServiceProvider.GetRequiredService<IAsyncCrdtApplicator>();
        var result = await applicator.ApplyPatchAsync(crdtDoc, patch);
        return result.Document;
    }
}