namespace Ama.CRDT.Partitioning.Streams.Models.Serialization;

using System.Text.Json.Serialization;

/// <summary>
/// Provides AOT-compatible source generation for internal Stream Partitioning models.
/// </summary>
[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]
[JsonSerializable(typeof(BPlusTreeNode))]
[JsonSerializable(typeof(BTreeHeader))]
[JsonSerializable(typeof(DataStreamHeader))]
[JsonSerializable(typeof(FreeSpaceState))]
internal partial class StreamsJsonContext : JsonSerializerContext
{
}