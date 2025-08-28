namespace Ama.CRDT.UnitTests.Services;

using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Attributes;
using Shouldly;
using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
using Ama.CRDT.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Ama.CRDT.Services.Providers;

public sealed class CrdtPatcherTests : IDisposable
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
    private readonly ICrdtTimestampProvider timestampProvider;
    private readonly IServiceScope scope;

    public CrdtPatcherTests()
    {
        var services = new ServiceCollection();
        services.AddCrdt();
        services.AddSingleton<ICrdtTimestampProvider, SequentialTimestampProvider>();

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
        var ts = timestampProvider.Create(100);
        var model = new TestModel { Name = "Test", Likes = 10, Unchanged = 123 };
        var metadata = new CrdtMetadata();
        metadata.Lww["$.name"] = ts;
        metadata.Lww["$.likes"] = ts;
        metadata.Lww["$.unchanged"] = ts;

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
        var ts1 = timestampProvider.Create(100);
        var fromModel = new TestModel { Name = "Original", Likes = 5, Unchanged = 123 };
        var fromMeta = new CrdtMetadata();
        fromMeta.Lww["$.name"] = ts1;
        fromMeta.Lww["$.likes"] = ts1;
        fromMeta.Lww["$.unchanged"] = ts1;
        var from = new CrdtDocument<TestModel>(fromModel, fromMeta);

        var ts2 = timestampProvider.Create(200);
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
        fromMeta.Lww["$.nested.value"] = ts1;
        var from = new CrdtDocument<TestModel>(fromModel, fromMeta);

        var ts2 = timestampProvider.Create(200);
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