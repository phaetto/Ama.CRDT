namespace Ama.CRDT.Models.Aot;

using System;
using System.Collections.Generic;

/// <summary>
/// Contains AOT-compatible metadata, factory methods, and property accessors for a specific type.
/// </summary>
public sealed class CrdtTypeInfo
{
    /// <summary>
    /// Gets the runtime type this metadata describes.
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// Gets a delegate to create a new instance of the type without reflection.
    /// </summary>
    public Func<object>? CreateInstance { get; }

    /// <summary>
    /// Gets the dictionary of strongly-typed property accessors.
    /// </summary>
    public IReadOnlyDictionary<string, CrdtPropertyInfo> Properties { get; }

    /// <summary>
    /// Gets a value indicating whether the type is a collection.
    /// </summary>
    public bool IsCollection { get; }

    /// <summary>
    /// Gets the element type if this type is a collection.
    /// </summary>
    public Type? CollectionElementType { get; }

    /// <summary>
    /// Gets the delegate to add an item to the collection.
    /// </summary>
    public Action<object, object?>? CollectionAdd { get; }

    /// <summary>
    /// Gets the delegate to remove an item from the collection.
    /// </summary>
    public Action<object, object?>? CollectionRemove { get; }

    /// <summary>
    /// Gets the delegate to clear the collection.
    /// </summary>
    public Action<object>? CollectionClear { get; }

    /// <summary>
    /// Gets a value indicating whether the type is a dictionary.
    /// </summary>
    public bool IsDictionary { get; }

    /// <summary>
    /// Gets the key type if this type is a dictionary.
    /// </summary>
    public Type? DictionaryKeyType { get; }

    /// <summary>
    /// Gets the value type if this type is a dictionary.
    /// </summary>
    public Type? DictionaryValueType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CrdtTypeInfo"/> class.
    /// </summary>
    public CrdtTypeInfo(
        Type type,
        Func<object>? createInstance,
        IReadOnlyDictionary<string, CrdtPropertyInfo> properties,
        bool isCollection,
        Type? collectionElementType,
        Action<object, object?>? collectionAdd,
        Action<object, object?>? collectionRemove,
        Action<object>? collectionClear,
        bool isDictionary,
        Type? dictionaryKeyType,
        Type? dictionaryValueType)
    {
        Type = type;
        CreateInstance = createInstance;
        Properties = properties;
        IsCollection = isCollection;
        CollectionElementType = collectionElementType;
        CollectionAdd = collectionAdd;
        CollectionRemove = collectionRemove;
        CollectionClear = collectionClear;
        IsDictionary = isDictionary;
        DictionaryKeyType = dictionaryKeyType;
        DictionaryValueType = dictionaryValueType;
    }
}