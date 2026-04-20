namespace Ama.CRDT.Models.Serialization;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

/// <summary>
/// A custom JSON type info resolver that aggregates results from multiple resolvers.
/// Unlike the default <see cref="JsonTypeInfoResolver.Combine(IJsonTypeInfoResolver?[])"/> which short-circuits on the first match,
/// this resolver inquires ALL provided resolvers and merges critical metadata, such as PolymorphismOptions.
/// This ensures that if multiple AOT JSON contexts define derived types or apply modifiers to the same base type,
/// all of them are correctly aggregated.
/// </summary>
public sealed class CrdtAggregateJsonTypeInfoResolver : IJsonTypeInfoResolver
{
    private readonly IReadOnlyList<IJsonTypeInfoResolver> _resolvers;

    /// <summary>
    /// Initializes a new instance of the <see cref="CrdtAggregateJsonTypeInfoResolver"/> class.
    /// </summary>
    /// <param name="resolvers">The collection of resolvers to aggregate.</param>
    public CrdtAggregateJsonTypeInfoResolver(IEnumerable<IJsonTypeInfoResolver> resolvers)
    {
        ArgumentNullException.ThrowIfNull(resolvers);
        _resolvers = resolvers.Where(r => r != null).ToList();
    }

    /// <inheritdoc />
    public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        JsonTypeInfo? primaryInfo = null;

        foreach (var resolver in _resolvers)
        {
            var info = resolver.GetTypeInfo(type, options);
            if (info != null)
            {
                if (primaryInfo == null)
                {
                    primaryInfo = info;
                }
                else
                {
                    // Merge polymorphism options from subsequent resolvers.
                    // This is crucial for fixing the issue where secondary contexts are ignored
                    // by JsonTypeInfoResolver.Combine when resolving common base polymorphic types.
                    if (info.PolymorphismOptions != null)
                    {
                        primaryInfo.PolymorphismOptions ??= new JsonPolymorphismOptions
                        {
                            IgnoreUnrecognizedTypeDiscriminators = info.PolymorphismOptions.IgnoreUnrecognizedTypeDiscriminators,
                            UnknownDerivedTypeHandling = info.PolymorphismOptions.UnknownDerivedTypeHandling,
                            TypeDiscriminatorPropertyName = info.PolymorphismOptions.TypeDiscriminatorPropertyName
                        };

                        foreach (var derivedType in info.PolymorphismOptions.DerivedTypes)
                        {
                            if (!primaryInfo.PolymorphismOptions.DerivedTypes.Any(d => d.DerivedType == derivedType.DerivedType))
                            {
                                primaryInfo.PolymorphismOptions.DerivedTypes.Add(derivedType);
                            }
                        }
                    }
                }
            }
        }

        return primaryInfo;
    }
}