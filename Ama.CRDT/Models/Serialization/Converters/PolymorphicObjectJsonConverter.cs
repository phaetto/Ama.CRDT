namespace Ama.CRDT.Models.Serialization.Converters;

using System;

/// <summary>
/// A custom <see cref="System.Text.Json.Serialization.JsonConverter"/> for serializing and deserializing properties of type <see cref="object"/>.
/// This converter handles polymorphism by embedding a type discriminator ('$type') in the JSON.
/// It supports primitives, known CRDT payload types, and other complex objects recursively.
/// </summary>
public sealed class PolymorphicObjectJsonConverter : CrdtPolymorphicConverterBase<object>
{
    public static PolymorphicObjectJsonConverter Instance { get; } = new();

    /// <summary>
    /// Registers a type with a unique discriminator for polymorphic serialization.
    /// Exposing this publicly allows plugin packages (like Streams) to inject their own specific model types.
    /// </summary>
    /// <param name="discriminator">A short, unique string to identify the type.</param>
    /// <param name="type">The type to register.</param>
    public static void Register(string discriminator, Type type)
    {
        if (string.IsNullOrWhiteSpace(discriminator))
        {
            throw new ArgumentException("Discriminator cannot be null or whitespace.", nameof(discriminator));
        }

        CrdtTypeRegistry.Register(discriminator, type);
    }
}