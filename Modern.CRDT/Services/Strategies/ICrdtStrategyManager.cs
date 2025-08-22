namespace Modern.CRDT.Services.Strategies;

using System.Reflection;

/// <summary>
/// Defines the contract for a service that resolves the appropriate CRDT strategy for a given property.
/// </summary>
public interface ICrdtStrategyManager
{
    /// <summary>
    /// Gets the <see cref="ICrdtStrategy"/> for a specified property.
    /// It resolves the strategy based on the <see cref="Attributes.CrdtStrategyAttribute"/> decorating the property.
    /// If no attribute is found, it returns the default strategy (LWW).
    /// </summary>
    /// <param name="propertyInfo">The property for which to resolve the strategy.</param>
    /// <returns>An instance of the resolved <see cref="ICrdtStrategy"/>.</returns>
    ICrdtStrategy GetStrategy(PropertyInfo propertyInfo);
}