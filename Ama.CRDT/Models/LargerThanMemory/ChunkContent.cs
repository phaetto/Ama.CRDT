namespace Ama.CRDT.Models.LargerThanMemory;

/// <summary>
/// A data structure representing the data and metadata content of a single partition.
/// </summary>
public readonly record struct ChunkContent(object Data, CrdtMetadata Metadata);