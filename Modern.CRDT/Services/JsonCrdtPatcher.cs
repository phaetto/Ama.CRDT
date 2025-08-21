using Modern.CRDT.Models;
using System.Text.Json.Nodes;

namespace Modern.CRDT.Services;

public sealed class JsonCrdtPatcher : IJsonCrdtPatcher
{
    public CrdtPatch GeneratePatch(CrdtDocument from, CrdtDocument to)
    {
        var operations = new List<CrdtOperation>();
        CompareNodes("$", from.Data, from.Metadata, to.Data, to.Metadata, operations);
        return new CrdtPatch(operations);
    }

    private void CompareNodes(string path, JsonNode? fromData, JsonNode? fromMeta, JsonNode? toData, JsonNode? toMeta, List<CrdtOperation> operations)
    {
        if (JsonNode.DeepEquals(fromData, toData))
        {
            return;
        }

        if (fromData is JsonObject fromObj && toData is JsonObject toObj)
        {
            CompareJsonObjects(path, fromObj, fromMeta as JsonObject, toObj, toMeta as JsonObject, operations);
            return;
        }

        if (fromData is JsonArray fromArr && toData is JsonArray toArr)
        {
            CompareJsonArrays(path, fromArr, fromMeta as JsonArray, toArr, toMeta as JsonArray, operations);
            return;
        }

        var toTimestamp = GetTimestamp(toMeta);
        var fromTimestamp = GetTimestamp(fromMeta);

        if (toTimestamp <= fromTimestamp)
        {
            return;
        }

        if (toData is null)
        {
            operations.Add(new CrdtOperation(path, OperationType.Remove, null, toTimestamp));
        }
        else
        {
            operations.Add(new CrdtOperation(path, OperationType.Upsert, toData.DeepClone(), toTimestamp));
        }
    }
    
    private void CompareJsonObjects(string path, JsonObject fromData, JsonObject? fromMeta, JsonObject toData, JsonObject? toMeta, List<CrdtOperation> operations)
    {
        var allKeys = fromData.Select(kvp => kvp.Key).Union(toData.Select(kvp => kvp.Key)).ToHashSet();

        foreach (var key in allKeys)
        {
            var currentPath = $"{path}.{key}";
            fromData.TryGetPropertyValue(key, out var fromValue);
            toData.TryGetPropertyValue(key, out var toValue);
            
            JsonNode? fromMetaValue = null;
            fromMeta?.TryGetPropertyValue(key, out fromMetaValue);
            
            JsonNode? toMetaValue = null;
            toMeta?.TryGetPropertyValue(key, out toMetaValue);

            CompareNodes(currentPath, fromValue, fromMetaValue, toValue, toMetaValue, operations);
        }
    }

    private void CompareJsonArrays(string path, JsonArray fromData, JsonArray? fromMeta, JsonArray toData, JsonArray? toMeta, List<CrdtOperation> operations)
    {
        var fromCount = fromData.Count;
        var toCount = toData.Count;
        var maxCount = Math.Max(fromCount, toCount);

        for (var i = 0; i < maxCount; i++)
        {
            var currentPath = $"{path}[{i}]";
            var fromItem = i < fromCount ? fromData[i] : null;
            var toItem = i < toCount ? toData[i] : null;

            var fromMetaItem = (fromMeta is not null && i < fromMeta.Count) ? fromMeta[i] : null;
            var toMetaItem = (toMeta is not null && i < toMeta.Count) ? toMeta[i] : null;

            CompareNodes(currentPath, fromItem, fromMetaItem, toItem, toMetaItem, operations);
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