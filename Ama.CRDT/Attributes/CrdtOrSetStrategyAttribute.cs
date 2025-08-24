namespace Ama.CRDT.Attributes;

using Ama.CRDT.Services.Strategies;
using System;

/// <summary>
/// Marks a collection property to be managed by the OR-Set (Observed-Remove Set) strategy.
/// An OR-Set allows elements to be added and removed, and correctly handles re-addition
/// by assigning a unique tag to each added instance. A remove operation only tombstones the instances
/// observed at the time of removal, preventing conflicts with concurrent additions.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class CrdtOrSetStrategyAttribute() : CrdtStrategyAttribute(typeof(OrSetStrategy));