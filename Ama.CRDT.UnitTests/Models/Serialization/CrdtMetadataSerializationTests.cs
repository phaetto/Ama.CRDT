namespace Ama.CRDT.UnitTests.Models.Serialization;

using Ama.CRDT.Models;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

public sealed class CrdtMetadataSerializationTests
{
    [Fact]
    public void ShouldSerializeAndDeserialize_WithAllPropertiesPopulated_UsingDefaultOptions()
    {
        // Arrange
        var originalMetadata = CreatePopulatedMetadata();

        // Act
        var options = TestOptionsHelper.GetDefaultOptions();
        var json = JsonSerializer.Serialize(originalMetadata, options);
        var deserializedMetadata = JsonSerializer.Deserialize<CrdtMetadata>(json, options);

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
        var options = TestOptionsHelper.GetCompactOptions();
        var json = JsonSerializer.Serialize(originalMetadata, options);
        var deserializedMetadata = JsonSerializer.Deserialize<CrdtMetadata>(json, options);

        // Assert
        deserializedMetadata.ShouldNotBeNull();
        AssertMetadataEquality(originalMetadata, deserializedMetadata);
    }


    [Fact]
    public void ShouldOmitEmptyCollections_WhenUsingCompactOptions()
    {
        // Arrange
        var metadata = new CrdtMetadata();
        metadata.States.Add("$.prop1", new CausalTimestamp(new EpochTimestamp(12345), "replica1", 1));
        metadata.VersionVector.Add("replica1", 100L);

        // Act
        var options = TestOptionsHelper.GetCompactOptions();
        var json = JsonSerializer.Serialize(metadata, options);
        var jsonNode = JsonNode.Parse(json)!.AsObject();

        // Assert
        jsonNode.ShouldNotBeNull();
        jsonNode.ContainsKey("States").ShouldBeTrue();
        jsonNode.ContainsKey("VersionVector").ShouldBeTrue();
        jsonNode.ContainsKey("SeenExceptions").ShouldBeFalse();
    }

    [Fact]
    public void ShouldSerializeAndDeserializeEmptyMetadataCorrectly()
    {
        // Arrange
        var emptyMetadata = new CrdtMetadata();

        // Act & Assert for DefaultOptions
        var defaultOptions = TestOptionsHelper.GetDefaultOptions();
        var defaultJson = JsonSerializer.Serialize(emptyMetadata, defaultOptions);
        var deserializedDefault = JsonSerializer.Deserialize<CrdtMetadata>(defaultJson, defaultOptions);
        deserializedDefault.ShouldNotBeNull();
        AssertAllCollectionsAreEmpty(deserializedDefault);

        // Act & Assert for MetadataCompactOptions
        var compactOptions = TestOptionsHelper.GetCompactOptions();
        var compactJson = JsonSerializer.Serialize(emptyMetadata, compactOptions);
        compactJson.ShouldBe("{}");
        var deserializedCompact = JsonSerializer.Deserialize<CrdtMetadata>(compactJson, compactOptions);
        deserializedCompact.ShouldNotBeNull();
        AssertAllCollectionsAreEmpty(deserializedCompact);
    }

    private static void AssertAllCollectionsAreEmpty(CrdtMetadata metadata)
    {
        metadata.States.ShouldBeEmpty();
        metadata.VersionVector.ShouldBeEmpty();
        metadata.SeenExceptions.ShouldBeEmpty();
    }

    private static CrdtMetadata CreatePopulatedMetadata()
    {
        var guid = Guid.NewGuid();
        var timestamp = new EpochTimestamp(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        var metadata = new CrdtMetadata();

        metadata.States.Add("$.prop1", new CausalTimestamp(new EpochTimestamp(12345), "replica1", 1));
        metadata.VersionVector.Add("replica1", 100L);
        metadata.SeenExceptions.Add(new CrdtOperation(guid, "replica2", "$.prop2", OperationType.Upsert, "value", timestamp, 0));
        
        metadata.States.Add("$.array", new PositionalState([new PositionalIdentifier("0.5", guid)]));
        
        metadata.States.Add("$.avg", new AverageRegisterState(new Dictionary<string, AverageRegisterValue>
        {
            ["replica1"] = new(10m, timestamp)
        }));
        
        metadata.States.Add("$.set1", new TwoPhaseSetState(
            new HashSet<object> { "item1" }, 
            new Dictionary<object, CausalTimestamp> { ["item2"] = new CausalTimestamp(timestamp, "replica1", 1) }));

        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();
        
        metadata.States.Add("$.lwwset1", new LwwSetState(
            new Dictionary<object, ICrdtTimestamp> { ["item1"] = new EpochTimestamp(1) },
            new Dictionary<object, CausalTimestamp> { ["item2"] = new CausalTimestamp(new EpochTimestamp(2), "replica1", 1) }
        ));
        
        metadata.States.Add("$.orset1", new OrSetState(
            new Dictionary<object, ISet<Guid>> { ["item1"] = new HashSet<Guid> { guid1 } },
            new Dictionary<object, IDictionary<Guid, CausalTimestamp>> { ["item2"] = new Dictionary<Guid, CausalTimestamp> { [guid2] = new CausalTimestamp(timestamp, "replica1", 1) } }
        ));
        
        metadata.States.Add("$.pq1", new LwwSetState(
            new Dictionary<object, ICrdtTimestamp> { ["task1"] = new EpochTimestamp(1) },
            new Dictionary<object, CausalTimestamp>()
        ));
        
        metadata.States.Add("$.lseq1", new LseqState([new LseqItem(new LseqIdentifier(ImmutableList.Create(new LseqPathSegment(1, "r1"))), "A")]));
        
        metadata.States.Add("$.lwwmap1", new LwwMapState(new Dictionary<object, CausalTimestamp> { ["key1"] = new CausalTimestamp(new EpochTimestamp(1), "replica1", 1), [2] = new CausalTimestamp(new EpochTimestamp(2), "replica1", 2) }));
        
        metadata.States.Add("$.ormap1", new OrSetState(
             new Dictionary<object, ISet<Guid>> { ["key1"] = new HashSet<Guid> { guid1 } },
             new Dictionary<object, IDictionary<Guid, CausalTimestamp>>()
        ));
        
        metadata.States.Add("$.countermap1", new CounterMapState(new Dictionary<object, PnCounterState>
        {
            ["key1"] = new PnCounterState(10m, 5m)
        }));

        var edge = new Edge("v1", "v2", null);
        metadata.States.Add("$.graph1", new TwoPhaseGraphState(
            new HashSet<object> { "v1", "v2" },
            new Dictionary<object, CausalTimestamp> { ["v3"] = new CausalTimestamp(timestamp, "replica1", 1) },
            new HashSet<object> { edge },
            new Dictionary<object, CausalTimestamp> { [new Edge("v4", "v5", null)] = new CausalTimestamp(timestamp, "replica1", 2) }
        ));

        metadata.States.Add("$.tree1", new OrSetState(
            new Dictionary<object, ISet<Guid>> { [1] = new HashSet<Guid> { guid1 } },
            new Dictionary<object, IDictionary<Guid, CausalTimestamp>>()
        ));

        // Missing Models specifically requested in Architecture Convention tests
        metadata.States.Add("$.epoch1", new EpochState(42));

        metadata.States.Add("$.quorum1", new QuorumState(new Dictionary<object, ISet<string>>
        {
            ["payload1"] = new HashSet<string> { "replica1", "replica2" }
        }));

        metadata.States.Add("$.fwwmap1", new FwwMapState(new Dictionary<object, CausalTimestamp>
        {
            ["key1"] = new CausalTimestamp(timestamp, "replica1", 1)
        }));

        metadata.States.Add("$.fwwset1", new FwwSetState(
            new Dictionary<object, ICrdtTimestamp> { ["item1"] = timestamp },
            new Dictionary<object, CausalTimestamp> { ["item2"] = new CausalTimestamp(timestamp, "replica1", 1) }
        ));

        metadata.States.Add("$.fwwts1", new FwwTimestamp(timestamp, "replica1", 1));

        metadata.States.Add("$.rga1", new RgaState([
            new RgaItem(new RgaIdentifier(12345L, "replica1"), null, "val1", false)
        ]));

        return metadata;
    }

    private static void AssertMetadataEquality(CrdtMetadata original, CrdtMetadata deserialized)
    {
        original.Equals(deserialized).ShouldBeTrue();
    }
}