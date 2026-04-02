namespace Ama.CRDT.Models.Aot;

using System;
using System.Collections.Generic;
using Ama.CRDT.Attributes;

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
    /// Gets the explicitly configured base CRDT strategy attribute for this property, if one was provided.
    /// </summary>
    public CrdtStrategyAttribute? StrategyAttribute { get; }

    /// <summary>
    /// Gets the list of explicitly configured decorator CRDT strategy attributes for this property, if any were provided.
    /// </summary>
    public IReadOnlyList<CrdtStrategyDecoratorAttribute> DecoratorAttributes { get; }

    /// <summary>
    /// Gets the explicitly configured base CRDT strategy type for this property, if one was provided via attributes.
    /// </summary>
    public Type? StrategyType { get; }

    /// <summary>
    /// Gets the list of explicitly configured decorator CRDT strategy types for this property, if any were provided via attributes.
    /// </summary>
    public IReadOnlyList<Type> DecoratorTypes { get; }

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
        Action<object, object?>? setter,
        CrdtStrategyAttribute? strategyAttribute,
        IReadOnlyList<CrdtStrategyDecoratorAttribute> decoratorAttributes)
    {
        Name = name;
        JsonName = jsonName;
        PropertyType = propertyType;
        CanRead = canRead;
        CanWrite = canWrite;
        Getter = getter;
        Setter = setter;
        StrategyAttribute = strategyAttribute;
        DecoratorAttributes = decoratorAttributes;

        StrategyType = strategyAttribute?.StrategyType;
        var decoratorTypes = new Type[decoratorAttributes.Count];
        for (int i = 0; i < decoratorAttributes.Count; i++)
        {
            decoratorTypes[i] = decoratorAttributes[i].StrategyType;
        }
        DecoratorTypes = decoratorTypes;
    }
}