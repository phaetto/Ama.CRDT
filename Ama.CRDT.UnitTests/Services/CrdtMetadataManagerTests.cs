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
        timestampProviderMock.Setup(p => p.Create(It.IsAny<long>())).Returns<long>(v => new SequentialTimestamp(v));
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

        // Assert - should eat 102, stop at 103 (gap), leaving 104 in exceptions
        metadata.VersionVector[replicaId].ShouldBe(102);
        metadata.SeenExceptions.Count.ShouldBe(1);
        metadata.SeenExceptions.Single().Clock.ShouldBe(104);
    }

    [Fact]
    public void AdvanceVersionVector_WithAllExceptionsPresent_ShouldAdvanceToNewTimestampAndClearExceptions()
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

        // Assert - should consume up to 104
        metadata.VersionVector[replicaId].ShouldBe(104);
        metadata.SeenExceptions.ShouldBeEmpty();
    }
    
    private CrdtOperation CreateOp(string replicaId, long clockValue)
        => new(Guid.NewGuid(), replicaId, "path", OperationType.Upsert, null, timestampProviderMock.Object.Create(0), clockValue);
}