namespace Modern.CRDT.Services.Strategies;

using System.Reflection;

/// <summary>
/// Defines the contract for a service that resolves the appropriate CRDT strategy for a given property.
/// </summary>
public interface ICrdtStrategyManager
{
    /// <summary>
    /// Gets the appropriate <see cref="ICrdtStrategy"/> for a property based on its attributes.
    /// </summary>
    /// <param name="propertyInfo">The <see cref="PropertyInfo"/> of the property to analyze.</param>
    /// <returns>The resolved <see cref="ICrdtStrategy"/>. Returns the default strategy (e.g., LwwStrategy) if no specific strategy attribute is found.</returns>
    ICrdtStrategy GetStrategy(PropertyInfo propertyInfo);
}