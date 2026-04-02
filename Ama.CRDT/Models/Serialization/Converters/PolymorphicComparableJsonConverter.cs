namespace Ama.CRDT.Models.Serialization.Converters;

using System;

/// <summary>
/// A custom <see cref="System.Text.Json.Serialization.JsonConverter"/> for serializing and deserializing properties of type <see cref="IComparable"/>.
/// This converter handles polymorphism by embedding a type discriminator ('$type') in the JSON.
/// </summary>
public sealed class PolymorphicComparableJsonConverter : CrdtPolymorphicConverterBase<IComparable>
{
    public static PolymorphicComparableJsonConverter Instance { get; } = new();
}