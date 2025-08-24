namespace Ama.CRDT.Attributes;

using Ama.CRDT.Services.Strategies;

/// <summary>
/// An attribute to explicitly mark a collection property to use the LSEQ (Log-structured Sequence) strategy.
/// LSEQ assigns dense, ordered identifiers to list elements, avoiding the floating-point precision issues
/// of fractional indexing while ensuring a stable, convergent order.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class CrdtLseqStrategyAttribute() : CrdtStrategyAttribute(typeof(LseqStrategy));