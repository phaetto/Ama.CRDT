namespace Ama.CRDT.Attributes.Strategies;

/// <summary>
/// Marks a CRDT strategy as commutative.
/// An operation is commutative if the order of operands does not matter: a * b = b * a.
/// This ensures that replicas can apply operations in different orders and still converge to the same state.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class CommutativeAttribute : Attribute
{
}