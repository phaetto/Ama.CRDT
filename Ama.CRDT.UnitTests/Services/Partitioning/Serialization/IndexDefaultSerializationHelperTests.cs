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
        var originalNode = new BPlusTreeNode
        {
            IsLeaf = true,
        };
        originalNode.Keys.Add(10L);
        originalNode.Keys.Add(20L);
        originalNode.Partitions.Add(new Partition(10, 20, 1L, 10, 10L, 10));
        originalNode.Partitions.Add(new Partition(20, null, 2L, 20, 20L, 20));
        
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

        readNode.Keys[0].ShouldBeOfType<long>().ShouldBe(10L);
        readNode.Partitions[0].StartKey.ShouldBe(10);
    }

    [Fact]
    public async Task WriteNodeAndReadNode_InternalNode_ShouldSucceed()
    {
        // Arrange
        var originalNode = new BPlusTreeNode
        {
            IsLeaf = false,
        };
        originalNode.Keys.Add(100L);
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
        var originalNode = new BPlusTreeNode { IsLeaf = true };
        originalNode.Keys.Add(1L);

        await using var stream = new MemoryStream();
        stream.SetLength(offset); // Pre-allocate space or simulate existing data

        // Act
        var bytesWritten = await helper.WriteNodeAsync(stream, originalNode, offset);
        var readNode = await helper.ReadNodeAsync(stream, offset);

        // Assert
        stream.Length.ShouldBe(offset + bytesWritten);
        readNode.ShouldNotBeNull();
        readNode.Keys[0].ShouldBeOfType<long>().ShouldBe(1L);
    }
    
    [Fact]
    public async Task ReadNode_PolymorphicKeys_ShouldPreserveType()
    {
        // Arrange
        var originalNode = new BPlusTreeNode { IsLeaf = true };
        originalNode.Keys.Add(123L);
        originalNode.Keys.Add("apple");
        originalNode.Keys.Add(Guid.Empty);
        
        originalNode.Partitions.Add(new Partition(123L, null, 1L, 1, 1L, 1));
        originalNode.Partitions.Add(new Partition("apple", null, 2L, 2, 2L, 2));
        originalNode.Partitions.Add(new Partition(Guid.Empty, null, 3L, 3, 3L, 3));

        await using var stream = new MemoryStream();

        // Act
        await helper.WriteNodeAsync(stream, originalNode, 0);
        var readNode = await helper.ReadNodeAsync(stream, 0);

        // Assert
        readNode.ShouldNotBeNull();
        readNode.Keys.Count.ShouldBe(3);
        
        readNode.Keys[0].ShouldBeOfType<long>().ShouldBe(123L);
        readNode.Keys[1].ShouldBeOfType<string>().ShouldBe("apple");
        readNode.Keys[2].ShouldBeOfType<Guid>().ShouldBe(Guid.Empty);
    }
}