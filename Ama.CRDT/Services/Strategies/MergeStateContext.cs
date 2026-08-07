namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;

/// <summary>
/// Defines the context for an <see cref="ICrdtStrategy.MergeState"/> call, encapsulating parameters like primary/secondary data and fully resolved property paths.
/// </summary>
/// <param name="PrimaryData">The primary document data object being merged into.</param>
/// <param name="PrimaryMetadata">The primary metadata state corresponding to the primary document data.</param>
/// <param name="SecondaryData">The secondary document data object being merged from.</param>
/// <param name="SecondaryMetadata">The secondary metadata state corresponding to the secondary document data.</param>
/// <param name="Property">The AOT-compatible property information for the property being merged.</param>
/// <param name="PropertyPath">The fully resolved string path representing the property's location within the document tree.</param>
public readonly record struct MergeStateContext(
    object PrimaryData,
    CrdtMetadata PrimaryMetadata,
    object SecondaryData,
    CrdtMetadata SecondaryMetadata,
    CrdtPropertyInfo Property,
    string PropertyPath
);