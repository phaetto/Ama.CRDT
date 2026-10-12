namespace Ama.CRDT.Models;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

/// <summary>
/// Represents a strongly-typed mapping of Shard IDs to their corresponding Dotted Version Vectors.
/// Used to track the causal history seen by a replica per shard.
/// </summary>
public sealed class ShardVersionVectorMap : IDictionary<string, DottedVersionVector>, IEquatable<ShardVersionVectorMap>
{
    private readonly Dictionary<string, DottedVersionVector> map;

    /// <summary>
    /// Initializes a new, empty instance of the <see cref="ShardVersionVectorMap"/> class.
    /// </summary>
    public ShardVersionVectorMap()
    {
        map = new Dictionary<string, DottedVersionVector>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShardVersionVectorMap"/> class that contains elements copied from the specified dictionary.
    /// </summary>
    /// <param name="dictionary">The dictionary whose elements are copied to the new map.</param>
    public ShardVersionVectorMap(IDictionary<string, DottedVersionVector> dictionary)
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        map = new Dictionary<string, DottedVersionVector>(dictionary);
    }

    /// <inheritdoc/>
    public DottedVersionVector this[string key]
    {
        get => map[key];
        set => map[key] = value;
    }

    /// <inheritdoc/>
    public ICollection<string> Keys => map.Keys;

    /// <inheritdoc/>
    public ICollection<DottedVersionVector> Values => map.Values;

    /// <inheritdoc/>
    public int Count => map.Count;

    /// <inheritdoc/>
    public bool IsReadOnly => ((IDictionary<string, DottedVersionVector>)map).IsReadOnly;

    /// <inheritdoc/>
    public void Add(string key, DottedVersionVector value) => map.Add(key, value);

    /// <inheritdoc/>
    public void Add(KeyValuePair<string, DottedVersionVector> item) => ((IDictionary<string, DottedVersionVector>)map).Add(item);

    /// <inheritdoc/>
    public void Clear() => map.Clear();

    /// <inheritdoc/>
    public bool Contains(KeyValuePair<string, DottedVersionVector> item) => ((IDictionary<string, DottedVersionVector>)map).Contains(item);

    /// <inheritdoc/>
    public bool ContainsKey(string key) => map.ContainsKey(key);

    /// <inheritdoc/>
    public void CopyTo(KeyValuePair<string, DottedVersionVector>[] array, int arrayIndex) => ((IDictionary<string, DottedVersionVector>)map).CopyTo(array, arrayIndex);

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<string, DottedVersionVector>> GetEnumerator() => map.GetEnumerator();

    /// <inheritdoc/>
    public bool Remove(string key) => map.Remove(key);

    /// <inheritdoc/>
    public bool Remove(KeyValuePair<string, DottedVersionVector> item) => ((IDictionary<string, DottedVersionVector>)map).Remove(item);

    /// <inheritdoc/>
    public bool TryGetValue(string key, [MaybeNullWhen(false)] out DottedVersionVector value) => map.TryGetValue(key, out value);

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)map).GetEnumerator();

    /// <inheritdoc/>
    public bool Equals(ShardVersionVectorMap? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        if (Count != other.Count) return false;

        foreach (var kvp in this)
        {
            if (!other.TryGetValue(kvp.Key, out var otherVector)) return false;
            
            if (kvp.Value is null && otherVector is null) continue;
            if (kvp.Value is null || otherVector is null) return false;
            if (!kvp.Value.Equals(otherVector)) return false;
        }

        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as ShardVersionVectorMap);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var kvp in this.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            hash.Add(kvp.Key);
            hash.Add(kvp.Value);
        }
        return hash.ToHashCode();
    }
}