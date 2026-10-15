namespace Ama.CRDT.IntegrationTests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Decorators;
using Ama.CRDT.Services.Journaling;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

[CrdtAotType(typeof(ShardedDocument))]
public partial class ShardingSimulationTestContext : CrdtAotContext
{
}

public sealed record ShardedDocument : IEquatable<ShardedDocument>
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    
    public string TenantId { get; init; } = "default";

    [CrdtLwwStrategy]
    public string Data { get; set; } = string.Empty;

    [CrdtCounterStrategy]
    public int Counter { get; set; }

    public bool Equals(ShardedDocument? other)
    {
        if (other is null) return false;
        return Id == other.Id &&
               TenantId == other.TenantId &&
               Data == other.Data &&
               Counter == other.Counter;
    }

    public override int GetHashCode() => HashCode.Combine(Id, TenantId, Data, Counter);
}

/// <summary>
/// A custom ShardId provider that extracts the TenantId from the document or its ID to dynamically shard causal tracking.
/// </summary>
public sealed class TenantShardIdProvider : IShardIdProvider
{
    public string GetShardId<T>(CrdtDocument<T> document) where T : class
    {
        if (document is CrdtDocument<ShardedDocument> shardedDoc)
        {
            return shardedDoc.Data?.TenantId ?? "default";
        }
        return "default";
    }

    public string GetShardId<T>(IComparable documentId) where T : class
    {
        ArgumentNullException.ThrowIfNull(documentId);
        return GetShardId(documentId, typeof(T));
    }

    public string GetShardId(IComparable documentId, Type documentType)
    {
        ArgumentNullException.ThrowIfNull(documentId);
        ArgumentNullException.ThrowIfNull(documentType);

        if (documentType == typeof(ShardedDocument) && documentId is string idStr)
        {
            var parts = idStr.Split(':');
            if (parts.Length > 1)
            {
                return parts[0];
            }
        }
        return "default";
    }
}

/// <summary>
/// An in-memory operation journal designed to simulate independent storage per Shard.
/// </summary>
public sealed class InMemoryShardedJournal : ICrdtOperationJournal
{
    public List<JournaledOperation> Operations { get; } = new List<JournaledOperation>();

    public void Append(IComparable documentId, IReadOnlyList<CrdtOperation> operations)
    {
        lock (Operations)
        {
            foreach (var op in operations)
            {
                if (!Operations.Any(o => o.Operation.Id == op.Id))
                {
                    Operations.Add(new JournaledOperation(documentId, op));
                }
            }
        }
    }

    public Task AppendAsync(IComparable documentId, IReadOnlyList<CrdtOperation> operations, CancellationToken cancellationToken = default)
    {
        Append(documentId, operations);
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<JournaledOperation> GetOperationsByRangeAsync(string shardId, string originReplicaId, long minShardClock, long maxShardClock, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        List<JournaledOperation> snapshot;
        lock (Operations) { snapshot = Operations.ToList(); }
        
        foreach (var op in snapshot.Where(o => o.Operation.ShardId == shardId && o.Operation.ReplicaId == originReplicaId && o.Operation.ShardClock > minShardClock && o.Operation.ShardClock <= maxShardClock))
        {
            yield return op;
        }

        await Task.CompletedTask;
    }

    public async IAsyncEnumerable<JournaledOperation> GetOperationsByDotsAsync(string shardId, string originReplicaId, IEnumerable<long> shardClocks, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var clocks = shardClocks.ToHashSet();
        List<JournaledOperation> snapshot;
        lock (Operations) { snapshot = Operations.ToList(); }
        
        foreach (var op in snapshot.Where(o => o.Operation.ShardId == shardId && o.Operation.ReplicaId == originReplicaId && clocks.Contains(o.Operation.ShardClock)))
        {
            yield return op;
        }

        await Task.CompletedTask;
    }
}

public sealed class ShardingSimulationTests
{
    [Fact]
    public async Task IndependentShardClocks_ShouldTrackCausalitySeparately()
    {
        // Arrange
        var provider = CreateServiceProvider();
        var scopeFactory = provider.GetRequiredService<ICrdtScopeFactory>();
        using var scopeA = scopeFactory.CreateScope("A");

        var doc1 = InitializeDocument(scopeA, "doc1", "tenant-1");
        var doc2 = InitializeDocument(scopeA, "doc2", "tenant-2");

        var patcherA = scopeA.ServiceProvider.GetRequiredService<IAsyncCrdtPatcher>();
        var applicatorA = scopeA.ServiceProvider.GetRequiredService<IAsyncCrdtApplicator>();

        // Act - Edit tenant-1
        var p1 = await patcherA.GenerateOperationAsync(doc1, d => d.Counter, new IncrementIntent(1));
        var res1 = await applicatorA.ApplyPatchAsync(doc1, new CrdtPatch(new[] { p1 }));
        doc1 = res1.Document;

        // Act - Edit tenant-2
        var p2 = await patcherA.GenerateOperationAsync(doc2, d => d.Counter, new IncrementIntent(1));
        var res2 = await applicatorA.ApplyPatchAsync(doc2, new CrdtPatch(new[] { p2 }));
        doc2 = res2.Document;

        // Act - Edit tenant-1 again
        var p3 = await patcherA.GenerateOperationAsync(doc1, d => d.Counter, new IncrementIntent(1));
        var res3 = await applicatorA.ApplyPatchAsync(doc1, new CrdtPatch(new[] { p3 }));
        doc1 = res3.Document;

        // Assert - Causality is properly partitioned
        var ctxA = scopeA.ServiceProvider.GetRequiredService<ReplicaContext>();
        
        ctxA.ShardVersionVectors.ContainsKey("tenant-1").ShouldBeTrue();
        ctxA.ShardVersionVectors["tenant-1"].Versions["A"].ShouldBe(2);

        ctxA.ShardVersionVectors.ContainsKey("tenant-2").ShouldBeTrue();
        ctxA.ShardVersionVectors["tenant-2"].Versions["A"].ShouldBe(1);

        // Verify operations were generated with correct specific ShardIds and monotonic ShardClocks
        p1.ShardId.ShouldBe("tenant-1");
        p1.ShardClock.ShouldBe(1);
        
        p2.ShardId.ShouldBe("tenant-2");
        p2.ShardClock.ShouldBe(1);
        
        p3.ShardId.ShouldBe("tenant-1");
        p3.ShardClock.ShouldBe(2);
    }

    [Fact]
    public async Task SelectiveShardSync_ShouldOnlySyncRequestedShard()
    {
        // Arrange
        var provider = CreateServiceProvider();
        var scopeFactory = provider.GetRequiredService<ICrdtScopeFactory>();
        
        using var scopeA = scopeFactory.CreateScope("A");
        using var scopeB = scopeFactory.CreateScope("B");

        var docsA = new Dictionary<string, CrdtDocument<ShardedDocument>>
        {
            ["doc1"] = InitializeDocument(scopeA, "doc1", "tenant-1"),
            ["doc2"] = InitializeDocument(scopeA, "doc2", "tenant-2")
        };
        
        var docsB = new Dictionary<string, CrdtDocument<ShardedDocument>>
        {
            ["doc1"] = InitializeDocument(scopeB, "doc1", "tenant-1"),
            ["doc2"] = InitializeDocument(scopeB, "doc2", "tenant-2")
        };

        // Node A makes edits to documents in both shards
        docsA["doc1"] = await ApplyModifierAsync(scopeA, docsA["doc1"], d => d with { Data = "Updated-1" });
        docsA["doc2"] = await ApplyModifierAsync(scopeA, docsA["doc2"], d => d with { Data = "Updated-2" });

        // Act
        // Node B initiates sync for tenant-1 ONLY
        await SyncShardAsync(scopeB, scopeA, "tenant-1", docsB);

        // Assert
        // Document 1 (tenant-1) successfully synchronized and converged
        docsB["doc1"].Data!.Data.ShouldBe("Updated-1");
        
        // Document 2 (tenant-2) remains isolated and unsynced
        docsB["doc2"].Data!.Data.ShouldBe(string.Empty);

        // Verify ReplicaContext state accurately reflects what was actually ingested
        var ctxB = scopeB.ServiceProvider.GetRequiredService<ReplicaContext>();
        ctxB.ShardVersionVectors.ContainsKey("tenant-1").ShouldBeTrue();
        ctxB.ShardVersionVectors.ContainsKey("tenant-2").ShouldBeFalse();
    }

    [Fact]
    public async Task MultiShardConflictResolution_ShouldConvergeIndependently()
    {
        // Arrange
        var provider = CreateServiceProvider();
        var scopeFactory = provider.GetRequiredService<ICrdtScopeFactory>();
        
        using var scopeA = scopeFactory.CreateScope("A");
        using var scopeB = scopeFactory.CreateScope("B");

        var docsA = new Dictionary<string, CrdtDocument<ShardedDocument>>
        {
            ["doc1"] = InitializeDocument(scopeA, "doc1", "tenant-1"),
            ["doc2"] = InitializeDocument(scopeA, "doc2", "tenant-2")
        };
        
        var docsB = new Dictionary<string, CrdtDocument<ShardedDocument>>
        {
            ["doc1"] = InitializeDocument(scopeB, "doc1", "tenant-1"),
            ["doc2"] = InitializeDocument(scopeB, "doc2", "tenant-2")
        };

        // Act
        // Concurrent modifications while disconnected across different shards
        docsA["doc1"] = await ApplyModifierAsync(scopeA, docsA["doc1"], d => d with { Data = "A-1" });
        docsA["doc2"] = await ApplyModifierAsync(scopeA, docsA["doc2"], d => d with { Data = "A-2" });

        docsB["doc1"] = await ApplyModifierAsync(scopeB, docsB["doc1"], d => d with { Data = "B-1" });
        docsB["doc2"] = await ApplyModifierAsync(scopeB, docsB["doc2"], d => d with { Data = "B-2" });

        // Phase 1: Sync only tenant-1 fully (A -> B, then B -> A)
        await SyncShardAsync(scopeB, scopeA, "tenant-1", docsB);
        await SyncShardAsync(scopeA, scopeB, "tenant-1", docsA);

        // Assert Phase 1
        // tenant-1 should converge due to LWW resolution (whichever clock/timestamp was higher wins)
        docsA["doc1"].Data!.Data.ShouldBe(docsB["doc1"].Data!.Data);
        
        // tenant-2 should still be completely divergent between nodes
        docsA["doc2"].Data!.Data.ShouldBe("A-2");
        docsB["doc2"].Data!.Data.ShouldBe("B-2");

        // Phase 2: Sync tenant-2
        await SyncShardAsync(scopeB, scopeA, "tenant-2", docsB);
        await SyncShardAsync(scopeA, scopeB, "tenant-2", docsA);

        // Assert Phase 2
        // tenant-2 should finally converge
        docsA["doc2"].Data!.Data.ShouldBe(docsB["doc2"].Data!.Data);
        
        // Final sanity checks validating identical states
        docsA["doc1"].Data.ShouldBe(docsB["doc1"].Data);
        docsA["doc2"].Data.ShouldBe(docsB["doc2"].Data);
    }

    [Fact]
    public void GetShardId_WithoutMaterializedDocument_ShouldResolveCorrectlyFromIdAndType()
    {
        // Arrange
        var provider = CreateServiceProvider();
        var scopeFactory = provider.GetRequiredService<ICrdtScopeFactory>();
        using var scope = scopeFactory.CreateScope("A");
        var shardProvider = scope.ServiceProvider.GetRequiredService<IShardIdProvider>();

        // Act
        var shard1 = shardProvider.GetShardId<ShardedDocument>("tenant-1:doc1");
        var shard2 = shardProvider.GetShardId("tenant-2:doc2", typeof(ShardedDocument));
        var fallbackShard = shardProvider.GetShardId<ShardedDocument>("doc-without-tenant");

        // Assert
        shard1.ShouldBe("tenant-1");
        shard2.ShouldBe("tenant-2");
        fallbackShard.ShouldBe("default");
    }

    private IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddCrdt()
                .AddCrdtAotContext<ShardingSimulationTestContext>()
                .AddCrdtJournaling<InMemoryShardedJournal>()
                .AddCrdtApplicatorDecorator<JournalingApplicatorDecorator>(DecoratorBehavior.Before)
                .AddCrdtPatcherDecorator<JournalingPatcherDecorator>(DecoratorBehavior.After)
                .AddCrdtShardIdProvider<TenantShardIdProvider>();

        return services.BuildServiceProvider();
    }

    private CrdtDocument<ShardedDocument> InitializeDocument(IServiceScope scope, string id, string tenantId)
    {
        var doc = new ShardedDocument { Id = id, TenantId = tenantId };
        var meta = scope.ServiceProvider.GetRequiredService<ICrdtMetadataManager>().Initialize(doc);
        return new CrdtDocument<ShardedDocument>(doc, meta);
    }

    private async Task<CrdtDocument<ShardedDocument>> ApplyModifierAsync(
        IServiceScope scope, CrdtDocument<ShardedDocument> crdtDoc, Func<ShardedDocument, ShardedDocument> modifier)
    {
        var patcher = scope.ServiceProvider.GetRequiredService<IAsyncCrdtPatcher>();
        var nextDoc = modifier(crdtDoc.Data!);
        var patch = await patcher.GeneratePatchAsync(crdtDoc, nextDoc);
        
        var applicator = scope.ServiceProvider.GetRequiredService<IAsyncCrdtApplicator>();
        var result = await applicator.ApplyPatchAsync(crdtDoc, patch);
        return result.Document;
    }

    private async Task SyncShardAsync(
        IServiceScope targetScope, IServiceScope sourceScope, string shardId, Dictionary<string, CrdtDocument<ShardedDocument>> targetDocs)
    {
        var vvSyncService = targetScope.ServiceProvider.GetRequiredService<IVersionVectorSyncService>();
        var targetCtx = targetScope.ServiceProvider.GetRequiredService<ReplicaContext>();
        var sourceCtx = sourceScope.ServiceProvider.GetRequiredService<ReplicaContext>();

        // Step 1: Calculate causality gaps specifically for this ShardId
        var syncReq = vvSyncService.CalculateRequirement(targetCtx, sourceCtx, shardId);
        if (!syncReq.IsBehind) return;

        // Step 2: Use the source's journal to selectively retrieve only the missing operations for this shard
        var sourceJournalManager = sourceScope.ServiceProvider.GetRequiredService<IJournalManager>();
        var missingOpsStream = sourceJournalManager.GetMissingOperationsAsync(shardId, syncReq);

        var opsByDoc = new Dictionary<IComparable, List<CrdtOperation>>();
        await foreach (var missingOp in missingOpsStream)
        {
            if (!opsByDoc.TryGetValue(missingOp.DocumentId, out var list))
            {
                list = new List<CrdtOperation>();
                opsByDoc[missingOp.DocumentId] = list;
            }
            list.Add(missingOp.Operation);
        }

        // Step 3: Apply the retrieved patches to the target node
        var applicator = targetScope.ServiceProvider.GetRequiredService<IAsyncCrdtApplicator>();
        foreach (var kvp in opsByDoc)
        {
            string docId = kvp.Key.ToString()!;
            if (targetDocs.TryGetValue(docId, out var doc))
            {
                var result = await applicator.ApplyPatchAsync(doc, new CrdtPatch(kvp.Value));
                targetDocs[docId] = result.Document;
            }
        }
    }
}