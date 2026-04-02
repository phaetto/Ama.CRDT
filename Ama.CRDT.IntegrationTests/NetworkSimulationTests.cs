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
using Ama.CRDT.Services.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

[CrdtSerializable(typeof(SimulationDocument))]
[CrdtSerializable(typeof(IList<string>))]
[CrdtSerializable(typeof(List<string>))]
[CrdtSerializable(typeof(IDictionary<string, string>))]
[CrdtSerializable(typeof(Dictionary<string, string>))]
public partial class NetworkSimulationTestContext : CrdtContext
{
}

public sealed record SimulationDocument : IEquatable<SimulationDocument>
{
    [CrdtLwwStrategy]
    public string Title { get; set; } = string.Empty;

    [CrdtCounterStrategy]
    public int ViewCount { get; set; }

    [CrdtOrSetStrategy]
    public IList<string> Tags { get; set; } = new List<string>();

    [CrdtLwwMapStrategy]
    public IDictionary<string, string> Preferences { get; set; } = new Dictionary<string, string>();

    [CrdtArrayLcsStrategy]
    public IList<string> Logs { get; set; } = new List<string>();

    public bool Equals(SimulationDocument? other)
    {
        if (other is null) return false;

        var tagsEqual = Tags.Count == other.Tags.Count && Tags.OrderBy(t => t).SequenceEqual(other.Tags.OrderBy(t => t));
        var prefsEqual = Preferences.Count == other.Preferences.Count && !Preferences.Except(other.Preferences).Any();
        var logsEqual = Logs.SequenceEqual(other.Logs);

        return Title == other.Title &&
               ViewCount == other.ViewCount &&
               tagsEqual &&
               prefsEqual &&
               logsEqual;
    }

    public override int GetHashCode() => HashCode.Combine(Title, ViewCount);
}

/// <summary>
/// A simple in-memory implementation of the journal to demonstrate local replication storage.
/// </summary>
public sealed class InMemoryJournal : ICrdtOperationJournal
{
    public List<JournaledOperation> Operations { get; } = new List<JournaledOperation>();

    public void Append(string documentId, IReadOnlyList<CrdtOperation> operations)
    {
        lock (Operations)
        {
            foreach (var op in operations)
            {
                // Ensure idempotency. When applying self-generated operations, 
                // the decorator might hit the journal multiple times.
                if (!Operations.Any(o => o.Operation.Id == op.Id))
                {
                    Operations.Add(new JournaledOperation(documentId, op));
                }
            }
        }
    }

    public Task AppendAsync(string documentId, IReadOnlyList<CrdtOperation> operations, CancellationToken cancellationToken = default)
    {
        Append(documentId, operations);
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<JournaledOperation> GetOperationsByRangeAsync(string originReplicaId, long minGlobalClock, long maxGlobalClock, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        List<JournaledOperation> snapshot;
        lock (Operations) { snapshot = Operations.ToList(); }
        
        foreach (var op in snapshot.Where(o => o.Operation.ReplicaId == originReplicaId && o.Operation.GlobalClock > minGlobalClock && o.Operation.GlobalClock <= maxGlobalClock))
        {
            yield return op;
        }

        await Task.CompletedTask;
    }

    public async IAsyncEnumerable<JournaledOperation> GetOperationsByDotsAsync(string originReplicaId, IEnumerable<long> globalClocks, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var clocks = globalClocks.ToHashSet();
        List<JournaledOperation> snapshot;
        lock (Operations) { snapshot = Operations.ToList(); }
        
        foreach (var op in snapshot.Where(o => o.Operation.ReplicaId == originReplicaId && clocks.Contains(o.Operation.GlobalClock)))
        {
            yield return op;
        }

        await Task.CompletedTask;
    }
}

public sealed class NetworkSimulationTests
{
    [Fact]
    public void OutOfOrderDelivery_ShouldConvergeToIdenticalState()
    {
        // Arrange
        var provider = CreateServiceProvider();
        var scopeFactory = provider.GetRequiredService<ICrdtScopeFactory>();

        using var scopeA = scopeFactory.CreateScope("A");
        using var scopeB = scopeFactory.CreateScope("B");
        using var scopeC = scopeFactory.CreateScope("C");

        var crdtDocA = InitializeDocument(scopeA);
        var crdtDocB = InitializeDocument(scopeB);
        var crdtDocC = InitializeDocument(scopeC);

        // Act - Replicas independently generate patches
        var patchesFromA = GeneratePatches(scopeA, ref crdtDocA, 5, "A");
        var patchesFromB = GeneratePatches(scopeB, ref crdtDocB, 5, "B");
        var patchesFromC = GeneratePatches(scopeC, ref crdtDocC, 5, "C");

        // Simulate complete out-of-order delivery
        ApplyPatchesRandomly(scopeA, ref crdtDocA, patchesFromB.Concat(patchesFromC).ToList());
        ApplyPatchesRandomly(scopeB, ref crdtDocB, patchesFromA.Concat(patchesFromC).ToList());
        ApplyPatchesRandomly(scopeC, ref crdtDocC, patchesFromA.Concat(patchesFromB).ToList());

        // Assert - All nodes must converge to the identical state
        crdtDocA.Data.ShouldBe(crdtDocB.Data);
        crdtDocB.Data.ShouldBe(crdtDocC.Data);
    }

    [Fact]
    public void NetworkPartition_ShouldConvergeAfterHealing()
    {
        // Arrange
        var provider = CreateServiceProvider();
        var scopeFactory = provider.GetRequiredService<ICrdtScopeFactory>();

        using var scopeA = scopeFactory.CreateScope("A");
        using var scopeB = scopeFactory.CreateScope("B");
        using var scopeC = scopeFactory.CreateScope("C");
        using var scopeD = scopeFactory.CreateScope("D");

        var crdtDocA = InitializeDocument(scopeA);
        var crdtDocB = InitializeDocument(scopeB);
        var crdtDocC = InitializeDocument(scopeC);
        var crdtDocD = InitializeDocument(scopeD);

        // --- PARTITION 1 (A & B) ---
        var p1Patches = new List<CrdtPatch>();

        // A performs State-Based Update
        var patchA1 = GenerateSinglePatch(scopeA, ref crdtDocA, d => d with { Title = "Partition1-Title" });
        p1Patches.Add(patchA1);
        ApplyTo(scopeB, ref crdtDocB, patchA1);

        // B performs Intent-Based Update on Counter
        var patcherB = scopeB.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        var opB = patcherB.GenerateOperation(crdtDocB, d => d.ViewCount, new IncrementIntent(5));
        var patchB1 = new CrdtPatch(new CrdtOperation[] { opB });
        ApplyTo(scopeB, ref crdtDocB, patchB1);
        ApplyTo(scopeA, ref crdtDocA, patchB1);
        p1Patches.Add(patchB1);

        // A performs Set Addition via Intent
        var patcherA = scopeA.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        var opA2 = patcherA.GenerateOperation(crdtDocA, d => d.Tags, new AddIntent("Tag-P1"));
        var patchA2 = new CrdtPatch(new CrdtOperation[] { opA2 });
        ApplyTo(scopeA, ref crdtDocA, patchA2);
        ApplyTo(scopeB, ref crdtDocB, patchA2);
        p1Patches.Add(patchA2);

        // --- PARTITION 2 (C & D) ---
        var p2Patches = new List<CrdtPatch>();

        // C performs State-Based Update
        var patchC1 = GenerateSinglePatch(scopeC, ref crdtDocC, d => d with { Title = "Partition2-Title" });
        p2Patches.Add(patchC1);
        ApplyTo(scopeD, ref crdtDocD, patchC1);

        // D performs Intent-Based Update on Counter
        var patcherD = scopeD.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        var opD = patcherD.GenerateOperation(crdtDocD, d => d.ViewCount, new IncrementIntent(10));
        var patchD1 = new CrdtPatch(new CrdtOperation[] { opD });
        ApplyTo(scopeD, ref crdtDocD, patchD1);
        ApplyTo(scopeC, ref crdtDocC, patchD1);
        p2Patches.Add(patchD1);

        // C performs Set Addition via Intent
        var patcherC = scopeC.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        var opC2 = patcherC.GenerateOperation(crdtDocC, d => d.Tags, new AddIntent("Tag-P2"));
        var patchC2 = new CrdtPatch(new CrdtOperation[] { opC2 });
        ApplyTo(scopeC, ref crdtDocC, patchC2);
        ApplyTo(scopeD, ref crdtDocD, patchC2);
        p2Patches.Add(patchC2);

        // Assert Partitions Diverged
        crdtDocA.Data.ShouldBe(crdtDocB.Data);
        crdtDocC.Data.ShouldBe(crdtDocD.Data);
        crdtDocA.Data.ShouldNotBe(crdtDocC.Data); // Ensure they actually differ before healing

        // --- HEALING PHASE ---
        // Partition 1 receives Partition 2's patches
        foreach (var p in p2Patches)
        {
            ApplyTo(scopeA, ref crdtDocA, p);
            ApplyTo(scopeB, ref crdtDocB, p);
        }

        // Partition 2 receives Partition 1's patches
        foreach (var p in p1Patches)
        {
            ApplyTo(scopeC, ref crdtDocC, p);
            ApplyTo(scopeD, ref crdtDocD, p);
        }

        // Assert - Fully Converged
        crdtDocA.Data.ShouldBe(crdtDocB.Data);
        crdtDocB.Data.ShouldBe(crdtDocC.Data);
        crdtDocC.Data.ShouldBe(crdtDocD.Data);

        // Assert - Specific conflict resolutions (LWW should pick one, Sets/Counters merge)
        crdtDocA.Data!.Tags.ShouldContain("Tag-P1");
        crdtDocA.Data.Tags.ShouldContain("Tag-P2");
        crdtDocA.Data.ViewCount.ShouldBe(15); // 5 from P1 + 10 from P2
    }

    [Fact]
    public void DuplicateMessageDelivery_ShouldMaintainIdempotency()
    {
        // Arrange
        var provider = CreateServiceProvider();
        var scopeFactory = provider.GetRequiredService<ICrdtScopeFactory>();

        using var scopeA = scopeFactory.CreateScope("A");
        using var scopeB = scopeFactory.CreateScope("B");

        var crdtDocA = InitializeDocument(scopeA);
        var crdtDocB = InitializeDocument(scopeB);

        // Act
        // Node A makes multiple varied changes
        var patchesFromA = GeneratePatches(scopeA, ref crdtDocA, 5, "A");

        // Delivery #1 to Node B
        foreach (var patch in patchesFromA)
        {
            ApplyTo(scopeB, ref crdtDocB, patch);
        }

        // Verify initial convergence
        crdtDocB.Data.ShouldBe(crdtDocA.Data);

        // Simulate network retries/at-least-once delivery (Delivery #2)
        foreach (var patch in patchesFromA)
        {
            ApplyTo(scopeB, ref crdtDocB, patch);
        }

        // Assert - Idempotency is maintained, counters don't double, arrays don't duplicate
        crdtDocB.Data.ShouldBe(crdtDocA.Data);
        crdtDocB.Data!.ViewCount.ShouldBe(crdtDocA.Data!.ViewCount);
        crdtDocB.Data.Logs.Count.ShouldBe(crdtDocA.Data.Logs.Count);
    }

    [Fact]
    public void LongLivedOfflineReplica_ShouldCatchUpAndConverge()
    {
        // Arrange
        var provider = CreateServiceProvider();
        var scopeFactory = provider.GetRequiredService<ICrdtScopeFactory>();

        using var scopeMain1 = scopeFactory.CreateScope("Main1");
        using var scopeMain2 = scopeFactory.CreateScope("Main2");
        using var scopeOffline = scopeFactory.CreateScope("Offline");

        var docMain1 = InitializeDocument(scopeMain1);
        var docMain2 = InitializeDocument(scopeMain2);
        var docOffline = InitializeDocument(scopeOffline);

        var mainClusterPatches = new List<CrdtPatch>();

        // Act
        // 1. The main cluster performs many operations while the Offline node is disconnected
        for (int i = 0; i < 15; i++)
        {
            var p1 = GeneratePatches(scopeMain1, ref docMain1, 1, "M1");
            var p2 = GeneratePatches(scopeMain2, ref docMain2, 1, "M2");

            mainClusterPatches.AddRange(p1);
            mainClusterPatches.AddRange(p2);

            // They sync constantly
            ApplyPatchesRandomly(scopeMain1, ref docMain1, p2);
            ApplyPatchesRandomly(scopeMain2, ref docMain2, p1);
        }

        // 2. The offline replica performs its own local operations during its long downtime
        var offlinePatches = GeneratePatches(scopeOffline, ref docOffline, 5, "Off");

        // Pre-assertion: Ensure divergence
        docOffline.Data.ShouldNotBe(docMain1.Data);

        // 3. The Offline replica reconnects. It syncs the entire history from the main cluster
        ApplyPatchesRandomly(scopeOffline, ref docOffline, mainClusterPatches);
        
        // 4. The main cluster receives the backlog from the formerly offline replica
        ApplyPatchesRandomly(scopeMain1, ref docMain1, offlinePatches);
        ApplyPatchesRandomly(scopeMain2, ref docMain2, offlinePatches);

        // Assert
        docMain1.Data.ShouldBe(docMain2.Data);
        docOffline.Data.ShouldBe(docMain1.Data);
    }

    [Fact]
    public void ConcurrentAddRemoveUpdate_ShouldConvergeCleanly()
    {
        // Arrange
        var provider = CreateServiceProvider();
        var scopeFactory = provider.GetRequiredService<ICrdtScopeFactory>();

        using var scopeA = scopeFactory.CreateScope("A");
        using var scopeB = scopeFactory.CreateScope("B");

        var crdtDocA = InitializeDocument(scopeA);
        
        // Establish some shared initial state
        var patcherA = scopeA.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        var opTag = patcherA.GenerateOperation(crdtDocA, d => d.Tags, new AddIntent("SharedTag"));
        var opPref = patcherA.GenerateOperation(crdtDocA, d => d.Preferences, new MapSetIntent("SharedKey", "InitialValue"));
        var initialPatch = new CrdtPatch(new[] { opTag, opPref });
        
        ApplyTo(scopeA, ref crdtDocA, initialPatch);

        // Sync initial state to B
        var crdtDocB = InitializeDocument(scopeB);
        ApplyTo(scopeB, ref crdtDocB, initialPatch);

        // Act
        // Node A concurrently removes the tag and updates the preference
        var opTagRemA = patcherA.GenerateOperation(crdtDocA, d => d.Tags, new RemoveValueIntent("SharedTag"));
        var opPrefUpdA = patcherA.GenerateOperation(crdtDocA, d => d.Preferences, new MapSetIntent("SharedKey", "ValueFromA"));
        var patchA = new CrdtPatch(new[] { opTagRemA, opPrefUpdA });
        ApplyTo(scopeA, ref crdtDocA, patchA);

        // Node B concurrently removes the *same* tag (idempotent remove), adds a new tag, and updates the same preference
        var patcherB = scopeB.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        var opTagRemB = patcherB.GenerateOperation(crdtDocB, d => d.Tags, new RemoveValueIntent("SharedTag"));
        var opTagAddB = patcherB.GenerateOperation(crdtDocB, d => d.Tags, new AddIntent("TagFromB"));
        var opPrefUpdB = patcherB.GenerateOperation(crdtDocB, d => d.Preferences, new MapSetIntent("SharedKey", "ValueFromB"));
        var patchB = new CrdtPatch(new[] { opTagRemB, opTagAddB, opPrefUpdB });
        ApplyTo(scopeB, ref crdtDocB, patchB);

        // Cross apply patches
        ApplyTo(scopeA, ref crdtDocA, patchB);
        ApplyTo(scopeB, ref crdtDocB, patchA);

        // Assert convergence and data integrity
        crdtDocA.Data.ShouldBe(crdtDocB.Data);

        // Tag checks: "SharedTag" must be removed. "TagFromB" must remain.
        crdtDocA.Data!.Tags.ShouldNotContain("SharedTag");
        crdtDocA.Data.Tags.ShouldContain("TagFromB");
        
        // Map LWW resolution: Either ValueFromA or ValueFromB won, depending on internal Timestamp rules, but they must be consistent across nodes
        crdtDocA.Data.Preferences["SharedKey"].ShouldBe(crdtDocB.Data!.Preferences["SharedKey"]);
        crdtDocA.Data.Preferences["SharedKey"].ShouldBeOneOf("ValueFromA", "ValueFromB");
    }

    [Fact]
    public void RingTopology_GossipReplication_ShouldConverge()
    {
        // Arrange
        var provider = CreateServiceProvider();
        var scopeFactory = provider.GetRequiredService<ICrdtScopeFactory>();

        using var scopeA = scopeFactory.CreateScope("A");
        using var scopeB = scopeFactory.CreateScope("B");
        using var scopeC = scopeFactory.CreateScope("C");

        var docA = InitializeDocument(scopeA);
        var docB = InitializeDocument(scopeB);
        var docC = InitializeDocument(scopeC);

        // Act
        // Initial diverse edits on all nodes
        var patchesA = GeneratePatches(scopeA, ref docA, 3, "A");
        var patchesB = GeneratePatches(scopeB, ref docB, 3, "B");
        var patchesC = GeneratePatches(scopeC, ref docC, 3, "C");

        // We simulate a ring: A -> B -> C -> A
        // Round 1: Each node sends its local changes to the next node in the ring
        ApplyPatchesRandomly(scopeB, ref docB, patchesA); // A -> B
        ApplyPatchesRandomly(scopeC, ref docC, patchesB); // B -> C
        ApplyPatchesRandomly(scopeA, ref docA, patchesC); // C -> A

        // Round 2: Each node forwards the changes it learned in Round 1
        ApplyPatchesRandomly(scopeB, ref docB, patchesC); // A forwards C's patches to B
        ApplyPatchesRandomly(scopeC, ref docC, patchesA); // B forwards A's patches to C
        ApplyPatchesRandomly(scopeA, ref docA, patchesB); // C forwards B's patches to A

        // Assert convergence
        docA.Data.ShouldBe(docB.Data);
        docB.Data.ShouldBe(docC.Data);
        
        // Counter checks: We generated 3 patches per node, each having 1 counter increment, totaling 9 increments
        docA.Data!.ViewCount.ShouldBe(9);
    }

    [Fact]
    public void CausalDelivery_AddThenRemove_ShouldNotResurrectItem()
    {
        // Arrange
        var provider = CreateServiceProvider();
        var scopeFactory = provider.GetRequiredService<ICrdtScopeFactory>();

        using var scopeA = scopeFactory.CreateScope("A");
        using var scopeB = scopeFactory.CreateScope("B");
        using var scopeC = scopeFactory.CreateScope("C");

        var docA = InitializeDocument(scopeA);
        var docB = InitializeDocument(scopeB);
        var docC = InitializeDocument(scopeC);

        // Act
        // 1. Node A adds an item
        var patcherA = scopeA.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        var addOp = patcherA.GenerateOperation(docA, d => d.Tags, new AddIntent("GhostItem"));
        var addPatch = new CrdtPatch(new[] { addOp });
        ApplyTo(scopeA, ref docA, addPatch);

        // 2. Node A syncs to Node B
        ApplyTo(scopeB, ref docB, addPatch);
        docB.Data!.Tags.ShouldContain("GhostItem");

        // 3. Node B immediately removes the item
        var patcherB = scopeB.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        var removeOp = patcherB.GenerateOperation(docB, d => d.Tags, new RemoveValueIntent("GhostItem"));
        var removePatch = new CrdtPatch(new[] { removeOp });
        ApplyTo(scopeB, ref docB, removePatch);
        docB.Data.Tags.ShouldNotContain("GhostItem");

        // 4. Node C receives B's removal BEFORE A's addition (violating causal order due to network anomaly)
        ApplyTo(scopeC, ref docC, removePatch); // C doesn't know about GhostItem yet. It should handle the ghost remove gracefully (e.g., via metadata tags).
        ApplyTo(scopeC, ref docC, addPatch);    // C gets the add operation later. It shouldn't resurrect the item because it knows a causal deletion happened.

        // 5. Cross sync everything to ensure final convergence
        ApplyTo(scopeA, ref docA, removePatch);

        // Assert - The item should remain deleted everywhere. The delayed 'Add' should not resurrect it.
        docA.Data.Tags.ShouldNotContain("GhostItem");
        docB.Data.Tags.ShouldNotContain("GhostItem");
        docC.Data.Tags.ShouldNotContain("GhostItem");
        
        docA.Data.ShouldBe(docC.Data);
    }

    [Fact]
    public async Task Journaling_Sync_ShouldProvideMissingOperations_AndConverge()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddCrdt()
                .AddCrdtAotContext<NetworkSimulationTestContext>()
                .AddCrdtJournaling<InMemoryJournal>()
                .AddCrdtApplicatorDecorator<JournalingApplicatorDecorator>()
                .AddCrdtPatcherDecorator<JournalingPatcherDecorator>();
        var provider = services.BuildServiceProvider();

        var scopeFactory = provider.GetRequiredService<ICrdtScopeFactory>();
        var vvSyncService = provider.GetRequiredService<IVersionVectorSyncService>();

        using var scopeA = scopeFactory.CreateScope("A");
        using var scopeB = scopeFactory.CreateScope("B");

        var docA = InitializeDocument(scopeA);
        var docB = InitializeDocument(scopeB);

        var patcherA = scopeA.ServiceProvider.GetRequiredService<IAsyncCrdtPatcher>();
        var applicatorA = scopeA.ServiceProvider.GetRequiredService<IAsyncCrdtApplicator>();

        // Act
        // Replica A makes several changes over time using the Async interfaces directly 
        // (which are automatically decorated to save to the local journal).
        for (int i = 0; i < 5; i++)
        {
            var op = await patcherA.GenerateOperationAsync(docA, d => d.ViewCount, new IncrementIntent(1));
            var patch = new CrdtPatch(new[] { op });
            
            var result = await applicatorA.ApplyPatchAsync(docA, patch);
            docA = result.Document;
        }

        // Replica B determines it's behind and needs to catch up

        // Calculate what Target (B) needs from Source (A)
        var syncReq = vvSyncService.CalculateRequirement(
            scopeB.ServiceProvider.GetRequiredService<ReplicaContext>(),
            scopeA.ServiceProvider.GetRequiredService<ReplicaContext>()
        );
        syncReq.IsBehind.ShouldBeTrue();

        // Replica A's journal serves the missing operations to satisfy B's sync requirement
        var journalManagerA = scopeA.ServiceProvider.GetRequiredService<IJournalManager>();
        var missingOpsStream = journalManagerA.GetMissingOperationsAsync(syncReq);
        
        var missingOps = new List<CrdtOperation>();
        await foreach (var missingOp in missingOpsStream)
        {
            missingOps.Add(missingOp.Operation);
        }

        missingOps.Count.ShouldBe(5); // 5 increments were made

        // Replica B applies the operations
        var applicatorB = scopeB.ServiceProvider.GetRequiredService<IAsyncCrdtApplicator>();
        var resultB = await applicatorB.ApplyPatchAsync(docB, new CrdtPatch(missingOps));
        docB = resultB.Document;

        // Assert convergence
        docB.Data.ShouldBe(docA.Data);
        docB.Data!.ViewCount.ShouldBe(5);
    }

    [Fact]
    public async Task Journaling_Sync_WithMissingDots_ShouldFetchOnlyMissingOperations()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddCrdt()
                .AddCrdtAotContext<NetworkSimulationTestContext>()
                .AddCrdtJournaling<InMemoryJournal>()
                .AddCrdtApplicatorDecorator<JournalingApplicatorDecorator>()
                .AddCrdtPatcherDecorator<JournalingPatcherDecorator>();
        var provider = services.BuildServiceProvider();

        var scopeFactory = provider.GetRequiredService<ICrdtScopeFactory>();
        var vvSyncService = provider.GetRequiredService<IVersionVectorSyncService>();

        using var scopeA = scopeFactory.CreateScope("A");
        using var scopeB = scopeFactory.CreateScope("B");

        var docA = InitializeDocument(scopeA);
        var docB = InitializeDocument(scopeB);

        var patcherA = scopeA.ServiceProvider.GetRequiredService<IAsyncCrdtPatcher>();
        var applicatorA = scopeA.ServiceProvider.GetRequiredService<IAsyncCrdtApplicator>();

        // Act - A generates 3 distinct operations
        var opsA = new List<CrdtOperation>();
        for (int i = 0; i < 3; i++)
        {
            var nextDoc = docA.Data! with { Title = $"Title-{i}" };
            var patch = await patcherA.GeneratePatchAsync(docA, nextDoc);
            opsA.AddRange(patch.Operations);
            
            var result = await applicatorA.ApplyPatchAsync(docA, patch);
            docA = result.Document;
        }

        // B receives operations out of order due to network anomaly (only gets 1st and 3rd)
        var applicatorB = scopeB.ServiceProvider.GetRequiredService<IAsyncCrdtApplicator>();
        var partialPatch = new CrdtPatch(new[] { opsA[0], opsA[2] });
        var resultB = await applicatorB.ApplyPatchAsync(docB, partialPatch);
        docB = resultB.Document;

        // B detects divergence and triggers a sync with A to get the missing 2nd operation (dot)
        var syncReq = vvSyncService.CalculateRequirement(
            scopeB.ServiceProvider.GetRequiredService<ReplicaContext>(),
            scopeA.ServiceProvider.GetRequiredService<ReplicaContext>()
        );
        syncReq.IsBehind.ShouldBeTrue();
        
        var journalManagerA = scopeA.ServiceProvider.GetRequiredService<IJournalManager>();
        var missingOpsStream = journalManagerA.GetMissingOperationsAsync(syncReq);
        
        var missingOps = new List<CrdtOperation>();
        await foreach (var missingOp in missingOpsStream)
        {
            missingOps.Add(missingOp.Operation);
        }

        // Assert that the Journal Manager only extracted exactly what was missing based on the Dotted Version Vector
        missingOps.Count.ShouldBe(1);
        missingOps[0].Id.ShouldBe(opsA[1].Id); 

        // B applies it and finally converges completely
        var finalResultB = await applicatorB.ApplyPatchAsync(docB, new CrdtPatch(missingOps));
        docB = finalResultB.Document;

        docB.Data.ShouldBe(docA.Data);
    }

    private IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddCrdt()
                .AddCrdtAotContext<NetworkSimulationTestContext>();
        return services.BuildServiceProvider();
    }

    private CrdtDocument<SimulationDocument> InitializeDocument(IServiceScope scope)
    {
        var doc = new SimulationDocument();
        var meta = scope.ServiceProvider.GetRequiredService<ICrdtMetadataManager>().Initialize(doc);
        return new CrdtDocument<SimulationDocument>(doc, meta);
    }

    private List<CrdtPatch> GeneratePatches(IServiceScope scope, ref CrdtDocument<SimulationDocument> crdtDoc, int count, string prefix)
    {
        var patcher = scope.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        var patches = new List<CrdtPatch>();

        for (int i = 0; i < count; i++)
        {
            // State-based diff generation
            var nextDoc = crdtDoc.Data! with
            {
                Title = $"{prefix}-Title-{i}",
                Preferences = new Dictionary<string, string>(crdtDoc.Data.Preferences) { [$"{prefix}-Key-{i}"] = $"Val-{i}" },
                Logs = new List<string>(crdtDoc.Data.Logs) { $"{prefix}-Log-{i}" }
            };
            var diffPatch = patcher.GeneratePatch(crdtDoc, nextDoc);
            ApplyTo(scope, ref crdtDoc, diffPatch);
            patches.Add(diffPatch);
            
            // Intent-based generation - Apply immediately to advance the logical clock sequentially
            var tagsOp = patcher.GenerateOperation(crdtDoc, d => d.Tags, new AddIntent($"{prefix}-Tag-{i}"));
            var tagsPatch = new CrdtPatch(new CrdtOperation[] { tagsOp });
            ApplyTo(scope, ref crdtDoc, tagsPatch);
            patches.Add(tagsPatch);

            var viewCountOp = patcher.GenerateOperation(crdtDoc, d => d.ViewCount, new IncrementIntent(1));
            var viewCountPatch = new CrdtPatch(new CrdtOperation[] { viewCountOp });
            ApplyTo(scope, ref crdtDoc, viewCountPatch);
            patches.Add(viewCountPatch);
        }

        return patches;
    }

    private void ApplyPatchesRandomly(IServiceScope scope, ref CrdtDocument<SimulationDocument> crdtDoc, List<CrdtPatch> patches)
    {
        var applicator = scope.ServiceProvider.GetRequiredService<ICrdtApplicator>();
        var random = new Random(42); // Seeded for deterministic testing
        var shuffled = patches.OrderBy(x => random.Next()).ToList();

        foreach (var patch in shuffled)
        {
            var result = applicator.ApplyPatch(crdtDoc, patch);
            crdtDoc = result.Document;
        }
    }

    private CrdtPatch GenerateSinglePatch(IServiceScope scope, ref CrdtDocument<SimulationDocument> crdtDoc, Func<SimulationDocument, SimulationDocument> modifier)
    {
        var patcher = scope.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        var nextDoc = modifier(crdtDoc.Data!);
        var patch = patcher.GeneratePatch(crdtDoc, nextDoc);
        ApplyTo(scope, ref crdtDoc, patch);
        return patch;
    }

    private void ApplyTo(IServiceScope scope, ref CrdtDocument<SimulationDocument> crdtDoc, CrdtPatch patch)
    {
        var applicator = scope.ServiceProvider.GetRequiredService<ICrdtApplicator>();
        var result = applicator.ApplyPatch(crdtDoc, patch);
        crdtDoc = result.Document;
    }
}