namespace Ama.CRDT.Attributes;

using Ama.CRDT.Services.Strategies;
using System;

/// <summary>
/// Marks a dictionary property to be managed by the Vote Counter strategy.
/// This strategy ensures that each voter can only have one active vote at a time, with conflicts resolved by Last-Writer-Wins.
/// The property must be of type IDictionary&lt;TKey, TValue&gt; where TValue is a collection like ISet&lt;TItem&gt; or HashSet&lt;TItem&gt;.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class CrdtVoteCounterStrategyAttribute() : CrdtStrategyAttribute(typeof(VoteCounterStrategy));