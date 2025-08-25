namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Models;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

/// <summary>
/// Defines the contract for a service that resolves the appropriate CRDT strategy for a given property or operation.
/// </summary>
public interface ICrdtStrategyManager
{
    /// <summary>
    /// Gets the appropriate <see cref="ICrdtStrategy"/> for a property based on its attributes or type.
    /// </summary>
    /// <param name="propertyInfo">The <see cref="PropertyInfo"/> of the property to analyze.</param>
    /// <returns>The resolved <see cref="ICrdtStrategy"/>. Returns a default strategy if no specific attribute is found.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="propertyInfo"/> is null.</exception>
    ICrdtStrategy GetStrategy([DisallowNull] PropertyInfo propertyInfo);

    /// <summary>
    /// Gets the appropriate <see cref="ICrdtStrategy"/> for applying a given operation to a document.
    /// </summary>
    /// <param name="operation">The <see cref="CrdtOperation"/> to be applied.</param>
    /// <param name="root">The root document object, used for path resolution.</param>
    /// <returns>The resolved <see cref="ICrdtStrategy"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="root"/> is null.</exception>
    ICrdtStrategy GetStrategy(CrdtOperation operation, [DisallowNull] object root);
}