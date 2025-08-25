namespace Ama.CRDT.Attributes;

using Ama.CRDT.Services.Strategies;
using System;

/// <summary>
/// An attribute to mark a collection property to be managed as a Priority Queue.
/// The collection is treated as an LWW-Set, and is kept sorted based on a specified priority property on its elements.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class CrdtPriorityQueueStrategyAttribute(string priorityPropertyName) : CrdtStrategyAttribute(typeof(PriorityQueueStrategy))
{
    /// <summary>
    /// Gets the name of the property on the collection's elements that holds the priority value.
    /// The priority value must be a comparable type (e.g., long, int, string).
    /// </summary>
    public string PriorityPropertyName { get; } = priorityPropertyName;
}