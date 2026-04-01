namespace Ama.CRDT.ShowCase.CollaborativeEditing.Models;

using Ama.CRDT.Models;

/// <summary>
/// A data structure for network messages.
/// </summary>
public readonly record struct NetworkMessage(string SenderId, CrdtPatch Patch);