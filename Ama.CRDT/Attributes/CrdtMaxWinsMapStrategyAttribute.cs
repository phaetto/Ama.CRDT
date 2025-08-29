namespace Ama.CRDT.Attributes;
using System.Collections;
using Ama.CRDT.Services.Strategies;

/// <summary>
/// An attribute to mark a dictionary property to be managed by the Max-Wins Map strategy.
/// For each key, conflicts are resolved by choosing the highest value, making the map's keys grow-only.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
[CrdtSupportedType(typeof(IDictionary))]
public sealed class CrdtMaxWinsMapStrategyAttribute() : CrdtStrategyAttribute(typeof(MaxWinsMapStrategy));