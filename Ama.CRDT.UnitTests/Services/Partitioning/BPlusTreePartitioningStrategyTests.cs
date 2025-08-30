namespace Ama.CRDT.UnitTests.Services.Partitioning;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Services.Partitioning.Serialization;
using Ama.CRDT.Services.Partitioning.Strategies;
using Shouldly;
using Xunit;

public sealed class BPlusTreePartitioningStrategyTests
{
    private readonly IndexDefaultSerializationHelper serializationHelper = new();

    [Fact]
    public async Task InitializeAsync_ShouldCreateValidHeaderAndRootNode()
    {
        // Arrange
        await using var stream = new MemoryStream();
        var strategy = new BPlusTreePartitioningStrategy(serializationHelper);

        // Act
        await strategy.InitializeAsync(stream);

        // Assert
        stream.Length.ShouldBeGreaterThan(0);
        stream.Position = 0;

        // We cannot directly read the header or node without duplicating serialization logic,
        // but we can check that FindPartitionAsync on the initialized empty tree returns null.
        var result = await strategy.FindPartitionAsync(0);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task InitializeAsync_OnExistingStream_ShouldTruncateAndReinitialize()
    {
        // Arrange
        await using var stream = new MemoryStream();
        await stream.WriteAsync(new byte[] { 1, 2, 3, 4, 5 });
        var originalLength = stream.Length;
        var strategy = new BPlusTreePartitioningStrategy(serializationHelper);

        // Act
        await strategy.InitializeAsync(stream);

        // Assert
        stream.Length.ShouldNotBe(originalLength);
        stream.Length.ShouldBeGreaterThan(0);

        var result = await strategy.FindPartitionAsync(0);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task FindPartitionAsync_OnUninitializedStrategy_ShouldThrow()
    {
        // Arrange
        var strategy = new BPlusTreePartitioningStrategy(serializationHelper);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(async () => await strategy.FindPartitionAsync(0));
    }

    [Fact]
    public async Task FindPartitionAsync_OnEmptyTree_ShouldReturnNull()
    {
        // Arrange
        await using var stream = new MemoryStream();
        var strategy = new BPlusTreePartitioningStrategy(serializationHelper);
        await strategy.InitializeAsync(stream);

        // Act
        var result = await strategy.FindPartitionAsync(123);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task InsertAndFindAsync_SinglePartition_ShouldSucceed()
    {
        // Arrange
        await using var stream = new MemoryStream();
        var strategy = new BPlusTreePartitioningStrategy(serializationHelper);
        await strategy.InitializeAsync(stream);
        var partition = new Partition(10, null, 100L, 200, 300L, 100);

        // Act
        await strategy.InsertPartitionAsync(partition);

        // Assert
        var foundPartition = await strategy.FindPartitionAsync(15);
        foundPartition.ShouldNotBeNull();
        foundPartition.Value.ShouldBe(partition);

        var foundPartitionAtEdge = await strategy.FindPartitionAsync(10);
        foundPartitionAtEdge.ShouldNotBeNull();
        foundPartitionAtEdge.Value.ShouldBe(partition);

        var notFoundPartition = await strategy.FindPartitionAsync(5);
        notFoundPartition.ShouldBeNull();
    }

    [Fact]
    public async Task InsertAndFindAsync_MultiplePartitions_InOrder_ShouldSucceed()
    {
        // Arrange
        await using var stream = new MemoryStream();
        var strategy = new BPlusTreePartitioningStrategy(serializationHelper);
        await strategy.InitializeAsync(stream);
        var partition10 = new Partition(10, 20, 100L, 10, 110L, 5);
        var partition20 = new Partition(20, 30, 200L, 20, 220L, 5);
        var partition30 = new Partition(30, null, 300L, 30, 330L, 5);

        // Act
        await strategy.InsertPartitionAsync(partition10);
        await strategy.InsertPartitionAsync(partition20);
        await strategy.InsertPartitionAsync(partition30);

        // Assert
        (await strategy.FindPartitionAsync(5)).ShouldBeNull();
        (await strategy.FindPartitionAsync(15))!.Value.ShouldBe(partition10);
        (await strategy.FindPartitionAsync(25))!.Value.ShouldBe(partition20);
        (await strategy.FindPartitionAsync(35))!.Value.ShouldBe(partition30);
    }

    [Fact]
    public async Task InsertAndFindAsync_MultiplePartitions_RandomOrder_ShouldSucceed()
    {
        // Arrange
        await using var stream = new MemoryStream();
        var strategy = new BPlusTreePartitioningStrategy(serializationHelper);
        await strategy.InitializeAsync(stream);
        var partition10 = new Partition(10, 20, 100L, 10, 110L, 5);
        var partition20 = new Partition(20, 30, 200L, 20, 220L, 5);
        var partition30 = new Partition(30, null, 300L, 30, 330L, 5);

        // Act
        await strategy.InsertPartitionAsync(partition30);
        await strategy.InsertPartitionAsync(partition10);
        await strategy.InsertPartitionAsync(partition20);

        // Assert
        (await strategy.FindPartitionAsync(5)).ShouldBeNull();
        (await strategy.FindPartitionAsync(10))!.Value.ShouldBe(partition10);
        (await strategy.FindPartitionAsync(19))!.Value.ShouldBe(partition10);
        (await strategy.FindPartitionAsync(20))!.Value.ShouldBe(partition20);
        (await strategy.FindPartitionAsync(29))!.Value.ShouldBe(partition20);
        (await strategy.FindPartitionAsync(30))!.Value.ShouldBe(partition30);
        (await strategy.FindPartitionAsync(100))!.Value.ShouldBe(partition30);
    }

    [Fact]
    public async Task InsertPartitionAsync_ShouldTriggerLeafSplit_AndFindShouldWork()
    {
        // Arrange
        // Degree is 8, so a node splits when it gets its 16th key.
        const int degree = 8;
        const int splitSize = 2 * degree;
        await using var stream = new MemoryStream();
        var strategy = new BPlusTreePartitioningStrategy(serializationHelper);
        await strategy.InitializeAsync(stream);
        var partitions = Enumerable.Range(0, splitSize)
            .Select(i => new Partition(
                i * 10,
                null,
                (long)i * 100,
                10,
                (long)i * 100 + 50,
                10))
            .ToList();

        // Act
        foreach (var p in partitions)
        {
            await strategy.InsertPartitionAsync(p);
        }

        // Assert
        (await strategy.FindPartitionAsync(-5)).ShouldBeNull();
        (await strategy.FindPartitionAsync(5))!.Value.ShouldBe(partitions[0]);
        (await strategy.FindPartitionAsync(15))!.Value.ShouldBe(partitions[1]);
        (await strategy.FindPartitionAsync(10 * (splitSize - 1) + 5))!.Value.ShouldBe(partitions[splitSize - 1]);
        (await strategy.FindPartitionAsync(10 * splitSize + 5))!.Value.ShouldBe(partitions[splitSize - 1]);
    }

    [Fact]
    public async Task InsertPartitionAsync_ShouldHandleManyInsertions_CausingMultipleSplits()
    {
        // Arrange
        // This will cause multiple splits at different levels of the tree.
        const int totalPartitions = 300;
        await using var stream = new MemoryStream();
        var strategy = new BPlusTreePartitioningStrategy(serializationHelper);
        await strategy.InitializeAsync(stream);
        var partitions = Enumerable.Range(0, totalPartitions)
            .Select(i => new Partition(
                i,
                null,
                (long)i * 10,
                5,
                (long)i * 10 + 50,
                5))
            .ToList();

        // Act
        foreach (var p in partitions)
        {
            await strategy.InsertPartitionAsync(p);
        }

        // Assert
        (await strategy.FindPartitionAsync(-1)).ShouldBeNull();

        for (int i = 0; i < totalPartitions; i++)
        {
            var found = await strategy.FindPartitionAsync(i);
            found.ShouldNotBeNull();
            found.Value.ShouldBe(partitions[i]);
        }

        var lastPartition = await strategy.FindPartitionAsync(totalPartitions + 10);
        lastPartition.ShouldNotBeNull();
        lastPartition.Value.ShouldBe(partitions[totalPartitions - 1]);
    }

    [Fact]
    public async Task InsertPartitionAsync_WithStringKeys_ShouldWork()
    {
        // Arrange
        await using var stream = new MemoryStream();
        var strategy = new BPlusTreePartitioningStrategy(serializationHelper);
        await strategy.InitializeAsync(stream);
        var pApple = new Partition("apple", "banana", 1L, 1, 10L, 1);
        var pBanana = new Partition("banana", "cherry", 2L, 2, 20L, 2);
        var pCherry = new Partition("cherry", null, 3L, 3, 30L, 3);

        // Act
        await strategy.InsertPartitionAsync(pCherry);
        await strategy.InsertPartitionAsync(pApple);
        await strategy.InsertPartitionAsync(pBanana);

        // Assert
        (await strategy.FindPartitionAsync("apricot"))!.Value.ShouldBe(pApple);
        (await strategy.FindPartitionAsync("azure"))!.Value.ShouldBe(pApple);
        (await strategy.FindPartitionAsync("blueberry"))!.Value.ShouldBe(pBanana);
        (await strategy.FindPartitionAsync("date"))!.Value.ShouldBe(pCherry);
        (await strategy.FindPartitionAsync("aaa")).ShouldBeNull();
    }

    [Fact]
    public async Task UpdateAndFindAsync_ShouldReflectChanges()
    {
        // Arrange
        await using var stream = new MemoryStream();
        var strategy = new BPlusTreePartitioningStrategy(serializationHelper);
        await strategy.InitializeAsync(stream);
        var originalPartition = new Partition(10, 20, 100L, 10, 110L, 5);
        await strategy.InsertPartitionAsync(originalPartition);

        // Act
        var updatedPartition = originalPartition with { DataLength = 999 };
        await strategy.UpdatePartitionAsync(updatedPartition);

        // Assert
        var foundPartition = await strategy.FindPartitionAsync(15);
        foundPartition.ShouldNotBeNull();
        foundPartition.Value.DataLength.ShouldBe(999);
        foundPartition.Value.ShouldBe(updatedPartition);
    }

    [Fact]
    public async Task UpdateAndFindAsync_InMultiLevelTree_ShouldReflectChanges()
    {
        // Arrange
        const int totalPartitions = 50;
        await using var stream = new MemoryStream();
        var strategy = new BPlusTreePartitioningStrategy(serializationHelper);
        await strategy.InitializeAsync(stream);
        var partitions = Enumerable.Range(0, totalPartitions)
            .Select(i => new Partition(i * 10, null, (long)i * 100, 10, (long)i * 100 + 50, 10))
            .ToList();
        
        foreach (var p in partitions)
        {
            await strategy.InsertPartitionAsync(p);
        }

        var partitionToUpdate = partitions[25];
        var updatedPartition = partitionToUpdate with { DataOffset = 9999L };
        
        // Act
        await strategy.UpdatePartitionAsync(updatedPartition);

        // Assert
        var foundUpdated = await strategy.FindPartitionAsync(partitionToUpdate.StartKey);
        foundUpdated.ShouldNotBeNull();
        foundUpdated.Value.ShouldBe(updatedPartition);
        foundUpdated.Value.DataOffset.ShouldBe(9999L);

        // Verify other partitions are not affected
        var foundUntouched = await strategy.FindPartitionAsync(partitions[10].StartKey);
        foundUntouched.ShouldNotBeNull();
        foundUntouched.Value.ShouldBe(partitions[10]);
        
        var foundLast = await strategy.FindPartitionAsync(partitions[totalPartitions - 1].StartKey);
        foundLast.ShouldNotBeNull();
        foundLast.Value.ShouldBe(partitions[totalPartitions - 1]);
    }
    
    [Fact]
    public async Task UpdateAndFindAsync_MultipleSequentialUpdates_ShouldSucceed()
    {
        // Arrange
        await using var stream = new MemoryStream();
        var strategy = new BPlusTreePartitioningStrategy(serializationHelper);
        await strategy.InitializeAsync(stream);
        var p10 = new Partition(10, 20, 100L, 10, 110L, 5);
        var p20 = new Partition(20, 30, 200L, 20, 220L, 5);
        var p30 = new Partition(30, null, 300L, 30, 330L, 5);
        await strategy.InsertPartitionAsync(p10);
        await strategy.InsertPartitionAsync(p20);
        await strategy.InsertPartitionAsync(p30);

        // Act
        var updatedP10 = p10 with { DataLength = 111 };
        var updatedP30 = p30 with { DataLength = 333 };
        await strategy.UpdatePartitionAsync(updatedP10);
        await strategy.UpdatePartitionAsync(updatedP30);

        // Assert
        var foundP10 = await strategy.FindPartitionAsync(15);
        foundP10.ShouldNotBeNull();
        foundP10.Value.ShouldBe(updatedP10);

        var foundP20 = await strategy.FindPartitionAsync(25);
        foundP20.ShouldNotBeNull();
        foundP20.Value.ShouldBe(p20); // Should be untouched

        var foundP30 = await strategy.FindPartitionAsync(35);
        foundP30.ShouldNotBeNull();
        foundP30.Value.ShouldBe(updatedP30);
    }
    
    [Fact]
    public async Task UpdateAndFindAsync_WithStringKeys_ShouldReflectChanges()
    {
        // Arrange
        await using var stream = new MemoryStream();
        var strategy = new BPlusTreePartitioningStrategy(serializationHelper);
        await strategy.InitializeAsync(stream);
        var pApple = new Partition("apple", "banana", 1L, 1, 10L, 1);
        var pBanana = new Partition("banana", "cherry", 2L, 2, 20L, 2);
        var pCherry = new Partition("cherry", null, 3L, 3, 30L, 3);
        await strategy.InsertPartitionAsync(pApple);
        await strategy.InsertPartitionAsync(pBanana);
        await strategy.InsertPartitionAsync(pCherry);
        
        // Act
        var updatedBanana = pBanana with { MetadataLength = 99 };
        await strategy.UpdatePartitionAsync(updatedBanana);
        
        // Assert
        (await strategy.FindPartitionAsync("apple"))!.Value.ShouldBe(pApple);
        
        var foundBanana = await strategy.FindPartitionAsync("banana");
        foundBanana.ShouldNotBeNull();
        foundBanana.Value.ShouldBe(updatedBanana);
        foundBanana.Value.MetadataLength.ShouldBe(99);

        (await strategy.FindPartitionAsync("cherry"))!.Value.ShouldBe(pCherry);
    }

    [Fact]
    public async Task UpdatePartitionAsync_OnNonExistentKey_ShouldThrow()
    {
        // Arrange
        await using var stream = new MemoryStream();
        var strategy = new BPlusTreePartitioningStrategy(serializationHelper);
        await strategy.InitializeAsync(stream);
        var partition = new Partition(10, null, 100L, 200, 300L, 100);

        // Act & Assert
        await Should.ThrowAsync<KeyNotFoundException>(async () => await strategy.UpdatePartitionAsync(partition));
    }

    [Fact]
    public async Task UpdatePartitionAsync_OnNonExistentKeyInPopulatedTree_ShouldThrow()
    {
        // Arrange
        await using var stream = new MemoryStream();
        var strategy = new BPlusTreePartitioningStrategy(serializationHelper);
        await strategy.InitializeAsync(stream);
        await strategy.InsertPartitionAsync(new Partition(10, null, 1L, 1, 1L, 1));
        await strategy.InsertPartitionAsync(new Partition(30, null, 3L, 3, 3L, 3));
        
        var nonExistentPartition = new Partition(20, null, 2L, 2, 2L, 2);

        // Act & Assert
        await Should.ThrowAsync<KeyNotFoundException>(async () => await strategy.UpdatePartitionAsync(nonExistentPartition));
    }

    [Fact]
    public async Task DeletePartitionAsync_OnNonExistentKey_ShouldThrow()
    {
        // Arrange
        await using var stream = new MemoryStream();
        var strategy = new BPlusTreePartitioningStrategy(serializationHelper);
        await strategy.InitializeAsync(stream);
        await strategy.InsertPartitionAsync(new Partition(10, null, 1L, 1, 1L, 1));
        var partitionToDelete = new Partition(20, null, 2L, 2, 2L, 2);

        // Act & Assert
        await Should.ThrowAsync<KeyNotFoundException>(async () => await strategy.DeletePartitionAsync(partitionToDelete));
    }

    [Fact]
    public async Task DeleteAndFindAsync_SimpleDelete_ShouldSucceed()
    {
        // Arrange
        await using var stream = new MemoryStream();
        var strategy = new BPlusTreePartitioningStrategy(serializationHelper);
        await strategy.InitializeAsync(stream);
        var p10 = new Partition(10, 20, 100L, 10, 110L, 5);
        var p20 = new Partition(20, 30, 200L, 20, 220L, 5);
        var p30 = new Partition(30, null, 300L, 30, 330L, 5);
        await strategy.InsertPartitionAsync(p10);
        await strategy.InsertPartitionAsync(p20);
        await strategy.InsertPartitionAsync(p30);

        // Act
        await strategy.DeletePartitionAsync(p20);

        // Assert
        (await strategy.FindPartitionAsync(15))!.Value.ShouldBe(p10);
        (await strategy.FindPartitionAsync(25))!.Value.ShouldBe(p10);
        (await strategy.FindPartitionAsync(35))!.Value.ShouldBe(p30);
    }

    [Fact]
    public async Task DeletePartitionAsync_ShouldTriggerNodeMerge()
    {
        // Arrange
        await using var stream = new MemoryStream();
        var strategy = new BPlusTreePartitioningStrategy(serializationHelper);
        await strategy.InitializeAsync(stream);
        var partitions = Enumerable.Range(0, 15).Select(i => new Partition(i * 10, null, (long)i, (long)i, (long)i, (long)i)).ToList();
        foreach (var p in partitions)
        {
            await strategy.InsertPartitionAsync(p);
        }

        // After 15 insertions, degree 8: root has [70], left child [0..60] (7 keys, min), right [70..140] (8 keys)
        // Delete from right child until it's underfull, forcing merge with left (which is at min).
        await strategy.DeletePartitionAsync(partitions[14]); // Delete 140. Right has 7 keys (min).
        
        // Act
        await strategy.DeletePartitionAsync(partitions[13]); // Delete 130. Right has 6 keys (underfull). Merge should happen.

        // Assert
        var allKeys = Enumerable.Range(0, 13).Select(i => i * 10).ToList();
        foreach (var key in allKeys)
        {
            (await strategy.FindPartitionAsync(key))!.Value.StartKey.ShouldBe(key);
        }
        
        (await strategy.FindPartitionAsync(130))!.Value.ShouldBe(partitions[12]); // KeyNotFoundException is thrown. Find should return partition for 120
        (await strategy.FindPartitionAsync(135))!.Value.ShouldBe(partitions[12]);
        (await strategy.FindPartitionAsync(145))!.Value.ShouldBe(partitions[12]);
    }

    [Fact]
    public async Task DeletePartitionAsync_ShouldTriggerBorrow()
    {
        // Arrange
        await using var stream = new MemoryStream();
        var strategy = new BPlusTreePartitioningStrategy(serializationHelper);
        await strategy.InitializeAsync(stream);
        var partitions = Enumerable.Range(0, 16).Select(i => new Partition(i * 10, null, (long)i, (long)i, (long)i, (long)i)).ToList();
        foreach (var p in partitions)
        {
            await strategy.InsertPartitionAsync(p);
        }

        // After 16 insertions, degree 8: root has [80], left child [0..70] (8 keys), right [80..150] (8 keys)
        // Delete from right until underfull. Left has extra keys, so borrow.
        await strategy.DeletePartitionAsync(partitions[15]); // Delete 150. Right has 7 keys (min).

        // Act
        await strategy.DeletePartitionAsync(partitions[14]); // Delete 140. Right has 6 keys. Borrow should occur.

        // Assert
        // Key 70 should have moved from left to right child. Searching for 75 should still find partition 70.
        (await strategy.FindPartitionAsync(75))!.Value.ShouldBe(partitions[7]);
        // Key 60 should still be in left child.
        (await strategy.FindPartitionAsync(65))!.Value.ShouldBe(partitions[6]);
        // Deleted keys should not be found exactly, but find logic will get the one before.
        (await strategy.FindPartitionAsync(145))!.Value.ShouldBe(partitions[13]);
        (await strategy.FindPartitionAsync(155))!.Value.ShouldBe(partitions[13]);
    }

    [Fact]
    public async Task DeletePartitionAsync_DeleteAllPartitions_ShouldResultInEmptyTree()
    {
        // Arrange
        await using var stream = new MemoryStream();
        var strategy = new BPlusTreePartitioningStrategy(serializationHelper);
        await strategy.InitializeAsync(stream);
        var partitions = Enumerable.Range(0, 5).Select(i => new Partition(i, null, 0, 0, 0, 0)).ToList();
        var shuffledPartitions = partitions.OrderBy(x => Guid.NewGuid()).ToList();
        foreach (var p in partitions)
        {
            await strategy.InsertPartitionAsync(p);
        }

        // Act
        foreach (var p in shuffledPartitions)
        {
            await strategy.DeletePartitionAsync(p);
        }

        // Assert
        var result = await strategy.FindPartitionAsync(3);
        result.ShouldBeNull();
    }
}