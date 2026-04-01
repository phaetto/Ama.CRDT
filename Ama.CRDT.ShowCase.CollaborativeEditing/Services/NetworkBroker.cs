namespace Ama.CRDT.ShowCase.CollaborativeEditing.Services;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Ama.CRDT.Models;
using Ama.CRDT.Services.Versioning;

/// <summary>
/// A singleton service to simulate a fast network message bus broadcasting CRDT patches.
/// Tracks active replica version vectors to compute a Global Minimum Version Vector (GMVV) for safe garbage collection.
/// </summary>
public sealed class NetworkBroker
{
    private readonly IVersionVectorSyncService _syncService;
    private readonly MemoryJournal _journal;
    
    private readonly ConcurrentDictionary<string, DottedVersionVector> _replicaStates = new();
    private readonly ConcurrentDictionary<string, Func<string>> _snapshotProviders = new();

    public event EventHandler<NetworkMessageEventArgs>? MessageReceived;

    public NetworkBroker(IVersionVectorSyncService syncService, MemoryJournal journal)
    {
        _syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
        _journal = journal ?? throw new ArgumentNullException(nameof(journal));
    }

    public void RegisterReplica(string replicaId, DottedVersionVector state, Func<string> snapshotProvider)
    {
        _replicaStates[replicaId] = state;
        _snapshotProviders[replicaId] = snapshotProvider;
        RunGarbageCollection();
    }

    public void UnregisterReplica(string replicaId)
    {
        _replicaStates.TryRemove(replicaId, out _);
        _snapshotProviders.TryRemove(replicaId, out _);
        RunGarbageCollection();
    }

    public void UpdateReplicaState(string replicaId, DottedVersionVector state)
    {
        _replicaStates[replicaId] = state;
        RunGarbageCollection();
    }

    public void Broadcast(string senderId, CrdtPatch patch)
    {
        if (patch.Operations.Count == 0) return;

        MessageReceived?.Invoke(this, new NetworkMessageEventArgs(senderId, patch));
    }

    public string? GetSnapshotJson()
    {
        // Pick an active replica to provide a baseline snapshot for new editors
        var provider = _snapshotProviders.Values.FirstOrDefault();
        return provider?.Invoke();
    }

    public DottedVersionVector GetClusterState()
    {
        var states = _replicaStates.Values.ToList();
        if (states.Count == 0) return new DottedVersionVector();

        // Merge all known versions to create a "max" state representing everything the network knows
        return _syncService.CalculateGlobalMaximumVersionVector(states);
    }

    public IReadOnlyDictionary<string, long> GetGmvv()
    {
        var states = _replicaStates.Values.ToList();
        if (states.Count == 0) return new Dictionary<string, long>();
        
        return _syncService.CalculateGlobalMinimumVersionVector(states);
    }

    private void RunGarbageCollection()
    {
        var gmvv = GetGmvv();
        if (gmvv.Count > 0)
        {
            _journal.Trim(gmvv);
        }
    }
}

public sealed class NetworkMessageEventArgs : EventArgs
{
    public string SenderId { get; }
    public CrdtPatch Patch { get; }

    public NetworkMessageEventArgs(string senderId, CrdtPatch patch)
    {
        SenderId = senderId ?? throw new ArgumentNullException(nameof(senderId));
        Patch = patch;
    }
}