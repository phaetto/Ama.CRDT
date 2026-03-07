namespace Ama.CRDT.UnitTests.Services;

using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Providers;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

public sealed class CrdtMetadataManagerTests
{
    private readonly CrdtMetadataManager manager;
    private readonly Mock<ICrdtStrategyProvider> strategyProviderMock;
    private readonly Mock<ICrdtTimestampProvider> timestampProviderMock;
    private readonly Mock<IElementComparerProvider> elementComparerProviderMock;

    public CrdtMetadataManagerTests()
    {
        strategyProviderMock = new Mock<ICrdtStrategyProvider>();
        timestampProviderMock = new Mock<ICrdtTimestampProvider>();
        elementComparerProviderMock = new Mock<IElementComparerProvider>();
        manager = new CrdtMetadataManager(strategyProviderMock.Object, timestampProviderMock.Object, elementComparerProviderMock.Object,new ReplicaContext { ReplicaId = "replica" });
        timestampProviderMock.Setup(p => p.Create(It.IsAny<long>())).Returns<long>(v => new EpochTimestamp(v));
    }
    
    [Theory]
    [InlineData(true, false, false, false, false)]
    [InlineData(false, true, false, false, false)]
    [InlineData(false, false, true, false, false)]
    [InlineData(false, false, false, true, false)]
    [InlineData(false, false, false, false, true)]
    public void PublicMethods_WithNullArguments_ShouldThrowArgumentNullException(bool testInitialize, bool testReset, bool testPrune, bool testAdvanceVector, bool testLocking)
    {
        // Arrange
        var doc = new object();
        var metadata = new CrdtMetadata();
        var timestamp = timestampProviderMock.Object.Create(1);
        var operation = new CrdtOperation();
        
        // Act & Assert
        if (testInitialize)
        {
            // Test Initialize<T>(T document) and Initialize<T>(T document, ICrdtTimestamp timestamp)
            Should.Throw<ArgumentNullException>(() => manager.Initialize<object>(null!));
            Should.Throw<ArgumentNullException>(() => manager.Initialize<object>(null!, timestamp));
            Should.Throw<ArgumentNullException>(() => manager.Initialize(doc, null!));
            
            // Test Initialize<T>(CrdtDocument<T> document) and Initialize<T>(CrdtDocument<T> document, ICrdtTimestamp timestamp)
            Should.Throw<ArgumentNullException>(() => manager.Initialize(new CrdtDocument<object>(null!, metadata)));
            Should.Throw<ArgumentNullException>(() => manager.Initialize(new CrdtDocument<object>(doc, null!)));
            Should.Throw<ArgumentNullException>(() => manager.Initialize(new CrdtDocument<object>(null!, metadata), timestamp));
            Should.Throw<ArgumentNullException>(() => manager.Initialize(new CrdtDocument<object>(doc, null!), timestamp));
            Should.Throw<ArgumentNullException>(() => manager.Initialize(new CrdtDocument<object>(doc, metadata), null!));
        }
        
        if (testReset)
        {
            // Test Reset<T>(CrdtDocument<T> document) and Reset<T>(CrdtDocument<T> document, ICrdtTimestamp timestamp)
            Should.Throw<ArgumentNullException>(() => manager.Reset(new CrdtDocument<object>(null!, metadata)));
            Should.Throw<ArgumentNullException>(() => manager.Reset(new CrdtDocument<object>(doc, null!)));
            Should.Throw<ArgumentNullException>(() => manager.Reset(new CrdtDocument<object>(null!, metadata), timestamp));
            Should.Throw<ArgumentNullException>(() => manager.Reset(new CrdtDocument<object>(doc, null!), timestamp));
            Should.Throw<ArgumentNullException>(() => manager.Reset(new CrdtDocument<object>(doc, metadata), null!));
        }
        
        if (testPrune)
        {
            Should.Throw<ArgumentNullException>(() => manager.PruneLwwTombstones(null!, timestamp));
            Should.Throw<ArgumentNullException>(() => manager.PruneLwwTombstones(metadata, null!));
        }
        
        if (testAdvanceVector)
        {
            Should.Throw<ArgumentNullException>(() => manager.AdvanceVersionVector(null!, operation));
            Should.Throw<ArgumentException>(() => manager.AdvanceVersionVector(metadata, null!, 1L));
            Should.Throw<ArgumentException>(() => manager.AdvanceVersionVector(metadata, " ", 1L));
        }
    }

    [Fact]
    public void Reset_ShouldClearAllState()
    {
        // Arrange
        var metadata = new CrdtMetadata();
        metadata.Lww["$.a"] = timestampProviderMock.Object.Create(100);
        metadata.PositionalTrackers["$.b"] = [];
        metadata.AverageRegisters["$.c"] = new Dictionary<string, AverageRegisterValue>();
        metadata.PriorityQueues["$.d"] = new LwwSetState(new Dictionary<object, ICrdtTimestamp>(), new Dictionary<object, ICrdtTimestamp>());
        metadata.LwwMaps["$.f"] = new Dictionary<object, ICrdtTimestamp>();
        metadata.OrMaps["$.g"] = new OrSetState(new Dictionary<object, ISet<Guid>>(), new Dictionary<object, ISet<Guid>>());
        metadata.CounterMaps["$.h"] = new Dictionary<object, PnCounterState> { { "key", new PnCounterState(1, 1) } };

        var doc = new object();
        timestampProviderMock.Setup(p => p.Now()).Returns(timestampProviderMock.Object.Create(200));
        
        // Act
        manager.Reset(new CrdtDocument<object>(doc, metadata));
        
        // Assert
        metadata.Lww.ShouldBeEmpty();
        metadata.PositionalTrackers.ShouldBeEmpty();
        metadata.AverageRegisters.ShouldBeEmpty();
        metadata.PriorityQueues.ShouldBeEmpty();
        metadata.LwwMaps.ShouldBeEmpty();
        metadata.OrMaps.ShouldBeEmpty();
        metadata.CounterMaps.ShouldBeEmpty();
    }
    
    [Fact]
    public void PruneLwwTombstones_ShouldRemoveOlderEntries()
    {
        // Arrange
        var metadata = new CrdtMetadata();
        metadata.Lww["$.a"] = timestampProviderMock.Object.Create(100);
        metadata.Lww["$.b"] = timestampProviderMock.Object.Create(200);
        metadata.Lww["$.c"] = timestampProviderMock.Object.Create(300);
        var threshold = timestampProviderMock.Object.Create(250);
        
        // Act
        manager.PruneLwwTombstones(metadata, threshold);

        // Assert
        metadata.Lww.ShouldContainKey("$.c");
        metadata.Lww.ShouldNotContainKey("$.a");
        metadata.Lww.ShouldNotContainKey("$.b");
        metadata.Lww.Count.ShouldBe(1);
    }

    [Fact]
    public void PruneLwwSetTombstones_WithNullArguments_ShouldThrowArgumentNullException()
    {
        var metadata = new CrdtMetadata();
        var threshold = timestampProviderMock.Object.Create(1);
        Should.Throw<ArgumentNullException>(() => manager.PruneLwwSetTombstones(null!, threshold));
        Should.Throw<ArgumentNullException>(() => manager.PruneLwwSetTombstones(metadata, null!));
    }

    [Fact]
    public void PruneLwwSetTombstones_ShouldRemoveFullyResolvedOlderTombstones()
    {
        // Arrange
        var metadata = new CrdtMetadata();
        var threshold = timestampProviderMock.Object.Create(200);
        
        var lwwSetState = new LwwSetState(new Dictionary<object, ICrdtTimestamp>(), new Dictionary<object, ICrdtTimestamp>());
        
        // Case 1: Removed, remove TS < threshold, add TS < remove TS (Fully resolved & old -> Prune)
        lwwSetState.Adds["prune-me"] = timestampProviderMock.Object.Create(100);
        lwwSetState.Removes["prune-me"] = timestampProviderMock.Object.Create(150);
        
        // Case 2: Removed, remove TS < threshold, but add TS > remove TS (Item was re-added -> DO NOT Prune Removes)
        lwwSetState.Adds["keep-me-readded"] = timestampProviderMock.Object.Create(180);
        lwwSetState.Removes["keep-me-readded"] = timestampProviderMock.Object.Create(150);
        
        // Case 3: Removed, remove TS >= threshold (Too new -> DO NOT Prune)
        lwwSetState.Adds["keep-me-new"] = timestampProviderMock.Object.Create(210);
        lwwSetState.Removes["keep-me-new"] = timestampProviderMock.Object.Create(250);

        // Case 4: Only removed (no add), remove TS < threshold -> Prune
        lwwSetState.Removes["prune-me-no-add"] = timestampProviderMock.Object.Create(150);

        metadata.LwwSets["$.set"] = lwwSetState;
        
        // Setup similar state for PriorityQueue to test it cascades correctly
        var priorityQueueState = new LwwSetState(
            new Dictionary<object, ICrdtTimestamp> { { "prune-me-pq", timestampProviderMock.Object.Create(100) } },
            new Dictionary<object, ICrdtTimestamp> { { "prune-me-pq", timestampProviderMock.Object.Create(150) } }
        );
        metadata.PriorityQueues["$.pq"] = priorityQueueState;

        // Act
        manager.PruneLwwSetTombstones(metadata, threshold);

        // Assert LWW-Sets
        lwwSetState.Adds.ShouldNotContainKey("prune-me");
        lwwSetState.Removes.ShouldNotContainKey("prune-me");
        
        lwwSetState.Removes.ShouldNotContainKey("prune-me-no-add");
        
        lwwSetState.Adds.ShouldContainKey("keep-me-readded");
        lwwSetState.Removes.ShouldContainKey("keep-me-readded");
        
        lwwSetState.Adds.ShouldContainKey("keep-me-new");
        lwwSetState.Removes.ShouldContainKey("keep-me-new");

        // Assert PriorityQueues
        priorityQueueState.Adds.ShouldNotContainKey("prune-me-pq");
        priorityQueueState.Removes.ShouldNotContainKey("prune-me-pq");
    }

    [Fact]
    public void PruneOrSetTombstones_WithNullArguments_ShouldThrowArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => manager.PruneOrSetTombstones(null!));
    }

    [Fact]
    public void PruneOrSetTombstones_ShouldRemoveFullyCoveredElements()
    {
        // Arrange
        var metadata = new CrdtMetadata();
        
        var orSetState = new OrSetState(new Dictionary<object, ISet<Guid>>(), new Dictionary<object, ISet<Guid>>());
        
        var tag1 = Guid.NewGuid();
        var tag2 = Guid.NewGuid();
        var tag3 = Guid.NewGuid();

        // Case 1: Fully covered (Adds subset of Removes) -> Prune
        orSetState.Adds["prune-me"] = new HashSet<Guid> { tag1 };
        orSetState.Removes["prune-me"] = new HashSet<Guid> { tag1, tag2 };
        
        // Case 2: Not fully covered (Adds has tag not in Removes) -> Do Not Prune
        orSetState.Adds["keep-me"] = new HashSet<Guid> { tag1, tag3 };
        orSetState.Removes["keep-me"] = new HashSet<Guid> { tag1 };

        // Case 3: No removes at all -> Do Not Prune
        orSetState.Adds["keep-me-new"] = new HashSet<Guid> { tag1 };
        
        metadata.OrSets["$.orset"] = orSetState;
        
        // Setup for OrMaps
        var orMapState = new OrSetState(
            new Dictionary<object, ISet<Guid>> { { "prune-map", new HashSet<Guid> { tag1 } } }, 
            new Dictionary<object, ISet<Guid>> { { "prune-map", new HashSet<Guid> { tag1 } } }
        );
        metadata.OrMaps["$.ormap"] = orMapState;

        // Setup for ReplicatedTrees
        var replicatedTreeState = new OrSetState(
            new Dictionary<object, ISet<Guid>> { { "prune-tree", new HashSet<Guid> { tag1 } } }, 
            new Dictionary<object, ISet<Guid>> { { "prune-tree", new HashSet<Guid> { tag1 } } }
        );
        metadata.ReplicatedTrees["$.tree"] = replicatedTreeState;

        // Act
        manager.PruneOrSetTombstones(metadata);

        // Assert OR-Sets
        orSetState.Adds.ShouldNotContainKey("prune-me");
        orSetState.Removes.ShouldNotContainKey("prune-me");

        orSetState.Adds.ShouldContainKey("keep-me");
        orSetState.Removes.ShouldContainKey("keep-me");
        
        orSetState.Adds.ShouldContainKey("keep-me-new");

        // Assert OR-Maps
        orMapState.Adds.ShouldNotContainKey("prune-map");
        orMapState.Removes.ShouldNotContainKey("prune-map");

        // Assert ReplicatedTrees
        replicatedTreeState.Adds.ShouldNotContainKey("prune-tree");
        replicatedTreeState.Removes.ShouldNotContainKey("prune-tree");
    }

    [Fact]
    public void PruneSeenExceptions_WithNullArguments_ShouldThrowArgumentNullException()
    {
        var metadata = new CrdtMetadata();
        var threshold = timestampProviderMock.Object.Create(1);
        Should.Throw<ArgumentNullException>(() => manager.PruneSeenExceptions(null!, threshold));
        Should.Throw<ArgumentNullException>(() => manager.PruneSeenExceptions(metadata, null!));
    }

    [Fact]
    public void PruneSeenExceptions_ShouldRemoveExceptionsOlderThanThreshold()
    {
        // Arrange
        var metadata = new CrdtMetadata();
        var threshold = timestampProviderMock.Object.Create(200);

        var op1 = new CrdtOperation(Guid.NewGuid(), "rep1", "$.path", OperationType.Upsert, null, timestampProviderMock.Object.Create(100), 1); // Older -> Prune
        var op2 = new CrdtOperation(Guid.NewGuid(), "rep1", "$.path", OperationType.Upsert, null, timestampProviderMock.Object.Create(200), 2); // Exact threshold -> Keep
        var op3 = new CrdtOperation(Guid.NewGuid(), "rep1", "$.path", OperationType.Upsert, null, timestampProviderMock.Object.Create(300), 3); // Newer -> Keep

        metadata.SeenExceptions.Add(op1);
        metadata.SeenExceptions.Add(op2);
        metadata.SeenExceptions.Add(op3);

        // Act
        manager.PruneSeenExceptions(metadata, threshold);

        // Assert
        metadata.SeenExceptions.Count.ShouldBe(2);
        metadata.SeenExceptions.ShouldNotContain(op1);
        metadata.SeenExceptions.ShouldContain(op2);
        metadata.SeenExceptions.ShouldContain(op3);
    }

    [Theory]
    [InlineData(100, 50)]
    [InlineData(100, 100)]
    public void AdvanceVersionVector_WhenOperationIsOldOrSame_ShouldDoNothing(long vector, long newOp)
    {
        // Arrange
        var metadata = new CrdtMetadata();
        var replicaId = "replica-1";
        metadata.VersionVector[replicaId] = vector;

        // Act
        manager.AdvanceVersionVector(metadata, replicaId, newOp);

        // Assert
        metadata.VersionVector[replicaId].ShouldBe(vector);
    }

    [Fact]
    public void AdvanceVersionVector_WithNoExceptionsAndNoGap_ShouldAdvanceVector()
    {
        // Arrange
        var metadata = new CrdtMetadata();
        var replicaId = "replica-1";
        metadata.VersionVector[replicaId] = 100;

        // Act
        manager.AdvanceVersionVector(metadata, replicaId, 101);

        // Assert
        metadata.VersionVector[replicaId].ShouldBe(101);
    }

    [Fact]
    public void AdvanceVersionVector_WithNoExceptionsAndGap_ShouldNotAdvanceVector()
    {
        // Arrange
        var metadata = new CrdtMetadata();
        var replicaId = "replica-1";
        long initialVector = 100;
        metadata.VersionVector[replicaId] = initialVector;

        // Act
        manager.AdvanceVersionVector(metadata, replicaId, 105);

        // Assert
        metadata.VersionVector[replicaId].ShouldBe(initialVector);
    }

    [Fact]
    public void AdvanceVersionVector_WithGapInExceptions_ShouldAdvanceToLastContiguousTimestamp()
    {
        // Arrange
        var metadata = new CrdtMetadata();
        var replicaId = "replica-1";
        metadata.VersionVector[replicaId] = 100;
        
        // Exceptions with a gap at 103
        metadata.SeenExceptions.Add(CreateOp(replicaId, 102));
        metadata.SeenExceptions.Add(CreateOp(replicaId, 104));
        
        // Act - Apply the contiguous next operation (101)
        manager.AdvanceVersionVector(metadata, replicaId, 101);

        // Assert - should eat 102, stop at 103 (gap), leaving 104 in exceptions. 102 is pruned.
        metadata.VersionVector[replicaId].ShouldBe(102);
        metadata.SeenExceptions.Count.ShouldBe(1);
        metadata.SeenExceptions.Single().Clock.ShouldBe(104);
    }

    [Fact]
    public void AdvanceVersionVector_WithAllExceptionsPresent_ShouldAdvanceToNewTimestampAndPruneAll()
    {
        // Arrange
        var metadata = new CrdtMetadata();
        var replicaId = "replica-1";
        metadata.VersionVector[replicaId] = 100;
        
        // Exceptions that fill the gap after 101
        metadata.SeenExceptions.Add(CreateOp(replicaId, 102));
        metadata.SeenExceptions.Add(CreateOp(replicaId, 103));
        metadata.SeenExceptions.Add(CreateOp(replicaId, 104));
        
        // Act - Apply 101
        manager.AdvanceVersionVector(metadata, replicaId, 101);

        // Assert - should consume up to 104, completely emptying SeenExceptions
        metadata.VersionVector[replicaId].ShouldBe(104);
        metadata.SeenExceptions.ShouldBeEmpty();
    }
    
    private CrdtOperation CreateOp(string replicaId, long clockValue)
        => new(Guid.NewGuid(), replicaId, "path", OperationType.Upsert, null, timestampProviderMock.Object.Create(0), clockValue);
}