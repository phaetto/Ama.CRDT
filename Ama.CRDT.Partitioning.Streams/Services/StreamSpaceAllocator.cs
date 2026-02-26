namespace Ama.CRDT.Partitioning.Streams.Services;

using Ama.CRDT.Partitioning.Streams.Models;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A common utility for allocating and freeing space within a stream, managing in-place updates and free block reuse.
/// </summary>
public static class StreamSpaceAllocator
{
    public static (long offset, FreeSpaceState newState) Allocate(
        FreeSpaceState state,
        long requiredSize,
        long oldOffset = -1,
        long oldSize = -1)
    {
        var currentState = state;
        long offsetToUse = -1;

        if (oldOffset >= 0)
        {
            if (requiredSize <= oldSize)
            {
                offsetToUse = oldOffset;
            }
            else
            {
                currentState = Free(currentState, oldOffset, oldSize);
            }
        }

        if (offsetToUse == -1)
        {
            (offsetToUse, currentState) = AllocateFromFreeList(currentState, requiredSize);
        }

        return (offsetToUse, currentState);
    }

    private static (long offset, FreeSpaceState state) AllocateFromFreeList(FreeSpaceState state, long requiredSize)
    {
        if (state.FreeBlocks == null || state.FreeBlocks.Count == 0)
        {
            long newOffset = state.NextAvailableOffset;
            return (newOffset, state with { NextAvailableOffset = newOffset + requiredSize });
        }

        int bestIndex = -1;
        long bestSize = long.MaxValue;

        for (int i = 0; i < state.FreeBlocks.Count; i++)
        {
            var block = state.FreeBlocks[i];
            if (block.Size >= requiredSize && block.Size < bestSize)
            {
                bestIndex = i;
                bestSize = block.Size;
            }
        }

        if (bestIndex != -1)
        {
            var block = state.FreeBlocks[bestIndex];
            var blocks = state.FreeBlocks.ToList();
            blocks.RemoveAt(bestIndex);

            return (block.Offset, state with { FreeBlocks = blocks });
        }

        long offset = state.NextAvailableOffset;
        return (offset, state with { NextAvailableOffset = offset + requiredSize });
    }

    public static FreeSpaceState Free(FreeSpaceState state, long offset, long size, int maxFreeBlocks = 20)
    {
        var blocks = state.FreeBlocks?.ToList() ?? new List<FreeBlock>();
        blocks.Add(new FreeBlock(offset, size));

        if (blocks.Count > maxFreeBlocks)
        {
            blocks = blocks.OrderByDescending(b => b.Size).Take(maxFreeBlocks).ToList();
        }

        return state with { FreeBlocks = blocks };
    }
}