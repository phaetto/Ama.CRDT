namespace Ama.CRDT.Services;

using Ama.CRDT.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Defines the contract for a service that compares two versions of a data model
/// and generates a CRDT patch.
/// </summary>
public interface ICrdtPatcher
{
    /// <summary>
    /// Compares two instances of a POCO and generates a CRDT patch.
    /// It recursively traverses the object graph, using reflection to find properties
    /// and applying the appropriate CRDT strategy for each one.
    /// </summary>
    /// <typeparam name="T">The type of the data model.</typeparam>
    /// <param name="from">The original document, containing the data and its metadata.</param>
    /// <param name="to">The modified document, containing the new data and its metadata.</param>
    /// <returns>A <see cref="CrdtPatch"/> containing the operations to transform 'from' into 'to'.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the <c>Metadata</c> property of <paramref name="from"/> or <paramref name="to"/> is null.</exception>
    CrdtPatch GeneratePatch<T>(CrdtDocument<T> from, CrdtDocument<T> to) where T : class;
    
    /// <summary>
    /// Compares two objects property by property and generates CRDT operations.
    /// This method is typically used by strategies for recursive diffing of nested objects.
    /// </summary>
    /// <param name="path">The base JSON path for the comparison.</param>
    /// <param name="type">The CLR type that defines the schema of the objects.</param>
    /// <param name="fromObj">The original object.</param>
    /// <param name="fromMeta">The metadata for the original object.</param>
    /// <param name="toObj">The modified object.</param>
    /// <param name="toMeta">The metadata for the modified object.</param>
    /// <param name="operations">The list to which generated operations will be added.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="type"/>, <paramref name="fromMeta"/>, <paramref name="toMeta"/>, or <paramref name="operations"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="path"/> is null or whitespace.</exception>
    void DifferentiateObject(string path, [DisallowNull] Type type, object? fromObj, [DisallowNull] CrdtMetadata fromMeta, object? toObj, [DisallowNull] CrdtMetadata toMeta, [DisallowNull] List<CrdtOperation> operations);
}