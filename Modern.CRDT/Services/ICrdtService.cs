namespace Modern.CRDT.Services;

using Modern.CRDT.Models;

/// <summary>
/// Defines the public facade service for orchestrating CRDT operations.
/// This service provides a high-level, user-friendly API that encapsulates the
/// underlying complexity of patch generation and application.
/// </summary>
public interface ICrdtService
{
    /// <summary>
    /// Creates a patch that represents the changes between an original and a modified document, using generic POCOs.
    /// </summary>
    /// <typeparam name="T">The type of the POCO data.</typeparam>
    /// <param name="original">The original document state, wrapping a POCO and its metadata.</param>
    /// <param name="modified">The modified document state, wrapping a POCO and its metadata.</param>
    /// <returns>A <see cref="CrdtPatch"/> containing the operations to transform the original into the modified document.</returns>
    CrdtPatch CreatePatch<T>(CrdtDocument<T> original, CrdtDocument<T> modified) where T : class;

    /// <summary>
    /// Applies a patch to a POCO document to produce a new, merged document state, using external state for conflict resolution.
    /// The original document instance is modified in place.
    /// </summary>
    /// <typeparam name="T">The type of the POCO data.</typeparam>
    /// <param name="document">The POCO document to which the patch will be applied.</param>
    /// <param name="patch">The patch containing the changes.</param>
    /// <param name="metadata">The metadata object containing the current conflict resolution state.</param>
    /// <returns>The original document instance with the patch applied.</returns>
    T Merge<T>(T document, CrdtPatch patch, CrdtMetadata metadata) where T : class;
}