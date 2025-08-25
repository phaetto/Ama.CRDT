namespace Ama.CRDT.UnitTests.Services;

using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Strategies;
using Microsoft.Extensions.Options;
using Ama.CRDT.Attributes;
using Shouldly;
using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class CrdtPatcherTests
{
    private sealed record TestModel
    {
        public string? Name { get; init; }

        [CrdtCounterStrategy]
        public int Likes { get; init; }

        public NestedModel? Nested { get; init; }

        public long Unchanged { get; init; }

        public List<string>? Tags { get; init; }
    }

    private sealed record NestedModel
    {
        [CrdtLwwStrategy]
        public string? Value { get; init; }
    }

    private readonly ICrdtPatcher patcher;

    public CrdtPatcherTests()
    {
        var options = Options.Create(new CrdtOptions { ReplicaId = "test-patcher" });
        var timestampProvider = new EpochTimestampProvider();
        var lwwStrategy = new LwwStrategy(options);
        var counterStrategy = new CounterStrategy(timestampProvider, options);
        var comparerProvider = new ElementComparerProvider(Enumerable.Empty<IElementComparer>());
        var arrayStrategy = new SortedSetStrategy(comparerProvider, timestampProvider, options);
        var strategies = new ICrdtStrategy[] { lwwStrategy, counterStrategy, arrayStrategy };
        var strategyManager = new CrdtStrategyManager(strategies);
        patcher = new CrdtPatcher(strategyManager);
    }

    [Fact]
    public void GeneratePatch_WithNullMetadata_ShouldThrowArgumentNullException()
    {
        // Arrange
        var model = new TestModel();
        var fromWithNullMeta = new CrdtDocument<TestModel>(model, null);
        var toWithNullMeta = new CrdtDocument<TestModel>(model, null);
        var validDoc = new CrdtDocument<TestModel>(model, new CrdtMetadata());

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => patcher.GeneratePatch(fromWithNullMeta, validDoc));
        Should.Throw<ArgumentNullException>(() => patcher.GeneratePatch(validDoc, toWithNullMeta));
    }

    [Fact]
    public void GeneratePatch_WhenNoChanges_ShouldReturnEmptyPatch()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var model = new TestModel { Name = "Test", Likes = 10, Unchanged = 123 };
        var metadata = new CrdtMetadata();
        metadata.Lww["$.name"] = new EpochTimestamp(now);
        metadata.Lww["$.likes"] = new EpochTimestamp(now);
        metadata.Lww["$.unchanged"] = new EpochTimestamp(now);

        var from = new CrdtDocument<TestModel>(model, metadata);
        var to = new CrdtDocument<TestModel>(model, metadata);

        // Act
        var patch = patcher.GeneratePatch(from, to);

        // Assert
        patch.Operations.ShouldBeEmpty();
    }

    [Fact]
    public void GeneratePatch_WithMixedStrategyProperties_ShouldGenerateCorrectOperations()
    {
        // Arrange
        var ts1 = new EpochTimestamp(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var fromModel = new TestModel { Name = "Original", Likes = 5, Unchanged = 123 };
        var fromMeta = new CrdtMetadata();
        fromMeta.Lww["$.name"] = ts1;
        fromMeta.Lww["$.likes"] = ts1;
        fromMeta.Lww["$.unchanged"] = ts1;
        var from = new CrdtDocument<TestModel>(fromModel, fromMeta);

        var ts2 = new EpochTimestamp(ts1.Value + 100);
        var toModel = new TestModel { Name = "Updated", Likes = 15, Unchanged = 123 };
        var toMeta = new CrdtMetadata();
        toMeta.Lww["$.name"] = ts2;
        toMeta.Lww["$.likes"] = ts2;
        toMeta.Lww["$.unchanged"] = ts1;
        var to = new CrdtDocument<TestModel>(toModel, toMeta);

        // Act
        var patch = patcher.GeneratePatch(from, to);

        // Assert
        patch.Operations.Count.ShouldBe(2);

        var lwwOp = patch.Operations.FirstOrDefault(op => op.JsonPath == "$.name");
        lwwOp!.Type.ShouldBe(OperationType.Upsert);
        lwwOp.Value!.ShouldBe("Updated");
        lwwOp.Timestamp.ShouldBe(ts2);

        var counterOp = patch.Operations.FirstOrDefault(op => op.JsonPath == "$.likes");
        counterOp!.Type.ShouldBe(OperationType.Increment);
        counterOp.Value!.ShouldBe(10m);
    }

    [Fact]
    public void GeneratePatch_WithNestedObjectChanges_ShouldGenerateCorrectPath()
    {
        // Arrange
        var ts1 = new EpochTimestamp(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var fromModel = new TestModel { Nested = new NestedModel { Value = "Nested Original" } };
        var fromMeta = new CrdtMetadata();
        fromMeta.Lww["$.nested.value"] = ts1;
        var from = new CrdtDocument<TestModel>(fromModel, fromMeta);

        var ts2 = new EpochTimestamp(ts1.Value + 100);
        var toModel = new TestModel { Nested = new NestedModel { Value = "Nested Updated" } };
        var toMeta = new CrdtMetadata();
        toMeta.Lww["$.nested.value"] = ts2;
        var to = new CrdtDocument<TestModel>(toModel, toMeta);

        // Act
        var patch = patcher.GeneratePatch(from, to);

        // Assert
        patch.Operations.Count.ShouldBe(1);

        var op = patch.Operations.Single();
        op.JsonPath.ShouldBe("$.nested.value");
        op.Type.ShouldBe(OperationType.Upsert);
        op.Value!.ShouldBe("Nested Updated");
        op.Timestamp.ShouldBe(ts2);
    }
}