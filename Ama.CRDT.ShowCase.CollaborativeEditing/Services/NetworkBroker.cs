namespace Ama.CRDT.ShowCase.CollaborativeEditing.Services;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Ama.CRDT.Models;
using Ama.CRDT.Services.Versioning;
using Ama.CRDT.ShowCase.CollaborativeEditing.Models;

/// <summary>
/// A singleton service to simulate a fast network message bus broadcasting CRDT patches.
/// Tracks active replica version vectors to compute a Global Minimum Version Vector (GMVV) for safe garbage collection.
/// </summary>
public sealed class NetworkBroker
{
    private readonly IVersionVectorSyncService syncService;
    private readonly MemoryJournal journal;
    
    private readonly ConcurrentDictionary<string, ShardVersionVectorMap> replicaStates = new();
    private readonly ConcurrentDictionary<string, Func<string>> snapshotProviders = new();

    public event EventHandler<NetworkMessage>? MessageReceived;

    public NetworkBroker(IVersionVectorSyncService syncService, MemoryJournal journal)
    {
        if (syncService == null) throw new ArgumentNullException(nameof(syncService));
        if (journal == null) throw new ArgumentNullException(nameof(journal));

        this.syncService = syncService;
        this.journal = journal;
    }

    public void RegisterReplica(string replicaId, ShardVersionVectorMap state, Func<string> snapshotProvider)
    {
        if (string.IsNullOrWhiteSpace(replicaId)) throw new ArgumentException("Replica ID cannot be empty", nameof(replicaId));
        if (snapshotProvider == null) throw new ArgumentNullException(nameof(snapshotProvider));

        replicaStates[replicaId] = state;
        snapshotProviders[replicaId] = snapshotProvider;
        RunGarbageCollection();
    }

    public void UnregisterReplica(string replicaId)
    {
        if (string.IsNullOrWhiteSpace(replicaId)) throw new ArgumentException("Replica ID cannot be empty", nameof(replicaId));

        replicaStates.TryRemove(replicaId, out _);
        snapshotProviders.TryRemove(replicaId, out _);
        RunGarbageCollection();
    }

    public void UpdateReplicaState(string replicaId, ShardVersionVectorMap state)
    {
        if (string.IsNullOrWhiteSpace(replicaId)) throw new ArgumentException("Replica ID cannot be empty", nameof(replicaId));

        replicaStates[replicaId] = state;
        RunGarbageCollection();
    }

    public void Broadcast(string senderId, CrdtPatch patch)
    {
        if (string.IsNullOrWhiteSpace(senderId)) throw new ArgumentException("Sender ID cannot be empty", nameof(senderId));

        if (patch.Operations.Count == 0) return;

        MessageReceived?.Invoke(this, new NetworkMessage(senderId, patch));
    }

    public string? GetSnapshotJson()
    {
        var provider = snapshotProviders.Values.FirstOrDefault();
        return provider?.Invoke();
    }

    public ShardVersionVectorMap GetClusterState()
    {
        var map = new ShardVersionVectorMap();
        var states = replicaStates.Values.ToList();
        if (states.Count == 0) return map;

        var allShards = states.SelectMany(s => s.Keys).Distinct();
        foreach (var shard in allShards)
        {
            var vectorsForShard = states.Select(s => s.TryGetValue(shard, out var v) ? v : new DottedVersionVector());
            map[shard] = syncService.CalculateGlobalMaximumVersionVector(vectorsForShard);
        }

        return map;
    }

    public IReadOnlyDictionary<string, long> GetGmvv()
    {
        var states = replicaStates.Values.ToList();
        if (states.Count == 0) return new Dictionary<string, long>();
        
        var defaultVectors = states.Select(s => s.TryGetValue("default", out var v) ? v : new DottedVersionVector());
        return syncService.CalculateGlobalMinimumVersionVector(defaultVectors);
    }

    private void RunGarbageCollection()
    {
        var gmvv = GetGmvv();
        if (gmvv.Count > 0)
        {
            journal.Trim(gmvv);
        }
    }
}