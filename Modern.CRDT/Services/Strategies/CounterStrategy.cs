namespace Modern.CRDT.Services.Strategies;

using Microsoft.Extensions.Options;
using Modern.CRDT.Models;
using Modern.CRDT.Services.Helpers;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json.Nodes;

/// <summary>
/// A CRDT strategy for handling numeric properties as counters.
/// It generates 'Increment' operations based on the delta between two values
/// and applies these operations by adding the delta to the current value.
/// </summary>
public sealed class CounterStrategy : ICrdtStrategy
{
    private readonly ICrdtTimestampProvider timestampProvider;
    private readonly string replicaId;

    /// <summary>
    /// Initializes a new instance of the <see cref="CounterStrategy"/> class.
    /// </summary>
    /// <param name="timestampProvider">The provider for generating timestamps.</param>
    /// <param name="options">Configuration options containing the replica ID.</param>
    public CounterStrategy(ICrdtTimestampProvider timestampProvider, IOptions<CrdtOptions> options)
    {
        this.timestampProvider = timestampProvider ?? throw new ArgumentNullException(nameof(timestampProvider));
        ArgumentNullException.ThrowIfNull(options?.Value);
        this.replicaId = options.Value.ReplicaId;
    }

    /// <inheritdoc/>
    public void GeneratePatch(IJsonCrdtPatcher patcher, List<CrdtOperation> operations, string path, PropertyInfo property, JsonNode? originalValue, JsonNode? modifiedValue, JsonNode? originalMetadata, JsonNode? modifiedMetadata)
    {
        var originalNumeric = GetNumericValue(originalValue);
        var modifiedNumeric = GetNumericValue(modifiedValue);

        var delta = modifiedNumeric - originalNumeric;

        if (delta == 0)
        {
            return;
        }
        
        var timestamp = GetTimestamp(modifiedMetadata);
        if (timestamp.CompareTo(EpochTimestamp.MinValue) == 0)
        {
            timestamp = timestampProvider.Now();
        }

        var operation = new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Increment, JsonValue.Create(delta), timestamp);
        operations.Add(operation);
    }

    /// <inheritdoc/>
    public void ApplyOperation(JsonNode rootNode, CrdtOperation operation)
    {
        ArgumentNullException.ThrowIfNull(rootNode);

        if (operation.Type != OperationType.Increment)
        {
            throw new InvalidOperationException($"CounterStrategy can only handle 'Increment' operations, but received '{operation.Type}'.");
        }
        
        var (dataParent, lastSegment) = JsonNodePathHelper.FindOrCreateParentNode(rootNode, operation.JsonPath);

        if (dataParent is null || string.IsNullOrEmpty(lastSegment)) return;

        var incrementValue = GetNumericValue(operation.Value);
        
        var existingNode = JsonNodePathHelper.GetChildNode(dataParent, lastSegment);
        var existingValue = GetNumericValue(existingNode);
        var newValue = existingValue + incrementValue;
        
        JsonNodePathHelper.SetChildNode(dataParent, lastSegment, JsonValue.Create(newValue));
    }
    
    private static ICrdtTimestamp GetTimestamp(JsonNode? metaNode)
    {
        if (metaNode is JsonValue value && value.TryGetValue<long>(out var timestamp))
        {
            return new EpochTimestamp(timestamp);
        }
        return EpochTimestamp.MinValue;
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