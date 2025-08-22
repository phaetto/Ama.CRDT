namespace Modern.CRDT.Services;

using Modern.CRDT.Models;

/// <summary>
/// Defines the contract for a service that applies a CRDT patch to a document.
/// The applicator is the central authority for conflict resolution and idempotency,
/// using an external metadata object to track state.
/// </summary>
public interface IJsonCrdtApplicator
{
    /// <summary>
    /// Applies a set of CRDT operations from a patch to a POCO document.
    /// The process is strategy-driven, idempotent, and resolves conflicts based on the provided metadata.
    /// </summary>
    /// <typeparam name="T">The type of the POCO model representing the document structure.</typeparam>
    /// <param name="document">The base document object to which the patch will be applied.</param>
    /// <param name="patch">A <see cref="CrdtPatch"/> containing the list of operations to apply.</param>
    /// <param name="metadata">The <see cref="CrdtMetadata"/> object containing the current state for conflict resolution.</param>
    /// <returns>A new instance of the document with the patch applied. The original document is not modified.</returns>
    T ApplyPatch<T>(T document, CrdtPatch patch, CrdtMetadata metadata) where T : class;
}