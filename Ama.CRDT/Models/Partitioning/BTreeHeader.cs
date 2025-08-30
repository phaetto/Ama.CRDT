namespace Ama.CRDT.Models.Partitioning;

/// <summary>
/// Represents the header of the B+ Tree index file, storing metadata like the root node offset and degree of the tree.
/// </summary>
public sealed record BTreeHeader(long RootNodeOffset = -1, long NextAvailableOffset = 0, int Degree = 8);