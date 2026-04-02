namespace Ama.CRDT.Services.Providers;

using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Services.Strategies;
using System;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Defines the contract for a service that resolves the appropriate CRDT strategy for a given property or operation.
/// </summary>
public interface ICrdtStrategyProvider
{
    /// <summary>
    /// Gets the appropriate <see cref="ICrdtStrategy"/> for a property based on its attributes or type, including decorators.
    /// </summary>
    /// <param name="declaringType">The type that declares the property.</param>
    /// <param name="propertyInfo">The <see cref="CrdtPropertyInfo"/> of the property to analyze.</param>
    /// <returns>The resolved <see cref="ICrdtStrategy"/>. Returns a default strategy if no specific attribute is found.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="declaringType"/> or <paramref name="propertyInfo"/> is null.</exception>
    ICrdtStrategy GetStrategy([DisallowNull] Type declaringType, [DisallowNull] CrdtPropertyInfo propertyInfo);

    /// <summary>
    /// Gets the inner <see cref="ICrdtStrategy"/> in the decorator chain for a property. 
    /// If no further decorators exist, returns the base strategy.
    /// </summary>
    /// <param name="declaringType">The type that declares the property.</param>
    /// <param name="propertyInfo">The <see cref="CrdtPropertyInfo"/> of the property to analyze.</param>
    /// <param name="currentDecoratorType">The <see cref="Type"/> of the current decorator calling this method.</param>
    /// <returns>The resolved next <see cref="ICrdtStrategy"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="declaringType"/>, <paramref name="propertyInfo"/> or <paramref name="currentDecoratorType"/> is null.</exception>
    ICrdtStrategy GetInnerStrategy([DisallowNull] Type declaringType, [DisallowNull] CrdtPropertyInfo propertyInfo, [DisallowNull] Type currentDecoratorType);

    /// <summary>
    /// Gets the base <see cref="ICrdtStrategy"/> for a property, bypassing any decorators.
    /// </summary>
    /// <param name="declaringType">The type that declares the property.</param>
    /// <param name="propertyInfo">The <see cref="CrdtPropertyInfo"/> of the property to analyze.</param>
    /// <returns>The resolved base <see cref="ICrdtStrategy"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="declaringType"/> or <paramref name="propertyInfo"/> is null.</exception>
    ICrdtStrategy GetBaseStrategy([DisallowNull] Type declaringType, [DisallowNull] CrdtPropertyInfo propertyInfo);

    /// <summary>
    /// Gets the appropriate <see cref="ICrdtStrategy"/> for applying a given operation to a document.
    /// </summary>
    /// <param name="operation">The <see cref="CrdtOperation"/> to be applied.</param>
    /// <param name="root">The root document object, used for path resolution.</param>
    /// <returns>The resolved <see cref="ICrdtStrategy"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="root"/> is null.</exception>
    ICrdtStrategy GetStrategy(CrdtOperation operation, [DisallowNull] object root);
}