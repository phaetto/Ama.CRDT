namespace Ama.CRDT.Attributes;

/// <summary>
/// Specifies a supported property type for a CRDT strategy.
/// A strategy class can be decorated with multiple instances of this attribute
/// to declare all the types it is designed to handle.
/// </summary>
/// <param name="supportedType">The type that the CRDT strategy supports.</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class CrdtSupportedTypeAttribute(Type supportedType) : Attribute
{
    /// <summary>
    /// Gets the type supported by the CRDT strategy.
    /// </summary>
    public Type SupportedType { get; } = supportedType;
}