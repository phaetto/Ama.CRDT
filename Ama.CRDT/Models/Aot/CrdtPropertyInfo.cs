namespace Ama.CRDT.Models.Aot;

using System;

/// <summary>
/// Contains AOT-compatible metadata and fast accessors for a single property, eliminating the need for <see cref="System.Reflection.PropertyInfo"/>.
/// </summary>
public sealed class CrdtPropertyInfo
{
    /// <summary>
    /// Gets the exact C# name of the property.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the camelCase JSON name of the property.
    /// </summary>
    public string JsonName { get; }

    /// <summary>
    /// Gets the type of the property.
    /// </summary>
    public Type PropertyType { get; }

    /// <summary>
    /// Gets a value indicating whether the property can be read.
    /// </summary>
    public bool CanRead { get; }

    /// <summary>
    /// Gets a value indicating whether the property can be written to.
    /// </summary>
    public bool CanWrite { get; }

    /// <summary>
    /// Gets the strongly-typed, allocation-free getter delegate.
    /// </summary>
    public Func<object, object?>? Getter { get; }

    /// <summary>
    /// Gets the strongly-typed, allocation-free setter delegate.
    /// </summary>
    public Action<object, object?>? Setter { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CrdtPropertyInfo"/> class.
    /// </summary>
    public CrdtPropertyInfo(
        string name,
        string jsonName,
        Type propertyType,
        bool canRead,
        bool canWrite,
        Func<object, object?>? getter,
        Action<object, object?>? setter)
    {
        Name = name;
        JsonName = jsonName;
        PropertyType = propertyType;
        CanRead = canRead;
        CanWrite = canWrite;
        Getter = getter;
        Setter = setter;
    }
}