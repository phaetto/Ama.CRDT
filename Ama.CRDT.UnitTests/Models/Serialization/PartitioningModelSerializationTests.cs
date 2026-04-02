namespace Ama.CRDT.UnitTests.Models.Serialization;

using System.Text.Json;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Partitioning;
using Shouldly;
using Xunit;

public sealed class PartitioningModelSerializationTests
{
    [Fact]
    public void CompositePartitionKey_ShouldSerializeAndDeserialize()
    {
        var key = new CompositePartitionKey("tenant-1", 42);

        var options = TestOptionsHelper.GetDefaultOptions();
        var json = JsonSerializer.Serialize(key, options);
        var deserialized = JsonSerializer.Deserialize<CompositePartitionKey>(json, options);

        deserialized.LogicalKey.ShouldBe("tenant-1");
        deserialized.RangeKey.ShouldBe(42);
        deserialized.ShouldBe(key);
    }

    [Fact]
    public void DataPartition_ShouldSerializeAndDeserialize()
    {
        var startKey = new CompositePartitionKey("doc1", "a");
        var endKey = new CompositePartitionKey("doc1", "z");
        var partition = new DataPartition(startKey, endKey, 100, 50, 200, 25);

        var options = TestOptionsHelper.GetDefaultOptions();
        var json = JsonSerializer.Serialize(partition, options);
        var deserialized = JsonSerializer.Deserialize<DataPartition>(json, options);

        deserialized.ShouldBe(partition);
    }

    [Fact]
    public void HeaderPartition_ShouldSerializeAndDeserialize()
    {
        var key = new CompositePartitionKey("doc1", null);
        var partition = new HeaderPartition(key, 0, 100, 100, 50);

        var options = TestOptionsHelper.GetDefaultOptions();
        var json = JsonSerializer.Serialize(partition, options);
        var deserialized = JsonSerializer.Deserialize<HeaderPartition>(json, options);

        deserialized.ShouldBe(partition);
    }

    [Fact]
    public void PartitionContent_ShouldSerializeAndDeserialize()
    {
        var data = "test-data";
        var metadata = new CrdtMetadata();
        metadata.Lww["$.prop"] = new CausalTimestamp(new EpochTimestamp(1), "R1", 1);
        
        var content = new PartitionContent(data, metadata);

        var options = TestOptionsHelper.GetDefaultOptions();
        var json = JsonSerializer.Serialize(content, options);
        var deserialized = JsonSerializer.Deserialize<PartitionContent>(json, options);

        deserialized.Data.ShouldBe(data);
        deserialized.Metadata.Equals(metadata).ShouldBeTrue();
    }

    [Fact]
    public void SplitResult_ShouldSerializeAndDeserialize()
    {
        var content1 = new PartitionContent("data1", new CrdtMetadata());
        var content2 = new PartitionContent("data2", new CrdtMetadata());
        var splitKey = "split-key";

        var result = new SplitResult(content1, content2, splitKey);

        var options = TestOptionsHelper.GetDefaultOptions();
        var json = JsonSerializer.Serialize(result, options);
        var deserialized = JsonSerializer.Deserialize<SplitResult>(json, options);

        deserialized.Partition1.Data.ShouldBe(content1.Data);
        deserialized.Partition2.Data.ShouldBe(content2.Data);
        deserialized.SplitKey.ShouldBe(splitKey);
    }
}