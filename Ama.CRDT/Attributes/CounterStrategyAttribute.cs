namespace Ama.CRDT.Attributes;

using Ama.CRDT.Services.Strategies;
using System;

/// <summary>
/// Specifies that a numeric property should be treated as a CRDT Counter.
/// Changes to this property will be represented as Increment operations.
/// This strategy is suitable for properties like scores, vote counts, or quantities
/// where concurrent additions and subtractions must be correctly aggregated.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class CounterStrategyAttribute() : CrdtStrategyAttribute(typeof(CounterStrategy));