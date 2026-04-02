namespace Ama.CRDT.Models.Serialization.Converters;

using Ama.CRDT.Models.Partitioning;

/// <summary>
/// A custom <see cref="System.Text.Json.Serialization.JsonConverter"/> for serializing and deserializing properties of type <see cref="IPartition"/>.
/// This converter handles polymorphism by embedding a type discriminator ('$type') in the JSON.
/// </summary>
public sealed class PolymorphicPartitionJsonConverter : CrdtPolymorphicConverterBase<IPartition>
{
    public static PolymorphicPartitionJsonConverter Instance { get; } = new();
}