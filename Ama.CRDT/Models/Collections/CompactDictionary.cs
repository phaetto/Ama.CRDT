namespace Ama.CRDT.Models.Collections;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Ama.CRDT.Services.Pooling;

/// <summary>
/// A memory-controlled dictionary that minimizes per-instance heap allocations using a 3-tier adaptive layout:
/// Tier 1 (Empty with zero allocation), Tier 2 (Flat contiguous array up to <see cref="CrdtCollectionMemoryOptions.LinearScanThreshold"/>),
/// and Tier 3 (Promoted hash dictionary for large collections).
/// </summary>
/// <typeparam name="TKey">The type of the keys in the dictionary.</typeparam>
/// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
public sealed class CompactDictionary<TKey, TValue> :
    IDictionary<TKey, TValue>,
    IReadOnlyDictionary<TKey, TValue>,
    IDictionary,
    IEquatable<CompactDictionary<TKey, TValue>>
    where TKey : notnull
{
    private readonly IEqualityComparer<TKey> comparer;
    private KeyValuePair<TKey, TValue>[] entries = [];
    private Dictionary<TKey, TValue>? promotedMap;
    private int count;

    /// <summary>
    /// Initializes a new, empty instance of the <see cref="CompactDictionary{TKey, TValue}"/> class.
    /// </summary>
    public CompactDictionary()
        : this(0, null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompactDictionary{TKey, TValue}"/> class with the specified capacity, comparer, and string pool.
    /// </summary>
    /// <param name="capacity">The initial number of elements that the dictionary can contain.</param>
    /// <param name="comparer">The equality comparer to use when comparing keys, or <see langword="null"/> to use the default comparer.</param>
    /// <param name="stringPool">An optional string pool used to deduplicate keys when <typeparamref name="TKey"/> is <see cref="string"/>.</param>
    public CompactDictionary(int capacity, IEqualityComparer<TKey>? comparer = null, ICrdtStringPool? stringPool = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);

        this.comparer = comparer ?? (typeof(TKey) == typeof(string)
            ? (IEqualityComparer<TKey>)(object)StringComparer.Ordinal
            : EqualityComparer<TKey>.Default);

        if (capacity > CrdtCollectionMemoryOptions.LinearScanThreshold)
        {
            promotedMap = new Dictionary<TKey, TValue>(capacity, this.comparer);
        }
        else if (capacity > 0)
        {
            entries = new KeyValuePair<TKey, TValue>[capacity];
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompactDictionary{TKey, TValue}"/> class with the specified comparer and string pool.
    /// </summary>
    /// <param name="comparer">The equality comparer to use when comparing keys, or <see langword="null"/> to use the default comparer.</param>
    /// <param name="stringPool">An optional string pool used to deduplicate keys when <typeparamref name="TKey"/> is <see cref="string"/>.</param>
    public CompactDictionary(IEqualityComparer<TKey>? comparer, ICrdtStringPool? stringPool = null)
        : this(0, comparer, stringPool)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompactDictionary{TKey, TValue}"/> class that contains elements copied from the specified dictionary.
    /// </summary>
    /// <param name="dictionary">The dictionary whose elements are copied to the new dictionary.</param>
    /// <param name="comparer">The equality comparer to use when comparing keys, or <see langword="null"/> to use the default comparer.</param>
    /// <param name="stringPool">An optional string pool used to deduplicate keys when <typeparamref name="TKey"/> is <see cref="string"/>.</param>
    public CompactDictionary(IDictionary<TKey, TValue> dictionary, IEqualityComparer<TKey>? comparer = null, ICrdtStringPool? stringPool = null)
    {
        ArgumentNullException.ThrowIfNull(dictionary);

        this.comparer = comparer
            ?? (dictionary as CompactDictionary<TKey, TValue>)?.comparer
            ?? (dictionary as Dictionary<TKey, TValue>)?.Comparer
            ?? (typeof(TKey) == typeof(string)
                ? (IEqualityComparer<TKey>)(object)StringComparer.Ordinal
                : EqualityComparer<TKey>.Default);

        if (dictionary is CompactDictionary<TKey, TValue> compactSource &&
            (ReferenceEquals(compactSource.comparer, this.comparer) || compactSource.comparer.Equals(this.comparer)))
        {
            if (compactSource.promotedMap is not null)
            {
                promotedMap = new Dictionary<TKey, TValue>(compactSource.promotedMap, this.comparer);
                return;
            }

            if (compactSource.count > 0)
            {
                if (compactSource.count <= CrdtCollectionMemoryOptions.LinearScanThreshold)
                {
                    entries = new KeyValuePair<TKey, TValue>[compactSource.count];
                    Array.Copy(compactSource.entries, entries, compactSource.count);
                    count = compactSource.count;
                }
                else
                {
                    promotedMap = new Dictionary<TKey, TValue>(compactSource.count, this.comparer);
                    for (int i = 0; i < compactSource.count; i++)
                    {
                        var kvp = compactSource.entries[i];
                        promotedMap[kvp.Key] = kvp.Value;
                    }
                }
            }

            return;
        }

        int initialCount = dictionary.Count;
        if (initialCount > 0)
        {
            if (initialCount <= CrdtCollectionMemoryOptions.LinearScanThreshold)
            {
                entries = new KeyValuePair<TKey, TValue>[initialCount];
            }
            else
            {
                promotedMap = new Dictionary<TKey, TValue>(initialCount, this.comparer);
            }
        }

        foreach (var kvp in dictionary)
        {
            this[kvp.Key] = kvp.Value;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompactDictionary{TKey, TValue}"/> class that contains elements copied from the specified collection.
    /// </summary>
    /// <param name="collection">The collection whose elements are copied to the new dictionary.</param>
    /// <param name="comparer">The equality comparer to use when comparing keys, or <see langword="null"/> to use the default comparer.</param>
    /// <param name="stringPool">An optional string pool used to deduplicate keys when <typeparamref name="TKey"/> is <see cref="string"/>.</param>
    public CompactDictionary(IEnumerable<KeyValuePair<TKey, TValue>> collection, IEqualityComparer<TKey>? comparer = null, ICrdtStringPool? stringPool = null)
        : this(
            collection is ICollection<KeyValuePair<TKey, TValue>> c ? c.Count : 0,
            comparer ?? (collection as CompactDictionary<TKey, TValue>)?.comparer ?? (collection as Dictionary<TKey, TValue>)?.Comparer,
            stringPool)
    {
        ArgumentNullException.ThrowIfNull(collection);

        foreach (var kvp in collection)
        {
            this[kvp.Key] = kvp.Value;
        }
    }

    /// <summary>
    /// Gets the <see cref="IEqualityComparer{T}"/> that is used to determine equality of keys for the dictionary.
    /// </summary>
    public IEqualityComparer<TKey> Comparer => comparer;

    /// <summary>
    /// Gets a value indicating whether this dictionary has promoted its internal storage to a hash table.
    /// </summary>
    public bool IsPromoted => promotedMap is not null;

    /// <summary>
    /// Gets the current capacity of the backing storage.
    /// </summary>
    public int Capacity => promotedMap is not null ? promotedMap.EnsureCapacity(0) : entries.Length;

    /// <inheritdoc/>
    public int Count => promotedMap is not null ? promotedMap.Count : count;

    /// <inheritdoc/>
    public bool IsReadOnly => false;

    /// <inheritdoc/>
    public ICollection<TKey> Keys => promotedMap is not null ? promotedMap.Keys : GetFlatKeys();

    /// <inheritdoc/>
    public ICollection<TValue> Values => promotedMap is not null ? promotedMap.Values : GetFlatValues();

    /// <inheritdoc/>
    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => Keys;

    /// <inheritdoc/>
    IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => Values;

    /// <inheritdoc/>
    bool IDictionary.IsFixedSize => false;

    /// <inheritdoc/>
    bool IDictionary.IsReadOnly => false;

    /// <inheritdoc/>
    ICollection IDictionary.Keys => (ICollection)Keys;

    /// <inheritdoc/>
    ICollection IDictionary.Values => (ICollection)Values;

    /// <inheritdoc/>
    bool ICollection.IsSynchronized => false;

    /// <inheritdoc/>
    object ICollection.SyncRoot => this;

    /// <inheritdoc/>
    public TValue this[TKey key]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(key);

            if (promotedMap is not null)
            {
                return promotedMap[key];
            }

            int idx = FindIndex(key);
            if (idx >= 0)
            {
                return entries[idx].Value;
            }

            throw new KeyNotFoundException($"The given key '{key}' was not present in the dictionary.");
        }
        set
        {
            ArgumentNullException.ThrowIfNull(key);
            var normalizedKey = PoolKeyIfNeeded(key);

            if (promotedMap is not null)
            {
                promotedMap[normalizedKey] = value;
                return;
            }

            int idx = FindIndex(normalizedKey);
            if (idx >= 0)
            {
                entries[idx] = new KeyValuePair<TKey, TValue>(entries[idx].Key, value);
                return;
            }

            InsertNewEntry(normalizedKey, value);
        }
    }

    /// <inheritdoc/>
    object? IDictionary.this[object key]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(key);
            if (key is TKey typedKey && TryGetValue(typedKey, out var value))
            {
                return value;
            }

            return null;
        }
        set
        {
            ArgumentNullException.ThrowIfNull(key);
            if (key is not TKey typedKey)
            {
                throw new ArgumentException($"Key must be of type {typeof(TKey)}.", nameof(key));
            }

            if (value is null && default(TValue) is not null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            this[typedKey] = (TValue)value!;
        }
    }

    /// <inheritdoc/>
    public void Add(TKey key, TValue value)
    {
        ArgumentNullException.ThrowIfNull(key);
        var normalizedKey = PoolKeyIfNeeded(key);

        if (promotedMap is not null)
        {
            promotedMap.Add(normalizedKey, value);
            return;
        }

        if (FindIndex(normalizedKey) >= 0)
        {
            throw new ArgumentException($"An item with the same key has already been added. Key: {key}", nameof(key));
        }

        InsertNewEntry(normalizedKey, value);
    }

    /// <summary>
    /// Attempts to add the specified key and value to the dictionary.
    /// </summary>
    /// <param name="key">The key of the element to add.</param>
    /// <param name="value">The value of the element to add.</param>
    /// <returns><see langword="true"/> if the key/value pair was added to the dictionary successfully; otherwise, <see langword="false"/>.</returns>
    public bool TryAdd(TKey key, TValue value)
    {
        ArgumentNullException.ThrowIfNull(key);
        var normalizedKey = PoolKeyIfNeeded(key);

        if (promotedMap is not null)
        {
            return promotedMap.TryAdd(normalizedKey, value);
        }

        if (FindIndex(normalizedKey) >= 0)
        {
            return false;
        }

        InsertNewEntry(normalizedKey, value);
        return true;
    }

    /// <inheritdoc/>
    public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

    /// <inheritdoc/>
    public bool ContainsKey(TKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (promotedMap is not null)
        {
            return promotedMap.ContainsKey(key);
        }

        return FindIndex(key) >= 0;
    }

    /// <inheritdoc/>
    public bool Contains(KeyValuePair<TKey, TValue> item)
    {
        if (!TryGetValue(item.Key, out var value))
        {
            return false;
        }

        return EqualityComparer<TValue>.Default.Equals(value, item.Value);
    }

    /// <inheritdoc/>
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (promotedMap is not null)
        {
            return promotedMap.TryGetValue(key, out value);
        }

        int idx = FindIndex(key);
        if (idx >= 0)
        {
            value = entries[idx].Value;
            return true;
        }

        value = default;
        return false;
    }

    /// <inheritdoc/>
    public bool Remove(TKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (promotedMap is not null)
        {
            bool removed = promotedMap.Remove(key);
            if (removed)
            {
                if (promotedMap.Count == 0 && CrdtCollectionMemoryOptions.AutoTrimOnEmpty)
                {
                    promotedMap = null;
                    entries = [];
                }
                else if (CrdtCollectionMemoryOptions.AutoDemoteToFlat &&
                         promotedMap.Count > 0 &&
                         promotedMap.Count <= (int)(CrdtCollectionMemoryOptions.LinearScanThreshold * CrdtCollectionMemoryOptions.AutoDemoteRatio))
                {
                    DemoteToFlatArray();
                }
            }

            return removed;
        }

        int idx = FindIndex(key);
        if (idx < 0)
        {
            return false;
        }

        count--;
        if (count == 0 && CrdtCollectionMemoryOptions.AutoTrimOnEmpty)
        {
            entries = [];
            return true;
        }

        if (idx < count)
        {
            Array.Copy(entries, idx + 1, entries, idx, count - idx);
        }

        entries[count] = default;
        return true;
    }

    /// <summary>
    /// Removes the value with the specified key from the <see cref="CompactDictionary{TKey, TValue}"/>, and copies the element to the <paramref name="value"/> parameter.
    /// </summary>
    /// <param name="key">The key of the element to remove.</param>
    /// <param name="value">The removed element.</param>
    /// <returns><see langword="true"/> if the element is successfully found and removed; otherwise, <see langword="false"/>.</returns>
    public bool Remove(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (promotedMap is not null)
        {
            bool removed = promotedMap.Remove(key, out value);
            if (removed)
            {
                if (promotedMap.Count == 0 && CrdtCollectionMemoryOptions.AutoTrimOnEmpty)
                {
                    promotedMap = null;
                    entries = [];
                }
                else if (CrdtCollectionMemoryOptions.AutoDemoteToFlat &&
                         promotedMap.Count > 0 &&
                         promotedMap.Count <= (int)(CrdtCollectionMemoryOptions.LinearScanThreshold * CrdtCollectionMemoryOptions.AutoDemoteRatio))
                {
                    DemoteToFlatArray();
                }
            }

            return removed;
        }

        int idx = FindIndex(key);
        if (idx < 0)
        {
            value = default;
            return false;
        }

        value = entries[idx].Value;
        count--;
        if (count == 0 && CrdtCollectionMemoryOptions.AutoTrimOnEmpty)
        {
            entries = [];
            return true;
        }

        if (idx < count)
        {
            Array.Copy(entries, idx + 1, entries, idx, count - idx);
        }

        entries[count] = default;
        return true;
    }

    /// <inheritdoc/>
    public bool Remove(KeyValuePair<TKey, TValue> item)
    {
        if (!Contains(item))
        {
            return false;
        }

        return Remove(item.Key);
    }

    /// <inheritdoc/>
    public void Clear()
    {
        if (promotedMap is not null)
        {
            if (CrdtCollectionMemoryOptions.AutoTrimOnEmpty)
            {
                promotedMap = null;
                entries = [];
            }
            else
            {
                promotedMap.Clear();
            }

            return;
        }

        if (count > 0)
        {
            if (CrdtCollectionMemoryOptions.AutoTrimOnEmpty)
            {
                entries = [];
            }
            else
            {
                Array.Clear(entries, 0, count);
            }

            count = 0;
        }
        else if (CrdtCollectionMemoryOptions.AutoTrimOnEmpty && entries.Length > 0)
        {
            entries = [];
        }
    }

    /// <summary>
    /// Sets the capacity of this dictionary to what it would be if it had been originally initialized with all its entries.
    /// Also demotes from Tier 3 to Tier 2 if the count is within <see cref="CrdtCollectionMemoryOptions.LinearScanThreshold"/>.
    /// </summary>
    public void TrimExcess()
    {
        if (promotedMap is not null)
        {
            if (promotedMap.Count == 0)
            {
                promotedMap = null;
                entries = [];
                count = 0;
            }
            else if (promotedMap.Count <= CrdtCollectionMemoryOptions.LinearScanThreshold)
            {
                DemoteToFlatArray();
            }
            else
            {
                promotedMap.TrimExcess();
            }

            return;
        }

        if (count == 0)
        {
            entries = [];
        }
        else if (entries.Length > count)
        {
            Array.Resize(ref entries, count);
        }
    }

    /// <inheritdoc/>
    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);
        if (arrayIndex < 0 || arrayIndex > array.Length || (array.Length - arrayIndex) < Count)
        {
            throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        }

        if (promotedMap is not null)
        {
            ((ICollection<KeyValuePair<TKey, TValue>>)promotedMap).CopyTo(array, arrayIndex);
            return;
        }

        if (count > 0)
        {
            Array.Copy(entries, 0, array, arrayIndex, count);
        }
    }

    /// <inheritdoc/>
    public bool Equals(CompactDictionary<TKey, TValue>? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (Count != other.Count)
        {
            return false;
        }

        var valueComparer = EqualityComparer<TValue>.Default;
        foreach (var kvp in this)
        {
            if (!other.TryGetValue(kvp.Key, out var otherValue) || !valueComparer.Equals(kvp.Value, otherValue))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Creates a shallow copy of the <see cref="CompactDictionary{TKey, TValue}"/> with an exact-fit backing capacity.
    /// </summary>
    /// <returns>A new <see cref="CompactDictionary{TKey, TValue}"/> containing the same key-value pairs.</returns>
    public CompactDictionary<TKey, TValue> Clone() => new(this, comparer);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as CompactDictionary<TKey, TValue>);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        int hash = 0;
        var valueComparer = EqualityComparer<TValue>.Default;
        foreach (var kvp in this)
        {
            int entryHash = HashCode.Combine(
                comparer.GetHashCode(kvp.Key),
                kvp.Value is not null ? valueComparer.GetHashCode(kvp.Value) : 0);
            hash ^= entryHash;
        }

        return hash;
    }

    /// <summary>
    /// Returns a value-type enumerator that iterates through the <see cref="CompactDictionary{TKey, TValue}"/> without heap allocations.
    /// </summary>
    /// <returns>A <see cref="Enumerator"/> for the <see cref="CompactDictionary{TKey, TValue}"/>.</returns>
    public Enumerator GetEnumerator() => new(this);

    /// <inheritdoc/>
    IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
    {
        if (promotedMap is not null)
        {
            return promotedMap.GetEnumerator();
        }

        return IterateFlatEntries();
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<KeyValuePair<TKey, TValue>>)this).GetEnumerator();

    /// <inheritdoc/>
    void IDictionary.Add(object key, object? value)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key is not TKey typedKey)
        {
            throw new ArgumentException($"Key must be of type {typeof(TKey)}.", nameof(key));
        }

        if (value is null && default(TValue) is not null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        Add(typedKey, (TValue)value!);
    }

    /// <inheritdoc/>
    bool IDictionary.Contains(object key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return key is TKey typedKey && ContainsKey(typedKey);
    }

    /// <inheritdoc/>
    IDictionaryEnumerator IDictionary.GetEnumerator()
    {
        if (promotedMap is not null)
        {
            return ((IDictionary)promotedMap).GetEnumerator();
        }

        return new FlatDictionaryEnumerator(entries, count);
    }

    /// <inheritdoc/>
    void IDictionary.Remove(object key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key is TKey typedKey)
        {
            Remove(typedKey);
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

        if (promotedMap is not null)
        {
            ((ICollection)promotedMap).CopyTo(array, index);
            return;
        }

        if (array is KeyValuePair<TKey, TValue>[] typedArray)
        {
            CopyTo(typedArray, index);
            return;
        }

        if (array is DictionaryEntry[] entryArray)
        {
            for (int i = 0; i < count; i++)
            {
                entryArray[index + i] = new DictionaryEntry(entries[i].Key, entries[i].Value);
            }

            return;
        }

        if (array is object[] objects)
        {
            for (int i = 0; i < count; i++)
            {
                objects[index + i] = entries[i];
            }

            return;
        }

        throw new ArgumentException("Target array type is not compatible with the type of items in the collection.", nameof(array));
    }

    /// <summary>
    /// Enumerates the elements of a <see cref="CompactDictionary{TKey, TValue}"/> without allocating on the heap.
    /// </summary>
    public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>
    {
        private readonly CompactDictionary<TKey, TValue> dictionary;
        private Dictionary<TKey, TValue>.Enumerator mapEnumerator;
        private int index;
        private KeyValuePair<TKey, TValue> current;
        private readonly bool isMap;

        internal Enumerator(CompactDictionary<TKey, TValue> dictionary)
        {
            this.dictionary = dictionary;
            if (dictionary.promotedMap is not null)
            {
                isMap = true;
                mapEnumerator = dictionary.promotedMap.GetEnumerator();
                index = 0;
                current = default;
            }
            else
            {
                isMap = false;
                mapEnumerator = default;
                index = 0;
                current = default;
            }
        }

        /// <inheritdoc/>
        public readonly KeyValuePair<TKey, TValue> Current => isMap ? mapEnumerator.Current : current;

        /// <inheritdoc/>
        readonly object IEnumerator.Current => Current;

        /// <inheritdoc/>
        public bool MoveNext()
        {
            if (isMap)
            {
                return mapEnumerator.MoveNext();
            }

            if (index < dictionary.count)
            {
                current = dictionary.entries[index++];
                return true;
            }

            current = default;
            return false;
        }

        /// <inheritdoc/>
        public void Reset()
        {
            if (isMap)
            {
                ((IEnumerator)mapEnumerator).Reset();
            }
            else
            {
                index = 0;
                current = default;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (isMap)
            {
                mapEnumerator.Dispose();
            }
        }
    }

    private static TKey PoolKeyIfNeeded(TKey key)
    {
        if (typeof(TKey) == typeof(string) && key is string strKey && CrdtCollectionMemoryOptions.EnableAutomaticStringPooling)
        {
            var pooled = CrdtStringPool.Shared.GetOrAdd(strKey);
            return Unsafe.As<string, TKey>(ref pooled);
        }

        return key;
    }

    private int FindIndex(TKey key)
    {
        if (count == 0)
        {
            return -1;
        }

        if (typeof(TKey) == typeof(string))
        {
            var targetStr = (string)(object)key;
            for (int i = 0; i < count; i++)
            {
                var currentKey = entries[i].Key;
                var currentStr = (string)(object)currentKey;
                if (ReferenceEquals(currentStr, targetStr) || comparer.Equals(currentKey, key))
                {
                    return i;
                }
            }

            return -1;
        }

        for (int i = 0; i < count; i++)
        {
            if (comparer.Equals(entries[i].Key, key))
            {
                return i;
            }
        }

        return -1;
    }

    private void InsertNewEntry(TKey normalizedKey, TValue value)
    {
        int threshold = CrdtCollectionMemoryOptions.LinearScanThreshold;
        if (count >= threshold)
        {
            PromoteAndAdd(normalizedKey, value);
            return;
        }

        if (count == entries.Length)
        {
            int nextCapacity = Math.Max(
                entries.Length == 0 ? CrdtCollectionMemoryOptions.InitialFlatCapacity : (int)(entries.Length * CrdtCollectionMemoryOptions.GrowthFactor),
                entries.Length + CrdtCollectionMemoryOptions.MinimumGrowthStep);

            if (nextCapacity > CrdtCollectionMemoryOptions.LinearScanThreshold)
            {
                nextCapacity = CrdtCollectionMemoryOptions.LinearScanThreshold;
            }

            int newCapacity = Math.Min(threshold, CrdtCollectionMemoryOptions.CalculateNewCapacity(entries.Length, count + 1));
            Array.Resize(ref entries, newCapacity);
        }

        entries[count++] = new KeyValuePair<TKey, TValue>(normalizedKey, value);
    }

    private void PromoteToMap(int targetCapacity)
    {
        var map = new Dictionary<TKey, TValue>(targetCapacity, comparer);
        for (int i = 0; i < count; i++)
        {
            map[entries[i].Key] = entries[i].Value;
        }

        promotedMap = map;
        entries = [];
    }

    private void PromoteAndAdd(TKey newKey, TValue newValue)
    {
        int newCapacity = CrdtCollectionMemoryOptions.CalculateNewCapacity(count, count + 1);
        var map = new Dictionary<TKey, TValue>(newCapacity, comparer);

        for (int i = 0; i < count; i++)
        {
            map[entries[i].Key] = entries[i].Value;
        }

        map[newKey] = newValue;
        promotedMap = map;
        entries = [];
        count = 0;
    }

    private void DemoteToFlatArray()
    {
        int newCount = promotedMap!.Count;
        var newEntries = new KeyValuePair<TKey, TValue>[newCount];
        int i = 0;
        foreach (var kvp in promotedMap)
        {
            newEntries[i++] = kvp;
        }

        promotedMap = null;
        entries = newEntries;
        count = newCount;
    }

    private ICollection<TKey> GetFlatKeys()
    {
        if (count == 0)
        {
            return [];
        }

        var keys = new TKey[count];
        for (int i = 0; i < count; i++)
        {
            keys[i] = entries[i].Key;
        }

        return keys;
    }

    private ICollection<TValue> GetFlatValues()
    {
        if (count == 0)
        {
            return [];
        }

        var values = new TValue[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = entries[i].Value;
        }

        return values;
    }

    private IEnumerator<KeyValuePair<TKey, TValue>> IterateFlatEntries()
    {
        for (int i = 0; i < count; i++)
        {
            yield return entries[i];
        }
    }

    private sealed class FlatDictionaryEnumerator(KeyValuePair<TKey, TValue>[] entries, int count) : IDictionaryEnumerator
    {
        private int index = -1;

        public DictionaryEntry Entry
        {
            get
            {
                if (index < 0 || index >= count)
                {
                    throw new InvalidOperationException();
                }

                return new DictionaryEntry(entries[index].Key, entries[index].Value);
            }
        }

        public object Key => Entry.Key;

        public object? Value => Entry.Value;

        public object Current => Entry;

        public bool MoveNext()
        {
            index++;
            return index < count;
        }

        public void Reset() => index = -1;
    }
}