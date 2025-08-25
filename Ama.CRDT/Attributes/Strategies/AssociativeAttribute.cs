namespace Ama.CRDT.Attributes.Strategies;

/// <summary>
/// Marks a CRDT strategy as associative.
/// An operation is associative if the order of grouping does not matter: (a * b) * c = a * (b * c).
/// This property is crucial for systems where operations might be batched or grouped differently across replicas.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class AssociativeAttribute : Attribute
{
}