namespace Modern.CRDT.Services.Strategies;

using Microsoft.Extensions.Options;
using Modern.CRDT.Models;
using Modern.CRDT.Services.Helpers;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json.Nodes;

/// <summary>
/// Implements the Last-Writer-Wins (LWW) strategy. It generates an operation
/// only if the 'modified' value has a more recent timestamp than the 'original' value.
/// This strategy does not recurse into child nodes; it treats the given nodes as atomic values.
/// </summary>
public sealed class LwwStrategy : ICrdtStrategy
{
    private readonly string replicaId;

    /// <summary>
    /// Initializes a new instance of the <see cref="LwwStrategy"/> class.
    /// </summary>
    /// <param name="options">Configuration options containing the replica ID.</param>
    public LwwStrategy(IOptions<CrdtOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options?.Value);
        this.replicaId = options.Value.ReplicaId;
    }

    /// <inheritdoc/>
    public void GeneratePatch(IJsonCrdtPatcher patcher, List<CrdtOperation> operations, string path, PropertyInfo property, JsonNode? originalValue, JsonNode? modifiedValue, JsonNode? originalMetadata, JsonNode? modifiedMetadata)
    {
        if (JsonNode.DeepEquals(originalValue, modifiedValue))
        {
            return;
        }

        var modifiedTimestamp = GetTimestamp(modifiedMetadata);
        var originalTimestamp = GetTimestamp(originalMetadata);

        if (modifiedTimestamp.CompareTo(originalTimestamp) <= 0)
        {
            return;
        }

        var operation = new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, modifiedValue?.DeepClone(), modifiedTimestamp);
        
        if (modifiedValue is null)
        {
            operation = operation with { Type = OperationType.Remove, Value = null };
        }
        
        operations.Add(operation);
    }

    /// <inheritdoc/>
    public void ApplyOperation(JsonNode rootNode, CrdtOperation operation)
    {
        ArgumentNullException.ThrowIfNull(rootNode);

        var (dataParent, lastSegment) = JsonNodePathHelper.FindOrCreateParentNode(rootNode, operation.JsonPath);

        if (dataParent is null || string.IsNullOrEmpty(lastSegment)) return;
        
        if (operation.Type == OperationType.Remove)
        {
            JsonNodePathHelper.RemoveChildNode(dataParent, lastSegment);
        }
        else if (operation.Type == OperationType.Upsert)
        {
            JsonNodePathHelper.SetChildNode(dataParent, lastSegment, operation.Value?.DeepClone());
        }
    }

    private static ICrdtTimestamp GetTimestamp(JsonNode? metaNode)
    {
        if (metaNode is JsonValue value && value.TryGetValue<long>(out var timestamp))
        {
            return new EpochTimestamp(timestamp);
        }

        return EpochTimestamp.MinValue;
    }
}