namespace Ama.CRDT.Models.Collections;

using System;

/// <summary>
/// Centralized configuration for memory-controlled CRDT collections (<see cref="CompactDictionary{TKey, TValue}"/> and <see cref="CompactSet{T}"/>).
/// Allows tuning the trade-off between CPU scan time and per-instance heap allocations across the application.
/// </summary>
public static class CrdtCollectionMemoryOptions
{
    private static int linearScanThreshold = 16;
    private static double growthFactor = 1.5;
    private static int minimumGrowthStep = 1;
    private static int initialFlatCapacity = 2;
    private static double autoDemoteRatio = 0.5;

    /// <summary>
    /// Gets or sets the maximum number of elements stored in a flat contiguous array (Tier 2)
    /// before promoting to a full hash table (<see cref="System.Collections.Generic.Dictionary{TKey, TValue}"/> or <see cref="System.Collections.Generic.HashSet{T}"/>).
    /// Must be between 1 and 256. Default is 16.
    /// </summary>
    public static int LinearScanThreshold
    {
        get => linearScanThreshold;
        set
        {
            if (value < 1 || value > 256)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "LinearScanThreshold must be between 1 and 256.");
            }

            linearScanThreshold = value;
        }
    }

    /// <summary>
    /// Gets or sets the growth multiplier for flat arrays when resizing in Tier 2.
    /// Set to 1.0 to use purely linear chunked growth controlled by <see cref="MinimumGrowthStep"/>.
    /// Must be between 1.0 and 4.0. Default is 1.5 (50% growth instead of 2x doubling).
    /// </summary>
    public static double GrowthFactor
    {
        get => growthFactor;
        set
        {
            if (value < 1.0 || value > 4.0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "GrowthFactor must be between 1.0 and 4.0.");
            }

            growthFactor = value;
        }
    }

    /// <summary>
    /// Gets or sets the minimum number of elements to grow by when resizing a flat array in Tier 2.
    /// When <see cref="GrowthFactor"/> is 1.0, the array grows by exactly this step size.
    /// Must be between 1 and 64. Default is 1.
    /// </summary>
    public static int MinimumGrowthStep
    {
        get => minimumGrowthStep;
        set
        {
            if (value < 1 || value > 64)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "MinimumGrowthStep must be between 1 and 64.");
            }

            minimumGrowthStep = value;
        }
    }

    /// <summary>
    /// Gets or sets the initial array capacity allocated when transitioning from Tier 1 (Empty) to Tier 2 (Flat array).
    /// Must be between 1 and 64. Default is 2.
    /// </summary>
    public static int InitialFlatCapacity
    {
        get => initialFlatCapacity;
        set
        {
            if (value < 1 || value > 64)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "InitialFlatCapacity must be between 1 and 64.");
            }

            initialFlatCapacity = value;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether collections automatically release their internal backing arrays
    /// or hash tables when their count reaches zero (via <c>Remove</c> or <c>Clear</c>).
    /// Default is <see langword="true"/>.
    /// </summary>
    public static bool AutoTrimOnEmpty { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether a promoted Tier 3 hash table automatically demotes back to a
    /// Tier 2 flat array upon removal when its count drops to or below <c>LinearScanThreshold * AutoDemoteRatio</c>.
    /// Default is <see langword="false"/>.
    /// </summary>
    public static bool AutoDemoteToFlat { get; set; } = false;

    /// <summary>
    /// Gets or sets the hysteresis ratio of <see cref="LinearScanThreshold"/> at or below which a promoted Tier 3
    /// hash table automatically demotes back to a Tier 2 flat array when <see cref="AutoDemoteToFlat"/> is enabled.
    /// Must be between 0.0 and 1.0. Default is 0.5.
    /// </summary>
    public static double AutoDemoteRatio
    {
        get => autoDemoteRatio;
        set
        {
            if (value < 0.0 || value > 1.0 || double.IsNaN(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "AutoDemoteRatio must be between 0.0 and 1.0.");
            }

            autoDemoteRatio = value;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether <see cref="CompactDictionary{TKey, TValue}"/> and <see cref="CompactSet{T}"/>
    /// automatically intern string keys and elements through <see cref="Ama.CRDT.Services.Pooling.CrdtStringPool.Shared"/>.
    /// Default is <see langword="true"/>.
    /// </summary>
    public static bool EnableAutomaticStringPooling { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether <see cref="Ama.CRDT.Services.CrdtMetadataManager.Compact{T}"/>
    /// automatically invokes <c>TrimExcess()</c> on compact metadata collections after garbage collection completes.
    /// Default is <see langword="false"/>.
    /// </summary>
    public static bool TrimExcessOnMetadataCompaction { get; set; } = false;

    internal static int CalculateNewCapacity(int currentCapacity, int minRequired)
    {
        if (currentCapacity == 0)
        {
            return Math.Max(initialFlatCapacity, minRequired);
        }

        int grownByFactor = (int)(currentCapacity * growthFactor);
        int grownByStep = currentCapacity + minimumGrowthStep;
        int next = Math.Max(grownByFactor, grownByStep);
        return Math.Max(next, minRequired);
    }
}