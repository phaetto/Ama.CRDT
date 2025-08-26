namespace Ama.CRDT.Attributes;

using Ama.CRDT.Services.Strategies;
using System;

/// <summary>
/// An attribute to mark a property to be managed by the Exclusive Lock strategy.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class CrdtExclusiveLockStrategyAttribute : CrdtStrategyAttribute
{
    /// <summary>
    /// Gets the JSON path within the root object to the property that holds the lock owner's identifier.
    /// </summary>
    public string LockHolderPropertyPath { get; }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="CrdtExclusiveLockStrategyAttribute"/> class.
    /// </summary>
    /// <param name="lockHolderPropertyPath">The JSON path within the root object to the property that holds the lock owner's identifier (e.g., "$.userId").</param>
    public CrdtExclusiveLockStrategyAttribute(string lockHolderPropertyPath) : base(typeof(ExclusiveLockStrategy))
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockHolderPropertyPath);
        LockHolderPropertyPath = lockHolderPropertyPath;
    }
}