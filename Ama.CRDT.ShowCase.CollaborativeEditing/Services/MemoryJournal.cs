namespace Ama.CRDT.ShowCase.CollaborativeEditing.Services;

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
    private readonly List<JournaledOperation> _operations = new();

    public void Append(string documentId, IReadOnlyList<CrdtOperation> operations)
    {
        lock (_operations)
        {
            foreach (var op in operations)
            {
                // Ensure idempotency for self-generated operations
                if (!_operations.Any(o => o.Operation.Id == op.Id))
                {
                    _operations.Add(new JournaledOperation(documentId, op));
                }
            }
        }
    }

    public Task AppendAsync(string documentId, IReadOnlyList<CrdtOperation> operations, CancellationToken cancellationToken = default)
    {
        Append(documentId, operations);
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<JournaledOperation> GetOperationsByRangeAsync(string originReplicaId, long minGlobalClock, long maxGlobalClock, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        List<JournaledOperation> snapshot;
        lock (_operations) { snapshot = _operations.ToList(); }

        foreach (var op in snapshot.Where(o => o.Operation.ReplicaId == originReplicaId && o.Operation.GlobalClock > minGlobalClock && o.Operation.GlobalClock <= maxGlobalClock))
        {
            yield return op;
        }

        await Task.CompletedTask;
    }

    public async IAsyncEnumerable<JournaledOperation> GetOperationsByDotsAsync(string originReplicaId, IEnumerable<long> globalClocks, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var clocks = globalClocks.ToHashSet();
        List<JournaledOperation> snapshot;
        lock (_operations) { snapshot = _operations.ToList(); }

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
        lock (_operations)
        {
            _operations.RemoveAll(op => 
                gmvv.TryGetValue(op.Operation.ReplicaId, out var minKnown) && 
                op.Operation.GlobalClock <= minKnown);
        }
    }
}