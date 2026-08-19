namespace Ama.CRDT.UnitTests.Models.Serialization;

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Ama.CRDT.Models;
using Ama.CRDT.Models.LargerThanMemory;
using Shouldly;
using Xunit;

public sealed class LargerThanMemoryModelSerializationTests
{
    [Fact]
    public void CompositePartitionKey_ShouldSerializeAndDeserialize()
    {
        var key = new CompositeChunkKey("tenant-1", 42);

        var options = TestOptionsHelper.GetDefaultOptions();
        var typeInfo = (JsonTypeInfo<CompositeChunkKey>)options.GetTypeInfo(typeof(CompositeChunkKey));
        
        var json = JsonSerializer.Serialize(key, typeInfo);
        var deserialized = JsonSerializer.Deserialize(json, typeInfo);

        deserialized.LogicalKey.ShouldBe("tenant-1");
        deserialized.RangeKey.ShouldBe(42);
        deserialized.ShouldBe(key);
    }

    [Fact]
    public void DataPartition_ShouldSerializeAndDeserialize()
    {
        var startKey = new CompositeChunkKey("doc1", "a");
        var endKey = new CompositeChunkKey("doc1", "z");
        var partition = new CollectionChunk(startKey, endKey, 100, 50, 200, 25);

        var options = TestOptionsHelper.GetDefaultOptions();
        var typeInfo = (JsonTypeInfo<CollectionChunk>)options.GetTypeInfo(typeof(CollectionChunk));
        
        var json = JsonSerializer.Serialize(partition, typeInfo);
        var deserialized = JsonSerializer.Deserialize(json, typeInfo);

        deserialized.ShouldBe(partition);
    }

    [Fact]
    public void HeaderPartition_ShouldSerializeAndDeserialize()
    {
        var key = new CompositeChunkKey("doc1", null);
        var partition = new HeaderChunk(key, 0, 100, 100, 50);

        var options = TestOptionsHelper.GetDefaultOptions();
        var typeInfo = (JsonTypeInfo<HeaderChunk>)options.GetTypeInfo(typeof(HeaderChunk));
        
        var json = JsonSerializer.Serialize(partition, typeInfo);
        var deserialized = JsonSerializer.Deserialize(json, typeInfo);

        deserialized.ShouldBe(partition);
    }

    [Fact]
    public void PartitionContent_ShouldSerializeAndDeserialize()
    {
        var data = "test-data";
        var metadata = new CrdtMetadata();
        metadata.States["$.prop"] = new CausalTimestamp(new EpochTimestamp(1), "R1", 1);
        
        var content = new ChunkContent(data, metadata);

        var options = TestOptionsHelper.GetDefaultOptions();
        var typeInfo = (JsonTypeInfo<ChunkContent>)options.GetTypeInfo(typeof(ChunkContent));
        
        var json = JsonSerializer.Serialize(content, typeInfo);
        var deserialized = JsonSerializer.Deserialize(json, typeInfo);

        deserialized.Data.ShouldBe(data);
        deserialized.Metadata.Equals(metadata).ShouldBeTrue();
    }

    [Fact]
    public void SplitResult_ShouldSerializeAndDeserialize()
    {
        var content1 = new ChunkContent("data1", new CrdtMetadata());
        var content2 = new ChunkContent("data2", new CrdtMetadata());
        var splitKey = "split-key";

        var result = new ChunkSplitResult(content1, content2, splitKey);

        var options = TestOptionsHelper.GetDefaultOptions();
        var typeInfo = (JsonTypeInfo<ChunkSplitResult>)options.GetTypeInfo(typeof(ChunkSplitResult));
        
        var json = JsonSerializer.Serialize(result, typeInfo);
        var deserialized = JsonSerializer.Deserialize(json, typeInfo);

        deserialized.Partition1.Data.ShouldBe(content1.Data);
        deserialized.Partition2.Data.ShouldBe(content2.Data);
        deserialized.SplitKey.ShouldBe(splitKey);
    }
}