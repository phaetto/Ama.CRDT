namespace Ama.CRDT.ShowCase.LargerThanMemory.Services;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Serialization;

/// <summary>
/// Simulates a network synchronization queue for patches.
/// Keeps track of patches that need to be pushed to other replicas.
/// State is persisted to disk so syncing can resume after restarts.
/// </summary>
public sealed class SyncService
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<(Guid LogicalKey, CrdtPatch Patch)>> pendingPatches = new();
    private readonly string storageFile;
    private readonly object lockObj = new();

    public SyncService()
    {
        var basePath = Path.Combine(Environment.CurrentDirectory, "data");
        Directory.CreateDirectory(basePath);
        storageFile = Path.Combine(basePath, "sync_queue.json");
        LoadState();
    }

    private void LoadState()
    {
        if (File.Exists(storageFile))
        {
            try
            {
                var json = File.ReadAllText(storageFile);
                var dict = JsonSerializer.Deserialize<Dictionary<string, List<SyncItem>>>(json, CrdtJsonContext.DefaultOptions);
                if (dict != null)
                {
                    foreach (var kvp in dict)
                    {
                        var queue = new ConcurrentQueue<(Guid LogicalKey, CrdtPatch Patch)>();
                        foreach (var item in kvp.Value)
                        {
                            queue.Enqueue((item.LogicalKey, item.Patch));
                        }
                        pendingPatches[kvp.Key] = queue;
                    }
                }
            }
            catch { /* Ignore deserialization issues for showcase */ }
        }
    }

    private void SaveState()
    {
        lock (lockObj)
        {
            try
            {
                var dict = pendingPatches.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Select(x => new SyncItem { LogicalKey = x.LogicalKey, Patch = x.Patch }).ToList()
                );
                var json = JsonSerializer.Serialize(dict, CrdtJsonContext.DefaultOptions);
                File.WriteAllText(storageFile, json);
            }
            catch { /* Ignore serialization issues for showcase */ }
        }
    }

    public void QueuePatch(string sourceReplica, Guid logicalKey, CrdtPatch patch, IEnumerable<string> allReplicas)
    {
        foreach (var replica in allReplicas.Where(r => r != sourceReplica))
        {
            var queue = pendingPatches.GetOrAdd(replica, _ => new ConcurrentQueue<(Guid LogicalKey, CrdtPatch Patch)>());
            queue.Enqueue((logicalKey, patch));
        }
        SaveState();
    }

    public bool TryDequeue(string targetReplica, out Guid logicalKey, out CrdtPatch patch)
    {
        if (pendingPatches.TryGetValue(targetReplica, out var queue) && queue.TryDequeue(out var item))
        {
            logicalKey = item.LogicalKey;
            patch = item.Patch;
            SaveState();
            return true;
        }
        
        logicalKey = default;
        patch = default;
        return false;
    }

    public int GetPendingCount(string targetReplica)
    {
        return pendingPatches.TryGetValue(targetReplica, out var queue) ? queue.Count : 0;
    }

    private sealed class SyncItem
    {
        public Guid LogicalKey { get; set; }
        public CrdtPatch Patch { get; set; }
    }
}