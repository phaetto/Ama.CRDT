namespace Ama.CRDT.Attributes;

using Ama.CRDT.Services.Strategies;

/// <summary>
/// An attribute to mark a dictionary property to be managed by the OR-Map (Observed-Remove Map) strategy.
/// Key presence is managed using OR-Set logic, allowing keys to be re-added after removal.
/// Value updates are managed using LWW logic.
/// </summary>
public sealed class CrdtOrMapStrategyAttribute() : CrdtStrategyAttribute(typeof(OrMapStrategy));