namespace Ama.CRDT.Attributes;

using Ama.CRDT.Services.Strategies;
using System;

/// <summary>
/// An attribute to explicitly mark a collection property to use the RGA (Replicated Growable Array) strategy.
/// RGA maintains order by linking elements to their predecessors and uses tombstones for deletions,
/// providing excellent convergence properties for collaborative text and lists.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class CrdtRgaStrategyAttribute() : CrdtStrategyAttribute(typeof(RgaStrategy));