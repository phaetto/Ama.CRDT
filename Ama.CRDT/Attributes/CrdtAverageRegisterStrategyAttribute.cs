namespace Ama.CRDT.Attributes;

using Ama.CRDT.Services.Strategies;

/// <summary>
/// Specifies that a property should be treated as an Average Register.
/// In this strategy, each replica contributes a value, and the property's state converges to the average of all contributions.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class CrdtAverageRegisterStrategyAttribute() : CrdtStrategyAttribute(typeof(AverageRegisterStrategy));