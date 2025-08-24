namespace Ama.CRDT.Attributes;

using Ama.CRDT.Services.Strategies;

/// <summary>
/// Specifies that a property should use a Max-Wins Register strategy.
/// Conflicts are resolved by choosing the highest numeric value, regardless of timestamps.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class CrdtMaxWinsStrategyAttribute() : CrdtStrategyAttribute(typeof(MaxWinsStrategy));