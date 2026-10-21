namespace Ama.CRDT.Services.Pooling;

using System;

/// <summary>
/// Defines a thread-safe, bounded string pooling service used to deduplicate recurring structural CRDT strings
/// such as <c>ReplicaId</c>, <c>ShardId</c>, and <c>JsonPath</c> without causing unbounded memory leaks in the CLR intern pool.
/// </summary>
public interface ICrdtStringPool
{
    /// <summary>
    /// Retrieves an existing pooled string instance that matches the specified <paramref name="value"/>,
    /// or adds the string to the pool if it is within the bounded capacity and length limits.
    /// </summary>
    /// <param name="value">The string to pool.</param>
    /// <returns>The pooled string instance, or the original <paramref name="value"/> if not pooled.</returns>
    string GetOrAdd(string value);

    /// <summary>
    /// Retrieves an existing pooled string instance that matches the specified character span without allocating
    /// a temporary string if the value is already pooled, or allocates and adds it to the pool if within limits.
    /// </summary>
    /// <param name="value">The character span to look up or pool.</param>
    /// <returns>The pooled or newly allocated string instance.</returns>
    string GetOrAdd(ReadOnlySpan<char> value);
}