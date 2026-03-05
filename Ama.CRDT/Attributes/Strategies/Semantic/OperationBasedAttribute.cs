namespace Ama.CRDT.Attributes.Strategies.Semantic;

using System;

/// <summary>
/// Marks a CRDT strategy as Operation-based (CmRDT).
/// Operation-based strategies achieve convergence by broadcasting and applying discrete operations (deltas).
/// Because individual operations (like numeric increments or sequential list insertions) are not inherently idempotent, 
/// this strategy relies heavily on the framework's causal delivery guarantees (e.g., Version Vectors) to ensure 
/// exactly-once application and causal ordering. Replicas cannot trivially merge their raw states without replaying the operation log.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class OperationBasedAttribute : Attribute
{
}