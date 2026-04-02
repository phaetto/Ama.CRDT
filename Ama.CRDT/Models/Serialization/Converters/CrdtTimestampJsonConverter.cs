namespace Ama.CRDT.Models.Serialization.Converters;

using Ama.CRDT.Models;
using System;

/// <summary>
/// A custom <see cref="System.Text.Json.Serialization.JsonConverter"/> for serializing and deserializing the <see cref="ICrdtTimestamp"/> interface.
/// This converter handles polymorphism by adding a "$type" discriminator to the JSON output. It is extensible,
/// allowing custom timestamp types to be registered for serialization.
/// </summary>
public sealed class CrdtTimestampJsonConverter : CrdtPolymorphicConverterBase<ICrdtTimestamp>
{
    /// <summary>
    /// Registers a custom <see cref="ICrdtTimestamp"/> implementation for polymorphic serialization.
    /// This method is thread-safe and can be called during application startup to extend the converter.
    /// </summary>
    /// <param name="discriminator">A unique string identifier for the type, used in JSON output.</param>
    /// <param name="type">The type that implements <see cref="ICrdtTimestamp"/>.</param>
    /// <exception cref="ArgumentException">Thrown if the discriminator is empty or the type does not implement <see cref="ICrdtTimestamp"/>.</exception>
    public static void Register(string discriminator, Type type)
    {
        if (string.IsNullOrWhiteSpace(discriminator))
        {
            throw new ArgumentException("Discriminator cannot be null or whitespace.", nameof(discriminator));
        }

        if (!typeof(ICrdtTimestamp).IsAssignableFrom(type))
        {
            throw new ArgumentException($"Type {type.FullName} must implement {nameof(ICrdtTimestamp)}.", nameof(type));
        }

        CrdtTypeRegistry.Register(discriminator, type);
    }

    /// <inheritdoc />
    protected override string GetDiscriminatorOrThrow(Type type)
    {
        var discriminator = CrdtTypeRegistry.GetDiscriminator(type);
        if (discriminator is null)
        {
            throw new NotSupportedException($"Type {type.FullName} is not a supported or registered ICrdtTimestamp for serialization.");
        }
        return discriminator;
    }
}