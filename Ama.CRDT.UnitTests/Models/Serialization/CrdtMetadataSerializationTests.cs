namespace Ama.CRDT.UnitTests.Models.Serialization;

using Ama.CRDT.Models;
using Ama.CRDT.Models.Serialization;
using Shouldly;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;
using static Ama.CRDT.Models.CrdtGraph;

public sealed class CrdtMetadataSerializationTests
{
    [Fact]
    public void ShouldSerializeAndDeserialize_WithAllPropertiesPopulated_UsingDefaultOptions()
    {
        // Arrange
        var originalMetadata = CreatePopulatedMetadata();

        // Act
        var json = JsonSerializer.Serialize(originalMetadata, CrdtJsonContext.DefaultOptions);
        var deserializedMetadata = JsonSerializer.Deserialize<CrdtMetadata>(json, CrdtJsonContext.DefaultOptions);

        // Assert
        deserializedMetadata.ShouldNotBeNull();
        AssertMetadataEquality(originalMetadata, deserializedMetadata);
    }

    [Fact]
    public void ShouldSerializeAndDeserialize_WithAllPropertiesPopulated_UsingCompactOptions()
    {
        // Arrange
        var originalMetadata = CreatePopulatedMetadata();

        // Act
        var json = JsonSerializer.Serialize(originalMetadata, CrdtJsonContext.MetadataCompactOptions);
        var deserializedMetadata = JsonSerializer.Deserialize<CrdtMetadata>(json, CrdtJsonContext.MetadataCompactOptions);

        // Assert
        deserializedMetadata.ShouldNotBeNull();
        AssertMetadataEquality(originalMetadata, deserializedMetadata);
    }


    [Fact]
    public void ShouldOmitEmptyCollections_WhenUsingCompactOptions()
    {
        // Arrange
        var metadata = new CrdtMetadata();
        metadata.Lww.Add("$.prop1", new EpochTimestamp(12345));
        metadata.VersionVector.Add("replica1", new SequentialTimestamp(100));

        // Act
        var json = JsonSerializer.Serialize(metadata, CrdtJsonContext.MetadataCompactOptions);
        var jsonNode = JsonNode.Parse(json)!.AsObject();

        // Assert
        jsonNode.ShouldNotBeNull();
        jsonNode.ContainsKey("Lww").ShouldBeTrue();
        jsonNode.ContainsKey("VersionVector").ShouldBeTrue();
        jsonNode.ContainsKey("SeenExceptions").ShouldBeFalse();
        jsonNode.ContainsKey("PositionalTrackers").ShouldBeFalse();
        jsonNode.ContainsKey("AverageRegisters").ShouldBeFalse();
        jsonNode.ContainsKey("TwoPhaseSets").ShouldBeFalse();
        jsonNode.ContainsKey("LwwSets").ShouldBeFalse();
        jsonNode.ContainsKey("OrSets").ShouldBeFalse();
        jsonNode.ContainsKey("PriorityQueues").ShouldBeFalse();
        jsonNode.ContainsKey("LseqTrackers").ShouldBeFalse();
        jsonNode.ContainsKey("ExclusiveLocks").ShouldBeFalse();
        jsonNode.ContainsKey("LwwMaps").ShouldBeFalse();
        jsonNode.ContainsKey("OrMaps").ShouldBeFalse();
        jsonNode.ContainsKey("CounterMaps").ShouldBeFalse();
        jsonNode.ContainsKey("TwoPhaseGraphs").ShouldBeFalse();
        jsonNode.ContainsKey("ReplicatedTrees").ShouldBeFalse();
    }

    [Fact]
    public void ShouldSerializeAndDeserializeEmptyMetadataCorrectly()
    {
        // Arrange
        var emptyMetadata = new CrdtMetadata();

        // Act & Assert for DefaultOptions
        var defaultJson = JsonSerializer.Serialize(emptyMetadata, CrdtJsonContext.DefaultOptions);
        var deserializedDefault = JsonSerializer.Deserialize<CrdtMetadata>(defaultJson, CrdtJsonContext.DefaultOptions);
        deserializedDefault.ShouldNotBeNull();
        AssertAllCollectionsAreEmpty(deserializedDefault);

        // Act & Assert for MetadataCompactOptions
        var compactJson = JsonSerializer.Serialize(emptyMetadata, CrdtJsonContext.MetadataCompactOptions);
        compactJson.ShouldBe("{}");
        var deserializedCompact = JsonSerializer.Deserialize<CrdtMetadata>(compactJson, CrdtJsonContext.MetadataCompactOptions);
        deserializedCompact.ShouldNotBeNull();
        AssertAllCollectionsAreEmpty(deserializedCompact);
    }

    private static void AssertAllCollectionsAreEmpty(CrdtMetadata metadata)
    {
        metadata.Lww.ShouldBeEmpty();
        metadata.VersionVector.ShouldBeEmpty();
        metadata.SeenExceptions.ShouldBeEmpty();
        metadata.PositionalTrackers.ShouldBeEmpty();
        metadata.AverageRegisters.ShouldBeEmpty();
        metadata.TwoPhaseSets.ShouldBeEmpty();
        metadata.LwwSets.ShouldBeEmpty();
        metadata.OrSets.ShouldBeEmpty();
        metadata.PriorityQueues.ShouldBeEmpty();
        metadata.LseqTrackers.ShouldBeEmpty();
        metadata.ExclusiveLocks.ShouldBeEmpty();
        metadata.LwwMaps.ShouldBeEmpty();
        metadata.OrMaps.ShouldBeEmpty();
        metadata.CounterMaps.ShouldBeEmpty();
        metadata.TwoPhaseGraphs.ShouldBeEmpty();
        metadata.ReplicatedTrees.ShouldBeEmpty();
    }

    private static CrdtMetadata CreatePopulatedMetadata()
    {
        var guid = Guid.NewGuid();
        var timestamp = new EpochTimestamp(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        var metadata = new CrdtMetadata();

        metadata.Lww.Add("$.prop1", new EpochTimestamp(12345));
        metadata.VersionVector.Add("replica1", new SequentialTimestamp(100));
        metadata.SeenExceptions.Add(new CrdtOperation(guid, "replica2", "$.prop2", OperationType.Upsert, "value", timestamp));
        metadata.PositionalTrackers.Add("$.array", [new PositionalIdentifier("0.5", guid)]);
        metadata.AverageRegisters.Add("$.avg", new Dictionary<string, AverageRegisterValue>
        {
            ["replica1"] = new(10m, timestamp)
        });
        metadata.TwoPhaseSets.Add("$.set1", new TwoPhaseSetState(new HashSet<object> { "item1" }, new HashSet<object> { "item2" }));

        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();
        metadata.LwwSets.Add("$.lwwset1", new LwwSetState(
            new Dictionary<object, ICrdtTimestamp> { ["item1"] = new EpochTimestamp(1) },
            new Dictionary<object, ICrdtTimestamp> { ["item2"] = new EpochTimestamp(2) }
        ));
        metadata.OrSets.Add("$.orset1", new OrSetState(
            new Dictionary<object, ISet<Guid>> { ["item1"] = new HashSet<Guid> { guid1 } },
            new Dictionary<object, ISet<Guid>> { ["item2"] = new HashSet<Guid> { guid2 } }
        ));
        metadata.PriorityQueues.Add("$.pq1", new LwwSetState(
            new Dictionary<object, ICrdtTimestamp> { ["task1"] = new EpochTimestamp(1) },
            new Dictionary<object, ICrdtTimestamp>()
        ));
        metadata.LseqTrackers.Add("$.lseq1", [new LseqItem(new LseqIdentifier(ImmutableList.Create(new LseqPathSegment(1, "r1"))), "A")]);
        metadata.ExclusiveLocks.Add("$.lock1", new LockInfo("replica1", timestamp));
        metadata.LwwMaps.Add("$.lwwmap1", new Dictionary<object, ICrdtTimestamp> { ["key1"] = new EpochTimestamp(1), [2] = new EpochTimestamp(2) });
        metadata.OrMaps.Add("$.ormap1", new OrSetState(
             new Dictionary<object, ISet<Guid>> { ["key1"] = new HashSet<Guid> { guid1 } },
             new Dictionary<object, ISet<Guid>>()
        ));
        metadata.CounterMaps.Add("$.countermap1", new Dictionary<object, PnCounterState>
        {
            ["key1"] = new PnCounterState(10m, 5m)
        });

        var edge = new Edge("v1", "v2", null);
        metadata.TwoPhaseGraphs.Add("$.graph1", new TwoPhaseGraphState(
            new HashSet<object> { "v1", "v2" },
            new HashSet<object> { "v3" },
            new HashSet<object> { edge },
            new HashSet<object> { new Edge("v4", "v5", null) }
        ));

        metadata.ReplicatedTrees.Add("$.tree1", new OrSetState(
            new Dictionary<object, ISet<Guid>> { [1] = new HashSet<Guid> { guid1 } },
            new Dictionary<object, ISet<Guid>>()
        ));

        return metadata;
    }

    private static void AssertMetadataEquality(CrdtMetadata original, CrdtMetadata deserialized)
    {
        original.Equals(deserialized).ShouldBeTrue();
    }
}