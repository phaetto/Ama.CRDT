namespace Modern.CRDT.UnitTests.Services;

using Modern.CRDT.Models;
using Modern.CRDT.Services;
using Modern.CRDT.Services.Strategies;
using Moq;
using Shouldly;
using System;
using Xunit;

public sealed class CrdtMetadataManagerTests
{
    private readonly CrdtMetadataManager manager;
    private readonly Mock<ICrdtStrategyManager> strategyManagerMock;
    private readonly Mock<ICrdtTimestampProvider> timestampProviderMock;

    public CrdtMetadataManagerTests()
    {
        strategyManagerMock = new Mock<ICrdtStrategyManager>();
        timestampProviderMock = new Mock<ICrdtTimestampProvider>();
        manager = new CrdtMetadataManager(strategyManagerMock.Object, timestampProviderMock.Object);
    }

    [Fact]
    public void PruneLwwTombstones_ShouldRemoveOlderEntries()
    {
        // Arrange
        var metadata = new CrdtMetadata();
        metadata.Lww["$.a"] = new EpochTimestamp(100);
        metadata.Lww["$.b"] = new EpochTimestamp(200);
        metadata.Lww["$.c"] = new EpochTimestamp(300);
        var threshold = new EpochTimestamp(250);
        
        // Act
        manager.PruneLwwTombstones(metadata, threshold);

        // Assert
        metadata.Lww.ShouldContainKey("$.c");
        metadata.Lww.ShouldNotContainKey("$.a");
        metadata.Lww.ShouldNotContainKey("$.b");
        metadata.Lww.Count.ShouldBe(1);
    }

    [Fact]
    public void AdvanceVersionVector_ShouldUpdateVectorAndCompactExceptions()
    {
        // Arrange
        var metadata = new CrdtMetadata();
        var replicaId = "replica-1";

        var op1 = new CrdtOperation(Guid.NewGuid(), replicaId, "path", OperationType.Upsert, null, new EpochTimestamp(100));
        var op2 = new CrdtOperation(Guid.NewGuid(), replicaId, "path", OperationType.Upsert, null, new EpochTimestamp(200));
        var op3 = new CrdtOperation(Guid.NewGuid(), "other-replica", "path", OperationType.Upsert, null, new EpochTimestamp(150));

        metadata.SeenExceptions.Add(op1);
        metadata.SeenExceptions.Add(op2);
        metadata.SeenExceptions.Add(op3);

        var newTimestamp = new EpochTimestamp(150);

        // Act
        manager.AdvanceVersionVector(metadata, replicaId, newTimestamp);

        // Assert
        metadata.VersionVector[replicaId].ShouldBe(newTimestamp);
        metadata.SeenExceptions.Count.ShouldBe(2);
        metadata.SeenExceptions.ShouldNotContain(op1);
        metadata.SeenExceptions.ShouldContain(op2); // Still in the future
        metadata.SeenExceptions.ShouldContain(op3); // Different replica
    }
    
    [Fact]
    public void AdvanceVersionVector_WhenNoExceptionsExist_ShouldOnlyUpdateVector()
    {
        // Arrange
        var metadata = new CrdtMetadata();
        var replicaId = "replica-1";
        var newTimestamp = new EpochTimestamp(100);

        // Act
        manager.AdvanceVersionVector(metadata, replicaId, newTimestamp);
        
        // Assert
        metadata.VersionVector[replicaId].ShouldBe(newTimestamp);
        metadata.SeenExceptions.ShouldBeEmpty();
    }
}