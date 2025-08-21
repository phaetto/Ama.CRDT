using Modern.CRDT.Models;
using Modern.CRDT.Services;
using Shouldly;
using System.Text.Json.Nodes;

namespace Modern.CRDT.UnitTests.Services;

public sealed class JsonCrdtPatcherTests
{
    private readonly JsonCrdtPatcher patcher = new();

    [Fact]
    public void GeneratePatch_WithIdenticalDocuments_ShouldReturnEmptyPatch()
    {
        var data = JsonNode.Parse("""{"name":"John","age":30}""");
        var meta = JsonNode.Parse("""{"name":100,"age":100}""");
        var from = new CrdtDocument(data, meta);
        var to = new CrdtDocument(data.DeepClone(), meta.DeepClone());

        var patch = patcher.GeneratePatch(from, to);

        patch.Operations.ShouldBeEmpty();
    }

    [Fact]
    public void GeneratePatch_ValueChange_WithNewerTimestamp_ShouldGenerateUpsert()
    {
        var fromData = JsonNode.Parse("""{"age":30}""");
        var fromMeta = JsonNode.Parse("""{"age":100}""");
        var toData = JsonNode.Parse("""{"age":31}""");
        var toMeta = JsonNode.Parse("""{"age":200}""");
        var from = new CrdtDocument(fromData, fromMeta);
        var to = new CrdtDocument(toData, toMeta);

        var patch = patcher.GeneratePatch(from, to);

        patch.Operations.Count.ShouldBe(1);
        var op = patch.Operations[0];
        op.Type.ShouldBe(OperationType.Upsert);
        op.JsonPath.ShouldBe("$.age");
        op.Value!.GetValue<int>().ShouldBe(31);
        op.Timestamp.ShouldBe(200);
    }

    [Fact]
    public void GeneratePatch_ValueChange_WithOlderTimestamp_ShouldReturnEmptyPatch()
    {
        var fromData = JsonNode.Parse("""{"age":30}""");
        var fromMeta = JsonNode.Parse("""{"age":200}""");
        var toData = JsonNode.Parse("""{"age":31}""");
        var toMeta = JsonNode.Parse("""{"age":100}""");
        var from = new CrdtDocument(fromData, fromMeta);
        var to = new CrdtDocument(toData, toMeta);

        var patch = patcher.GeneratePatch(from, to);

        patch.Operations.ShouldBeEmpty();
    }
    
    [Fact]
    public void GeneratePatch_PropertyAdded_ShouldGenerateUpsert()
    {
        var fromData = JsonNode.Parse("""{"name":"John"}""");
        var fromMeta = JsonNode.Parse("""{"name":100}""");
        var toData = JsonNode.Parse("""{"name":"John","city":"New York"}""");
        var toMeta = JsonNode.Parse("""{"name":100,"city":200}""");
        var from = new CrdtDocument(fromData, fromMeta);
        var to = new CrdtDocument(toData, toMeta);

        var patch = patcher.GeneratePatch(from, to);

        patch.Operations.Count.ShouldBe(1);
        var op = patch.Operations[0];
        op.Type.ShouldBe(OperationType.Upsert);
        op.JsonPath.ShouldBe("$.city");
        op.Value!.GetValue<string>().ShouldBe("New York");
        op.Timestamp.ShouldBe(200);
    }

    [Fact]
    public void GeneratePatch_PropertyRemoved_ShouldGenerateRemove()
    {
        var fromData = JsonNode.Parse("""{"name":"John","age":30}""");
        var fromMeta = JsonNode.Parse("""{"name":100,"age":100}""");
        var toData = JsonNode.Parse("""{"name":"John"}""");
        var toMetaWithTombstone = JsonNode.Parse("""{"name":100,"age":200}""");
        var from = new CrdtDocument(fromData, fromMeta);
        var to = new CrdtDocument(toData, toMetaWithTombstone);
        
        var patch = patcher.GeneratePatch(from, to);
        
        patch.Operations.Count.ShouldBe(1);
        var op = patch.Operations[0];
        op.Type.ShouldBe(OperationType.Remove);
        op.JsonPath.ShouldBe("$.age");
        op.Timestamp.ShouldBe(200);
        op.Value.ShouldBeNull();
    }
    
    [Fact]
    public void GeneratePatch_ComplexScenario_ShouldGenerateCorrectOperations()
    {
        var fromData = JsonNode.Parse("""{"user":"john","profile":{"tags":["a","b"]},"obsolete":true}""");
        var fromMeta = JsonNode.Parse("""{"user":100,"profile":{"tags":[50,60]},"obsolete":110}""");
        var toData = JsonNode.Parse("""{"user":"jane","profile":{"tags":["a","c","d"]},"new":1}""");
        var toMeta = JsonNode.Parse("""{"user":200,"profile":{"tags":[50,300,400]},"obsolete":120,"new":500}""");
        
        var from = new CrdtDocument(fromData, fromMeta);
        var to = new CrdtDocument(toData, toMeta);

        var patch = patcher.GeneratePatch(from, to);
        var ops = patch.Operations.OrderBy(o => o.JsonPath).ToList();

        ops.Count.ShouldBe(5);

        ops[0].JsonPath.ShouldBe("$.new");
        ops[0].Type.ShouldBe(OperationType.Upsert);
        ops[0].Value!.GetValue<int>().ShouldBe(1);
        ops[0].Timestamp.ShouldBe(500);

        ops[1].JsonPath.ShouldBe("$.obsolete");
        ops[1].Type.ShouldBe(OperationType.Remove);
        ops[1].Timestamp.ShouldBe(120);

        ops[2].JsonPath.ShouldBe("$.profile.tags[1]");
        ops[2].Type.ShouldBe(OperationType.Upsert);
        ops[2].Value!.GetValue<string>().ShouldBe("c");
        ops[2].Timestamp.ShouldBe(300);
        
        ops[3].JsonPath.ShouldBe("$.profile.tags[2]");
        ops[3].Type.ShouldBe(OperationType.Upsert);
        ops[3].Value!.GetValue<string>().ShouldBe("d");
        ops[3].Timestamp.ShouldBe(400);

        var userOp = ops.Single(o => o.JsonPath == "$.user");
        userOp.Type.ShouldBe(OperationType.Upsert);
        userOp.Value!.GetValue<string>().ShouldBe("jane");
        userOp.Timestamp.ShouldBe(200);
    }
}