namespace Modern.CRDT.UnitTests.Services;

using Microsoft.Extensions.DependencyInjection;
using Modern.CRDT.Extensions;
using Modern.CRDT.Models;
using Modern.CRDT.Services;
using Shouldly;
using System.Text.Json;
using System.Text.Json.Nodes;

public sealed class JsonCrdtServiceTests
{
    private readonly IJsonCrdtService jsonCrdtService;

    public JsonCrdtServiceTests()
    {
        var serviceProvider = new ServiceCollection()
            .AddJsonCrdt()
            .BuildServiceProvider();

        jsonCrdtService = serviceProvider.GetRequiredService<IJsonCrdtService>()!;
    }

    private record TestDocument(string Id, string Name, int Version, List<string> Tags);

    [Fact]
    public void Merge_Poco_ShouldConverge_WhenDivergentChangesOccur()
    {
        // Arrange: Base state with manually created metadata for predictability
        var baseCrdt = new CrdtDocument<TestDocument>(
            new TestDocument("doc1", "Base Name", 1, ["tag1"]),
            JsonNode.Parse("""{"Id":1,"Name":1,"Version":1,"Tags":[1]}""")
        );

        // Replica A changes the name (timestamp 2)
        var replicaA = new CrdtDocument<TestDocument>(
            baseCrdt.Data with { Name = "Name A" },
            JsonNode.Parse("""{"Id":1,"Name":2,"Version":1,"Tags":[1]}""")
        );

        // Replica B changes the version and adds a tag (timestamp 3)
        var replicaB = new CrdtDocument<TestDocument>(
            baseCrdt.Data with { Version = 2, Tags = ["tag1", "tag2"] },
            JsonNode.Parse("""{"Id":1,"Name":1,"Version":3,"Tags":[1, 3]}""")
        );

        // Act: Cross-merge
        // 1. Get changes from A and apply to B
        var patchFromBaseToA = jsonCrdtService.CreatePatch(baseCrdt, replicaA);
        var bMergedWithA = jsonCrdtService.Merge(replicaB, patchFromBaseToA);

        // 2. Get changes from B and apply to A
        var patchFromBaseToB = jsonCrdtService.CreatePatch(baseCrdt, replicaB);
        var aMergedWithB = jsonCrdtService.Merge(replicaA, patchFromBaseToB);

        // Assert: Both replicas should converge to the same state
        var finalAJson = JsonSerializer.Serialize(aMergedWithB);
        var finalBJson = JsonSerializer.Serialize(bMergedWithA);

        finalAJson.ShouldBe(finalBJson);

        // Assert final state details based on Last-Writer-Wins
        aMergedWithB.Data.ShouldNotBeNull();
        aMergedWithB.Data.Name.ShouldBe("Name A");     // From A (ts 2 > 1)
        aMergedWithB.Data.Version.ShouldBe(2);          // From B (ts 3 > 1)
        aMergedWithB.Data.Tags.ShouldBe(["tag1", "tag2"]); // From B (ts 3 > 1)
    }
    
    [Fact]
    public void Merge_JsonNode_ShouldConverge_WhenDivergentChangesOccur()
    {
        // Arrange: Base state
        var baseCrdt = new CrdtDocument(
            JsonNode.Parse("""{"id":"doc1","name":"Base Name","version":1,"tags":["tag1"]}"""),
            JsonNode.Parse("""{"id":1,"name":1,"version":1,"tags":[1]}""")
        );

        // Replica A changes the name (timestamp 2)
        var replicaA = new CrdtDocument(
            JsonNode.Parse("""{"id":"doc1","name":"Name A","version":1,"tags":["tag1"]}"""),
            JsonNode.Parse("""{"id":1,"name":2,"version":1,"tags":[1]}""")
        );

        // Replica B changes the version and adds a tag (timestamp 3)
        var replicaB = new CrdtDocument(
            JsonNode.Parse("""{"id":"doc1","name":"Base Name","version":2,"tags":["tag1", "tag2"]}"""),
            JsonNode.Parse("""{"id":1,"name":1,"version":3,"tags":[1, 3]}""")
        );

        // Act: Cross-merge
        var patchForB = jsonCrdtService.CreatePatch(baseCrdt, replicaA);
        var finalB = jsonCrdtService.Merge(replicaB, patchForB);

        var patchForA = jsonCrdtService.CreatePatch(baseCrdt, replicaB);
        var finalA = jsonCrdtService.Merge(replicaA, patchForA);

        // Assert: Both replicas should converge to the same state
        var finalAJson = finalA.Data.ToJsonString();
        var finalBJson = finalB.Data.ToJsonString();

        finalAJson.ShouldBe(finalBJson);

        var finalData = JsonObject.Parse(finalAJson)!.AsObject();
        finalData["name"]!.GetValue<string>().ShouldBe("Name A");
        finalData["version"]!.GetValue<int>().ShouldBe(2);
        finalData["tags"]!.AsArray().Count.ShouldBe(2);
        finalData["tags"]![1]!.GetValue<string>().ShouldBe("tag2");
    }

    [Fact]
    public void Merge_ConvenienceOverload_ShouldProduceSameResultAsManualSteps()
    {
        // Arrange
        var original = new CrdtDocument(
            JsonNode.Parse("""{"value":1}"""),
            JsonNode.Parse("""{"value":10}""")
        );

        var modified = new CrdtDocument(
            JsonNode.Parse("""{"value":2}"""),
            JsonNode.Parse("""{"value":20}""")
        );

        // Act
        var patch = jsonCrdtService.CreatePatch(original, modified);
        var resultFromManualSteps = jsonCrdtService.Merge(original, patch);

        var resultFromConvenience = jsonCrdtService.Merge(original, modified);
        
        // Assert
        resultFromConvenience.Data.ToJsonString().ShouldBe(resultFromManualSteps.Data.ToJsonString());
        resultFromConvenience.Metadata.ToJsonString().ShouldBe(resultFromManualSteps.Metadata.ToJsonString());
    }
}