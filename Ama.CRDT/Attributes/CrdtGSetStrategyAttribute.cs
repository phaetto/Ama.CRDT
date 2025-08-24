namespace Ama.CRDT.Attributes;

using Ama.CRDT.Services.Strategies;
using System;

/// <summary>
/// Marks a collection property to be managed by the G-Set (Grow-Only Set) strategy.
/// In a G-Set, elements can only be added. Remove operations are ignored.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class CrdtGSetStrategyAttribute() : CrdtStrategyAttribute(typeof(GSetStrategy));