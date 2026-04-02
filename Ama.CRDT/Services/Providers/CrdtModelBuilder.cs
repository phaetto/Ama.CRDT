namespace Ama.CRDT.Services.Providers;

using Ama.CRDT.Models;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A fluent builder used to configure CRDT strategies and decorators for C# types without using attributes.
/// </summary>
public sealed class CrdtModelBuilder
{
    private readonly Dictionary<CrdtPropertyKey, Type> strategies = new();
    private readonly Dictionary<CrdtPropertyKey, List<Type>> decorators = new();

    /// <summary>
    /// Starts configuring the CRDT model for a specific entity type.
    /// </summary>
    /// <typeparam name="T">The type of the entity to configure.</typeparam>
    /// <returns>A builder to configure properties of the entity.</returns>
    public CrdtEntityBuilder<T> Entity<T>() where T : class
    {
        return new CrdtEntityBuilder<T>(this);
    }

    internal void AddStrategy(CrdtPropertyKey propertyKey, Type strategyType)
    {
        this.strategies[propertyKey] = strategyType;
    }

    internal void AddDecorator(CrdtPropertyKey propertyKey, Type decoratorType)
    {
        if (!this.decorators.TryGetValue(propertyKey, out var list))
        {
            list = new List<Type>();
            this.decorators[propertyKey] = list;
        }
        
        list.Add(decoratorType);
    }

    internal ICrdtModelRegistry Build()
    {
        var frozenDecorators = this.decorators.ToDictionary(k => k.Key, v => (IReadOnlyList<Type>)v.Value);
        return new CrdtModelRegistry(this.strategies, frozenDecorators);
    }
}