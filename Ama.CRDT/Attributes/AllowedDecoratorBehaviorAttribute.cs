namespace Ama.CRDT.Attributes;

using System;
using System.Collections.Generic;
using Ama.CRDT.Models;

/// <summary>
/// Specifies the allowed <see cref="DecoratorBehavior"/> phases that a specific decorator class is designed to support.
/// This attribute is intended to be used in conjunction with Roslyn Analyzers to statically verify that 
/// the constructor parameters map correctly to the intended behavior flow.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class AllowedDecoratorBehaviorAttribute : Attribute
{
    /// <summary>
    /// Gets the list of behaviors permitted for this decorator.
    /// </summary>
    public IEnumerable<DecoratorBehavior> AllowedBehaviors { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AllowedDecoratorBehaviorAttribute"/> class.
    /// </summary>
    /// <param name="allowedBehaviors">The behaviors that are valid for this decorator.</param>
    public AllowedDecoratorBehaviorAttribute(params DecoratorBehavior[] allowedBehaviors)
    {
        AllowedBehaviors = allowedBehaviors ?? Array.Empty<DecoratorBehavior>();
    }
}