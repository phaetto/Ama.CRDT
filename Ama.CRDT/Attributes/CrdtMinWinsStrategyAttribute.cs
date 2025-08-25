namespace Ama.CRDT.Attributes;

using Ama.CRDT.Services.Strategies;

/// <summary>
/// Specifies that a property should use a Min-Wins Register strategy.
/// Conflicts are resolved by choosing the lowest numeric value, regardless of timestamps.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class CrdtMinWinsStrategyAttribute() : CrdtStrategyAttribute(typeof(MinWinsStrategy));