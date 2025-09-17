namespace Ama.CRDT.Attributes;

/// <summary>
/// Designates a property on a CRDT document class as the logical key for partitioning.
/// The class using this attribute must have exactly one property with a strategy that implements <see cref="Services.Partitioning.IPartitionableCrdtStrategy"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class PartitionKeyAttribute(string propertyName) : Attribute
{
    /// <summary>
    /// The name of the property that acts as the logical partition key.
    /// </summary>
    public string PropertyName { get; } = !string.IsNullOrWhiteSpace(propertyName) ? propertyName : throw new ArgumentNullException(nameof(propertyName));
}