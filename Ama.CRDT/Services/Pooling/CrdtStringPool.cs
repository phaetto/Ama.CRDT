namespace Ama.CRDT.Services.Pooling;

using System;
using System.Collections.Concurrent;
using System.Threading;

/// <summary>
/// A thread-safe, bounded string pooling service that leverages <see cref="ConcurrentDictionary{TKey,TValue}.GetAlternateLookup{TAlternateKey}"/>
/// to deduplicate structural CRDT strings without allocating temporary lookup strings.
/// </summary>
public sealed class CrdtStringPool : ICrdtStringPool
{
    private const int DefaultMaxCapacity = 16384;
    private const int DefaultMaxStringLength = 128;

    private readonly ConcurrentDictionary<string, string> pool = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string>.AlternateLookup<ReadOnlySpan<char>> spanLookup;
    private readonly int maxCapacity;
    private readonly int maxStringLength;
    private int count;

    /// <summary>
    /// Gets the default shared instance of <see cref="CrdtStringPool"/>.
    /// </summary>
    public static CrdtStringPool Shared { get; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="CrdtStringPool"/> class with default capacity and length limits.
    /// </summary>
    public CrdtStringPool()
        : this(DefaultMaxCapacity, DefaultMaxStringLength)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CrdtStringPool"/> class with specified capacity and length limits.
    /// </summary>
    /// <param name="maxCapacity">The maximum number of unique strings to retain in the pool.</param>
    /// <param name="maxStringLength">The maximum character length of a string eligible for pooling.</param>
    public CrdtStringPool(int maxCapacity, int maxStringLength = DefaultMaxStringLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCapacity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxStringLength);

        this.maxCapacity = maxCapacity;
        this.maxStringLength = maxStringLength;
        spanLookup = pool.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    /// <inheritdoc/>
    public string GetOrAdd(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.Length == 0)
        {
            return string.Empty;
        }

        if (pool.TryGetValue(value, out var existing))
        {
            return existing;
        }

        if (!ShouldPool(value.AsSpan()))
        {
            return value;
        }

        if (Volatile.Read(ref count) >= maxCapacity)
        {
            return value;
        }

        if (pool.TryAdd(value, value))
        {
            Interlocked.Increment(ref count);
            return value;
        }

        return pool.TryGetValue(value, out existing) ? existing : value;
    }

    /// <inheritdoc/>
    public string GetOrAdd(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty)
        {
            return string.Empty;
        }

        if (spanLookup.TryGetValue(value, out var existing))
        {
            return existing;
        }

        var allocated = new string(value);

        if (!ShouldPool(value))
        {
            return allocated;
        }

        if (Volatile.Read(ref count) >= maxCapacity)
        {
            return allocated;
        }

        if (pool.TryAdd(allocated, allocated))
        {
            Interlocked.Increment(ref count);
            return allocated;
        }

        return pool.TryGetValue(allocated, out existing) ? existing : allocated;
    }

    private bool ShouldPool(ReadOnlySpan<char> value)
    {
        if (value.Length > maxStringLength)
        {
            return false;
        }

        // Skip dynamic dictionary keys in paths (e.g., "$.users['guid-1234'].name") to avoid cache thrashing
        if (value.IndexOf("['".AsSpan(), StringComparison.Ordinal) >= 0)
        {
            return false;
        }

        return true;
    }
}