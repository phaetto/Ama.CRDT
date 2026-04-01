namespace Ama.CRDT.ShowCase.CollaborativeEditing.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Ama.CRDT.Models;
using Ama.CRDT.Services.Journaling;

/// <summary>
/// A simple, in-memory implementation of an operation journal for the showcase.
/// Stores generated operations to allow new replicas to fetch missing history.
/// </summary>
public sealed class MemoryJournal : ICrdtOperationJournal
{
    private readonly List<JournaledOperation> operations = new();

    public void Append(string documentId, IReadOnlyList<CrdtOperation> operationsList)
    {
        if (string.IsNullOrWhiteSpace(documentId)) throw new ArgumentException("Document ID cannot be null or empty.", nameof(documentId));
        if (operationsList == null) throw new ArgumentNullException(nameof(operationsList));

        lock (operations)
        {
            foreach (var op in operationsList)
            {
                if (!operations.Any(o => o.Operation.Id == op.Id))
                {
                    operations.Add(new JournaledOperation(documentId, op));
                }
            }
        }
    }

    public Task AppendAsync(string documentId, IReadOnlyList<CrdtOperation> operationsList, CancellationToken cancellationToken = default)
    {
        Append(documentId, operationsList);
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<JournaledOperation> GetOperationsByRangeAsync(string originReplicaId, long minGlobalClock, long maxGlobalClock, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(originReplicaId)) throw new ArgumentException("Origin Replica ID cannot be null or empty.", nameof(originReplicaId));

        List<JournaledOperation> snapshot;
        lock (operations) { snapshot = operations.ToList(); }

        foreach (var op in snapshot.Where(o => o.Operation.ReplicaId == originReplicaId && o.Operation.GlobalClock > minGlobalClock && o.Operation.GlobalClock <= maxGlobalClock))
        {
            yield return op;
        }

        await Task.CompletedTask;
    }

    public async IAsyncEnumerable<JournaledOperation> GetOperationsByDotsAsync(string originReplicaId, IEnumerable<long> globalClocks, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(originReplicaId)) throw new ArgumentException("Origin Replica ID cannot be null or empty.", nameof(originReplicaId));
        if (globalClocks == null) throw new ArgumentNullException(nameof(globalClocks));

        var clocks = globalClocks.ToHashSet();
        List<JournaledOperation> snapshot;
        lock (operations) { snapshot = operations.ToList(); }

        foreach (var op in snapshot.Where(o => o.Operation.ReplicaId == originReplicaId && clocks.Contains(o.Operation.GlobalClock)))
        {
            yield return op;
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Trims operations that are strictly older than the global minimum version vector 
    /// to safely manage the memory footprint across the cluster.
    /// </summary>
    public void Trim(IReadOnlyDictionary<string, long> gmvv)
    {
        if (gmvv == null) throw new ArgumentNullException(nameof(gmvv));

        lock (operations)
        {
            operations.RemoveAll(op => 
                gmvv.TryGetValue(op.Operation.ReplicaId, out var minKnown) && 
                op.Operation.GlobalClock <= minKnown);
        }
    }
}