namespace Ama.CRDT.Services;

using Ama.CRDT.Models;

/// <summary>
/// Defines a service for managing and compacting CRDT metadata to prevent unbounded state growth.
/// It also provides methods to initialize metadata based on a document's state.
/// </summary>
public interface ICrdtMetadataManager
{
    /// <summary>
    /// Creates and initializes a new <see cref="CrdtMetadata"/> object for a given document.
    /// It populates LWW timestamps for properties managed by the LWW strategy using the current time.
    /// </summary>
    /// <typeparam name="T">The type of the document.</typeparam>
    /// <param name="document">The document object to initialize metadata for.</param>
    /// <returns>A new, initialized <see cref="CrdtMetadata"/> object.</returns>
    CrdtMetadata Initialize<T>(T document) where T : class;

    /// <summary>
    /// Creates and initializes a new <see cref="CrdtMetadata"/> object for a given document with a specific timestamp.
    /// It populates LWW timestamps for properties managed by the LWW strategy.
    /// </summary>
    /// <typeparam name="T">The type of the document.</typeparam>
    /// <param name="document">The document object to initialize metadata for.</param>
    /// <param name="timestamp">The timestamp to use for initialization.</param>
    /// <returns>A new, initialized <see cref="CrdtMetadata"/> object.</returns>
    CrdtMetadata Initialize<T>(T document, ICrdtTimestamp timestamp) where T : class;

    /// <summary>
    /// Populates LWW-related metadata for a given document object into an existing metadata object.
    /// This method recursively traverses the document and adds timestamps for properties using the LWW strategy.
    /// </summary>
    /// <typeparam name="T">The type of the document.</typeparam>
    /// <param name="metadata">The metadata object to populate.</param>
    /// <param name="document">The document object from which to derive metadata.</param>
    void InitializeLwwMetadata<T>(CrdtMetadata metadata, T document) where T : class;

    /// <summary>
    /// Populates LWW-related metadata for a given document object into an existing metadata object using a specific timestamp.
    /// This method recursively traverses the document and adds timestamps for properties using the LWW strategy.
    /// </summary>
    /// <typeparam name="T">The type of the document.</typeparam>
    /// <param name="metadata">The metadata object to populate.</param>
    /// <param name="document">The document object from which to derive metadata.</param>
    /// <param name="timestamp">The timestamp to use for initialization.</param>
    void InitializeLwwMetadata<T>(CrdtMetadata metadata, T document, ICrdtTimestamp timestamp) where T : class;

    /// <summary>
    /// Removes LWW tombstones (entries) from the metadata that are older than the specified threshold.
    /// This helps to compact the metadata and prevent it from growing indefinitely.
    /// </summary>
    /// <param name="metadata">The metadata object to prune.</param>
    /// <param name="threshold">The timestamp threshold. Any LWW entry older than this will be removed.</param>
    void PruneLwwTombstones(CrdtMetadata metadata, ICrdtTimestamp threshold);
    
    /// <summary>
    /// Advances the version vector for the replica that generated the operation.
    /// It ensures that the vector only moves forward and prunes any seen exceptions that are now covered by the new vector timestamp.
    /// </summary>
    /// <param name="metadata">The metadata object to update.</param>
    /// <param name="operation">The operation whose replica and timestamp will be used to advance the vector.</param>
    void AdvanceVersionVector(CrdtMetadata metadata, CrdtOperation operation);
}