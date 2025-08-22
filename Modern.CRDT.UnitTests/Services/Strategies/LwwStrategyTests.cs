namespace Modern.CRDT.UnitTests.Services.Strategies;

using Modern.CRDT.Models;
using Modern.CRDT.Services.Strategies;
using Shouldly;
using System.Linq;
using System.Text.Json.Nodes;
using Xunit;

public sealed class LwwStrategyTests
{
    private readonly LwwStrategy strategy = new();

    [Fact]
    public void GeneratePatch_WhenModifiedIsNewer_ShouldGenerateUpsert()
    {
        var originalValue = JsonValue.Create(10);
        var modifiedValue = JsonValue.Create(20);
        var originalMeta = JsonValue.Create(100L);
        var modifiedMeta = JsonValue.Create(200L);

        var ops = strategy.GeneratePatch("$.value", originalValue, modifiedValue, originalMeta, modifiedMeta).ToList();

        ops.Count.ShouldBe(1);
        var op = ops[0];
        op.Type.ShouldBe(OperationType.Upsert);
        op.JsonPath.ShouldBe("$.value");
        op.Value!.GetValue<int>().ShouldBe(20);
        op.Timestamp.ShouldBe(200L);
    }
    
    [Fact]
    public void GeneratePatch_WhenModifiedIsOlder_ShouldGenerateNothing()
    {
        var originalValue = JsonValue.Create(10);
        var modifiedValue = JsonValue.Create(20);
        var originalMeta = JsonValue.Create(200L);
        var modifiedMeta = JsonValue.Create(100L);

        var ops = strategy.GeneratePatch("$.value", originalValue, modifiedValue, originalMeta, modifiedMeta).ToList();

        ops.ShouldBeEmpty();
    }
    
    [Fact]
    public void GeneratePatch_WhenModifiedIsSameTimestamp_ShouldGenerateNothing()
    {
        var originalValue = JsonValue.Create(10);
        var modifiedValue = JsonValue.Create(20);
        var originalMeta = JsonValue.Create(200L);
        var modifiedMeta = JsonValue.Create(200L);

        var ops = strategy.GeneratePatch("$.value", originalValue, modifiedValue, originalMeta, modifiedMeta).ToList();

        ops.ShouldBeEmpty();
    }
    
    [Fact]
    public void GeneratePatch_WhenValueIsRemovedAndTimestampIsNewer_ShouldGenerateRemove()
    {
        var originalValue = JsonValue.Create(10);
        JsonNode? modifiedValue = null;
        var originalMeta = JsonValue.Create(100L);
        var modifiedMeta = JsonValue.Create(200L);

        var ops = strategy.GeneratePatch("$.value", originalValue, modifiedValue, originalMeta, modifiedMeta).ToList();
        
        ops.Count.ShouldBe(1);
        var op = ops[0];
        op.Type.ShouldBe(OperationType.Remove);
        op.JsonPath.ShouldBe("$.value");
        op.Value.ShouldBeNull();
        op.Timestamp.ShouldBe(200L);
    }

    [Fact]
    public void GeneratePatch_WhenValueIsAdded_ShouldGenerateUpsert()
    {
        JsonNode? originalValue = null;
        var modifiedValue = JsonValue.Create("new");
        JsonNode? originalMeta = null;
        var modifiedMeta = JsonValue.Create(200L);

        var ops = strategy.GeneratePatch("$.value", originalValue, modifiedValue, originalMeta, modifiedMeta).ToList();

        ops.Count.ShouldBe(1);
        var op = ops[0];
        op.Type.ShouldBe(OperationType.Upsert);
        op.JsonPath.ShouldBe("$.value");
        op.Value!.GetValue<string>().ShouldBe("new");
        op.Timestamp.ShouldBe(200L);
    }
    
    [Fact]
    public void GeneratePatch_WhenValuesAreIdentical_ShouldGenerateNothing()
    {
        var originalValue = JsonValue.Create(10);
        var modifiedValue = JsonValue.Create(10);
        var originalMeta = JsonValue.Create(100L);
        var modifiedMeta = JsonValue.Create(200L);

        var ops = strategy.GeneratePatch("$.value", originalValue, modifiedValue, originalMeta, modifiedMeta).ToList();

        ops.ShouldBeEmpty();
    }
}