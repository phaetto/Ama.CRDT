namespace Ama.CRDT.Attributes;

using Ama.CRDT.Services.Strategies;

/// <summary>
/// An attribute to mark a property to be managed by the Replicated Tree strategy.
/// This strategy manages a hierarchical tree structure, supporting concurrent node additions, removals (with re-addition), and moves.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class CrdtReplicatedTreeStrategyAttribute() : CrdtStrategyAttribute(typeof(ReplicatedTreeStrategy));