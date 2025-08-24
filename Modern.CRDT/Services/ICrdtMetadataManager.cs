namespace Modern.CRDT.Services;

using Modern.CRDT.Models;

/// <summary>
/// Defines a service for managing and compacting CRDT metadata to prevent unbounded state growth.
/// </summary>
public interface ICrdtMetadataManager
{
    /// <summary>
    /// Initializes or updates LWW metadata for a given document based on its current state and a provided timestamp.
    /// It inspects the document for properties managed by the LWW strategy and recursively assigns the timestamp to them in the metadata.
    /// This is useful for creating metadata for a new or modified document before generating a patch.
    /// </summary>
    /// <typeparam name="T">The type of the document.</typeparam>
    /// <param name="metadata">The metadata object to update.</param>
    /// <param name="document">The document instance whose LWW properties will be timestamped.</param>
    /// <param name="timestamp">The timestamp to assign to the LWW properties.</param>
    void InitializeLwwMetadata<T>(CrdtMetadata metadata, T document, ICrdtTimestamp timestamp) where T : class;

    /// <summary>
    /// Initializes or updates LWW metadata for a given document based on its current state, using the current time from <see cref="ICrdtTimestampProvider"/>.
    /// It inspects the document for properties managed by the LWW strategy and recursively assigns the current timestamp to them in the metadata.
    /// This is useful for creating metadata for a new or modified document before generating a patch.
    /// </summary>
    /// <typeparam name="T">The type of the document.</typeparam>
    /// <param name="metadata">The metadata object to update.</param>
    /// <param name="document">The document instance whose LWW properties will be timestamped.</param>
    void InitializeLwwMetadata<T>(CrdtMetadata metadata, T document) where T : class;

    /// <summary>
    /// Removes entries from the LWW metadata dictionary if their timestamp is older than a specified threshold.
    /// This is useful for garbage collecting tombstones for deleted fields.
    /// </summary>
    /// <param name="metadata">The metadata object to prune.</param>
    /// <param name="threshold">The timestamp threshold. Any LWW entry with a timestamp older than this will be removed.</param>
    void PruneLwwTombstones(CrdtMetadata metadata, ICrdtTimestamp threshold);

    /// <summary>
    /// Advances the version vector for a given replica to a new, higher timestamp. This action also
    /// compacts the 'SeenExceptions' set by removing any operations from that replica that are now
    /// covered by the new version vector timestamp.
    /// </summary>
    /// <param name="metadata">The metadata object to update.</param>
    /// <param name="replicaId">The ID of the replica whose vector is being advanced.</param>
    /// <param name="newTimestamp">The new, higher timestamp to set for the replica's version vector entry.</param>
    void AdvanceVersionVector(CrdtMetadata metadata, string replicaId, ICrdtTimestamp newTimestamp);
}