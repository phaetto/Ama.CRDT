namespace Ama.CRDT.Models.Partitioning;

/// <summary>
/// A data structure representing the data and metadata content of a single partition.
/// </summary>
public readonly record struct PartitionContent(object Data, CrdtMetadata Metadata);