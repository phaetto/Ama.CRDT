namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Models;
using Ama.CRDT.Services.GarbageCollection;

/// <summary>
/// Defines the context for an <see cref="ICrdtStrategy.Compact"/> call, encapsulating all necessary parameters for garbage collection.
/// </summary>
public readonly record struct CompactionContext(
    CrdtMetadata Metadata,
    ICompactionPolicy Policy,
    string PropertyName,
    string PropertyPath,
    object? Document
);