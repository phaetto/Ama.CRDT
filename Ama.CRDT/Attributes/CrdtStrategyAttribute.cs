namespace Ama.CRDT.Attributes;

using Ama.CRDT.Services.Strategies;
using System;

/// <summary>
/// A base attribute for specifying a CRDT strategy for a property.
/// This attribute must be inherited by concrete strategy attributes.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public abstract class CrdtStrategyAttribute(Type strategyType) : Attribute
{
    /// <summary>
    /// Gets the type of the <see cref="ICrdtStrategy"/> to be used for the property.
    /// </summary>
    public Type StrategyType { get; } = strategyType ?? throw new ArgumentNullException(nameof(strategyType));
}