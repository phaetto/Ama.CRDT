namespace Ama.CRDT.Attributes;
using System.Collections;
using Ama.CRDT.Services.Strategies;

/// <summary>
/// An attribute to mark a dictionary property to be managed by the Counter-Map strategy.
/// Each key in the dictionary is treated as an independent PN-Counter.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
[CrdtSupportedType(typeof(IDictionary))]
public sealed class CrdtCounterMapStrategyAttribute() : CrdtStrategyAttribute(typeof(CounterMapStrategy));