namespace Ama.CRDT.Attributes.Strategies;

/// <summary>
/// Marks a CRDT strategy as idempotent.
/// An operation is idempotent if applying it multiple times has the same effect as applying it once: a * a = a.
/// This property simplifies systems by allowing operations to be safely reapplied, which can happen in unreliable networks.
/// Short term idepotency is based on the fact that CrdtMetadata has a short tern SeenExceptions, that can be cleaned up, so it is not guaranteed forever.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class IdempotentShortTermImplementationAttribute : Attribute
{
}