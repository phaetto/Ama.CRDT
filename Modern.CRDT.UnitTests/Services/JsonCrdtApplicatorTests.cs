namespace Modern.CRDT.UnitTests.Services;

using Modern.CRDT.Attributes;
using Modern.CRDT.Models;
using Modern.CRDT.Services;
using Modern.CRDT.Services.Strategies;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Xunit;

public sealed class JsonCrdtApplicatorTests
{
    private sealed record TestModel
    {
        public string? Name { get; init; }

        [CrdtCounter]
        public int Likes { get; init; }
        
        [CrdtArrayLcsStrategy]
        public List<string>? Tags { get; init; }
    }

    private readonly IJsonCrdtApplicator applicator;

    public JsonCrdtApplicatorTests()
    {
        var lwwStrategy = new LwwStrategy();
        var counterStrategy = new CounterStrategy();
        var comparerProvider = new JsonNodeComparerProvider(Enumerable.Empty<IJsonNodeComparer>());
        var arrayStrategy = new ArrayLcsStrategy(comparerProvider);
        var strategies = new ICrdtStrategy[] { lwwStrategy, counterStrategy, arrayStrategy };
        var strategyManager = new CrdtStrategyManager(strategies);
        applicator = new JsonCrdtApplicator(strategyManager);
    }

    [Fact]
    public void ApplyPatch_WithMixedOperations_ShouldUpdatePocoCorrectly_And_UpdateMetadata()
    {
        // Arrange
        var ts1 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var model = new TestModel { Name = "Initial", Likes = 10, Tags = new List<string> { "tech", "crdt" } };
        var metadata = new CrdtMetadata();
        metadata.LwwTimestamps["$.name"] = ts1;
        
        var ts2 = ts1 + 100;
        var ts3 = ts1 + 200;
        var ts4 = ts1 + 300;
        var ts5 = ts1 + 400;

        var patch = new CrdtPatch(new List<CrdtOperation>
        {
            new("$.name", OperationType.Upsert, JsonValue.Create("Updated"), ts2),
            new("$.likes", OperationType.Increment, JsonValue.Create(5), ts3),
            new("$.tags[1]", OperationType.Remove, null, ts4),
            new("$.tags[1]", OperationType.Upsert, JsonValue.Create("new-tag"), ts5)
        });

        // Act
        var result = applicator.ApplyPatch(model, patch, metadata);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Updated");
        result.Likes.ShouldBe(15);
        result.Tags.ShouldBe(new List<string> { "tech", "new-tag" });

        metadata.LwwTimestamps["$.name"].ShouldBe(ts2);
        metadata.SeenOperationIds.ShouldContain(ts3);
        metadata.SeenOperationIds.ShouldContain(ts4);
        metadata.SeenOperationIds.ShouldContain(ts5);
    }
    
    [Fact]
    public void ApplyPatch_WithStaleLwwTimestamp_ShouldNotApplyOperation()
    {
        // Arrange
        var currentTs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var model = new TestModel { Name = "Initial" };
        var metadata = new CrdtMetadata();
        metadata.LwwTimestamps["$.name"] = currentTs;
        
        var staleTimestamp = currentTs - 100;
        var patch = new CrdtPatch(new List<CrdtOperation>
        {
            new("$.name", OperationType.Upsert, JsonValue.Create("Should Not Update"), staleTimestamp)
        });

        // Act
        var result = applicator.ApplyPatch(model, patch, metadata);

        // Assert
        result.Name.ShouldBe("Initial");
        metadata.LwwTimestamps["$.name"].ShouldBe(currentTs);
    }

    [Fact]
    public void ApplyPatch_WithSeenOperationIdForCounter_ShouldNotApplyOperation()
    {
        // Arrange
        var seenTs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var model = new TestModel { Likes = 10 };
        var metadata = new CrdtMetadata();
        metadata.SeenOperationIds.Add(seenTs);

        var patch = new CrdtPatch(new List<CrdtOperation>
        {
            new("$.likes", OperationType.Increment, JsonValue.Create(5), seenTs)
        });
        
        // Act
        var result = applicator.ApplyPatch(model, patch, metadata);

        // Assert
        result.Likes.ShouldBe(10);
        metadata.SeenOperationIds.Count.ShouldBe(1);
    }
    
    [Fact]
    public void ApplyPatch_WithSeenOperationIdForArray_ShouldNotApplyOperation()
    {
        // Arrange
        var seenTs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var model = new TestModel { Tags = new List<string> { "a" } };
        var metadata = new CrdtMetadata();
        metadata.SeenOperationIds.Add(seenTs);

        var patch = new CrdtPatch(new List<CrdtOperation>
        {
            new("$.tags[1]", OperationType.Upsert, JsonValue.Create("b"), seenTs)
        });
        
        // Act
        var result = applicator.ApplyPatch(model, patch, metadata);

        // Assert
        result.Tags.ShouldBe(new List<string> { "a" });
        metadata.SeenOperationIds.Count.ShouldBe(1);
    }
}