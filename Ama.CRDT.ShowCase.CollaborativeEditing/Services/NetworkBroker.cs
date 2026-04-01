namespace Ama.CRDT.ShowCase.CollaborativeEditing.Services;

using System;
using Ama.CRDT.Models;

/// <summary>
/// A singleton service to simulate a fast network message bus broadcasting CRDT patches.
/// </summary>
public sealed class NetworkBroker
{
    public event EventHandler<NetworkMessageEventArgs>? MessageReceived;

    public void Broadcast(string senderId, CrdtPatch patch)
    {
        if (patch.Operations.Count == 0) return;

        MessageReceived?.Invoke(this, new NetworkMessageEventArgs(senderId, patch));
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