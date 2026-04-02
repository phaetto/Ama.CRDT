namespace Ama.CRDT.Attributes;

using System;

/// <summary>
/// Instructs the CRDT source generator to generate AOT-compatible serialization and reflection metadata for the specified type.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class CrdtSerializableAttribute : Attribute
{
    /// <summary>
    /// Gets the type to generate metadata for.
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CrdtSerializableAttribute"/> class.
    /// </summary>
    /// <param name="type">The type to generate metadata for.</param>
    public CrdtSerializableAttribute(Type type)
    {
        Type = type;
    }
}