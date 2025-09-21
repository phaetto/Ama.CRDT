namespace Ama.CRDT.UnitTests.Services.Partitioning.Serialization;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Services.Partitioning.Serialization;
using Shouldly;
using Xunit;

public sealed class IndexDefaultSerializationHelperTests
{
    private readonly IndexDefaultSerializationHelper helper = new();

    [Fact]
    public async Task WriteHeaderAndReadHeader_ShouldSucceed()
    {
        // Arrange
        const int headerSize = 1024;
        var originalHeader = new BTreeHeader(12345L, 54321L, 16);
        await using var stream = new MemoryStream();

        // Act
        await helper.WriteHeaderAsync(stream, originalHeader, headerSize);
        var readHeader = await helper.ReadHeaderAsync(stream, headerSize);

        // Assert
        readHeader.ShouldBe(originalHeader);
        stream.Length.ShouldBe(headerSize);
    }

    [Fact]
    public async Task WriteHeader_WhenHeaderTooLarge_ShouldThrow()
    {
        // Arrange
        const int headerSize = 10; // Intentionally too small
        var header = new BTreeHeader(long.MaxValue, long.MaxValue, int.MaxValue);
        await using var stream = new MemoryStream();

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(() => helper.WriteHeaderAsync(stream, header, headerSize));
    }

    [Fact]
    public async Task WriteNodeAndReadNode_LeafNode_ShouldSucceed()
    {
        // Arrange
        const string logicalKey = "test";
        var originalNode = new BPlusTreeNode
        {
            IsLeaf = true,
        };
        var startKey1 = new CompositePartitionKey(logicalKey, "10");
        var startKey2 = new CompositePartitionKey(logicalKey, "20");

        originalNode.Keys.Add(startKey1);
        originalNode.Keys.Add(startKey2);
        originalNode.Partitions.Add(new DataPartition(startKey1, startKey2, 1L, 10, 10L, 10));
        originalNode.Partitions.Add(new DataPartition(startKey2, null, 2L, 20, 20L, 20));
        
        await using var stream = new MemoryStream();

        // Act
        await helper.WriteNodeAsync(stream, originalNode, 0);
        var readNode = await helper.ReadNodeAsync(stream, 0);

        // Assert
        readNode.ShouldNotBeNull();
        readNode.IsLeaf.ShouldBeTrue();
        readNode.Keys.Count.ShouldBe(2);
        readNode.Partitions.Count.ShouldBe(2);
        readNode.ChildrenOffsets.ShouldBeEmpty();

        readNode.Keys[0].ShouldBeOfType<CompositePartitionKey>().ShouldBe(startKey1);
        readNode.Partitions[0].ShouldBeOfType<DataPartition>().StartKey.ShouldBe(startKey1);
    }

    [Fact]
    public async Task WriteNodeAndReadNode_WithHeaderAndDataPartitions_ShouldSucceed()
    {
        // Arrange
        const string logicalKey = "test";
        var originalNode = new BPlusTreeNode
        {
            IsLeaf = true,
        };
        var headerKey = new CompositePartitionKey(logicalKey, null);
        var dataKey = new CompositePartitionKey(logicalKey, "data");

        var headerPartition = new HeaderPartition(headerKey, 1L, 10, 10L, 10);
        var dataPartition = new DataPartition(dataKey, null, 2L, 20, 20L, 20);

        originalNode.Keys.Add(headerKey);
        originalNode.Keys.Add(dataKey);
        originalNode.Partitions.Add(headerPartition);
        originalNode.Partitions.Add(dataPartition);
        
        await using var stream = new MemoryStream();

        // Act
        await helper.WriteNodeAsync(stream, originalNode, 0);
        var readNode = await helper.ReadNodeAsync(stream, 0);

        // Assert
        readNode.ShouldNotBeNull();
        readNode.Partitions.Count.ShouldBe(2);
        readNode.Partitions[0].ShouldBeOfType<HeaderPartition>().ShouldBe(headerPartition);
        readNode.Partitions[1].ShouldBeOfType<DataPartition>().ShouldBe(dataPartition);
    }

    [Fact]
    public async Task WriteNodeAndReadNode_InternalNode_ShouldSucceed()
    {
        // Arrange
        const string logicalKey = "test";
        var originalNode = new BPlusTreeNode
        {
            IsLeaf = false,
        };
        originalNode.Keys.Add(new CompositePartitionKey(logicalKey, 100));
        originalNode.ChildrenOffsets.Add(1234L);
        originalNode.ChildrenOffsets.Add(5678L);

        await using var stream = new MemoryStream();

        // Act
        await helper.WriteNodeAsync(stream, originalNode, 0);
        var readNode = await helper.ReadNodeAsync(stream, 0);

        // Assert
        readNode.ShouldNotBeNull();
        readNode.IsLeaf.ShouldBeFalse();
        readNode.Keys.Count.ShouldBe(1);
        readNode.ChildrenOffsets.Count.ShouldBe(2);
        readNode.Partitions.ShouldBeEmpty();
        readNode.ChildrenOffsets[0].ShouldBe(1234L);
        readNode.ChildrenOffsets[1].ShouldBe(5678L);
    }

    [Fact]
    public async Task WriteNodeAndReadNode_AtOffset_ShouldSucceed()
    {
        // Arrange
        const long offset = 50;
        const string logicalKey = "test";
        var startKey = new CompositePartitionKey(logicalKey, 1);
        var originalNode = new BPlusTreeNode { IsLeaf = true };
        originalNode.Keys.Add(startKey);
        originalNode.Partitions.Add(new DataPartition(startKey, null, 1, 1, 1, 1));

        await using var stream = new MemoryStream();
        stream.SetLength(offset); // Pre-allocate space or simulate existing data

        // Act
        var bytesWritten = await helper.WriteNodeAsync(stream, originalNode, offset);
        var readNode = await helper.ReadNodeAsync(stream, offset);

        // Assert
        stream.Length.ShouldBe(offset + bytesWritten);
        readNode.ShouldNotBeNull();
        readNode.Keys[0].ShouldBeOfType<CompositePartitionKey>().ShouldBe(startKey);
    }
    
    [Fact]
    public async Task ReadNode_PolymorphicRangeKeys_ShouldPreserveType()
    {
        // Arrange
        const string logicalKey = "test";
        var originalNode = new BPlusTreeNode { IsLeaf = true };
        
        var keyString = new CompositePartitionKey(logicalKey, "apple");
        var keyInt = new CompositePartitionKey(logicalKey, 123);
        var keyPosId = new CompositePartitionKey(logicalKey, new PositionalIdentifier("1.5", Guid.NewGuid()));
        
        originalNode.Keys.Add(keyString);
        originalNode.Keys.Add(keyInt);
        originalNode.Keys.Add(keyPosId);
        
        originalNode.Partitions.Add(new DataPartition(keyString, null, 1L, 1, 1L, 1));
        originalNode.Partitions.Add(new DataPartition(keyInt, null, 2L, 2, 2L, 2));
        originalNode.Partitions.Add(new DataPartition(keyPosId, null, 3L, 3, 3L, 3));

        await using var stream = new MemoryStream();

        // Act
        await helper.WriteNodeAsync(stream, originalNode, 0);
        var readNode = await helper.ReadNodeAsync(stream, 0);

        // Assert
        readNode.ShouldNotBeNull();
        readNode.Keys.Count.ShouldBe(3);
        
        var readKeys = readNode.Keys.Cast<CompositePartitionKey>().ToList();
        
        readKeys[0].LogicalKey.ShouldBe(logicalKey);
        readKeys[0].RangeKey.ShouldBeOfType<string>().ShouldBe("apple");

        readKeys[1].LogicalKey.ShouldBe(logicalKey);
        readKeys[1].RangeKey.ShouldBeOfType<int>().ShouldBe(123);
        
        readKeys[2].LogicalKey.ShouldBe(logicalKey);
        readKeys[2].RangeKey.ShouldBeOfType<PositionalIdentifier>().ShouldBe((PositionalIdentifier)keyPosId.RangeKey!);
    }
}