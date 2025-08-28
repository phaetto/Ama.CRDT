namespace Ama.CRDT.Attributes.Strategies;
/// <summary>
/// Marks a CRDT strategy as idempotent, but only when the time is sequential and has no gaps.
/// An operation is idempotent if applying it multiple times has the same effect as applying it once: a * a = a.
/// This property simplifies systems by allowing operations to be safely reapplied, which can happen in unreliable networks.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class IdempotentWithContinuousTimeAttribute : Attribute
{
}