namespace Ama.CRDT.Models.Partitioning;

/// <summary>
/// Represents a composite key used for partitioning, consisting of a logical key for data isolation
/// and a range key for splitting large collections.
/// </summary>
/// <param name="LogicalKey">The key that identifies a logical document (e.g., Tenant ID).</param>
/// <param name="RangeKey">The key that identifies a range within the logical document (e.g., a dictionary key). A null value typically identifies a header partition.</param>
public readonly record struct CompositePartitionKey(object LogicalKey, object? RangeKey)
    : IComparable<CompositePartitionKey>, IComparable
{
    /// <inheritdoc/>
    public int CompareTo(CompositePartitionKey other)
    {
        var logicalKeyCompare = Comparer<object>.Default.Compare(LogicalKey, other.LogicalKey);
        if (logicalKeyCompare != 0)
        {
            return logicalKeyCompare;
        }

        // A null RangeKey represents the header partition or the start of a range, and should come first.
        if (RangeKey is null && other.RangeKey is null) return 0;
        if (RangeKey is null) return -1;
        if (other.RangeKey is null) return 1;

        return Comparer<object>.Default.Compare(RangeKey, other.RangeKey);
    }

    /// <inheritdoc/>
    public int CompareTo(object? obj)
    {
        if (obj is null) return 1;
        if (obj is CompositePartitionKey other)
        {
            return CompareTo(other);
        }
        throw new ArgumentException($"Object must be of type {nameof(CompositePartitionKey)}");
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"({LogicalKey}, {RangeKey ?? "null"})";
    }
}