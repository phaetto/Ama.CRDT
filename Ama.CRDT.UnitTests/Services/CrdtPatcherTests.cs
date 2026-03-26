namespace Ama.CRDT.UnitTests.Services;

using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Shouldly;
using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
using Ama.CRDT.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Attributes.Strategies;

public sealed class CrdtPatcherTests : IDisposable
{
    private sealed record TestModel
    {
        public string? Name { get; init; }

        [CrdtCounterStrategy]
        public int Likes { get; init; }

        public NestedModel? Nested { get; init; }

        public long Unchanged { get; init; }

        [CrdtOrSetStrategy]
        public List<string>? Tags { get; init; }

        [CrdtLwwMapStrategy]
        public Dictionary<string, int>? Scores { get; init; }
    }

    private sealed record NestedModel
    {
        [CrdtLwwStrategy]
        public string? Value { get; init; }
    }

    private readonly ICrdtPatcher patcher;
    private readonly ICrdtTimestampProvider timestampProvider;
    private readonly IServiceScope scope;

    public CrdtPatcherTests()
    {
        var services = new ServiceCollection();
        services.AddCrdt();

        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        scope = scopeFactory.CreateScope("test-patcher");

        patcher = scope.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        timestampProvider = scope.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();
    }

    public void Dispose()
    {
        scope.Dispose();
    }

    [Fact]
    public void GeneratePatch_WithNullMetadata_ShouldThrowArgumentNullException()
    {
        // Arrange
        var model = new TestModel();
        var fromWithNullMeta = new CrdtDocument<TestModel>(model, null!);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => patcher.GeneratePatch(fromWithNullMeta, model));
    }

    [Fact]
    public void GeneratePatch_WhenNoChanges_ShouldReturnEmptyPatch()
    {
        // Arrange
        var ts = timestampProvider.Create(100);
        var model = new TestModel { Name = "Test", Likes = 10, Unchanged = 123 };
        var metadata = new CrdtMetadata();
        metadata.Lww["$.name"] = new CausalTimestamp(ts, "test-replica", 1);
        metadata.Lww["$.likes"] = new CausalTimestamp(ts, "test-replica", 1);
        metadata.Lww["$.unchanged"] = new CausalTimestamp(ts, "test-replica", 1);

        var from = new CrdtDocument<TestModel>(model, metadata);

        // Act
        var patch = patcher.GeneratePatch(from, model);

        // Assert
        patch.Operations.ShouldBeEmpty();
    }

    [Fact]
    public void GeneratePatch_WithMixedStrategyProperties_ShouldGenerateCorrectOperations()
    {
        // Arrange
        var ts1 = timestampProvider.Create(100);
        var fromModel = new TestModel { Name = "Original", Likes = 5, Unchanged = 123 };
        var fromMeta = new CrdtMetadata();
        fromMeta.Lww["$.name"] = new CausalTimestamp(ts1, "test-replica", 1);
        fromMeta.Lww["$.likes"] = new CausalTimestamp(ts1, "test-replica", 1);
        fromMeta.Lww["$.unchanged"] = new CausalTimestamp(ts1, "test-replica", 1);
        var from = new CrdtDocument<TestModel>(fromModel, fromMeta);

        var ts2 = timestampProvider.Create(200);
        var toModel = new TestModel { Name = "Updated", Likes = 15, Unchanged = 123 };

        // Act
        var patch = patcher.GeneratePatch(from, toModel, ts2);

        // Assert
        patch.Operations.Count.ShouldBe(2);

        var lwwOp = patch.Operations.FirstOrDefault(op => op.JsonPath == "$.name");
        lwwOp.Type.ShouldBe(OperationType.Upsert);
        lwwOp.Value!.ShouldBe("Updated");
        // The timestamp should be the one from the 'to' document's metadata, indicating a win
        lwwOp.Timestamp.ShouldBe(ts2);

        var counterOp = patch.Operations.FirstOrDefault(op => op.JsonPath == "$.likes");
        counterOp.Type.ShouldBe(OperationType.Increment);
        counterOp.Value!.ShouldBe(10m);
    }

    [Fact]
    public void GeneratePatch_WithNestedObjectChanges_ShouldGenerateCorrectPath()
    {
        // Arrange
        var ts1 = timestampProvider.Create(100);
        var fromModel = new TestModel { Nested = new NestedModel { Value = "Nested Original" } };
        var fromMeta = new CrdtMetadata();
        fromMeta.Lww["$.nested.value"] = new CausalTimestamp(ts1, "test-replica", 1);
        var from = new CrdtDocument<TestModel>(fromModel, fromMeta);

        var ts2 = timestampProvider.Create(200);
        var toModel = new TestModel { Nested = new NestedModel { Value = "Nested Updated" } };

        // Act
        var patch = patcher.GeneratePatch(from, toModel, ts2);

        // Assert
        patch.Operations.Count.ShouldBe(1);

        var op = patch.Operations.Single();
        op.JsonPath.ShouldBe("$.nested.value");
        op.Type.ShouldBe(OperationType.Upsert);
        op.Value!.ShouldBe("Nested Updated");
        op.Timestamp.ShouldBe(ts2);
    }

    [Fact]
    public void GeneratePatch_ShouldIncrementAndAssignGlobalClock()
    {
        // Arrange
        var fromModel = new TestModel { Name = "Original", Likes = 5 };
        var from = new CrdtDocument<TestModel>(fromModel, new CrdtMetadata());
        var toModel = new TestModel { Name = "Updated", Likes = 15 };
        
        var replicaContext = scope.ServiceProvider.GetRequiredService<ReplicaContext>();
        // Pre-fill the global vector to ensure we increment from the existing state.
        // DottedVersionVector expects continuous adds to advance the main sequence number.
        for (long i = 1; i <= 5; i++)
        {
            replicaContext.GlobalVersionVector.Add("test-patcher", i);
        }

        // Act
        var patch = patcher.GeneratePatch(from, toModel);

        // Assert
        patch.Operations.Count.ShouldBe(2);

        // They should receive sequential global clocks starting after 5
        var nameOp = patch.Operations.First(o => o.JsonPath == "$.name");
        var likesOp = patch.Operations.First(o => o.JsonPath == "$.likes");
        
        // Either could be generated first depending on reflection ordering, 
        // but they must be 6 and 7.
        var clocks = new[] { nameOp.GlobalClock, likesOp.GlobalClock };
        clocks.ShouldContain(6L);
        clocks.ShouldContain(7L);

        // The replica context global version vector must be updated
        replicaContext.GlobalVersionVector.Versions["test-patcher"].ShouldBe(7L);
    }

    [Fact]
    public void BuildOperation_Set_ShouldProduceCorrectOperation()
    {
        // Arrange
        var model = new TestModel { Name = "Original" };
        var metadata = new CrdtMetadata();
        var doc = new CrdtDocument<TestModel>(model, metadata);

        // Act
        var op = patcher.BuildOperation(doc, m => m.Name).Set("Updated");

        // Assert
        op.JsonPath.ShouldBe("$.name");
        op.Type.ShouldBe(OperationType.Upsert);
        op.Value.ShouldBe("Updated");
        
        var replicaContext = scope.ServiceProvider.GetRequiredService<ReplicaContext>();
        op.ReplicaId.ShouldBe(replicaContext.ReplicaId);
        op.Clock.ShouldBe(1);
        op.GlobalClock.ShouldBe(1);
    }

    [Fact]
    public void BuildOperation_Increment_ShouldProduceCorrectOperation()
    {
        // Arrange
        var model = new TestModel { Likes = 5 };
        var metadata = new CrdtMetadata();
        var doc = new CrdtDocument<TestModel>(model, metadata);

        // Act
        var op = patcher.BuildOperation(doc, m => m.Likes).Increment(10);

        // Assert
        op.JsonPath.ShouldBe("$.likes");
        op.Type.ShouldBe(OperationType.Increment);
        op.Value.ShouldBe(10);
        op.GlobalClock.ShouldBe(1);
    }

    [Fact]
    public void BuildOperation_ListAdd_ShouldProduceCorrectOperation()
    {
        // Arrange
        var model = new TestModel { Tags = [] };
        var metadata = new CrdtMetadata();
        var doc = new CrdtDocument<TestModel>(model, metadata);

        // Act
        var op = patcher.BuildOperation(doc, m => m.Tags).Add("NewTag");

        // Assert
        op.JsonPath.ShouldBe("$.tags");
        op.Type.ShouldBe(OperationType.Upsert);
        op.Value.ShouldNotBeNull();
        op.GlobalClock.ShouldBe(1);
    }

    [Fact]
    public void BuildOperation_DictionaryMapSet_ShouldProduceCorrectOperation()
    {
        // Arrange
        var model = new TestModel { Scores = new Dictionary<string, int>() };
        var metadata = new CrdtMetadata();
        var doc = new CrdtDocument<TestModel>(model, metadata);

        // Act
        var op = patcher.BuildOperation(doc, m => m.Scores).Set("Player1", 100);

        // Assert
        op.JsonPath.ShouldStartWith("$.scores");
        op.Type.ShouldBe(OperationType.Upsert);
        op.Value.ShouldBe(new KeyValuePair<object, object>("Player1", 100));
        op.GlobalClock.ShouldBe(1);
    }
}