namespace Ama.CRDT.Attributes;

using Ama.CRDT.Services.Strategies;

/// <summary>
/// An attribute to mark a dictionary property to be managed by the LWW-Map (Last-Writer-Wins Map) strategy.
/// Each key-value pair is treated as an independent LWW-Register.
/// </summary>
public sealed class CrdtLwwMapStrategyAttribute() : CrdtStrategyAttribute(typeof(LwwMapStrategy));