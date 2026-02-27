namespace Ama.CRDT.Partitioning.Streams.UnitTests.Services;

using System.Collections.Generic;
using Ama.CRDT.Partitioning.Streams.Models;
using Ama.CRDT.Partitioning.Streams.Services;
using Shouldly;
using Xunit;

public sealed class StreamSpaceAllocatorTests
{
    [Fact]
    public void Allocate_FirstTime_ShouldUseNextAvailableOffset()
    {
        // Arrange
        var state = new FreeSpaceState { NextAvailableOffset = 10, FreeBlocks = new List<FreeBlock>() };

        // Act
        var (offset, newState) = StreamSpaceAllocator.Allocate(state, 15);

        // Assert
        offset.ShouldBe(10);
        newState.NextAvailableOffset.ShouldBe(25);
        newState.FreeBlocks.ShouldBeEmpty();
    }

    [Fact]
    public void Allocate_WithOldOffset_SameSize_ShouldReuseOldOffset()
    {
        // Arrange
        var state = new FreeSpaceState { NextAvailableOffset = 100, FreeBlocks = new List<FreeBlock>() };

        // Act
        var (offset, newState) = StreamSpaceAllocator.Allocate(state, 20, oldOffset: 50, oldSize: 20);

        // Assert
        offset.ShouldBe(50);
        newState.NextAvailableOffset.ShouldBe(100);
        newState.FreeBlocks.ShouldBeEmpty();
    }

    [Fact]
    public void Allocate_WithOldOffset_SmallerSize_ShouldReuseOldOffset()
    {
        // Arrange
        var state = new FreeSpaceState { NextAvailableOffset = 100, FreeBlocks = new List<FreeBlock>() };

        // Act
        var (offset, newState) = StreamSpaceAllocator.Allocate(state, 10, oldOffset: 50, oldSize: 20);

        // Assert
        offset.ShouldBe(50);
        newState.NextAvailableOffset.ShouldBe(100);
        newState.FreeBlocks.ShouldBeEmpty();
    }

    [Fact]
    public void Allocate_WithOldOffset_LargerSize_ShouldFreeOldAndAllocateNew()
    {
        // Arrange
        var state = new FreeSpaceState { NextAvailableOffset = 100, FreeBlocks = new List<FreeBlock>() };

        // Act
        var (offset, newState) = StreamSpaceAllocator.Allocate(state, 30, oldOffset: 50, oldSize: 20);

        // Assert
        offset.ShouldBe(100);
        newState.NextAvailableOffset.ShouldBe(130);
        
        // Old block should be added to the free list
        newState.FreeBlocks.ShouldNotBeNull();
        newState.FreeBlocks.ShouldHaveSingleItem();
        newState.FreeBlocks[0].Offset.ShouldBe(50);
        newState.FreeBlocks[0].Size.ShouldBe(20);
    }

    [Fact]
    public void Allocate_WithOldOffset_LargerSize_ShouldFreeOldAndReuseAnotherFreeBlock()
    {
        // Arrange
        var state = new FreeSpaceState
        {
            NextAvailableOffset = 200,
            FreeBlocks = new List<FreeBlock> { new FreeBlock(10, 50) } // We already have a free block of size 50
        };

        // Act
        // We need 40, which is larger than our old block (20).
        // It should free the old block (size 20), and then find the existing free block of size 50.
        var (offset, newState) = StreamSpaceAllocator.Allocate(state, 40, oldOffset: 150, oldSize: 20);

        // Assert
        offset.ShouldBe(10); // It reused the existing 50-sized block
        newState.NextAvailableOffset.ShouldBe(200); // Unchanged
        
        newState.FreeBlocks.ShouldNotBeNull();
        newState.FreeBlocks.ShouldHaveSingleItem();
        
        // The list should now contain the freed old block
        newState.FreeBlocks[0].Offset.ShouldBe(150);
        newState.FreeBlocks[0].Size.ShouldBe(20);
    }

    [Fact]
    public void Allocate_FromFreeList_ExactMatch_ShouldReuseFreeBlock()
    {
        // Arrange
        var state = new FreeSpaceState
        {
            NextAvailableOffset = 100,
            FreeBlocks = new List<FreeBlock> { new FreeBlock(20, 15), new FreeBlock(50, 25) }
        };

        // Act
        var (offset, newState) = StreamSpaceAllocator.Allocate(state, 25);

        // Assert
        offset.ShouldBe(50);
        newState.NextAvailableOffset.ShouldBe(100); // Unchanged
        
        newState.FreeBlocks.ShouldNotBeNull();
        newState.FreeBlocks.ShouldHaveSingleItem();
        newState.FreeBlocks[0].Offset.ShouldBe(20);
        newState.FreeBlocks[0].Size.ShouldBe(15);
    }

    [Fact]
    public void Allocate_FromFreeList_BestFit_ShouldReuseSmallestSufficientBlock()
    {
        // Arrange
        var state = new FreeSpaceState
        {
            NextAvailableOffset = 200,
            FreeBlocks = new List<FreeBlock>
            {
                new FreeBlock(10, 50),
                new FreeBlock(70, 30),
                new FreeBlock(120, 20)
            }
        };

        // Act
        var (offset, newState) = StreamSpaceAllocator.Allocate(state, 25);

        // Assert
        offset.ShouldBe(70); // 30 is the smallest block that is >= 25
        
        newState.FreeBlocks.ShouldNotBeNull();
        newState.FreeBlocks.Count.ShouldBe(2);
        newState.FreeBlocks.ShouldNotContain(b => b.Offset == 70); // 70 should be removed
    }

    [Fact]
    public void Allocate_FromFreeList_NoSufficientBlock_ShouldAllocateFromEnd()
    {
        // Arrange
        var state = new FreeSpaceState
        {
            NextAvailableOffset = 100,
            FreeBlocks = new List<FreeBlock>
            {
                new FreeBlock(10, 10),
                new FreeBlock(30, 15)
            }
        };

        // Act
        var (offset, newState) = StreamSpaceAllocator.Allocate(state, 25);

        // Assert
        offset.ShouldBe(100);
        newState.NextAvailableOffset.ShouldBe(125);
        
        newState.FreeBlocks.ShouldNotBeNull();
        newState.FreeBlocks.Count.ShouldBe(2); // Free blocks unchanged
    }

    [Fact]
    public void Free_ShouldAddNewBlock()
    {
        // Arrange
        var state = new FreeSpaceState { NextAvailableOffset = 100, FreeBlocks = new List<FreeBlock>() };

        // Act
        var newState = StreamSpaceAllocator.Free(state, 50, 20);

        // Assert
        newState.FreeBlocks.ShouldNotBeNull();
        newState.FreeBlocks.ShouldHaveSingleItem();
        newState.FreeBlocks[0].Offset.ShouldBe(50);
        newState.FreeBlocks[0].Size.ShouldBe(20);
    }

    [Fact]
    public void Free_WithNullFreeBlocks_ShouldInitializeList()
    {
        // Arrange
        var state = new FreeSpaceState { NextAvailableOffset = 100, FreeBlocks = null };

        // Act
        var newState = StreamSpaceAllocator.Free(state, 20, 30);

        // Assert
        newState.FreeBlocks.ShouldNotBeNull();
        newState.FreeBlocks.ShouldHaveSingleItem();
        newState.FreeBlocks[0].Offset.ShouldBe(20);
        newState.FreeBlocks[0].Size.ShouldBe(30);
    }

    [Fact]
    public void Free_ExceedingMaxBlocks_ShouldKeepLargest()
    {
        // Arrange
        var initialBlocks = new List<FreeBlock>();
        for (int i = 1; i <= 20; i++)
        {
            initialBlocks.Add(new FreeBlock(i * 10, i)); // Sizes 1 to 20
        }
        
        var state = new FreeSpaceState { NextAvailableOffset = 500, FreeBlocks = initialBlocks };
        
        // Act
        // We add a block of size 15. The total becomes 21 blocks, exceeding the default max of 20.
        // It should drop the smallest block, which has a size of 1.
        var newState = StreamSpaceAllocator.Free(state, 300, 15);

        // Assert
        newState.FreeBlocks.ShouldNotBeNull();
        newState.FreeBlocks.Count.ShouldBe(20);
        
        // Should have dropped the size 1 block
        newState.FreeBlocks.ShouldNotContain(b => b.Size == 1);
        
        // Should contain the newly added block
        newState.FreeBlocks.ShouldContain(b => b.Offset == 300 && b.Size == 15);
        
        // Should still contain the largest block
        newState.FreeBlocks.ShouldContain(b => b.Size == 20);
    }
}