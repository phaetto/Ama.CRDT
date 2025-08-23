namespace Ama.CRDT.Attributes;

using Ama.CRDT.Services.Strategies;
using System;

/// <summary>
/// Specifies that a property should use the Last-Writer-Wins (LWW) merge strategy.
/// This is the default strategy if no other attribute is specified.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class LwwStrategyAttribute() : CrdtStrategyAttribute(typeof(LwwStrategy));