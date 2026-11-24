namespace Ama.CRDT.Models.Collections;

using System;
using System.Collections;
using System.Collections.Generic;
using Ama.CRDT.Services.Pooling;

/// <summary>
/// A memory-controlled set that minimizes per-instance heap allocations using a 3-tier adaptive layout:
/// Tier 1 (Empty with zero allocation), Tier 2 (Flat contiguous array up to <see cref="CrdtMemoryOptions.LinearScanThreshold"/>),
/// and Tier 3 (Promoted <see cref="HashSet{T}"/> for large sets).
/// </summary>
/// <typeparam name="T">The type of elements in the set.</typeparam>
public sealed class CompactSet<T> :
    ISet<T>,
    IReadOnlySet<T>,
    ICollection,
    IEquatable<CompactSet<T>>
{
    private readonly IEqualityComparer<T> comparer;
    private readonly ICrdtStringPool? stringPool;
    private T[] items = [];
    private HashSet<T>? promotedSet;
    private int count;

    /// <summary>
    /// Initializes a new, empty instance of the <see cref="CompactSet{T}"/> class.
    /// </summary>
    public CompactSet()
        : this(0, null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompactSet{T}"/> class with the specified capacity.
    /// </summary>
    /// <param name="capacity">The initial number of elements that the set can contain.</param>
    /// <param name="comparer">The equality comparer to use when comparing values, or <c>null</c> to use the default comparer.</param>
    /// <param name="stringPool">An optional string pool used to deduplicate items when <typeparamref name="T"/> is <see cref="string"/>.</param>
    public CompactSet(int capacity, IEqualityComparer<T>? comparer = null, ICrdtStringPool? stringPool = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);

        this.comparer = comparer ?? (typeof(T) == typeof(string)
            ? (IEqualityComparer<T>)(object)StringComparer.Ordinal
            : EqualityComparer<T>.Default);
        this.stringPool = stringPool ?? (typeof(T) == typeof(string) ? CrdtStringPool.Shared : null);

        if (capacity > CrdtMemoryOptions.LinearScanThreshold)
        {
            promotedSet = new HashSet<T>(capacity, this.comparer);
        }
        else if (capacity > 0)
        {
            items = new T[capacity];
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompactSet{T}"/> class with the specified comparer.
    /// </summary>
    /// <param name="comparer">The equality comparer to use when comparing values.</param>
    /// <param name="stringPool">An optional string pool used to deduplicate items when <typeparamref name="T"/> is <see cref="string"/>.</param>
    public CompactSet(IEqualityComparer<T>? comparer, ICrdtStringPool? stringPool = null)
        : this(0, comparer, stringPool)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompactSet{T}"/> class that contains elements copied from the specified collection.
    /// </summary>
    /// <param name="collection">The collection whose elements are copied to the new set.</param>
    /// <param name="comparer">The equality comparer to use when comparing values.</param>
    /// <param name="stringPool">An optional string pool used to deduplicate items when <typeparamref name="T"/> is <see cref="string"/>.</param>
    public CompactSet(IEnumerable<T> collection, IEqualityComparer<T>? comparer = null, ICrdtStringPool? stringPool = null)
        : this(
            collection is ICollection<T> c ? c.Count : 0,
            comparer ?? (collection as CompactSet<T>)?.comparer ?? (collection as HashSet<T>)?.Comparer,
            stringPool ?? (collection as CompactSet<T>)?.stringPool)
    {
        ArgumentNullException.ThrowIfNull(collection);
        foreach (var item in collection)
        {
            Add(item);
        }
    }

    /// <summary>
    /// Gets the <see cref="IEqualityComparer{T}"/> object that is used to determine equality for the values in the set.
    /// </summary>
    public IEqualityComparer<T> Comparer => comparer;

    /// <summary>
    /// Gets a value indicating whether this set has promoted its internal storage to a hash set.
    /// </summary>
    public bool IsPromoted => promotedSet is not null;

    /// <summary>
    /// Gets the current capacity of the backing storage.
    /// </summary>
    public int Capacity => promotedSet is not null ? promotedSet.EnsureCapacity(0) : items.Length;

    /// <inheritdoc/>
    public int Count => promotedSet is not null ? promotedSet.Count : count;

    /// <inheritdoc/>
    public bool IsReadOnly => false;

    /// <inheritdoc/>
    bool ICollection.IsSynchronized => false;

    /// <inheritdoc/>
    object ICollection.SyncRoot => this;

    /// <inheritdoc/>
    public bool Add(T item)
    {
        var normalized = NormalizeItem(item);

        if (promotedSet is not null)
        {
            return promotedSet.Add(normalized);
        }

        if (FindIndex(normalized) >= 0)
        {
            return false;
        }

        int threshold = CrdtMemoryOptions.LinearScanThreshold;
        if (count >= threshold)
        {
            PromoteAndAdd(normalized);
            return true;
        }

        if (count == items.Length)
        {
            int newCapacity = Math.Min(threshold, CrdtMemoryOptions.CalculateNewCapacity(items.Length, count + 1));
            Array.Resize(ref items, newCapacity);
        }

        items[count++] = normalized;
        return true;
    }

    /// <inheritdoc/>
    void ICollection<T>.Add(T item) => Add(item);

    /// <inheritdoc/>
    public void Clear()
    {
        if (promotedSet is not null)
        {
            if (CrdtMemoryOptions.AutoTrimOnEmpty)
            {
                promotedSet = null;
                items = [];
                count = 0;
            }
            else
            {
                promotedSet.Clear();
            }

            return;
        }

        if (CrdtMemoryOptions.AutoTrimOnEmpty)
        {
            items = [];
        }
        else if (count > 0)
        {
            Array.Clear(items, 0, count);
        }

        count = 0;
    }

    /// <inheritdoc/>
    public bool Contains(T item)
    {
        if (promotedSet is not null)
        {
            return promotedSet.Contains(item);
        }

        return FindIndex(item) >= 0;
    }

    /// <inheritdoc/>
    public void CopyTo(T[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);
        ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);

        if (array.Length - arrayIndex < Count)
        {
            throw new ArgumentException("The number of elements in the source set is greater than the available space from arrayIndex to the end of the destination array.");
        }

        if (promotedSet is not null)
        {
            promotedSet.CopyTo(array, arrayIndex);
            return;
        }

        Array.Copy(items, 0, array, arrayIndex, count);
    }

    /// <inheritdoc/>
    void ICollection.CopyTo(Array array, int index)
    {
        ArgumentNullException.ThrowIfNull(array);
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        if (promotedSet is not null)
        {
            ((ICollection)promotedSet).CopyTo(array, index);
            return;
        }

        Array.Copy(items, 0, array, index, count);
    }

    /// <inheritdoc/>
    public bool Remove(T item)
    {
        if (promotedSet is not null)
        {
            bool removed = promotedSet.Remove(item);
            if (removed && promotedSet.Count == 0 && CrdtMemoryOptions.AutoTrimOnEmpty)
            {
                promotedSet = null;
                items = [];
                count = 0;
            }

            return removed;
        }

        int index = FindIndex(item);
        if (index < 0)
        {
            return false;
        }

        count--;
        if (count == 0 && CrdtMemoryOptions.AutoTrimOnEmpty)
        {
            items = [];
            return true;
        }

        if (index < count)
        {
            Array.Copy(items, index + 1, items, index, count - index);
        }

        items[count] = default!;
        return true;
    }

    /// <inheritdoc/>
    public void ExceptWith(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (Count == 0)
        {
            return;
        }

        if (ReferenceEquals(other, this))
        {
            Clear();
            return;
        }

        foreach (var element in other)
        {
            Remove(element);
        }
    }

    /// <inheritdoc/>
    public void IntersectWith(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (Count == 0 || ReferenceEquals(other, this))
        {
            return;
        }

        var otherSet = other as ISet<T> ?? new HashSet<T>(other, comparer);
        var toRemove = new List<T>();

        foreach (var item in this)
        {
            if (!otherSet.Contains(item))
            {
                toRemove.Add(item);
            }
        }

        foreach (var item in toRemove)
        {
            Remove(item);
        }
    }

    /// <inheritdoc/>
    public bool IsProperSubsetOf(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var otherSet = new HashSet<T>(other, comparer);
        return Count < otherSet.Count && IsSubsetOf(otherSet);
    }

    /// <inheritdoc/>
    public bool IsProperSupersetOf(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var otherSet = new HashSet<T>(other, comparer);
        return Count > otherSet.Count && IsSupersetOf(otherSet);
    }

    /// <inheritdoc/>
    public bool IsSubsetOf(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (Count == 0 || ReferenceEquals(other, this))
        {
            return true;
        }

        var otherSet = other as ISet<T> ?? new HashSet<T>(other, comparer);
        foreach (var item in this)
        {
            if (!otherSet.Contains(item))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public bool IsSupersetOf(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (ReferenceEquals(other, this))
        {
            return true;
        }

        foreach (var item in other)
        {
            if (!Contains(item))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public bool Overlaps(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (Count == 0)
        {
            return false;
        }

        foreach (var item in other)
        {
            if (Contains(item))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public bool SetEquals(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (ReferenceEquals(other, this))
        {
            return true;
        }

        if (other is CompactSet<T> otherCompact && EqualityComparer<IEqualityComparer<T>>.Default.Equals(comparer, otherCompact.comparer))
        {
            if (Count != otherCompact.Count)
            {
                return false;
            }

            foreach (var item in this)
            {
                if (!otherCompact.Contains(item))
                {
                    return false;
                }
            }

            return true;
        }

        var otherSet = new HashSet<T>(other, comparer);
        if (Count != otherSet.Count)
        {
            return false;
        }

        foreach (var item in this)
        {
            if (!otherSet.Contains(item))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public void SymmetricExceptWith(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (Count == 0)
        {
            UnionWith(other);
            return;
        }

        if (ReferenceEquals(other, this))
        {
            Clear();
            return;
        }

        var uniqueOther = new HashSet<T>(other, comparer);
        foreach (var item in uniqueOther)
        {
            if (!Remove(item))
            {
                Add(item);
            }
        }
    }

    /// <inheritdoc/>
    public void UnionWith(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        foreach (var item in other)
        {
            Add(item);
        }
    }

    /// <summary>
    /// Sets the capacity of this set to the actual number of elements it contains.
    /// </summary>
    public void TrimExcess()
    {
        if (promotedSet is not null)
        {
            if (promotedSet.Count == 0)
            {
                promotedSet = null;
                items = [];
                count = 0;
            }
            else if (promotedSet.Count <= CrdtMemoryOptions.LinearScanThreshold)
            {
                int newCount = promotedSet.Count;
                var newItems = new T[newCount];
                promotedSet.CopyTo(newItems);
                promotedSet = null;
                items = newItems;
                count = newCount;
            }
            else
            {
                promotedSet.TrimExcess();
            }

            return;
        }

        if (count == 0)
        {
            items = [];
        }
        else if (items.Length > count)
        {
            Array.Resize(ref items, count);
        }
    }

    /// <summary>
    /// Creates an exact-sized shallow copy of this <see cref="CompactSet{T}"/>.
    /// </summary>
    /// <returns>A new <see cref="CompactSet{T}"/> with the exact capacity required for the current elements.</returns>
    public CompactSet<T> Clone()
    {
        var clone = new CompactSet<T>(Count, comparer, stringPool);
        if (promotedSet is not null)
        {
            foreach (var item in promotedSet)
            {
                clone.promotedSet!.Add(item);
            }
        }
        else if (count > 0)
        {
            Array.Copy(items, 0, clone.items, 0, count);
            clone.count = count;
        }

        return clone;
    }

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator()
    {
        if (promotedSet is not null)
        {
            return promotedSet.GetEnumerator();
        }

        return IterateFlatItems();
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    public bool Equals(CompactSet<T>? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return SetEquals(other);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as CompactSet<T>);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        int hash = 0;
        foreach (var item in this)
        {
            hash ^= item is null ? 0 : comparer.GetHashCode(item);
        }

        return hash;
    }

    private T NormalizeItem(T item)
    {
        if (typeof(T) == typeof(string) && item is not null && stringPool is not null)
        {
            var str = (string)(object)item;
            return (T)(object)stringPool.GetOrAdd(str);
        }

        return item;
    }

    private int FindIndex(T item)
    {
        if (count == 0)
        {
            return -1;
        }

        if (typeof(T) == typeof(string) && item is not null)
        {
            var targetStr = (string)(object)item;
            for (int i = 0; i < count; i++)
            {
                var currentItem = items[i];
                if (currentItem is not null)
                {
                    var currentStr = (string)(object)currentItem;
                    if (ReferenceEquals(currentStr, targetStr) || comparer.Equals(currentItem, item))
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        for (int i = 0; i < count; i++)
        {
            if (comparer.Equals(items[i], item))
            {
                return i;
            }
        }

        return -1;
    }

    private void PromoteAndAdd(T newItem)
    {
        int newCapacity = CrdtMemoryOptions.CalculateNewCapacity(count, count + 1);
        var set = new HashSet<T>(newCapacity, comparer);

        for (int i = 0; i < count; i++)
        {
            set.Add(items[i]);
        }

        set.Add(newItem);
        promotedSet = set;
        items = [];
        count = 0;
    }

    private IEnumerator<T> IterateFlatItems()
    {
        for (int i = 0; i < count; i++)
        {
            yield return items[i];
        }
    }
}