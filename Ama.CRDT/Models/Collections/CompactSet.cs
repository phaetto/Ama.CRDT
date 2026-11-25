namespace Ama.CRDT.Models.Collections;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Ama.CRDT.Services.Pooling;

/// <summary>
/// A memory-controlled set that minimizes per-instance heap allocations using a 3-tier adaptive layout:
/// Tier 1 (Empty with zero allocation), Tier 2 (Flat contiguous array up to <see cref="CrdtCollectionMemoryOptions.LinearScanThreshold"/>),
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
    /// Initializes a new instance of the <see cref="CompactSet{T}"/> class with the specified capacity, comparer, and string pool.
    /// </summary>
    /// <param name="capacity">The initial number of elements that the set can contain.</param>
    /// <param name="comparer">The equality comparer to use when comparing values, or <see langword="null"/> to use the default comparer.</param>
    /// <param name="stringPool">An optional string pool used to deduplicate items when <typeparamref name="T"/> is <see cref="string"/>.</param>
    public CompactSet(int capacity, IEqualityComparer<T>? comparer = null, ICrdtStringPool? stringPool = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);

        this.comparer = comparer ?? (typeof(T) == typeof(string)
            ? (IEqualityComparer<T>)(object)StringComparer.Ordinal
            : EqualityComparer<T>.Default);

        if (capacity > CrdtCollectionMemoryOptions.LinearScanThreshold)
        {
            promotedSet = new HashSet<T>(capacity, this.comparer);
        }
        else if (capacity > 0)
        {
            items = new T[capacity];
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompactSet{T}"/> class with the specified comparer and string pool.
    /// </summary>
    /// <param name="comparer">The equality comparer to use when comparing values, or <see langword="null"/> to use the default comparer.</param>
    /// <param name="stringPool">An optional string pool used to deduplicate items when <typeparamref name="T"/> is <see cref="string"/>.</param>
    public CompactSet(IEqualityComparer<T>? comparer, ICrdtStringPool? stringPool = null)
        : this(0, comparer, stringPool)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompactSet{T}"/> class that contains elements copied from the specified collection.
    /// </summary>
    /// <param name="collection">The collection whose elements are copied to the new set.</param>
    /// <param name="comparer">The equality comparer to use when comparing values, or <see langword="null"/> to use the default comparer.</param>
    /// <param name="stringPool">An optional string pool used to deduplicate items when <typeparamref name="T"/> is <see cref="string"/>.</param>
    public CompactSet(IEnumerable<T> collection, IEqualityComparer<T>? comparer = null, ICrdtStringPool? stringPool = null)
    {
        ArgumentNullException.ThrowIfNull(collection);

        this.comparer = comparer
            ?? (collection as CompactSet<T>)?.comparer
            ?? (collection as HashSet<T>)?.Comparer
            ?? (typeof(T) == typeof(string)
                ? (IEqualityComparer<T>)(object)StringComparer.Ordinal
                : EqualityComparer<T>.Default);

        if (collection is CompactSet<T> compactSource &&
            (ReferenceEquals(compactSource.comparer, this.comparer) || compactSource.comparer.Equals(this.comparer)))
        {
            if (compactSource.promotedSet is not null)
            {
                promotedSet = new HashSet<T>(compactSource.promotedSet, this.comparer);
                return;
            }

            if (compactSource.count > 0)
            {
                if (compactSource.count <= CrdtCollectionMemoryOptions.LinearScanThreshold)
                {
                    items = new T[compactSource.count];
                    Array.Copy(compactSource.items, items, compactSource.count);
                    count = compactSource.count;
                }
                else
                {
                    promotedSet = new HashSet<T>(compactSource.count, this.comparer);
                    for (int i = 0; i < compactSource.count; i++)
                    {
                        promotedSet.Add(compactSource.items[i]);
                    }
                }
            }

            return;
        }

        if (collection is ICollection<T> coll)
        {
            int initialCount = coll.Count;
            if (initialCount > 0)
            {
                if (initialCount <= CrdtCollectionMemoryOptions.LinearScanThreshold)
                {
                    items = new T[initialCount];
                }
                else
                {
                    promotedSet = new HashSet<T>(initialCount, this.comparer);
                }
            }
        }

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
        var normalizedItem = PoolItemIfNeeded(item);

        if (promotedSet is not null)
        {
            return promotedSet.Add(normalizedItem);
        }

        if (FindIndex(normalizedItem) >= 0)
        {
            return false;
        }

        int threshold = CrdtCollectionMemoryOptions.LinearScanThreshold;
        if (count >= threshold)
        {
            PromoteAndAdd(normalizedItem);
            return true;
        }

        if (count == items.Length)
        {
            int nextCapacity = Math.Max(
                items.Length == 0 ? CrdtCollectionMemoryOptions.InitialFlatCapacity : (int)(items.Length * CrdtCollectionMemoryOptions.GrowthFactor),
                items.Length + CrdtCollectionMemoryOptions.MinimumGrowthStep);

            if (nextCapacity > CrdtCollectionMemoryOptions.LinearScanThreshold)
            {
                nextCapacity = CrdtCollectionMemoryOptions.LinearScanThreshold;
            }
            
            int newCapacity = Math.Min(threshold, CrdtCollectionMemoryOptions.CalculateNewCapacity(items.Length, count + 1));
            Array.Resize(ref items, newCapacity);
        }

        items[count++] = normalizedItem;
        return true;
    }

    /// <inheritdoc/>
    void ICollection<T>.Add(T item) => Add(item);

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
    public bool Remove(T item)
    {
        if (promotedSet is not null)
        {
            bool removed = promotedSet.Remove(item);
            if (removed)
            {
                if (promotedSet.Count == 0 && CrdtCollectionMemoryOptions.AutoTrimOnEmpty)
                {
                    promotedSet = null;
                    items = [];
                }
                else if (CrdtCollectionMemoryOptions.AutoDemoteToFlat &&
                         promotedSet.Count > 0 &&
                         promotedSet.Count <= (int)(CrdtCollectionMemoryOptions.LinearScanThreshold * CrdtCollectionMemoryOptions.AutoDemoteRatio))
                {
                    DemoteToFlatArray();
                }
            }

            return removed;
        }

        int idx = FindIndex(item);
        if (idx < 0)
        {
            return false;
        }

        count--;
        if (count == 0 && CrdtCollectionMemoryOptions.AutoTrimOnEmpty)
        {
            items = [];
            return true;
        }

        if (idx < count)
        {
            Array.Copy(items, idx + 1, items, idx, count - idx);
        }

        items[count] = default!;
        return true;
    }

    /// <inheritdoc/>
    public void Clear()
    {
        if (promotedSet is not null)
        {
            if (CrdtCollectionMemoryOptions.AutoTrimOnEmpty)
            {
                promotedSet = null;
                items = [];
            }
            else
            {
                promotedSet.Clear();
            }

            return;
        }

        if (count > 0)
        {
            if (CrdtCollectionMemoryOptions.AutoTrimOnEmpty)
            {
                items = [];
            }
            else
            {
                Array.Clear(items, 0, count);
            }

            count = 0;
        }
        else if (CrdtCollectionMemoryOptions.AutoTrimOnEmpty && items.Length > 0)
        {
            items = [];
        }
    }

    /// <summary>
    /// Sets the capacity of this set to what it would be if it had been originally initialized with all its entries.
    /// Also demotes from Tier 3 to Tier 2 if the count is within <see cref="CrdtCollectionMemoryOptions.LinearScanThreshold"/>.
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
            else if (promotedSet.Count <= CrdtCollectionMemoryOptions.LinearScanThreshold)
            {
                DemoteToFlatArray();
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

    /// <inheritdoc/>
    public void CopyTo(T[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);
        if (arrayIndex < 0 || arrayIndex > array.Length || (array.Length - arrayIndex) < Count)
        {
            throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        }

        if (promotedSet is not null)
        {
            promotedSet.CopyTo(array, arrayIndex);
            return;
        }

        if (count > 0)
        {
            Array.Copy(items, 0, array, arrayIndex, count);
        }
    }

    /// <inheritdoc/>
    void ICollection.CopyTo(Array array, int index)
    {
        ArgumentNullException.ThrowIfNull(array);
        if (index < 0 || index > array.Length || (array.Length - index) < Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        if (promotedSet is not null)
        {
            ((ICollection)promotedSet).CopyTo(array, index);
            return;
        }

        if (array is T[] typedArray)
        {
            CopyTo(typedArray, index);
            return;
        }

        if (array is object?[] objects)
        {
            for (int i = 0; i < count; i++)
            {
                objects[index + i] = items[i];
            }

            return;
        }

        throw new ArgumentException("Target array type is not compatible with the type of items in the collection.", nameof(array));
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

    /// <inheritdoc/>
    public void IntersectWith(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (Count == 0 || ReferenceEquals(this, other))
        {
            return;
        }

        if (promotedSet is not null)
        {
            promotedSet.IntersectWith(other);
            if (promotedSet.Count == 0 && CrdtCollectionMemoryOptions.AutoTrimOnEmpty)
            {
                promotedSet = null;
                items = [];
            }
            else if (CrdtCollectionMemoryOptions.AutoDemoteToFlat &&
                     promotedSet.Count > 0 &&
                     promotedSet.Count <= (int)(CrdtCollectionMemoryOptions.LinearScanThreshold * CrdtCollectionMemoryOptions.AutoDemoteRatio))
            {
                DemoteToFlatArray();
            }

            return;
        }

        var otherSet = ToLookupSet(other);
        for (int i = count - 1; i >= 0; i--)
        {
            if (!otherSet.Contains(items[i]))
            {
                RemoveAtFlatIndex(i);
            }
        }
    }

    /// <inheritdoc/>
    public void ExceptWith(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (Count == 0)
        {
            return;
        }

        if (ReferenceEquals(this, other))
        {
            Clear();
            return;
        }

        foreach (var item in other)
        {
            Remove(item);
        }
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

        if (ReferenceEquals(this, other))
        {
            Clear();
            return;
        }

        if (promotedSet is not null)
        {
            promotedSet.SymmetricExceptWith(other);
            if (promotedSet.Count == 0 && CrdtCollectionMemoryOptions.AutoTrimOnEmpty)
            {
                promotedSet = null;
                items = [];
            }
            else if (CrdtCollectionMemoryOptions.AutoDemoteToFlat &&
                     promotedSet.Count > 0 &&
                     promotedSet.Count <= (int)(CrdtCollectionMemoryOptions.LinearScanThreshold * CrdtCollectionMemoryOptions.AutoDemoteRatio))
            {
                DemoteToFlatArray();
            }

            return;
        }

        var uniqueOther = ToLookupSet(other);
        foreach (var item in uniqueOther)
        {
            if (!Remove(item))
            {
                Add(item);
            }
        }
    }

    /// <inheritdoc/>
    public bool IsSubsetOf(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (Count == 0 || ReferenceEquals(this, other))
        {
            return true;
        }

        if (promotedSet is not null)
        {
            return promotedSet.IsSubsetOf(other);
        }

        var otherSet = ToLookupSet(other);
        if (Count > otherSet.Count)
        {
            return false;
        }

        for (int i = 0; i < count; i++)
        {
            if (!otherSet.Contains(items[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public bool IsProperSubsetOf(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (ReferenceEquals(this, other))
        {
            return false;
        }

        if (promotedSet is not null)
        {
            return promotedSet.IsProperSubsetOf(other);
        }

        var otherSet = ToLookupSet(other);
        if (Count >= otherSet.Count)
        {
            return false;
        }

        for (int i = 0; i < count; i++)
        {
            if (!otherSet.Contains(items[i]))
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

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (promotedSet is not null)
        {
            return promotedSet.IsSupersetOf(other);
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
    public bool IsProperSupersetOf(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (Count == 0 || ReferenceEquals(this, other))
        {
            return false;
        }

        if (promotedSet is not null)
        {
            return promotedSet.IsProperSupersetOf(other);
        }

        var otherSet = ToLookupSet(other);
        if (Count <= otherSet.Count)
        {
            return false;
        }

        foreach (var item in otherSet)
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

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (promotedSet is not null)
        {
            return promotedSet.Overlaps(other);
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

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (promotedSet is not null)
        {
            return promotedSet.SetEquals(other);
        }

        if (other is CompactSet<T> otherCompact &&
            (ReferenceEquals(comparer, otherCompact.comparer) || comparer.Equals(otherCompact.comparer)))
        {
            if (Count != otherCompact.Count)
            {
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                if (!otherCompact.Contains(items[i]))
                {
                    return false;
                }
            }

            return true;
        }

        var otherSet = ToLookupSet(other);
        if (Count != otherSet.Count)
        {
            return false;
        }

        for (int i = 0; i < count; i++)
        {
            if (!otherSet.Contains(items[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public bool Equals(CompactSet<T>? other)
    {
        if (other is null)
        {
            return false;
        }

        return SetEquals(other);
    }

    /// <summary>
    /// Creates a shallow copy of the <see cref="CompactSet{T}"/> with an exact-fit backing capacity.
    /// </summary>
    /// <returns>A new <see cref="CompactSet{T}"/> containing the same elements.</returns>
    public CompactSet<T> Clone() => new(this, comparer);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as CompactSet<T>);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        int hash = 0;
        foreach (var item in this)
        {
            if (item is not null)
            {
                hash ^= comparer.GetHashCode(item);
            }
        }

        return hash;
    }

    /// <summary>
    /// Returns a value-type enumerator that iterates through the <see cref="CompactSet{T}"/> without heap allocations.
    /// </summary>
    /// <returns>A <see cref="Enumerator"/> for the <see cref="CompactSet{T}"/>.</returns>
    public Enumerator GetEnumerator() => new(this);

    /// <inheritdoc/>
    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        if (promotedSet is not null)
        {
            return promotedSet.GetEnumerator();
        }

        return IterateFlatItems();
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<T>)this).GetEnumerator();

    /// <summary>
    /// Enumerates the elements of a <see cref="CompactSet{T}"/> without allocating on the heap.
    /// </summary>
    public struct Enumerator : IEnumerator<T>
    {
        private readonly CompactSet<T> compactSet;
        private HashSet<T>.Enumerator setEnumerator;
        private int index;
        private T current;
        private readonly bool isSet;

        internal Enumerator(CompactSet<T> compactSet)
        {
            this.compactSet = compactSet;
            if (compactSet.promotedSet is not null)
            {
                isSet = true;
                setEnumerator = compactSet.promotedSet.GetEnumerator();
                index = 0;
                current = default!;
            }
            else
            {
                isSet = false;
                setEnumerator = default;
                index = 0;
                current = default!;
            }
        }

        /// <inheritdoc/>
        public readonly T Current => isSet ? setEnumerator.Current : current;

        /// <inheritdoc/>
        readonly object? IEnumerator.Current => Current;

        /// <inheritdoc/>
        public bool MoveNext()
        {
            if (isSet)
            {
                return setEnumerator.MoveNext();
            }

            if (index < compactSet.count)
            {
                current = compactSet.items[index++];
                return true;
            }

            current = default!;
            return false;
        }

        /// <inheritdoc/>
        public void Reset()
        {
            if (isSet)
            {
                ((IEnumerator)setEnumerator).Reset();
            }
            else
            {
                index = 0;
                current = default!;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (isSet)
            {
                setEnumerator.Dispose();
            }
        }
    }

    private static T PoolItemIfNeeded(T item)
    {
        if (typeof(T) == typeof(string) && item is string strItem && CrdtCollectionMemoryOptions.EnableAutomaticStringPooling)
        {
            var pooled = CrdtStringPool.Shared.GetOrAdd(strItem);
            return Unsafe.As<string, T>(ref pooled);
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
        int newCapacity = CrdtCollectionMemoryOptions.CalculateNewCapacity(count, count + 1);
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

    private void DemoteToFlatArray()
    {
        int newCount = promotedSet!.Count;
        var newItems = new T[newCount];
        promotedSet.CopyTo(newItems);
        promotedSet = null;
        items = newItems;
        count = newCount;
    }

    private void RemoveAtFlatIndex(int idx)
    {
        count--;
        if (count == 0 && CrdtCollectionMemoryOptions.AutoTrimOnEmpty)
        {
            items = [];
            return;
        }

        if (idx < count)
        {
            Array.Copy(items, idx + 1, items, idx, count - idx);
        }

        items[count] = default!;
    }

    private CompactSet<T> ToLookupSet(IEnumerable<T> other)
    {
        if (other is CompactSet<T> compactOther &&
            (ReferenceEquals(comparer, compactOther.comparer) || comparer.Equals(compactOther.comparer)))
        {
            return compactOther;
        }

        return new CompactSet<T>(other, comparer);
    }

    private IEnumerator<T> IterateFlatItems()
    {
        for (int i = 0; i < count; i++)
        {
            yield return items[i];
        }
    }
}