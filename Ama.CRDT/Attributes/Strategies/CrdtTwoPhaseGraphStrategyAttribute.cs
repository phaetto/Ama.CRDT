namespace Ama.CRDT.Attributes;

using Ama.CRDT.Services.Strategies;

/// <summary>
/// An attribute to mark a property to be managed by the Two-Phase Graph strategy.
/// This strategy supports concurrent additions and removals of vertices and edges. Once removed, an element cannot be re-added.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class CrdtTwoPhaseGraphStrategyAttribute() : CrdtStrategyAttribute(typeof(TwoPhaseGraphStrategy));