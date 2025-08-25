namespace Ama.CRDT.Attributes.Strategies;

/// <summary>
/// Marks a CRDT strategy as having a mergeable state, allowing its metadata to be combined
/// from multiple parallel computations. This is typical for state-based CRDTs.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class MergeableAttribute : Attribute
{
}