using Modern.CRDT.Models;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Modern.CRDT.Services;

public sealed class JsonCrdtApplicator : IJsonCrdtApplicator
{
    private static readonly Regex PathRegex = new(@"\.([^.\[\]]+)|\[(\d+)\]", RegexOptions.Compiled);

    public CrdtDocument ApplyPatch(CrdtDocument document, CrdtPatch patch)
    {
        if (patch.Operations is null || patch.Operations.Count == 0)
        {
            return new CrdtDocument(document.Data?.DeepClone(), document.Metadata?.DeepClone());
        }

        var resultData = document.Data?.DeepClone();
        var resultMeta = document.Metadata?.DeepClone();

        var rootOperation = patch.Operations.FirstOrDefault(op => op.JsonPath == "$");
        if (rootOperation != default)
        {
            ApplyRootOperation(ref resultData, ref resultMeta, rootOperation);
            // Root operations are exclusive.
            return new CrdtDocument(resultData, resultMeta);
        }

        var upserts = patch.Operations.Where(o => o.Type == OperationType.Upsert && o.JsonPath != "$").ToList();
        var removes = patch.Operations.Where(o => o.Type == OperationType.Remove && o.JsonPath != "$")
            .OrderByDescending(o => o.JsonPath)
            .ToList();

        foreach (var operation in upserts)
        {
            ApplySingleOperation(ref resultData, ref resultMeta, operation, createPath: true);
        }

        foreach (var operation in removes)
        {
            ApplySingleOperation(ref resultData, ref resultMeta, operation, createPath: false);
        }

        return new CrdtDocument(resultData, resultMeta);
    }

    private void ApplyRootOperation(ref JsonNode? dataNode, ref JsonNode? metaNode, CrdtOperation operation)
    {
        var existingTimestamp = GetTimestamp(metaNode);
        if (operation.Timestamp <= existingTimestamp)
        {
            return;
        }

        if (operation.Type == OperationType.Remove)
        {
            dataNode = null;
            metaNode = null;
        }
        else // Upsert
        {
            dataNode = operation.Value?.DeepClone();
            metaNode = JsonValue.Create(operation.Timestamp);
        }
    }

    private void ApplySingleOperation(ref JsonNode? dataNode, ref JsonNode? metaNode, CrdtOperation operation, bool createPath)
    {
        var pathSegments = ParsePath(operation.JsonPath);
        if (pathSegments.Count == 0) return;

        if (createPath)
        {
            var isArrayRoot = int.TryParse(pathSegments[0], out _);
            dataNode ??= isArrayRoot ? new JsonArray() : new JsonObject();
            metaNode ??= isArrayRoot ? new JsonArray() : new JsonObject();
        }

        if (dataNode is null || metaNode is null) return;
        
        RecursiveApply(dataNode, metaNode, new Queue<string>(pathSegments), operation, createPath);
    }

    private void RecursiveApply(JsonNode currentData, JsonNode currentMeta, Queue<string> pathSegments, CrdtOperation operation, bool createPath)
    {
        var segment = pathSegments.Dequeue();
        var isLastSegment = pathSegments.Count == 0;

        if (int.TryParse(segment, out var index))
        {
            HandleArrayNavigation(currentData, currentMeta, index, pathSegments, operation, createPath, isLastSegment);
        }
        else
        {
            HandleObjectNavigation(currentData, currentMeta, segment, pathSegments, operation, createPath, isLastSegment);
        }
    }

    private void HandleArrayNavigation(JsonNode currentData, JsonNode currentMeta, int index, Queue<string> pathSegments, CrdtOperation operation, bool createPath, bool isLastSegment)
    {
        if (currentData is not JsonArray dataArray || currentMeta is not JsonArray metaArray) return;

        if (isLastSegment)
        {
            ExecuteOperationOnArray(dataArray, metaArray, index, operation);
            return;
        }
        
        if (createPath)
        {
            while (dataArray.Count <= index) dataArray.Add(null);
            while (metaArray.Count <= index) metaArray.Add(null);
        }

        if (index >= dataArray.Count || dataArray[index] is null) return;
        
        var nextDataNode = dataArray[index];
        var nextMetaNode = metaArray[index];

        if (nextDataNode is null && createPath)
        {
            var nextSegment = pathSegments.Peek();
            var isNextArray = int.TryParse(nextSegment, out _);
            nextDataNode = isNextArray ? new JsonArray() : new JsonObject();
            nextMetaNode = isNextArray ? new JsonArray() : new JsonObject();
            dataArray[index] = nextDataNode;
            metaArray[index] = nextMetaNode;
        }
        
        if (nextDataNode is null || nextMetaNode is null) return;

        RecursiveApply(nextDataNode, nextMetaNode, pathSegments, operation, createPath);
    }

    private void HandleObjectNavigation(JsonNode currentData, JsonNode currentMeta, string key, Queue<string> pathSegments, CrdtOperation operation, bool createPath, bool isLastSegment)
    {
        if (currentData is not JsonObject dataObject || currentMeta is not JsonObject metaObject) return;

        if (isLastSegment)
        {
            ExecuteOperationOnObject(dataObject, metaObject, key, operation);
            return;
        }
        
        dataObject.TryGetPropertyValue(key, out var nextDataNode);
        metaObject.TryGetPropertyValue(key, out var nextMetaNode);
        
        if (nextDataNode is null && createPath)
        {
            var nextSegment = pathSegments.Peek();
            var isNextArray = int.TryParse(nextSegment, out _);
            nextDataNode = isNextArray ? new JsonArray() : new JsonObject();
            nextMetaNode = isNextArray ? new JsonArray() : new JsonObject();
            dataObject[key] = nextDataNode;
            metaObject[key] = nextMetaNode;
        }

        if (nextDataNode is null || nextMetaNode is null) return;

        RecursiveApply(nextDataNode, nextMetaNode, pathSegments, operation, createPath);
    }

    private void ExecuteOperationOnObject(JsonObject dataParent, JsonObject metaParent, string key, CrdtOperation operation)
    {
        metaParent.TryGetPropertyValue(key, out var existingMetaNode);
        var existingTimestamp = GetTimestamp(existingMetaNode);
        
        if (operation.Timestamp <= existingTimestamp) return;

        if (operation.Type == OperationType.Remove)
        {
            dataParent.Remove(key);
            metaParent.Remove(key);
        }
        else // Upsert
        {
            dataParent[key] = operation.Value?.DeepClone();
            metaParent[key] = JsonValue.Create(operation.Timestamp);
        }
    }

    private void ExecuteOperationOnArray(JsonArray dataParent, JsonArray metaParent, int index, CrdtOperation operation)
    {
        if (operation.Type == OperationType.Remove)
        {
            if (index < dataParent.Count)
            {
                dataParent.RemoveAt(index);
                metaParent.RemoveAt(index);
            }
            return;
        }
        
        while (dataParent.Count <= index) dataParent.Add(null);
        while (metaParent.Count <= index) metaParent.Add(null);
        
        var existingTimestamp = (index < metaParent.Count && metaParent[index] is not null) ? GetTimestamp(metaParent[index]) : 0;
        
        if (operation.Timestamp > existingTimestamp)
        {
            dataParent[index] = operation.Value?.DeepClone();
            metaParent[index] = JsonValue.Create(operation.Timestamp);
        }
    }

    private List<string> ParsePath(string jsonPath)
    {
        var segments = new List<string>();
        var matches = PathRegex.Matches(jsonPath);
        foreach (Match match in matches.Cast<Match>())
        {
            segments.Add(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value);
        }
        return segments;
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