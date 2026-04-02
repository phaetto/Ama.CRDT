namespace Ama.CRDT.Models.Aot;

using System;

/// <summary>
/// The abstract base class for AOT source-generated CRDT reflection contexts.
/// </summary>
public abstract class CrdtContext
{
    /// <summary>
    /// Retrieves the AOT-compatible type information for the specified type.
    /// </summary>
    /// <param name="type">The type to retrieve information for.</param>
    /// <returns>The generated <see cref="CrdtTypeInfo"/>, or null if the type is not registered in this context.</returns>
    public abstract CrdtTypeInfo? GetTypeInfo(Type type);
}