namespace Ama.CRDT.UnitTests.Services;

using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Strategies;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using Xunit;

public sealed class CrdtMetadataManagerTests
{
    private readonly CrdtMetadataManager manager;
    private readonly Mock<ICrdtStrategyManager> strategyManagerMock;
    private readonly Mock<ICrdtTimestampProvider> timestampProviderMock;
    private readonly Mock<IElementComparerProvider> elementComparerProviderMock;

    public CrdtMetadataManagerTests()
    {
        strategyManagerMock = new Mock<ICrdtStrategyManager>();
        timestampProviderMock = new Mock<ICrdtTimestampProvider>();
        elementComparerProviderMock = new Mock<IElementComparerProvider>();
        manager = new CrdtMetadataManager(strategyManagerMock.Object, timestampProviderMock.Object, elementComparerProviderMock.Object);
    }
    
    [Theory]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, false, true)]
    public void PublicMethods_WithNullArguments_ShouldThrowArgumentNullException(bool testInitialize, bool testReset, bool testPrune, bool testAdvanceVector)
    {
        // Arrange
        var doc = new object();
        var metadata = new CrdtMetadata();
        var timestamp = new EpochTimestamp(1);
        var operation = new CrdtOperation();
        
        // Act & Assert
        if (testInitialize)
        {
            Should.Throw<ArgumentNullException>(() => manager.Initialize<object>(null!));
            Should.Throw<ArgumentNullException>(() => manager.Initialize<object>(null!, timestamp));
            Should.Throw<ArgumentNullException>(() => manager.Initialize(doc, null!));
            Should.Throw<ArgumentNullException>(() => manager.InitializeLwwMetadata(null!, doc));
            Should.Throw<ArgumentNullException>(() => manager.InitializeLwwMetadata<string>(metadata, null!));
            Should.Throw<ArgumentNullException>(() => manager.InitializeLwwMetadata(null!, doc, timestamp));
            Should.Throw<ArgumentNullException>(() => manager.InitializeLwwMetadata<string>(metadata, null!, timestamp));
            Should.Throw<ArgumentNullException>(() => manager.InitializeLwwMetadata(metadata, doc, null!));
        }
        
        if (testReset)
        {
            Should.Throw<ArgumentNullException>(() => manager.Reset(null!, doc));
            Should.Throw<ArgumentNullException>(() => manager.Reset<string>(metadata, null!));
            Should.Throw<ArgumentNullException>(() => manager.Reset(null!, doc, timestamp));
            Should.Throw<ArgumentNullException>(() => manager.Reset<string>(metadata, null!, timestamp));
            Should.Throw<ArgumentNullException>(() => manager.Reset(metadata, doc, null!));
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
    }

    [Fact]
    public void Reset_ShouldClearAllState()
    {
        // Arrange
        var metadata = new CrdtMetadata();
        metadata.Lww["$.a"] = new EpochTimestamp(100);
        metadata.PositionalTrackers["$.b"] = [];
        metadata.AverageRegisters["$.c"] = new Dictionary<string, AverageRegisterValue>();

        var doc = new object();
        timestampProviderMock.Setup(p => p.Now()).Returns(new EpochTimestamp(200));
        
        // Act
        manager.Reset(metadata, doc);
        
        // Assert
        metadata.Lww.ShouldBeEmpty();
        metadata.PositionalTrackers.ShouldBeEmpty();
        metadata.AverageRegisters.ShouldBeEmpty();
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
        var refOp = new CrdtOperation(Guid.NewGuid(), replicaId, "path", OperationType.Upsert, null, newTimestamp);

        // Act
        manager.AdvanceVersionVector(metadata, refOp);

        // Assert
        metadata.VersionVector[replicaId].ShouldBe(newTimestamp);
        metadata.SeenExceptions.Count.ShouldBe(2);
        metadata.SeenExceptions.ShouldNotContain(op1);
        metadata.SeenExceptions.ShouldContain(op2);
        metadata.SeenExceptions.ShouldContain(op3);
    }
    
    [Fact]
    public void AdvanceVersionVector_WhenNoExceptionsExist_ShouldOnlyUpdateVector()
    {
        // Arrange
        var metadata = new CrdtMetadata();
        var replicaId = "replica-1";
        var newTimestamp = new EpochTimestamp(100);

        var refOp = new CrdtOperation(Guid.NewGuid(), replicaId, "path", OperationType.Upsert, null, newTimestamp);
        
        // Act
        manager.AdvanceVersionVector(metadata, refOp);
        
        // Assert
        metadata.VersionVector[replicaId].ShouldBe(newTimestamp);
        metadata.SeenExceptions.ShouldBeEmpty();
    }
}