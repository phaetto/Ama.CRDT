using Modern.CRDT.Models;

namespace Modern.CRDT.Services;

/// <summary>
/// Defines the contract for a service that applies a CRDT patch to a JSON document.
/// The applicator is responsible for interpreting the operations in a patch and modifying
/// a document to converge its state, respecting Last-Writer-Wins (LWW) semantics.
/// </summary>
public interface IJsonCrdtApplicator
{
    /// <summary>
    /// Applies a set of CRDT operations from a patch to a base document.
    /// The process is idempotent; applying the same patch multiple times yields the same result.
    /// Conflict resolution is handled via Last-Writer-Wins (LWW): an operation is only
    /// applied if its timestamp is greater than the timestamp of the existing data at the target path.
    /// </summary>
    /// <param name="document">The base document (data and metadata) to which the patch will be applied.</param>
    /// <param name="patch">A <see cref="CrdtPatch"/> containing the list of operations to apply.</param>
    /// <returns>A new <see cref="CrdtDocument"/> instance representing the merged state. The original document is not modified.</returns>
    CrdtDocument ApplyPatch(CrdtDocument document, CrdtPatch patch);
}