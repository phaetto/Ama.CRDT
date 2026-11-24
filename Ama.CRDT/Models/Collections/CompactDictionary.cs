namespace Ama.CRDT.Models.Collections;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Ama.CRDT.Services.Pooling;

/// <summary>
/// A memory-controlled dictionary that minimizes per-instance heap allocations using a 3-tier adaptive layout:
/// Tier 1 (Empty with zero allocation), Tier 2 (Flat contiguous array up to <see cref="CrdtMemoryOptions.LinearScanThreshold"/>),
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
    private readonly ICrdtStringPool? stringPool;
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
    /// Initializes a new instance of the <see cref="CompactDictionary{TKey, TValue}"/> class with the specified capacity.
    /// </summary>
    /// <param name="capacity">The initial number of elements that the dictionary can contain.</param>
    /// <param name="comparer">The equality comparer to use when comparing keys, or <c>null</c> to use the default comparer.</param>
    /// <param name="stringPool">An optional string pool used to deduplicate keys when <typeparamref name="TKey"/> is <see cref="string"/>.</param>
    public CompactDictionary(int capacity, IEqualityComparer<TKey>? comparer = null, ICrdtStringPool? stringPool = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);

        this.comparer = comparer ?? (typeof(TKey) == typeof(string)
            ? (IEqualityComparer<TKey>)(object)StringComparer.Ordinal
            : EqualityComparer<TKey>.Default);
        this.stringPool = stringPool ?? (typeof(TKey) == typeof(string) ? CrdtStringPool.Shared : null);

        if (capacity > CrdtMemoryOptions.LinearScanThreshold)
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
    /// <param name="comparer">The equality comparer to use when comparing keys.</param>
    /// <param name="stringPool">An optional string pool used to deduplicate keys when <typeparamref name="TKey"/> is <see cref="string"/>.</param>
    public CompactDictionary(IEqualityComparer<TKey>? comparer, ICrdtStringPool? stringPool = null)
        : this(0, comparer, stringPool)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompactDictionary{TKey, TValue}"/> class that contains elements copied from the specified dictionary.
    /// </summary>
    /// <param name="dictionary">The dictionary whose elements are copied to the new dictionary.</param>
    /// <param name="comparer">The equality comparer to use when comparing keys.</param>
    /// <param name="stringPool">An optional string pool used to deduplicate keys when <typeparamref name="TKey"/> is <see cref="string"/>.</param>
    public CompactDictionary(IDictionary<TKey, TValue> dictionary, IEqualityComparer<TKey>? comparer = null, ICrdtStringPool? stringPool = null)
        : this(
            dictionary?.Count ?? throw new ArgumentNullException(nameof(dictionary)),
            comparer ?? (dictionary as CompactDictionary<TKey, TValue>)?.comparer ?? (dictionary as Dictionary<TKey, TValue>)?.Comparer,
            stringPool ?? (dictionary as CompactDictionary<TKey, TValue>)?.stringPool)
    {
        foreach (var kvp in dictionary)
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
            if (TryGetValue(key, out var value))
            {
                return value;
            }

            throw new KeyNotFoundException($"The given key '{key}' was not present in the dictionary.");
        }
        set
        {
            ArgumentNullException.ThrowIfNull(key);
            var normalizedKey = NormalizeKey(key);

            if (promotedMap is not null)
            {
                promotedMap[normalizedKey] = value;
                return;
            }

            int index = FindIndex(normalizedKey);
            if (index >= 0)
            {
                entries[index] = new KeyValuePair<TKey, TValue>(entries[index].Key, value);
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
            this[(TKey)key] = (TValue)value!;
        }
    }

    /// <inheritdoc/>
    public void Add(TKey key, TValue value)
    {
        ArgumentNullException.ThrowIfNull(key);
        var normalizedKey = NormalizeKey(key);

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

    /// <inheritdoc/>
    public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

    /// <inheritdoc/>
    public void Clear()
    {
        if (promotedMap is not null)
        {
            if (CrdtMemoryOptions.AutoTrimOnEmpty)
            {
                promotedMap = null;
                entries = [];
                count = 0;
            }
            else
            {
                promotedMap.Clear();
            }

            return;
        }

        if (CrdtMemoryOptions.AutoTrimOnEmpty)
        {
            entries = [];
        }
        else if (count > 0)
        {
            Array.Clear(entries, 0, count);
        }

        count = 0;
    }

    /// <inheritdoc/>
    public bool Contains(KeyValuePair<TKey, TValue> item)
    {
        return TryGetValue(item.Key, out var value) && EqualityComparer<TValue>.Default.Equals(value, item.Value);
    }

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
    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);
        ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);

        if (array.Length - arrayIndex < Count)
        {
            throw new ArgumentException("The number of elements in the source dictionary is greater than the available space from arrayIndex to the end of the destination array.");
        }

        if (promotedMap is not null)
        {
            ((ICollection<KeyValuePair<TKey, TValue>>)promotedMap).CopyTo(array, arrayIndex);
            return;
        }

        Array.Copy(entries, 0, array, arrayIndex, count);
    }

    /// <inheritdoc/>
    public bool Remove(TKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (promotedMap is not null)
        {
            bool removed = promotedMap.Remove(key);
            if (removed && promotedMap.Count == 0 && CrdtMemoryOptions.AutoTrimOnEmpty)
            {
                promotedMap = null;
                entries = [];
                count = 0;
            }

            return removed;
        }

        int index = FindIndex(key);
        if (index < 0)
        {
            return false;
        }

        count--;
        if (count == 0 && CrdtMemoryOptions.AutoTrimOnEmpty)
        {
            entries = [];
            return true;
        }

        if (index < count)
        {
            Array.Copy(entries, index + 1, entries, index, count - index);
        }

        entries[count] = default;
        return true;
    }

    /// <inheritdoc/>
    public bool Remove(KeyValuePair<TKey, TValue> item)
    {
        if (Contains(item))
        {
            return Remove(item.Key);
        }

        return false;
    }

    /// <inheritdoc/>
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (promotedMap is not null)
        {
            return promotedMap.TryGetValue(key, out value);
        }

        int index = FindIndex(key);
        if (index >= 0)
        {
            value = entries[index].Value;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Sets the capacity of this dictionary to what it would be if it had been originally initialized with all its entries.
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
            else if (promotedMap.Count <= CrdtMemoryOptions.LinearScanThreshold)
            {
                int newCount = promotedMap.Count;
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

    /// <summary>
    /// Creates a exact-sized shallow copy of this <see cref="CompactDictionary{TKey, TValue}"/>.
    /// </summary>
    /// <returns>A new <see cref="CompactDictionary{TKey, TValue}"/> with the exact capacity required for the current elements.</returns>
    public CompactDictionary<TKey, TValue> Clone()
    {
        var clone = new CompactDictionary<TKey, TValue>(Count, comparer, stringPool);
        if (promotedMap is not null)
        {
            foreach (var kvp in promotedMap)
            {
                clone.promotedMap![kvp.Key] = kvp.Value;
            }
        }
        else if (count > 0)
        {
            Array.Copy(entries, 0, clone.entries, 0, count);
            clone.count = count;
        }

        return clone;
    }

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        if (promotedMap is not null)
        {
            return promotedMap.GetEnumerator();
        }

        return IterateFlatEntries();
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    void IDictionary.Add(object key, object? value)
    {
        ArgumentNullException.ThrowIfNull(key);
        Add((TKey)key, (TValue)value!);
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
        ArgumentOutOfRangeException.ThrowIfNegative(index);

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

        for (int i = 0; i < count; i++)
        {
            array.SetValue(new DictionaryEntry(entries[i].Key, entries[i].Value), index + i);
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

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as CompactDictionary<TKey, TValue>);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        int hash = 0;
        var valueComparer = EqualityComparer<TValue>.Default;
        foreach (var kvp in this)
        {
            int keyHash = comparer.GetHashCode(kvp.Key);
            int valHash = kvp.Value is null ? 0 : valueComparer.GetHashCode(kvp.Value);
            hash ^= HashCode.Combine(keyHash, valHash);
        }

        return hash;
    }

    private TKey NormalizeKey(TKey key)
    {
        if (typeof(TKey) == typeof(string) && stringPool is not null)
        {
            var str = (string)(object)key;
            return (TKey)(object)stringPool.GetOrAdd(str);
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
        int threshold = CrdtMemoryOptions.LinearScanThreshold;
        if (count >= threshold)
        {
            PromoteAndAdd(normalizedKey, value);
            return;
        }

        if (count == entries.Length)
        {
            int newCapacity = Math.Min(threshold, CrdtMemoryOptions.CalculateNewCapacity(entries.Length, count + 1));
            Array.Resize(ref entries, newCapacity);
        }

        entries[count++] = new KeyValuePair<TKey, TValue>(normalizedKey, value);
    }

    private void PromoteAndAdd(TKey newKey, TValue newValue)
    {
        int newCapacity = CrdtMemoryOptions.CalculateNewCapacity(count, count + 1);
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