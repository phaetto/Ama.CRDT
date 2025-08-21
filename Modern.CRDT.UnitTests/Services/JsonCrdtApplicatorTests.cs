using Modern.CRDT.Models;
using Modern.CRDT.Services;
using Shouldly;
using System.Text.Json.Nodes;

namespace Modern.CRDT.UnitTests.Services;

public sealed class JsonCrdtApplicatorTests
{
    private readonly JsonCrdtApplicator applicator = new();

    [Fact]
    public void ApplyPatch_WithEmptyPatch_ShouldReturnClonedDocument()
    {
        var data = JsonNode.Parse("""{"a":1}""");
        var meta = JsonNode.Parse("""{"a":10}""");
        var doc = new CrdtDocument(data, meta);
        var patch = new CrdtPatch(new List<CrdtOperation>());

        var result = applicator.ApplyPatch(doc, patch);

        result.Data.ShouldNotBeSameAs(doc.Data);
        result.Metadata.ShouldNotBeSameAs(doc.Metadata);
        JsonNode.DeepEquals(result.Data, doc.Data).ShouldBeTrue();
        JsonNode.DeepEquals(result.Metadata, doc.Metadata).ShouldBeTrue();
    }

    [Fact]
    public void ApplyPatch_SimpleUpsert_ShouldAddPropertyToDataAndMeta()
    {
        var doc = new CrdtDocument(JsonNode.Parse("{}"), JsonNode.Parse("{}"));
        var op = new CrdtOperation("$.name", OperationType.Upsert, JsonValue.Create("John"), 100);
        var patch = new CrdtPatch(new[] { op });

        var result = applicator.ApplyPatch(doc, patch);

        result.Data["name"].GetValue<string>().ShouldBe("John");
        result.Metadata["name"].GetValue<long>().ShouldBe(100);
    }

    [Fact]
    public void ApplyPatch_LwwConflict_ShouldKeepNewerValue()
    {
        var doc = new CrdtDocument(
            JsonNode.Parse("""{"name": "Old"}"""),
            JsonNode.Parse("""{"name": 200}""")
        );

        // This operation is older and should be ignored
        var olderOp = new CrdtOperation("$.name", OperationType.Upsert, JsonValue.Create("Older"), 100);
        var patch1 = new CrdtPatch(new[] { olderOp });
        var result1 = applicator.ApplyPatch(doc, patch1);
        result1.Data["name"].GetValue<string>().ShouldBe("Old");
        result1.Metadata["name"].GetValue<long>().ShouldBe(200);

        // This operation is newer and should be applied
        var newerOp = new CrdtOperation("$.name", OperationType.Upsert, JsonValue.Create("Newer"), 300);
        var patch2 = new CrdtPatch(new[] { newerOp });
        var result2 = applicator.ApplyPatch(doc, patch2);
        result2.Data["name"].GetValue<string>().ShouldBe("Newer");
        result2.Metadata["name"].GetValue<long>().ShouldBe(300);
    }
    
    [Fact]
    public void ApplyPatch_NestedPathCreation_ShouldCreateObjects()
    {
        var doc = new CrdtDocument(JsonNode.Parse("{}"), JsonNode.Parse("{}"));
        var op = new CrdtOperation("$.a.b.c", OperationType.Upsert, JsonValue.Create(123), 100);
        var patch = new CrdtPatch(new[] { op });

        var result = applicator.ApplyPatch(doc, patch);

        result.Data["a"]["b"]["c"].GetValue<int>().ShouldBe(123);
        result.Metadata["a"]["b"]["c"].GetValue<long>().ShouldBe(100);
    }
    
    [Fact]
    public void ApplyPatch_Remove_ShouldRemovePropertyFromBoth()
    {
        var doc = new CrdtDocument(
            JsonNode.Parse("""{"name": "John", "age": 30}"""),
            JsonNode.Parse("""{"name": 100, "age": 100}""")
        );
        var op = new CrdtOperation("$.age", OperationType.Remove, null, 150);
        var patch = new CrdtPatch(new[] { op });

        var result = applicator.ApplyPatch(doc, patch);

        result.Data.AsObject().ContainsKey("age").ShouldBeFalse();
        result.Metadata.AsObject().ContainsKey("age").ShouldBeFalse();
        result.Data.AsObject().ContainsKey("name").ShouldBeTrue();
    }
    
    [Fact]
    public void ApplyPatch_RemoveWithOlderTimestamp_ShouldDoNothing()
    {
        var doc = new CrdtDocument(
            JsonNode.Parse("""{"age": 30}"""),
            JsonNode.Parse("""{"age": 200}""")
        );
        var op = new CrdtOperation("$.age", OperationType.Remove, null, 150); // Older timestamp
        var patch = new CrdtPatch(new[] { op });

        var result = applicator.ApplyPatch(doc, patch);

        result.Data["age"].GetValue<int>().ShouldBe(30);
        result.Metadata["age"].GetValue<long>().ShouldBe(200);
    }
    
    [Fact]
    public void ApplyPatch_ArrayAppend_ShouldAddValue()
    {
        var doc = new CrdtDocument(
            JsonNode.Parse("""{"tags": ["a"]}"""),
            JsonNode.Parse("""{"tags": [50]}""")
        );
        var op = new CrdtOperation("$.tags[1]", OperationType.Upsert, JsonValue.Create("b"), 100);
        var patch = new CrdtPatch(new[] { op });

        var result = applicator.ApplyPatch(doc, patch);

        result.Data["tags"].AsArray().Count.ShouldBe(2);
        result.Data["tags"][1].GetValue<string>().ShouldBe("b");
        result.Metadata["tags"][1].GetValue<long>().ShouldBe(100);
    }

    [Fact]
    public void ApplyPatch_ArrayRemove_ShouldRemoveElement()
    {
        var doc = new CrdtDocument(
            JsonNode.Parse("""{"tags": ["a", "b", "c"]}"""),
            JsonNode.Parse("""{"tags": [50, 60, 70]}""")
        );
        var op = new CrdtOperation("$.tags[1]", OperationType.Remove, null, 100);
        var patch = new CrdtPatch(new[] { op });

        var result = applicator.ApplyPatch(doc, patch);

        var dataArr = result.Data["tags"].AsArray();
        dataArr.Count.ShouldBe(2);
        dataArr[0].GetValue<string>().ShouldBe("a");
        dataArr[1].GetValue<string>().ShouldBe("c");

        var metaArr = result.Metadata["tags"].AsArray();
        metaArr.Count.ShouldBe(2);
        metaArr[0].GetValue<long>().ShouldBe(50);
        metaArr[1].GetValue<long>().ShouldBe(70);
    }
    
    [Fact]
    public void ApplyPatch_RootReplacement_ShouldReplaceDocument()
    {
        var doc = new CrdtDocument(
            JsonNode.Parse("""{"a":1}"""),
            JsonNode.Parse("""{"a":10}""")
        );
        var newValue = JsonNode.Parse("""{"b":2}""");
        var op = new CrdtOperation("$", OperationType.Upsert, newValue, 100);
        var patch = new CrdtPatch(new[] { op });

        var result = applicator.ApplyPatch(doc, patch);
        
        JsonNode.DeepEquals(result.Data, newValue).ShouldBeTrue();
        result.Metadata.GetValue<long>().ShouldBe(100);
    }

    [Fact]
    public void ApplyPatch_RootRemoval_ShouldReturnNullDocument()
    {
        var doc = new CrdtDocument(
            JsonNode.Parse("""{"a":1}"""),
            JsonNode.Parse("""{"a":10}""")
        );
        var op = new CrdtOperation("$", OperationType.Remove, null, 100);
        var patch = new CrdtPatch(new[] { op });

        var result = applicator.ApplyPatch(doc, patch);
        
        result.Data.ShouldBeNull();
        result.Metadata.ShouldBeNull();
    }
}