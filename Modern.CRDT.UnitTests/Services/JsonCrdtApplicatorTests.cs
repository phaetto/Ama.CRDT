namespace Modern.CRDT.UnitTests.Services;

using Modern.CRDT.Attributes;
using Modern.CRDT.Models;
using Modern.CRDT.Services;
using Modern.CRDT.Services.Strategies;
using Shouldly;
using System;
using System.Collections.Generic;
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
        var arrayStrategy = new ArrayLcsStrategy();
        var strategies = new ICrdtStrategy[] { lwwStrategy, counterStrategy, arrayStrategy };
        var strategyManager = new CrdtStrategyManager(strategies);
        applicator = new JsonCrdtApplicator(strategyManager);
    }

    [Fact]
    public void ApplyPatch_WithMixedOperations_ShouldUpdatePocoCorrectly()
    {
        // Arrange
        var ts1 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var model = new TestModel { Name = "Initial", Likes = 10, Tags = ["tech", "crdt"] };
        var metadata = JsonNode.Parse($"{{\"name\":{ts1},\"likes\":{ts1},\"tags\":[{ts1},{ts1}]}}");
        var document = new CrdtDocument<TestModel>(model, metadata);
        
        var ts2 = ts1 + 100;
        var ts3 = ts1 + 200;
        var ts4 = ts1 + 300;

        var patch = new CrdtPatch(new List<CrdtOperation>
        {
            new("$.name", OperationType.Upsert, JsonValue.Create("Updated"), ts2),
            new("$.likes", OperationType.Increment, JsonValue.Create(5), ts3),
            new("$.tags[1]", OperationType.Remove, null, ts4),
            new("$.tags[1]", OperationType.Upsert, JsonValue.Create("new-tag"), ts4)
        });

        // Act
        var result = applicator.ApplyPatch(document, patch);

        // Assert
        result.Data.ShouldNotBeNull();
        result.Data.Name.ShouldBe("Updated");
        result.Data.Likes.ShouldBe(15);
        result.Data.Tags.ShouldBe(new List<string> { "tech", "new-tag" });

        var finalMeta = result.Metadata?.AsObject();
        finalMeta.ShouldNotBeNull();
        finalMeta["name"]!.GetValue<long>().ShouldBe(ts2);
        finalMeta["likes"]!.GetValue<long>().ShouldBe(ts3);
        finalMeta["tags"]!.AsArray()[1]!.GetValue<long>().ShouldBe(ts4);
    }
    
    [Fact]
    public void ApplyPatch_WithStaleTimestamp_ShouldNotApplyOperation()
    {
        // Arrange
        var ts1 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var model = new TestModel { Name = "Initial" };
        var metadata = JsonNode.Parse($"{{\"name\":{ts1}}}");
        var document = new CrdtDocument<TestModel>(model, metadata);
        
        var staleTimestamp = ts1 - 100;
        var patch = new CrdtPatch(new List<CrdtOperation>
        {
            new("$.name", OperationType.Upsert, JsonValue.Create("Should Not Update"), staleTimestamp)
        });

        // Act
        var result = applicator.ApplyPatch(document, patch);

        // Assert
        result.Data.ShouldNotBeNull();
        result.Data.Name.ShouldBe("Initial");
        result.Metadata?.AsObject()["name"]!.GetValue<long>().ShouldBe(ts1);
    }
}