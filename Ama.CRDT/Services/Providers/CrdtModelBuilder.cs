namespace Ama.CRDT.Services.Providers;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

/// <summary>
/// A fluent builder used to configure CRDT strategies and decorators for C# types without using attributes.
/// </summary>
public sealed class CrdtModelBuilder
{
    private readonly Dictionary<PropertyInfo, Type> strategies = new();
    private readonly Dictionary<PropertyInfo, List<Type>> decorators = new();

    /// <summary>
    /// Starts configuring the CRDT model for a specific entity type.
    /// </summary>
    /// <typeparam name="T">The type of the entity to configure.</typeparam>
    /// <returns>A builder to configure properties of the entity.</returns>
    public CrdtEntityBuilder<T> Entity<T>() where T : class
    {
        return new CrdtEntityBuilder<T>(this);
    }

    internal void AddStrategy(PropertyInfo property, Type strategyType)
    {
        this.strategies[property] = strategyType;
    }

    internal void AddDecorator(PropertyInfo property, Type decoratorType)
    {
        if (!this.decorators.TryGetValue(property, out var list))
        {
            list = new List<Type>();
            this.decorators[property] = list;
        }
        
        list.Add(decoratorType);
    }

    internal ICrdtModelRegistry Build()
    {
        var frozenDecorators = this.decorators.ToDictionary(k => k.Key, v => (IReadOnlyList<Type>)v.Value);
        return new CrdtModelRegistry(this.strategies, frozenDecorators);
    }
}