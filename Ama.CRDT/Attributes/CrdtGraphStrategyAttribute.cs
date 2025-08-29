namespace Ama.CRDT.Attributes;

using Ama.CRDT.Services.Strategies;

/// <summary>
/// An attribute to mark a property to be managed by the add-only Graph strategy.
/// This strategy supports concurrent additions of vertices and edges.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class CrdtGraphStrategyAttribute() : CrdtStrategyAttribute(typeof(GraphStrategy));