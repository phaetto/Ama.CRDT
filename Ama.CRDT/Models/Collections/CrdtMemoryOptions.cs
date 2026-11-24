namespace Ama.CRDT.Models.Collections;

using System;

/// <summary>
/// Provides centralized configuration options for memory-controlled CRDT collections
/// such as <see cref="CompactDictionary{TKey, TValue}"/> and <see cref="CompactSet{T}"/>.
/// </summary>
public static class CrdtMemoryOptions
{
    private static int linearScanThreshold = 16;
    private static double growthFactor = 1.5;
    private static bool autoTrimOnEmpty = true;

    /// <summary>
    /// Gets or sets the maximum number of items stored in a flat contiguous array before promoting to a hashed index.
    /// Default is 16.
    /// </summary>
    public static int LinearScanThreshold
    {
        get => linearScanThreshold;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            linearScanThreshold = value;
        }
    }

    /// <summary>
    /// Gets or sets the capacity growth multiplier used when a compact collection needs to expand.
    /// Default is 1.5.
    /// </summary>
    public static double GrowthFactor
    {
        get => growthFactor;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, 1.0);
            growthFactor = value;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether compact collections automatically release their internal arrays
    /// back to <see cref="Array.Empty{T}"/> when all elements are removed.
    /// Default is <c>true</c>.
    /// </summary>
    public static bool AutoTrimOnEmpty
    {
        get => autoTrimOnEmpty;
        set => autoTrimOnEmpty = value;
    }

    internal static int CalculateNewCapacity(int currentCapacity, int minRequired)
    {
        if (currentCapacity == 0)
        {
            return Math.Max(2, minRequired);
        }

        int grown = (int)(currentCapacity * growthFactor);
        if (grown <= currentCapacity)
        {
            grown = currentCapacity + 1;
        }

        return Math.Max(grown, minRequired);
    }
}