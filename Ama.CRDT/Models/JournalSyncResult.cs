namespace Ama.CRDT.Models;

using System;
using System.Collections.Generic;

/// <summary>
/// Represents the result of evaluating journal operations against synchronization requirements.
/// </summary>
public readonly record struct JournalSyncResult
{
    /// <summary>
    /// Gets the collection of operations retrieved from the journal.
    /// </summary>
    public IReadOnlyList<JournaledOperation> Operations { get; }

    /// <summary>
    /// Gets a value indicating whether the journal was truncated, meaning a full snapshot is required to bridge the causal gap securely.
    /// </summary>
    public bool SnapshotRequired { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="JournalSyncResult"/> struct.
    /// </summary>
    /// <param name="operations">The retrieved operations.</param>
    /// <param name="snapshotRequired">True if a full snapshot is required; otherwise, false.</param>
    public JournalSyncResult(IReadOnlyList<JournaledOperation> operations, bool snapshotRequired)
    {
        Operations = operations ?? Array.Empty<JournaledOperation>();
        SnapshotRequired = snapshotRequired;
    }
}