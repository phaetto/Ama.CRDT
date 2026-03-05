namespace Ama.CRDT.Attributes;

using System;

/// <summary>
/// A base attribute for specifying a CRDT decorator strategy for a property.
/// Decorators wrap the base strategy to provide additional functionality (e.g., Epoch bounding),
/// allowing you to stack multiple attributes on a single property.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = true)]
public abstract class CrdtStrategyDecoratorAttribute(Type strategyType) : Attribute
{
    /// <summary>
    /// Gets the type of the <see cref="Services.Strategies.ICrdtStrategy"/> to be used as a decorator.
    /// </summary>
    public Type StrategyType { get; } = strategyType ?? throw new ArgumentNullException(nameof(strategyType));
}