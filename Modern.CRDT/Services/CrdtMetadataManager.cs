namespace Modern.CRDT.Services;

using Modern.CRDT.Models;
using System.Linq;

/// <summary>
/// Implements the logic for managing and compacting CRDT metadata.
/// </summary>
public sealed class CrdtMetadataManager : ICrdtMetadataManager
{
    /// <inheritdoc/>
    public void PruneLwwTombstones(CrdtMetadata metadata, ICrdtTimestamp threshold)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(threshold);

        var keysToRemove = metadata.Lww
            .Where(kvp => kvp.Value.CompareTo(threshold) < 0)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            metadata.Lww.Remove(key);
        }
    }
    
    /// <inheritdoc/>
    public void AdvanceVersionVector(CrdtMetadata metadata, string replicaId, ICrdtTimestamp newTimestamp)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentException.ThrowIfNullOrEmpty(replicaId);
        ArgumentNullException.ThrowIfNull(newTimestamp);

        metadata.VersionVector[replicaId] = newTimestamp;

        if (metadata.SeenExceptions.Count > 0)
        {
            var exceptionsToRemove = metadata.SeenExceptions
                .Where(op => op.ReplicaId == replicaId && op.Timestamp.CompareTo(newTimestamp) <= 0)
                .ToList();

            foreach (var exception in exceptionsToRemove)
            {
                metadata.SeenExceptions.Remove(exception);
            }
        }
    }
}