namespace Ama.CRDT.UnitTests.Services;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Strategies;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        manager = new CrdtMetadataManager(strategyProviderMock.Object, timestampProviderMock.Object, elementComparerProviderMock.Object);
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
            Should.Throw<ArgumentNullException>(() => manager.AdvanceVersionVector(null!, "r", timestamp));
            Should.Throw<ArgumentNullException>(() => manager.AdvanceVersionVector(metadata, "r", null!));
            Should.Throw<ArgumentException>(() => manager.AdvanceVersionVector(metadata, null!, timestamp));
            Should.Throw<ArgumentException>(() => manager.AdvanceVersionVector(metadata, " ", timestamp));
        }

        if (testLocking)
        {
            Should.Throw<ArgumentNullException>(() => manager.ExclusiveLock(null!, "p", "lh", timestamp));
            Should.Throw<ArgumentException>(() => manager.ExclusiveLock(metadata, null!, "lh", timestamp));
            Should.Throw<ArgumentException>(() => manager.ExclusiveLock(metadata, "p", null!, timestamp));
            Should.Throw<ArgumentNullException>(() => manager.ExclusiveLock(metadata, "p", "lh", null!));
            Should.Throw<ArgumentNullException>(() => manager.ReleaseLock(null!, "p", timestamp));
            Should.Throw<ArgumentException>(() => manager.ReleaseLock(metadata, null!, timestamp));
            Should.Throw<ArgumentNullException>(() => manager.ReleaseLock(metadata, "p", null!));
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
        metadata.PriorityQueues["$.d"] = (new Dictionary<object, ICrdtTimestamp>(), new Dictionary<object, ICrdtTimestamp>());
        metadata.ExclusiveLocks["$.e"] = new LockInfo("holder", timestampProviderMock.Object.Create(100));
        metadata.LwwMaps["$.f"] = new Dictionary<object, ICrdtTimestamp>();
        metadata.OrMaps["$.g"] = (new Dictionary<object, ISet<Guid>>(), new Dictionary<object, ISet<Guid>>());

        var doc = new object();
        timestampProviderMock.Setup(p => p.Now()).Returns(timestampProviderMock.Object.Create(200));
        
        // Act
        manager.Reset(new CrdtDocument<object>(doc, metadata));
        
        // Assert
        metadata.Lww.ShouldBeEmpty();
        metadata.PositionalTrackers.ShouldBeEmpty();
        metadata.AverageRegisters.ShouldBeEmpty();
        metadata.PriorityQueues.ShouldBeEmpty();
        metadata.ExclusiveLocks.ShouldBeEmpty();
        metadata.LwwMaps.ShouldBeEmpty();
        metadata.OrMaps.ShouldBeEmpty();
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
    public void AdvanceVersionVector_WhenProviderIsNotContinuous_ShouldDoNothing()
    {
        // Arrange
        var metadata = new CrdtMetadata();
        var replicaId = "replica-1";
        timestampProviderMock.Setup(p => p.IsContinuous).Returns(false);

        // Act
        manager.AdvanceVersionVector(metadata, replicaId, timestampProviderMock.Object.Create(100));

        // Assert
        metadata.VersionVector.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(100, 50)]
    [InlineData(100, 100)]
    public void AdvanceVersionVector_WhenOperationIsOldOrSame_ShouldDoNothing(long vector, long newOp)
    {
        // Arrange
        SetupSequentialTimestampProviderMock();
        var metadata = new CrdtMetadata();
        var replicaId = "replica-1";
        metadata.VersionVector[replicaId] = timestampProviderMock.Object.Create(vector);

        // Act
        manager.AdvanceVersionVector(metadata, replicaId, timestampProviderMock.Object.Create(newOp));

        // Assert
        metadata.VersionVector[replicaId].ShouldBe(timestampProviderMock.Object.Create(vector));
    }

    [Fact]
    public void AdvanceVersionVector_WithNoExceptionsAndNoGap_ShouldAdvanceVector()
    {
        // Arrange
        SetupSequentialTimestampProviderMock();
        var metadata = new CrdtMetadata();
        var replicaId = "replica-1";
        metadata.VersionVector[replicaId] = timestampProviderMock.Object.Create(100);
        var newTimestamp = timestampProviderMock.Object.Create(101);

        // Act
        manager.AdvanceVersionVector(metadata, replicaId, newTimestamp);

        // Assert
        metadata.VersionVector[replicaId].ShouldBe(newTimestamp);
    }

    [Fact]
    public void AdvanceVersionVector_WithNoExceptionsAndGap_ShouldNotAdvanceVector()
    {
        // Arrange
        SetupSequentialTimestampProviderMock();
        var metadata = new CrdtMetadata();
        var replicaId = "replica-1";
        var initialVector = timestampProviderMock.Object.Create(100);
        metadata.VersionVector[replicaId] = initialVector;
        var newTimestamp = timestampProviderMock.Object.Create(105);

        // Act
        manager.AdvanceVersionVector(metadata, replicaId, newTimestamp);

        // Assert
        metadata.VersionVector[replicaId].ShouldBe(initialVector);
    }

    [Fact]
    public void AdvanceVersionVector_WithGapInExceptions_ShouldAdvanceToLastContiguousTimestamp()
    {
        // Arrange
        SetupSequentialTimestampProviderMock();
        var metadata = new CrdtMetadata();
        var replicaId = "replica-1";
        metadata.VersionVector[replicaId] = timestampProviderMock.Object.Create(100);
        
        // Exceptions with a gap at 103
        metadata.SeenExceptions.Add(CreateOp(replicaId, 101));
        metadata.SeenExceptions.Add(CreateOp(replicaId, 102));
        metadata.SeenExceptions.Add(CreateOp(replicaId, 104));
        
        // Act
        manager.AdvanceVersionVector(metadata, replicaId, timestampProviderMock.Object.Create(105));

        // Assert
        metadata.VersionVector[replicaId].ShouldBe(timestampProviderMock.Object.Create(102));
        metadata.SeenExceptions.Count.ShouldBe(1);
        metadata.SeenExceptions.Single().Timestamp.ShouldBe(timestampProviderMock.Object.Create(104));
    }

    [Fact]
    public void AdvanceVersionVector_WithAllExceptionsPresent_ShouldAdvanceToNewTimestampAndClearExceptions()
    {
        // Arrange
        SetupSequentialTimestampProviderMock();
        var metadata = new CrdtMetadata();
        var replicaId = "replica-1";
        metadata.VersionVector[replicaId] = timestampProviderMock.Object.Create(100);
        
        // Exceptions that fill the gap
        metadata.SeenExceptions.Add(CreateOp(replicaId, 101));
        metadata.SeenExceptions.Add(CreateOp(replicaId, 102));
        metadata.SeenExceptions.Add(CreateOp(replicaId, 103));
        metadata.SeenExceptions.Add(CreateOp(replicaId, 104));
        
        // Act
        manager.AdvanceVersionVector(metadata, replicaId, timestampProviderMock.Object.Create(105));

        // Assert
        metadata.VersionVector[replicaId].ShouldBe(timestampProviderMock.Object.Create(105));
        metadata.SeenExceptions.ShouldBeEmpty();
    }

    [Fact]
    public void ExclusiveLock_ShouldSetLock_WhenTimestampIsNewer()
    {
        // Arrange
        var metadata = new CrdtMetadata();
        var path = "$.prop";
        manager.ExclusiveLock(metadata, path, "holder1", timestampProviderMock.Object.Create(100));

        // Act
        manager.ExclusiveLock(metadata, path, "holder2", timestampProviderMock.Object.Create(200));

        // Assert
        metadata.ExclusiveLocks[path].ShouldNotBeNull();
        metadata.ExclusiveLocks[path]?.LockHolderId.ShouldBe("holder2");
        metadata.ExclusiveLocks[path]?.Timestamp.ShouldBe(timestampProviderMock.Object.Create(200));
    }

    [Fact]
    public void ReleaseLock_ShouldReleaseLock_WhenTimestampIsNewer()
    {
        // Arrange
        var metadata = new CrdtMetadata();
        var path = "$.prop";
        manager.ExclusiveLock(metadata, path, "holder1", timestampProviderMock.Object.Create(100));

        // Act
        manager.ReleaseLock(metadata, path, timestampProviderMock.Object.Create(200));
        
        // Assert
        metadata.ExclusiveLocks[path].ShouldBeNull();
    }
    
    [Fact]
    public void Initialize_WithLockedProperty_ShouldNotPopulateExclusiveLocks()
    {
        // Arrange
        var model = new TestLockModel { UserId = "user1", LockedValue = "abc" };
        var property = typeof(TestLockModel).GetProperty(nameof(TestLockModel.LockedValue))!;
        strategyProviderMock.Setup(m => m.GetStrategy(It.Is<PropertyInfo>(p => p.Name == nameof(TestLockModel.LockedValue))))
                           .Returns(new ExclusiveLockStrategy(new ReplicaContext { ReplicaId = "test" }));
        
        // Act
        var metadata = manager.Initialize(model, timestampProviderMock.Object.Create(100));

        // Assert
        metadata.ExclusiveLocks.ShouldContainKey("$.lockedValue");
        metadata.ExclusiveLocks["$.lockedValue"].ShouldBeNull();
    }

    private sealed class TestLockModel
    {
        public string UserId { get; set; }

        [CrdtExclusiveLockStrategy("$.userId")]
        public string LockedValue { get; set; }
    }
    
    private void SetupSequentialTimestampProviderMock()
    {
        timestampProviderMock.Setup(p => p.IsContinuous).Returns(true);
        timestampProviderMock.Setup(p => p.Init()).Returns(timestampProviderMock.Object.Create(0));
        timestampProviderMock.Setup(p => p.IterateBetween(It.IsAny<ICrdtTimestamp>(), It.IsAny<ICrdtTimestamp>()))
            .Returns<ICrdtTimestamp, ICrdtTimestamp>((start, end) =>
            {
                var startVal = ((SequentialTimestamp)start).Value;
                var endVal = ((SequentialTimestamp)end).Value;
                var result = new List<ICrdtTimestamp>();
                for (var i = startVal + 1; i < endVal; i++)
                {
                    result.Add(timestampProviderMock.Object.Create(i));
                }
                return result;
            });
    }

    private CrdtOperation CreateOp(string replicaId, long timestampValue)
        => new(Guid.NewGuid(), replicaId, "path", OperationType.Upsert, null, timestampProviderMock.Object.Create(timestampValue));
}