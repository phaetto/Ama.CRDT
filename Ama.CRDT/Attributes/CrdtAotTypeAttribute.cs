namespace Ama.CRDT.Attributes;

using System;

/// <summary>
/// Instructs the CRDT source generator to generate AOT-compatible reflection metadata for the specified type.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class CrdtAotTypeAttribute : Attribute
{
    /// <summary>
    /// Gets the type to generate metadata for.
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CrdtAotTypeAttribute"/> class.
    /// </summary>
    /// <param name="type">The type to generate metadata for.</param>
    public CrdtAotTypeAttribute(Type type)
    {
        Type = type;
    }
}