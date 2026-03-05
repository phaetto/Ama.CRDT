namespace Ama.CRDT.Attributes;

using Ama.CRDT.Services.Strategies;
using System;

/// <summary>
/// Specifies that a dictionary property should use the First-Writer-Wins (FWW) Map merge strategy.
/// Each key-value pair is treated as an independent FWW-Register, meaning the first write to a key is kept.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class CrdtFwwMapStrategyAttribute() : CrdtStrategyAttribute(typeof(FwwMapStrategy));