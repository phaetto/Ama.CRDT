namespace Ama.CRDT.Services;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Ama.CRDT.Models;

/// <summary>
/// Defines the context for a <see cref="ICrdtPatcher.DifferentiateObject"/> operation.
/// </summary>
/// <param name="Path">The current JSON path being compared (e.g., "$.user.name").</param>
/// <param name="Type">The type of the objects being compared.</param>
/// <param name="FromObj">The original object instance (or null if it was added).</param>
/// <param name="ToObj">The modified object instance (or null if it was removed).</param>
/// <param name="FromRoot">The root object of the original document.</param>
/// <param name="ToRoot">The root object of the modified document.</param>
/// <param name="FromMeta">The metadata corresponding to the original document state.</param>
/// <param name="Operations">The list to populate with generated <see cref="CrdtOperation"/> instances.</param>
/// <param name="ChangeTimestamp">The timestamp for the change.</param>
public sealed record DifferentiateObjectContext(
    [DisallowNull] string Path,
    [DisallowNull] Type Type,
    object? FromObj,
    object? ToObj,
    object? FromRoot,
    object? ToRoot,
    [DisallowNull] CrdtMetadata FromMeta,
    [DisallowNull] List<CrdtOperation> Operations,
    [DisallowNull] ICrdtTimestamp ChangeTimestamp
);