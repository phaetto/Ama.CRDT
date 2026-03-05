namespace Ama.CRDT.Attributes;

using Ama.CRDT.Services.Strategies;
using System;

/// <summary>
/// Specifies that a collection property should use the First-Writer-Wins (FWW) Set merge strategy.
/// An element's membership is determined by the timestamp of its earliest add or remove operation.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class CrdtFwwSetStrategyAttribute() : CrdtStrategyAttribute(typeof(FwwSetStrategy));