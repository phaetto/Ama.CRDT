namespace Ama.CRDT.UnitTests.Services;

using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Strategies;
using Microsoft.Extensions.Options;
using Ama.CRDT.Attributes;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

public sealed class CrdtApplicatorTests
{
    private sealed class TestModel
    {
        public string? Name { get; set; }

        [CrdtCounterStrategy]
        public int Likes { get; set; }
    }

    private readonly ICrdtApplicator applicator;

    public CrdtApplicatorTests()
    {
        var options = Options.Create(new CrdtOptions { ReplicaId = Guid.NewGuid().ToString() });
        var timestampProvider = new EpochTimestampProvider();
        var lwwStrategy = new LwwStrategy(options);
        var counterStrategy = new CounterStrategy(timestampProvider, options);
        var comparerProvider = new ElementComparerProvider(Enumerable.Empty<IElementComparer>());
        var arrayLcsStrategy = new ArrayLcsStrategy(comparerProvider, timestampProvider, options);
        var strategies = new ICrdtStrategy[] { lwwStrategy, counterStrategy, arrayLcsStrategy };
        var strategyManager = new CrdtStrategyManager(strategies);
        applicator = new CrdtApplicator(strategyManager);
    }

    [Fact]
    public void ApplyPatch_WithNullDocument_ShouldThrowArgumentNullException()
    {
        // Arrange
        TestModel? model = null;
        var patch = new CrdtPatch([]);
        var metadata = new CrdtMetadata();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => applicator.ApplyPatch(model!, patch, metadata));
    }

    [Fact]
    public void ApplyPatch_WithNullMetadata_ShouldThrowArgumentNullException()
    {
        // Arrange
        var model = new TestModel();
        var patch = new CrdtPatch([]);
        CrdtMetadata? metadata = null;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => applicator.ApplyPatch(model, patch, metadata!));
    }
    
    [Fact]
    public void ApplyPatch_WithStaleLwwOperation_ShouldNotApply()
    {
        // Arrange
        var model = new TestModel { Name = "Initial" };
        var metadata = new CrdtMetadata();
        var lwwStrategy = new LwwStrategy(Options.Create(new CrdtOptions { ReplicaId = "test" }));
        var opTimestamp = new EpochTimestamp(100);
        var existingTimestamp = new EpochTimestamp(200);

        metadata.Lww["$.name"] = existingTimestamp;
        var operation = new CrdtOperation(Guid.NewGuid(), "replica-A", "$.name", OperationType.Upsert, "Stale", opTimestamp);
        var patch = new CrdtPatch(new List<CrdtOperation> { operation });

        // Act
        var result = applicator.ApplyPatch(model, patch, metadata);

        // Assert
        result.Name.ShouldBe("Initial");
        metadata.Lww["$.name"].ShouldBe(existingTimestamp);
    }
    
    [Fact]
    public void ApplyPatch_WithOperationInSeenExceptions_ShouldNotApply()
    {
        // Arrange
        var model = new TestModel { Likes = 10 };
        var metadata = new CrdtMetadata();
        var operation = new CrdtOperation(Guid.NewGuid(), "replica-A", "$.likes", OperationType.Increment, 5m, new EpochTimestamp(150));
        metadata.SeenExceptions.Add(operation);
        var patch = new CrdtPatch(new List<CrdtOperation> { operation });

        // Act
        var result = applicator.ApplyPatch(model, patch, metadata);
        
        // Assert
        result.Likes.ShouldBe(10);
        metadata.SeenExceptions.Count.ShouldBe(1);
    }
    
    [Fact]
    public void ApplyPatch_WithNewCounterOperation_ShouldApplyAndAddToExceptions()
    {
        // Arrange
        var model = new TestModel { Likes = 10 };
        var metadata = new CrdtMetadata();
        var operation = new CrdtOperation(Guid.NewGuid(), "replica-A", "$.likes", OperationType.Increment, 5m, new EpochTimestamp(100));
        var patch = new CrdtPatch(new List<CrdtOperation> { operation });

        // Act
        var result = applicator.ApplyPatch(model, patch, metadata);

        // Assert
        result.Likes.ShouldBe(15);
        metadata.SeenExceptions.ShouldContain(operation);
    }

    [Fact]
    public void ApplyPatch_WhenAppliedMultipleTimes_IsIdempotent()
    {
        // Arrange
        var model = new TestModel { Name = "Initial", Likes = 10 };
        var metadata = new CrdtMetadata();
        var lwwOperation = new CrdtOperation(Guid.NewGuid(), "replica-A", "$.name", OperationType.Upsert, "Updated", new EpochTimestamp(100));
        var counterOperation = new CrdtOperation(Guid.NewGuid(), "replica-A", "$.likes", OperationType.Increment, 5m, new EpochTimestamp(100));
        var patch = new CrdtPatch(new List<CrdtOperation> { lwwOperation, counterOperation });

        // Act
        var result1 = applicator.ApplyPatch(model, patch, metadata);
        var result2 = applicator.ApplyPatch(result1, patch, metadata);

        // Assert
        result1.Name.ShouldBe("Updated");
        result1.Likes.ShouldBe(15);
        metadata.Lww["$.name"].ShouldBe(lwwOperation.Timestamp);
        metadata.SeenExceptions.Count.ShouldBe(1);
        metadata.SeenExceptions.ShouldContain(counterOperation);

        result2.Name.ShouldBe("Updated");
        result2.Likes.ShouldBe(15);
        metadata.Lww["$.name"].ShouldBe(lwwOperation.Timestamp);
        metadata.SeenExceptions.Count.ShouldBe(1);
    }
    
    [Fact]
    public void ApplyPatch_WithConcurrentPatches_IsCommutative()
    {
        // Arrange
        var initialModel = new TestModel { Name = "Initial", Likes = 10 };

        var opA_Lww = new CrdtOperation(Guid.NewGuid(), "replica-A", "$.name", OperationType.Upsert, "Name A", new EpochTimestamp(100));
        var opA_Counter = new CrdtOperation(Guid.NewGuid(), "replica-A", "$.likes", OperationType.Increment, 5m, new EpochTimestamp(110));
        var patchA = new CrdtPatch(new List<CrdtOperation> { opA_Lww, opA_Counter });

        var opB_Lww = new CrdtOperation(Guid.NewGuid(), "replica-B", "$.name", OperationType.Upsert, "Name B", new EpochTimestamp(200));
        var opB_Counter = new CrdtOperation(Guid.NewGuid(), "replica-B", "$.likes", OperationType.Increment, 3m, new EpochTimestamp(120));
        var patchB = new CrdtPatch(new List<CrdtOperation> { opB_Lww, opB_Counter });

        var metadata_AB = new CrdtMetadata();
        var model_afterA = applicator.ApplyPatch(new TestModel { Name = initialModel.Name, Likes = initialModel.Likes }, patchA, metadata_AB);
        var result_AB = applicator.ApplyPatch(model_afterA, patchB, metadata_AB);

        var metadata_BA = new CrdtMetadata();
        var model_afterB = applicator.ApplyPatch(new TestModel { Name = initialModel.Name, Likes = initialModel.Likes }, patchB, metadata_BA);
        var result_BA = applicator.ApplyPatch(model_afterB, patchA, metadata_BA);
        
        // Assert
        result_AB.Name.ShouldBe("Name B");
        result_AB.Likes.ShouldBe(18);
        
        result_BA.Name.ShouldBe("Name B");
        result_BA.Likes.ShouldBe(18);
    }
}