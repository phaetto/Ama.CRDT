namespace Ama.CRDT.ShowCase.LargerThanMemory.Services;

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Ama.CRDT.Models;

/// <summary>
/// Simulates a network synchronization queue for patches.
/// Keeps track of patches that need to be pushed to other replicas.
/// </summary>
public sealed class SyncService
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<CrdtPatch>> pendingPatches = new();

    public void QueuePatch(string sourceReplica, CrdtPatch patch, IEnumerable<string> allReplicas)
    {
        foreach (var replica in allReplicas.Where(r => r != sourceReplica))
        {
            var queue = pendingPatches.GetOrAdd(replica, _ => new ConcurrentQueue<CrdtPatch>());
            queue.Enqueue(patch);
        }
    }

    public bool TryDequeue(string targetReplica, out CrdtPatch patch)
    {
        if (pendingPatches.TryGetValue(targetReplica, out var queue))
        {
            return queue.TryDequeue(out patch);
        }
        patch = default;
        return false;
    }

    public int GetPendingCount(string targetReplica)
    {
        return pendingPatches.TryGetValue(targetReplica, out var queue) ? queue.Count : 0;
    }
}