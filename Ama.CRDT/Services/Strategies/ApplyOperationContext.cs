namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Models;

/// <summary>
/// Defines the context for an <see cref="ICrdtStrategy.ApplyOperation"/> call, encapsulating all necessary parameters for applying a single CRDT operation to a document.
/// </summary>
/// <param name="Root">The root object of the document to which the operation will be applied.</param>
/// <param name="Metadata">The CRDT metadata associated with the document. The strategy can read or modify this metadata, for example, to update LWW timestamps.</param>
/// <param name="Operation">The CRDT operation to be applied.</param>
public sealed record ApplyOperationContext(
    object Root,
    CrdtMetadata Metadata,
    CrdtOperation Operation
);