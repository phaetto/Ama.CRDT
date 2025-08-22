namespace Modern.CRDT.Services.Strategies;

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
    /// <inheritdoc/>
    public void GeneratePatch(IJsonCrdtPatcher patcher, List<CrdtOperation> operations, string path, PropertyInfo property, JsonNode? originalValue, JsonNode? modifiedValue, JsonNode? originalMetadata, JsonNode? modifiedMetadata)
    {
        if (JsonNode.DeepEquals(originalValue, modifiedValue))
        {
            return;
        }

        var modifiedTimestamp = GetTimestamp(modifiedMetadata);
        var originalTimestamp = GetTimestamp(originalMetadata);

        if (modifiedTimestamp <= originalTimestamp)
        {
            return;
        }

        if (modifiedValue is null)
        {
            operations.Add(new CrdtOperation(path, OperationType.Remove, null, modifiedTimestamp));
        }
        else
        {
            operations.Add(new CrdtOperation(path, OperationType.Upsert, modifiedValue.DeepClone(), modifiedTimestamp));
        }
    }

    /// <inheritdoc/>
    public void ApplyOperation(JsonNode rootNode, CrdtOperation operation)
    {
        ArgumentNullException.ThrowIfNull(rootNode);
        ArgumentNullException.ThrowIfNull(operation);

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

    private static long GetTimestamp(JsonNode? metaNode)
    {
        if (metaNode is JsonValue value && value.TryGetValue<long>(out var timestamp))
        {
            return timestamp;
        }

        return 0;
    }
}