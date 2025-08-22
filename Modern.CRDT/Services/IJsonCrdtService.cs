namespace Modern.CRDT.Services;

using Modern.CRDT.Models;

/// <summary>
/// Defines the public facade service for orchestrating CRDT operations.
/// This service provides a high-level, user-friendly API that encapsulates the
/// underlying complexity of patch generation and application.
/// </summary>
public interface IJsonCrdtService
{
    /// <summary>
    /// Applies a patch to a document to produce a new, merged document state.
    /// </summary>
    /// <param name="original">The document to which the patch will be applied.</param>
    /// <param name="patch">The patch containing the changes.</param>
    /// <returns>A new <see cref="CrdtDocument"/> with the patch applied.</returns>
    CrdtDocument Merge(CrdtDocument original, CrdtPatch patch);

    /// <summary>
    /// Creates a patch that represents the changes between an original and a modified document, using generic POCOs.
    /// </summary>
    /// <typeparam name="T">The type of the POCO data.</typeparam>
    /// <param name="original">The original document state, wrapping a POCO.</param>
    /// <param name="modified">The modified document state, wrapping a POCO.</param>
    /// <returns>A <see cref="CrdtPatch"/> containing the operations to transform the original into the modified document.</returns>
    CrdtPatch CreatePatch<T>(CrdtDocument<T> original, CrdtDocument<T> modified) where T : class;

    /// <summary>
    /// Applies a patch to a document containing a POCO to produce a new, merged document state.
    /// </summary>
    /// <typeparam name="T">The type of the POCO data.</typeparam>
    /// <param name="original">The document, wrapping a POCO, to which the patch will be applied.</param>
    /// <param name="patch">The patch containing the changes.</param>
    /// <returns>A new <see cref="CrdtDocument{T}"/> with the patch applied, containing the deserialized POCO.</returns>
    CrdtDocument<T> Merge<T>(CrdtDocument<T> original, CrdtPatch patch) where T : class;

    /// <summary>
    /// A convenience method that creates a patch from two documents (wrapping POCOs) and immediately applies it to the original.
    /// This effectively merges the changes from 'modified' into 'original'.
    /// </summary>
    /// <typeparam name="T">The type of the POCO data.</typeparam>
    /// <param name="original">The base document state, wrapping a POCO.</param>
    /// <param name="modified">The document state, wrapping a POCO, with changes to merge.</param>
    /// <returns>A new <see cref="CrdtDocument{T}"/> representing the merged state, containing the deserialized POCO.</returns>
    CrdtDocument<T> Merge<T>(CrdtDocument<T> original, CrdtDocument<T> modified) where T : class;
}