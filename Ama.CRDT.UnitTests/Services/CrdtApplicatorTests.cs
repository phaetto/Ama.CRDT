namespace Ama.CRDT.UnitTests.Services;

using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Strategies;
using Microsoft.Extensions.Options;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Strategies;
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

        [CrdtCounter]
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
    public void ApplyPatch_WithStaleLwwOperation_ShouldNotApply()
    {
        // Arrange
        var model = new TestModel { Name = "Initial" };
        var metadata = new CrdtMetadata();
        metadata.Lww["$.name"] = new EpochTimestamp(200);
        var operation = new CrdtOperation(Guid.NewGuid(), "replica-A", "$.name", OperationType.Upsert, "Stale", new EpochTimestamp(100));
        var patch = new CrdtPatch(new List<CrdtOperation> { operation });
        
        // Act
        var result = applicator.ApplyPatch(model, patch, metadata);

        // Assert
        result.Name.ShouldBe("Initial");
        metadata.Lww["$.name"].ShouldBe(new EpochTimestamp(200));
        metadata.SeenExceptions.ShouldBeEmpty();
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
        // First application
        var result1 = applicator.ApplyPatch(model, patch, metadata);

        // Second application
        var result2 = applicator.ApplyPatch(result1, patch, metadata);

        // Assert
        // State after first application
        result1.Name.ShouldBe("Updated");
        result1.Likes.ShouldBe(15);
        metadata.Lww["$.name"].ShouldBe(lwwOperation.Timestamp);
        metadata.SeenExceptions.Count.ShouldBe(1);
        metadata.SeenExceptions.ShouldContain(counterOperation);

        // State after second application (should be unchanged)
        result2.Name.ShouldBe("Updated");
        result2.Likes.ShouldBe(15);
        metadata.Lww["$.name"].ShouldBe(lwwOperation.Timestamp);
        metadata.SeenExceptions.Count.ShouldBe(1); // No new exceptions added
    }
    
    [Fact]
    public void ApplyPatch_WithConcurrentPatches_IsCommutative()
    {
        // Arrange
        var initialModel = new TestModel { Name = "Initial", Likes = 10 };

        // Patch A from replica A
        var opA_Lww = new CrdtOperation(Guid.NewGuid(), "replica-A", "$.name", OperationType.Upsert, "Name A", new EpochTimestamp(100));
        var opA_Counter = new CrdtOperation(Guid.NewGuid(), "replica-A", "$.likes", OperationType.Increment, 5m, new EpochTimestamp(110));
        var patchA = new CrdtPatch(new List<CrdtOperation> { opA_Lww, opA_Counter });

        // Patch B from replica B
        var opB_Lww = new CrdtOperation(Guid.NewGuid(), "replica-B", "$.name", OperationType.Upsert, "Name B", new EpochTimestamp(200)); // Higher timestamp wins
        var opB_Counter = new CrdtOperation(Guid.NewGuid(), "replica-B", "$.likes", OperationType.Increment, 3m, new EpochTimestamp(120));
        var patchB = new CrdtPatch(new List<CrdtOperation> { opB_Lww, opB_Counter });

        // Scenario 1: Apply A, then B
        var metadata_AB = new CrdtMetadata();
        var model_afterA = applicator.ApplyPatch(new TestModel { Name = initialModel.Name, Likes = initialModel.Likes }, patchA, metadata_AB);
        var result_AB = applicator.ApplyPatch(model_afterA, patchB, metadata_AB);

        // Scenario 2: Apply B, then A
        var metadata_BA = new CrdtMetadata();
        var model_afterB = applicator.ApplyPatch(new TestModel { Name = initialModel.Name, Likes = initialModel.Likes }, patchB, metadata_BA);
        var result_BA = applicator.ApplyPatch(model_afterB, patchA, metadata_BA);
        
        // Assert
        // Both scenarios should converge to the same state.
        // LWW: 'Name B' wins due to higher timestamp.
        // Counter: 10 + 5 + 3 = 18.
        result_AB.Name.ShouldBe("Name B");
        result_AB.Likes.ShouldBe(18);
        
        result_BA.Name.ShouldBe("Name B");
        result_BA.Likes.ShouldBe(18);
    }
}