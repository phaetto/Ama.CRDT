namespace Ama.CRDT.UnitTests.Services;

using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Ama.CRDT.Extensions;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Attributes.Strategies;

public sealed class CrdtApplicatorTests : IDisposable
{
    internal sealed class TestModel
    {
        public string? Name { get; set; }

        [CrdtCounterStrategy]
        public int Likes { get; set; }
    }

    private readonly ICrdtApplicator applicator;
    private readonly ICrdtTimestampProvider timestampProvider;
    private readonly IServiceScope scope;
    private readonly string testReplicaId;

    public CrdtApplicatorTests()
    {
        testReplicaId = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddCrdt();
        services.AddCrdtAotContext<ServicesTestCrdtAotContext>();

        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        scope = scopeFactory.CreateScope(testReplicaId);

        applicator = scope.ServiceProvider.GetRequiredService<ICrdtApplicator>();
        timestampProvider = scope.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();
    }

    public void Dispose()
    {
        scope.Dispose();
    }

    [Fact]
    public void ApplyPatch_WithNullDocumentData_ShouldThrowArgumentNullException()
    {
        // Arrange
        var patch = new CrdtPatch([]);
        var document = new CrdtDocument<TestModel>(null!, new CrdtMetadata());

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => applicator.ApplyPatch(document, patch));
    }

    [Fact]
    public void ApplyPatch_WithNullMetadata_ShouldThrowArgumentNullException()
    {
        // Arrange
        var model = new TestModel();
        var patch = new CrdtPatch([]);
        var document = new CrdtDocument<TestModel>(model, null!);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => applicator.ApplyPatch(document, patch));
    }
    
    [Fact]
    public void ApplyPatch_WithStaleLwwOperation_ShouldNotApply()
    {
        // Arrange
        var model = new TestModel { Name = "Initial" };
        var metadata = new CrdtMetadata();
        var document = new CrdtDocument<TestModel>(model, metadata);
        var opTimestamp = timestampProvider.Create(100);
        var existingTimestamp = timestampProvider.Create(200);

        metadata.States["$.name"] = new CausalTimestamp(existingTimestamp, "replica-A", 1);
        var operation = new CrdtOperation(Guid.NewGuid(), "replica-A", "$.name", OperationType.Upsert, "Stale", opTimestamp, 1);
        var patch = new CrdtPatch(new List<CrdtOperation> { operation });

        // Act
        var result = applicator.ApplyPatch(document, patch);

        // Assert
        result.Document.Data!.Name.ShouldBe("Initial");
        ((CausalTimestamp)metadata.States["$.name"]).Timestamp.ShouldBe(existingTimestamp);
    }
    
    [Fact]
    public void ApplyPatch_WithOperationInSeenExceptions_ShouldNotApply()
    {
        // Arrange
        var model = new TestModel { Likes = 10 };
        var metadata = new CrdtMetadata();
        var document = new CrdtDocument<TestModel>(model, metadata);
        var operation = new CrdtOperation(Guid.NewGuid(), "replica-A", "$.likes", OperationType.Increment, 5m, timestampProvider.Create(150), 1);
        metadata.SeenExceptions.Add(operation);
        var patch = new CrdtPatch(new List<CrdtOperation> { operation });

        // Act
        var result = applicator.ApplyPatch(document, patch);
        
        // Assert
        result.Document.Data!.Likes.ShouldBe(10);
        metadata.SeenExceptions.Count.ShouldBe(1);
    }
    
    [Fact]
    public void ApplyPatch_WithNewCounterOperation_ShouldApplyAndAddToExceptions()
    {
        // Arrange
        var model = new TestModel { Likes = 10 };
        var metadata = new CrdtMetadata();
        var document = new CrdtDocument<TestModel>(model, metadata);
        var operation = new CrdtOperation(Guid.NewGuid(), "replica-A", "$.likes", OperationType.Increment, 5m, timestampProvider.Create(100), 1);
        var patch = new CrdtPatch(new List<CrdtOperation> { operation });

        // Act
        var result = applicator.ApplyPatch(document, patch);

        // Assert
        result.Document.Data!.Likes.ShouldBe(15);
    }

    [Fact]
    public void ApplyPatch_WhenAppliedMultipleTimes_IsIdempotent()
    {
        // Arrange
        var model = new TestModel { Name = "Initial", Likes = 10 };
        var metadata = new CrdtMetadata();
        var document = new CrdtDocument<TestModel>(model, metadata);
        // We use sequentially increasing clocks for operations within the same patch
        var lwwOperation = new CrdtOperation(Guid.NewGuid(), "replica-A", "$.name", OperationType.Upsert, "Updated", timestampProvider.Create(100), 1);
        var counterOperation = new CrdtOperation(Guid.NewGuid(), "replica-A", "$.likes", OperationType.Increment, 5m, timestampProvider.Create(100), 2);
        var patch = new CrdtPatch(new List<CrdtOperation> { lwwOperation, counterOperation });

        // Act
        var result1 = applicator.ApplyPatch(document, patch);
        var result2 = applicator.ApplyPatch(document, patch);

        // Assert
        result1.Document.Data!.Name.ShouldBe("Updated");
        result1.Document.Data!.Likes.ShouldBe(15);
        ((CausalTimestamp)metadata.States["$.name"]).Timestamp.ShouldBe(lwwOperation.Timestamp);
        // With the updated logic, SeenExceptions should be completely pruned for contiguous operations
        metadata.SeenExceptions.Count.ShouldBe(0);

        result2.Document.Data!.Name.ShouldBe("Updated");
        result2.Document.Data!.Likes.ShouldBe(15);
        ((CausalTimestamp)metadata.States["$.name"]).Timestamp.ShouldBe(lwwOperation.Timestamp);
        metadata.SeenExceptions.Count.ShouldBe(0);
    }
    
    [Fact]
    public void ApplyPatch_WithConcurrentPatches_IsCommutative()
    {
        // Arrange
        var initialModel = new TestModel { Name = "Initial", Likes = 10 };

        var opA_Lww = new CrdtOperation(Guid.NewGuid(), "replica-A", "$.name", OperationType.Upsert, "Name A", timestampProvider.Create(100), 1);
        var opA_Counter = new CrdtOperation(Guid.NewGuid(), "replica-A", "$.likes", OperationType.Increment, 5m, timestampProvider.Create(110), 2);
        var patchA = new CrdtPatch(new List<CrdtOperation> { opA_Lww, opA_Counter });

        var opB_Lww = new CrdtOperation(Guid.NewGuid(), "replica-B", "$.name", OperationType.Upsert, "Name B", timestampProvider.Create(200), 1);
        var opB_Counter = new CrdtOperation(Guid.NewGuid(), "replica-B", "$.likes", OperationType.Increment, 3m, timestampProvider.Create(120), 2);
        var patchB = new CrdtPatch(new List<CrdtOperation> { opB_Lww, opB_Counter });

        var metadata_AB = new CrdtMetadata();
        var model_AB = new TestModel { Name = initialModel.Name, Likes = initialModel.Likes };
        var doc_AB = new CrdtDocument<TestModel>(model_AB, metadata_AB);
        applicator.ApplyPatch(doc_AB, patchA);
        var result_AB = applicator.ApplyPatch(doc_AB, patchB);

        var metadata_BA = new CrdtMetadata();
        var model_BA = new TestModel { Name = initialModel.Name, Likes = initialModel.Likes };
        var doc_BA = new CrdtDocument<TestModel>(model_BA, metadata_BA);
        applicator.ApplyPatch(doc_BA, patchB);
        var result_BA = applicator.ApplyPatch(doc_BA, patchA);
        
        // Assert
        result_AB.Document.Data!.Name.ShouldBe("Name B");
        result_AB.Document.Data!.Likes.ShouldBe(18);
        
        result_BA.Document.Data!.Name.ShouldBe("Name B");
        result_BA.Document.Data!.Likes.ShouldBe(18);
    }

    [Fact]
    public void ApplyPatch_WithGlobalClock_ShouldUpdateReplicaContextGlobalVersionVector()
    {
        // Arrange
        var model = new TestModel { Likes = 10 };
        var metadata = new CrdtMetadata();
        var document = new CrdtDocument<TestModel>(model, metadata);
        
        // This operation represents an incoming change from "replica-B" with a global clock of 5.
        var operation = new CrdtOperation(Guid.NewGuid(), "replica-B", "$.likes", OperationType.Increment, 5m, timestampProvider.Create(100), 1, 5);
        var patch = new CrdtPatch(new List<CrdtOperation> { operation });

        var replicaContext = scope.ServiceProvider.GetRequiredService<ReplicaContext>();
        
        // Act
        applicator.ApplyPatch(document, patch);

        // Assert
        replicaContext.GlobalVersionVector.Includes("replica-B", 5).ShouldBeTrue();
    }
    
    [Fact]
    public void ApplyPatch_WithStaleLocalOperationButNewGlobalClock_ShouldAcknowledgeGlobalClock()
    {
        // Arrange
        var model = new TestModel { Name = "Initial" };
        var metadata = new CrdtMetadata();
        
        // The document ALREADY knows about replica-B's document-clock 1
        metadata.VersionVector["replica-B"] = 1;
        var document = new CrdtDocument<TestModel>(model, metadata);
        
        // But this operation brings a new global clock of 6
        var operation = new CrdtOperation(Guid.NewGuid(), "replica-B", "$.name", OperationType.Upsert, "Stale", timestampProvider.Create(100), 1, 6);
        var patch = new CrdtPatch(new List<CrdtOperation> { operation });

        var replicaContext = scope.ServiceProvider.GetRequiredService<ReplicaContext>();
        
        // Act
        var result = applicator.ApplyPatch(document, patch);

        // Assert
        result.UnappliedOperations.ShouldNotBeEmpty();
        result.UnappliedOperations.First().Reason.ShouldBe(CrdtOperationStatus.Obsolete);
        
        // Even though it was stale for the document, the global causality must acknowledge it saw sequence 6
        replicaContext.GlobalVersionVector.Includes("replica-B", 6).ShouldBeTrue();
    }
}