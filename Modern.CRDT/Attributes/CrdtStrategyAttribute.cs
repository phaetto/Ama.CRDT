namespace Modern.CRDT.Attributes;

/// <summary>
/// A base attribute for specifying a CRDT strategy for a property.
/// This attribute must be inherited by concrete strategy attributes.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public abstract class CrdtStrategyAttribute : Attribute
{
}