namespace Ama.CRDT.Attributes;

using Ama.CRDT.Services.Strategies;
using System;

/// <summary>
/// Marks a collection property to be managed by the 2P-Set (Two-Phase Set) strategy.
/// A 2P-Set allows elements to be added and removed, but once an element is removed,
/// it can never be re-added.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class CrdtTwoPhaseSetStrategyAttribute() : CrdtStrategyAttribute(typeof(TwoPhaseSetStrategy));