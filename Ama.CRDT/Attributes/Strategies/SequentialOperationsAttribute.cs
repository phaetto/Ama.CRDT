namespace Ama.CRDT.Attributes.Strategies;

/// <summary>
/// Marks a CRDT strategy as requiring sequential application of operations.
/// The state of such strategies is not easily mergeable, and convergence depends on the
/// historical order of operations. This prevents parallel reduction.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class SequentialOperationsAttribute : Attribute
{
}