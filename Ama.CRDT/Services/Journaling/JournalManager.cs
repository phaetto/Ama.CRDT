namespace Ama.CRDT.Services.Journaling;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Ama.CRDT.Models;

/// <summary>
/// Implements <see cref="IJournalManager"/> to retrieve missing operations based on <see cref="ReplicaSyncRequirement"/>
/// by querying an underlying <see cref="ICrdtOperationJournal"/>.
/// </summary>
public sealed class JournalManager : IJournalManager
{
    private readonly ICrdtOperationJournal journal;

    /// <summary>
    /// Initializes a new instance of the <see cref="JournalManager"/> class.
    /// </summary>
    /// <param name="journal">The underlying operation journal.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="journal"/> is null.</exception>
    public JournalManager(ICrdtOperationJournal journal)
    {
        ArgumentNullException.ThrowIfNull(journal);
        this.journal = journal;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<CrdtOperation> GetMissingOperationsAsync(
        ReplicaSyncRequirement requirement, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!requirement.IsBehind || requirement.RequirementsByOrigin == null)
        {
            yield break;
        }

        foreach (var kvp in requirement.RequirementsByOrigin)
        {
            var originReplicaId = kvp.Key;
            var originReq = kvp.Value;

            if (originReq.HasMissingData)
            {
                if (originReq.SourceContiguousVersion > originReq.TargetContiguousVersion)
                {
                    var rangeStream = this.journal.GetOperationsByRangeAsync(
                        originReplicaId, 
                        originReq.TargetContiguousVersion, 
                        originReq.SourceContiguousVersion, 
                        cancellationToken);

                    await foreach (var op in rangeStream.ConfigureAwait(false))
                    {
                        // Exclude operations that the target already knows (TargetKnownDots)
                        if (originReq.TargetKnownDots != null && originReq.TargetKnownDots.Contains(op.GlobalClock))
                        {
                            continue;
                        }

                        yield return op;
                    }
                }

                if (originReq.SourceMissingDots != null && originReq.SourceMissingDots.Count > 0)
                {
                    var dotsStream = this.journal.GetOperationsByDotsAsync(
                        originReplicaId, 
                        originReq.SourceMissingDots, 
                        cancellationToken);

                    await foreach (var op in dotsStream.ConfigureAwait(false))
                    {
                        yield return op;
                    }
                }
            }
        }
    }
}