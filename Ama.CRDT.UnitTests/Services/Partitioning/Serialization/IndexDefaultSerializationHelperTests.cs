namespace Ama.CRDT.UnitTests.Services.Partitioning.Serialization;

using System;
using System.IO;
using System.Threading.Tasks;
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
        var startKey1 = new CompositePartitionKey(logicalKey, 10L);
        var startKey2 = new CompositePartitionKey(logicalKey, 20L);

        originalNode.Keys.Add(startKey1);
        originalNode.Keys.Add(startKey2);
        originalNode.Partitions.Add(new Partition(startKey1, startKey2, 1L, 10, 10L, 10));
        originalNode.Partitions.Add(new Partition(startKey2, null, 2L, 20, 20L, 20));
        
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
        readNode.Partitions[0].StartKey.ShouldBe(startKey1);
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
        originalNode.Keys.Add(new CompositePartitionKey(logicalKey, 100L));
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
        var startKey = new CompositePartitionKey(logicalKey, 1L);
        var originalNode = new BPlusTreeNode { IsLeaf = true };
        originalNode.Keys.Add(startKey);

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
        var key1 = new CompositePartitionKey(logicalKey, 123L);
        var key2 = new CompositePartitionKey(logicalKey, "apple");
        var key3 = new CompositePartitionKey(logicalKey, Guid.Empty);
        
        originalNode.Keys.Add(key1);
        originalNode.Keys.Add(key2);
        originalNode.Keys.Add(key3);
        
        originalNode.Partitions.Add(new Partition(key1, null, 1L, 1, 1L, 1));
        originalNode.Partitions.Add(new Partition(key2, null, 2L, 2, 2L, 2));
        originalNode.Partitions.Add(new Partition(key3, null, 3L, 3, 3L, 3));

        await using var stream = new MemoryStream();

        // Act
        await helper.WriteNodeAsync(stream, originalNode, 0);
        var readNode = await helper.ReadNodeAsync(stream, 0);

        // Assert
        readNode.ShouldNotBeNull();
        readNode.Keys.Count.ShouldBe(3);
        
        readNode.Keys[0].ShouldBeOfType<CompositePartitionKey>().ShouldBe(key1);
        readNode.Keys[1].ShouldBeOfType<CompositePartitionKey>().ShouldBe(key2);
        readNode.Keys[2].ShouldBeOfType<CompositePartitionKey>().ShouldBe(key3);
        
        ((CompositePartitionKey)readNode.Keys[0]).RangeKey.ShouldBeOfType<long>().ShouldBe(123L);
        ((CompositePartitionKey)readNode.Keys[1]).RangeKey.ShouldBeOfType<string>().ShouldBe("apple");
        ((CompositePartitionKey)readNode.Keys[2]).RangeKey.ShouldBeOfType<Guid>().ShouldBe(Guid.Empty);
    }
}