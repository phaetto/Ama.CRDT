namespace Ama.CRDT.Models;

/// <summary>
/// Represents an item within the metadata of an LSEQ (Log-structured Sequence).
/// It pairs a dense LSEQ identifier with its corresponding value.
/// </summary>
/// <param name="Identifier">The unique, ordered LSEQ identifier.</param>
/// <param name="Value">The actual value of the item.</param>
public readonly record struct LseqItem(LseqIdentifier Identifier, object? Value);