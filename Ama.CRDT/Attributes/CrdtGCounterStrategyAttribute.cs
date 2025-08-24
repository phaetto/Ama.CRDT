namespace Ama.CRDT.Attributes;

using Ama.CRDT.Services.Strategies;

/// <summary>
/// Specifies that a numeric property should be treated as a G-Counter (Grow-Only Counter).
/// This counter only supports non-negative increments. Any operation that would decrease the value is ignored.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class CrdtGCounterStrategyAttribute() : CrdtStrategyAttribute(typeof(GCounterStrategy));