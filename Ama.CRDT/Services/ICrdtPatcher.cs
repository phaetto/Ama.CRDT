namespace Ama.CRDT.Services;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Ama.CRDT.Models;

/// <summary>
/// Defines the contract for a service that compares two versions of a data model and generates a CRDT patch.
/// </summary>
public interface ICrdtPatcher
{
    /// <summary>
    /// Generates a CRDT patch by comparing two versions of a document.
    /// </summary>
    /// <typeparam name="T">The type of the data model.</typeparam>
    /// <param name="from">The original document state.</param>
    /// <param name="to">The modified document state.</param>
    /// <returns>A <see cref="CrdtPatch"/> containing the operations to transform 'from' to 'to'.</returns>
    CrdtPatch GeneratePatch<T>(CrdtDocument<T> from, CrdtDocument<T> to) where T : class;

    /// <summary>
    /// Recursively differentiates two objects, populating a list of CRDT operations.
    /// </summary>
    /// <param name="path">The current JSON path.</param>
    /// <param name="type">The type of the objects being compared.</param>
    /// <param name="fromObj">The original object.</param>
    /// <param name="fromMeta">The metadata for the original object.</param>
    /// <param name="toObj">The modified object.</param>
    /// <param name="toMeta">The metadata for the modified object.</param>
    /// <param name="operations">The list to populate with generated operations.</param>
    /// <param name="fromRoot">The root of the original document.</param>
    /// <param name="toRoot">The root of the modified document.</param>
    void DifferentiateObject(string path, [DisallowNull] Type type, object? fromObj, [DisallowNull] CrdtMetadata fromMeta, object? toObj, [DisallowNull] CrdtMetadata toMeta, [DisallowNull] List<CrdtOperation> operations, object? fromRoot, object? toRoot);
}