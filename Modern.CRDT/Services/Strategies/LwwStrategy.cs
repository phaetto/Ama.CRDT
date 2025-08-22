namespace Modern.CRDT.Services.Strategies;

using Modern.CRDT.Models;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

/// <summary>
/// Implements the Last-Writer-Wins (LWW) strategy. It generates an operation
/// only if the 'modified' value has a more recent timestamp than the 'original' value.
/// This strategy does not recurse into child nodes; it treats the given nodes as atomic values.
/// </summary>
public sealed class LwwStrategy : ICrdtStrategy
{
    /// <inheritdoc/>
    public IEnumerable<CrdtOperation> GeneratePatch(string path, JsonNode? originalValue, JsonNode? modifiedValue, JsonNode? originalMetadata, JsonNode? modifiedMetadata)
    {
        if (JsonNode.DeepEquals(originalValue, modifiedValue))
        {
            yield break;
        }

        var modifiedTimestamp = GetTimestamp(modifiedMetadata);
        var originalTimestamp = GetTimestamp(originalMetadata);

        if (modifiedTimestamp <= originalTimestamp)
        {
            yield break;
        }

        if (modifiedValue is null)
        {
            yield return new CrdtOperation(path, OperationType.Remove, null, modifiedTimestamp);
        }
        else
        {
            yield return new CrdtOperation(path, OperationType.Upsert, modifiedValue.DeepClone(), modifiedTimestamp);
        }
    }

    /// <inheritdoc/>
    public void ApplyOperation(JsonNode rootNode, JsonNode metadataNode, CrdtOperation operation)
    {
        throw new NotImplementedException();
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