namespace Ama.CRDT.Attributes;

using Ama.CRDT.Services.Strategies;
using System;

/// <summary>
/// Marks a collection property to be managed by the LWW-Set (Last-Writer-Wins Set) strategy.
/// In an LWW-Set, an element's membership is determined by the timestamp of its last add or remove operation.
/// This allows an element to be removed and then re-added.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class CrdtLwwSetStrategyAttribute() : CrdtStrategyAttribute(typeof(LwwSetStrategy));