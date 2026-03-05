namespace Ama.CRDT.Attributes;

using Ama.CRDT.Services.Strategies;
using System;

/// <summary>
/// Specifies that a property should use the First-Writer-Wins (FWW) merge strategy.
/// When a conflict occurs, the value with the lowest timestamp "wins".
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class CrdtFwwStrategyAttribute() : CrdtStrategyAttribute(typeof(FwwStrategy));