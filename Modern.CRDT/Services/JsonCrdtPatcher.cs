using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Modern.CRDT.Models;
using Modern.CRDT.Services.Helpers;
using Modern.CRDT.Services.Strategies;

namespace Modern.CRDT.Services;

public sealed class JsonCrdtPatcher(ICrdtStrategyManager strategyManager) : IJsonCrdtPatcher
{
    private readonly ICrdtStrategyManager strategyManager = strategyManager ?? throw new ArgumentNullException(nameof(strategyManager));
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false };

    public CrdtPatch GeneratePatch<T>(CrdtDocument<T> from, CrdtDocument<T> to) where T : class
    {
        var operations = new List<CrdtOperation>();
        var fromData = from.Data is not null ? JsonSerializer.SerializeToNode(from.Data, SerializerOptions)?.AsObject() : null;
        var toData = to.Data is not null ? JsonSerializer.SerializeToNode(to.Data, SerializerOptions)?.AsObject() : null;

        var fromLwwTree = BuildLwwMetadataTree(from.Metadata?.Lww);
        var toLwwTree = BuildLwwMetadataTree(to.Metadata?.Lww);

        DifferentiateObject("$", typeof(T), fromData, fromLwwTree, toData, toLwwTree, operations);

        return new CrdtPatch(operations);
    }

    public void DifferentiateObject(string path, Type type, JsonObject? fromData, JsonObject? fromMeta, JsonObject? toData, JsonObject? toMeta, List<CrdtOperation> operations)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetCustomAttribute<JsonIgnoreAttribute>() == null);

        foreach (var property in properties)
        {
            var jsonPropertyName = SerializerOptions.PropertyNamingPolicy?.ConvertName(property.Name) ?? property.Name;
            var currentPath = path == "$" ? $"$.{jsonPropertyName}" : $"{path}.{jsonPropertyName}";

            JsonNode? fromValue = null;
            fromData?.TryGetPropertyValue(jsonPropertyName, out fromValue);

            JsonNode? toValue = null;
            toData?.TryGetPropertyValue(jsonPropertyName, out toValue);

            JsonNode? fromMetaValue = null;
            fromMeta?.TryGetPropertyValue(jsonPropertyName, out fromMetaValue);

            JsonNode? toMetaValue = null;
            toMeta?.TryGetPropertyValue(jsonPropertyName, out toMetaValue);

            if (JsonNode.DeepEquals(fromValue?.DeepClone(), toValue?.DeepClone()))
            {
                continue;
            }

            var strategy = strategyManager.GetStrategy(property);

            if (strategy is LwwStrategy && property.PropertyType.IsClass && property.PropertyType != typeof(string) && !IsCollection(property.PropertyType))
            {
                DifferentiateObject(currentPath, property.PropertyType, fromValue?.AsObject(), fromMetaValue?.AsObject(), toValue?.AsObject(), toMetaValue?.AsObject(), operations);
            }
            else
            {
                strategy.GeneratePatch(this, operations, currentPath, property, fromValue, toValue, fromMetaValue, toMetaValue);
            }
        }
    }

    private static JsonObject? BuildLwwMetadataTree(IDictionary<string, ICrdtTimestamp>? lww)
    {
        if (lww is null || lww.Count == 0)
        {
            return null;
        }

        var root = new JsonObject();
        foreach (var (path, timestamp) in lww)
        {
            if (timestamp is not EpochTimestamp epochTimestamp)
            {
                continue;
            }

            var segments = JsonNodePathHelper.ParsePath(path);
            if (segments.Length == 0)
            {
                continue;
            }

            var currentNode = (JsonNode)root;

            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                var isLastSegment = i == segments.Length - 1;

                if (int.TryParse(segment, out var index)) // Array path
                {
                    if (currentNode is not JsonArray currentArray) break;
                    while (currentArray.Count <= index) currentArray.Add(null);

                    if (isLastSegment)
                    {
                        var existingNode = currentArray[index];
                        if (existingNode is not JsonObject && existingNode is not JsonArray)
                        {
                            currentArray[index] = JsonValue.Create(epochTimestamp.Value);
                        }
                    }
                    else
                    {
                        var nextSegmentIsIndex = i + 1 < segments.Length && int.TryParse(segments[i + 1], out _);
                        var nextNode = currentArray[index];

                        if (nextSegmentIsIndex)
                        {
                            if (nextNode is not JsonArray arrayNode)
                            {
                                arrayNode = new JsonArray();
                                currentArray[index] = arrayNode;
                            }
                            currentNode = arrayNode;
                        }
                        else // next is object
                        {
                            if (nextNode is not JsonObject objectNode)
                            {
                                objectNode = new JsonObject();
                                currentArray[index] = objectNode;
                            }
                            currentNode = objectNode;
                        }
                    }
                }
                else // Object path
                {
                    if (currentNode is not JsonObject currentObject) break;

                    if (isLastSegment)
                    {
                        currentObject.TryGetPropertyValue(segment, out var existingNode);
                        if (existingNode is not JsonObject && existingNode is not JsonArray)
                        {
                            currentObject[segment] = JsonValue.Create(epochTimestamp.Value);
                        }
                    }
                    else
                    {
                        var nextSegmentIsIndex = i + 1 < segments.Length && int.TryParse(segments[i + 1], out _);
                        currentObject.TryGetPropertyValue(segment, out var nextNode);

                        if (nextSegmentIsIndex)
                        {
                            if (nextNode is not JsonArray arrayNode)
                            {
                                arrayNode = new JsonArray();
                                currentObject[segment] = arrayNode;
                            }
                            currentNode = arrayNode;
                        }
                        else // next is object
                        {
                            if (nextNode is not JsonObject objectNode)
                            {
                                objectNode = new JsonObject();
                                currentObject[segment] = objectNode;
                            }
                            currentNode = objectNode;
                        }
                    }
                }
            }
        }

        return root;
    }

    private static bool IsCollection(Type type)
    {
        return type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);
    }
}