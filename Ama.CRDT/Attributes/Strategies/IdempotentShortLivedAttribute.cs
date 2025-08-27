namespace Ama.CRDT.Attributes.Strategies;

using Ama.CRDT.Models;

/// <summary>
/// Marks a CRDT strategy as idempotent, but only while the state is not cleaned up while the event repeats.
/// An operation is idempotent if applying it multiple times has the same effect as applying it once: a * a = a.
/// This property simplifies systems by allowing operations to be safely reapplied, which can happen in unreliable networks.
/// Short term idempotency is based on the fact that <see cref="CrdtMetadata"/> has a short term <see cref="CrdtMetadata.SeenExceptions"/>, that can be cleaned up, so it is not guaranteed forever.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class IdempotentShortTermImplementationAttribute : Attribute
{
}