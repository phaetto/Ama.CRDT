namespace Ama.CRDT.Attributes.Strategies.Semantic;

using System;

/// <summary>
/// Marks a CRDT strategy as State-based (CvRDT).
/// State-based strategies achieve convergence by merging the local state of replicas. 
/// The merge function forms a join-semilattice, meaning it is mathematically commutative, associative, and idempotent. 
/// This allows the framework to safely and deterministically combine the data and metadata of two disconnected replicas 
/// at any time, without relying on a historical log of individual operations.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class StateBasedAttribute : Attribute
{
}