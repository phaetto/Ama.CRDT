namespace Modern.CRDT.Services.Strategies;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Modern.CRDT.Models;

/// <summary>
/// A CRDT strategy for handling numeric properties as counters.
/// It generates 'Increment' operations based on the delta between two values
/// and applies these operations by adding the delta to the current value.
/// </summary>
public sealed class CounterStrategy : ICrdtStrategy
{
    /// <inheritdoc/>
    public IEnumerable<CrdtOperation> GeneratePatch(string path, JsonNode? originalValue, JsonNode? modifiedValue, JsonNode? originalMetadata, JsonNode? modifiedMetadata)
    {
        var originalNumeric = GetNumericValue(originalValue);
        var modifiedNumeric = GetNumericValue(modifiedValue);

        var delta = modifiedNumeric - originalNumeric;

        if (delta == 0)
        {
            return Enumerable.Empty<CrdtOperation>();
        }
        
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var operation = new CrdtOperation(path, OperationType.Increment, JsonValue.Create(delta), timestamp);
        return [operation];
    }

    /// <inheritdoc/>
    public void ApplyOperation(JsonNode rootNode, JsonNode metadataNode, CrdtOperation operation)
    {
        ArgumentNullException.ThrowIfNull(rootNode);
        ArgumentNullException.ThrowIfNull(operation);

        if (operation.Type != OperationType.Increment)
        {
            throw new InvalidOperationException($"CounterStrategy can only handle 'Increment' operations, but received '{operation.Type}'.");
        }

        var (parentNode, propertyName) = FindParentNode(rootNode, operation.JsonPath);

        var incrementValue = GetNumericValue(operation.Value);
        var existingNode = parentNode[propertyName];
        var existingValue = GetNumericValue(existingNode);

        var newValue = existingValue + incrementValue;

        parentNode[propertyName] = JsonValue.Create(newValue);
    }

    private static (JsonObject parentNode, string propertyName) FindParentNode(JsonNode root, string jsonPath)
    {
        if (string.IsNullOrWhiteSpace(jsonPath) || !jsonPath.StartsWith("$."))
        {
            throw new ArgumentException("Invalid JSON path. Path must start with '$.' and specify a property.", nameof(jsonPath));
        }

        var segments = jsonPath.Substring(2).Split('.');
        if (segments.Length == 0 || segments.Any(string.IsNullOrEmpty))
        {
            throw new ArgumentException($"Invalid JSON path format: '{jsonPath}'", nameof(jsonPath));
        }

        var currentNode = root;

        for (var i = 0; i < segments.Length - 1; i++)
        {
            var segment = segments[i];
            if (currentNode is not JsonObject currentObject)
            {
                throw new InvalidOperationException($"Path segment '{segment}' in '{jsonPath}' requires an object, but the node is of type '{currentNode?.GetType().Name}'.");
            }

            var nextNode = currentObject[segment];
            if (nextNode == null)
            {
                throw new InvalidOperationException($"Path '{jsonPath}' does not exist in the document. Cannot find segment '{segment}'.");
            }
            currentNode = nextNode;
        }

        if (currentNode is not JsonObject parent)
        {
            throw new InvalidOperationException($"The parent path of '{jsonPath}' is not an object.");
        }

        return (parent, segments.Last());
    }

    private static decimal GetNumericValue(JsonNode? node)
    {
        if (node is null)
        {
            return 0;
        }

        if (node is not JsonValue jsonValue)
        {
            throw new InvalidOperationException($"Counter strategy requires a numeric JsonValue, but received a node of type {node.GetType().Name}.");
        }

        if (jsonValue.TryGetValue<decimal>(out var decValue)) return decValue;
        if (jsonValue.TryGetValue<int>(out var intValue)) return intValue;
        if (jsonValue.TryGetValue<long>(out var longValue)) return longValue;
        if (jsonValue.TryGetValue<double>(out var doubleValue)) return (decimal)doubleValue;
        if (jsonValue.TryGetValue<float>(out var floatValue)) return (decimal)floatValue;
        if (jsonValue.TryGetValue<short>(out var shortValue)) return shortValue;
        if (jsonValue.TryGetValue<byte>(out var byteValue)) return byteValue;

        throw new InvalidOperationException($"Counter strategy requires a numeric value, but the value '{jsonValue}' could not be converted.");
    }
}