namespace Ama.CRDT.Models.Aot;

using System;
using System.Collections.Generic;

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

    /// <summary>
    /// Retrieves all AOT-compatible type information registered in this context.
    /// </summary>
    /// <returns>An enumerable of generated <see cref="CrdtTypeInfo"/>.</returns>
    public virtual IEnumerable<CrdtTypeInfo> GetRegisteredTypes() => Array.Empty<CrdtTypeInfo>();
}