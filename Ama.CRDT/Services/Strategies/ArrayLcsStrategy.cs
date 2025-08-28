namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Models;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Providers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Ama.CRDT.Services;

/// <inheritdoc/>
[CrdtSupportedType(typeof(IEnumerable))]
[Commutative]
[Associative]
[IdempotentWithContinuousTime]
[SequentialOperations]
public sealed class ArrayLcsStrategy(
    IElementComparerProvider comparerProvider,
    ReplicaContext replicaContext) : ICrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;

    /// <inheritdoc/>
    public void GeneratePatch(GeneratePatchContext context)
    {
        var (patcher, operations, path, property, originalValue, modifiedValue, originalRoot, modifiedRoot, originalMeta, changeTimestamp) = context;

        var originalList = (originalValue as IList)?.Cast<object>().ToList() ?? new List<object>();
        var modifiedList = (modifiedValue as IList)?.Cast<object>().ToList() ?? new List<object>();

        if (originalList.SequenceEqual(modifiedList))
        {
            return;
        }

        var elementType = property.PropertyType.IsGenericType
            ? property.PropertyType.GetGenericArguments()[0]
            : property.PropertyType.GetElementType() ?? typeof(object);
        
        var comparer = comparerProvider.GetComparer(elementType);

        if (!originalMeta.PositionalTrackers.TryGetValue(path, out var originalPositions)
            || originalPositions.Count != originalList.Count
            || originalPositions.All(p => p.OperationId == Guid.Empty))
        {
            originalPositions = new List<PositionalIdentifier>();
            for (var i = 0; i < originalList.Count; i++)
            {
                originalPositions.Add(new PositionalIdentifier((i + 1).ToString(CultureInfo.InvariantCulture), Guid.Empty));
            }
        }
        
        var lcs = LongestCommonSubsequence(originalList, modifiedList, comparer);
        
        var lcsOriginalIndices = lcs.Select(t => t.Item1).ToHashSet();
        for (var i = 0; i < originalList.Count; i++)
        {
            if (!lcsOriginalIndices.Contains(i))
            {
                var removedIdentifier = originalPositions[i];
                operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Remove, removedIdentifier, changeTimestamp));
            }
        }

        var lcsWithBoundaries = new List<(int, int)> { (-1, -1) };
        lcsWithBoundaries.AddRange(lcs);
        lcsWithBoundaries.Add((originalList.Count, modifiedList.Count));

        for (var i = 0; i < lcsWithBoundaries.Count - 1; i++)
        {
            var start = lcsWithBoundaries[i];
            var end = lcsWithBoundaries[i+1];

            var beforePos = start.Item1 >= 0 ? originalPositions[start.Item1].Position : null;
            var afterPos = end.Item1 < originalList.Count ? originalPositions[end.Item1].Position : null;

            var lastGeneratedPos = beforePos;

            for (var modIdx = start.Item2 + 1; modIdx < end.Item2; modIdx++)
            {
                var newPos = GeneratePositionBetween(lastGeneratedPos, afterPos);
                var newItem = new PositionalItem(newPos, modifiedList[modIdx]);
                operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, newItem, changeTimestamp));
                lastGeneratedPos = newPos;
            }
        }
    }

    /// <inheritdoc/>
    public void ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        var (parent, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath);
        if (parent is null || property is null || property.GetValue(parent) is not IList list) return;

        if (!metadata.PositionalTrackers.TryGetValue(operation.JsonPath, out var positions))
        {
            positions = new List<PositionalIdentifier>();
            metadata.PositionalTrackers[operation.JsonPath] = positions;
        }

        if (list.Count > 0 && positions.Count == 0)
        {
            for (var i = 0; i < list.Count; i++)
            {
                positions.Add(new PositionalIdentifier((i + 1).ToString(CultureInfo.InvariantCulture), Guid.Empty));
            }
        }

        switch (operation.Type)
        {
            case OperationType.Upsert:
                ApplyUpsert(list, positions, operation);
                break;
            case OperationType.Remove:
                ApplyRemove(list, positions, operation);
                break;
        }
    }

    private void ApplyUpsert(IList list, List<PositionalIdentifier> positions, CrdtOperation operation)
    {
        if (operation.Value is null) return;

        string? position = null;
        object? value = null;

        if (operation.Value is PositionalItem pi)
        {
            position = pi.Position;
            value = pi.Value;
        }
        else if (operation.Value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in jsonElement.EnumerateObject())
            {
                if (string.Equals(property.Name, "Position", StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.String)
                {
                    position = property.Value.GetString();
                }
                else if (string.Equals(property.Name, "Value", StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                }
            }
        }

        if (position is null) return;

        var elementType = list.GetType().IsGenericType ? list.GetType().GetGenericArguments()[0] : typeof(object);
        var itemValue = DeserializeItemValue(value, elementType);

        var newIdentifier = new PositionalIdentifier(position, operation.Id);

        var index = positions.BinarySearch(newIdentifier);
        if (index < 0)
        {
            index = ~index;
        }
        
        positions.Insert(index, newIdentifier);
        list.Insert(index, itemValue);
    }
    
    private void ApplyRemove(IList list, List<PositionalIdentifier> positions, CrdtOperation operation)
    {
        if (operation.Value is null) return;

        PositionalIdentifier? identifierToRemove = null;

        if (operation.Value is PositionalIdentifier identifier)
        {
            identifierToRemove = identifier;
        }
        else if (operation.Value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
        {
            string? position = null;
            var operationId = Guid.Empty;

            foreach (var property in jsonElement.EnumerateObject())
            {
                if (string.Equals(property.Name, "Position", StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.String)
                {
                    position = property.Value.GetString();
                }
                else if (string.Equals(property.Name, "OperationId", StringComparison.OrdinalIgnoreCase) && property.Value.TryGetGuid(out var guid))
                {
                    operationId = guid;
                }
            }
            
            if (position is not null)
            {
                identifierToRemove = new PositionalIdentifier(position, operationId);
            }
        }

        if (identifierToRemove is null) return;
        
        var index = positions.IndexOf(identifierToRemove.Value);

        if (index >= 0)
        {
            positions.RemoveAt(index);
            list.RemoveAt(index);
        }
    }
    
    private static object? DeserializeItemValue(object? value, Type targetType)
    {
        if (value is null) return null;
        if (targetType.IsInstanceOfType(value)) return value;

        if (value is JsonElement jsonElement)
        {
            return JsonSerializer.Deserialize(jsonElement.GetRawText(), targetType);
        }

        try
        {
            return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            return null;
        }
    }
    
    private static string GeneratePositionBetween(string? posBefore, string? posAfter)
    {
        if (posBefore is not null && posBefore == posAfter)
        {
            return posBefore;
        }

        var decBefore = posBefore != null ? decimal.Parse(posBefore, CultureInfo.InvariantCulture) : 0m;
        var decAfter = posAfter != null ? decimal.Parse(posAfter, CultureInfo.InvariantCulture) : decBefore + 2m;
        
        if (decAfter <= decBefore) decAfter = decBefore + 2m;

        return ((decBefore + decAfter) / 2m).ToString(CultureInfo.InvariantCulture);
    }

    private static List<(int, int)> LongestCommonSubsequence(List<object> seq1, List<object> seq2, IEqualityComparer<object> comparer)
    {
        var lengths = new int[seq1.Count + 1, seq2.Count + 1];
        for (var i = 0; i < seq1.Count; i++)
        {
            for (var j = 0; j < seq2.Count; j++)
            {
                if (comparer.Equals(seq1[i], seq2[j]))
                {
                    lengths[i + 1, j + 1] = lengths[i, j] + 1;
                }
                else
                {
                    lengths[i + 1, j + 1] = Math.Max(lengths[i + 1, j], lengths[i, j + 1]);
                }
            }
        }

        var lcs = new List<(int, int)>();
        int x = seq1.Count, y = seq2.Count;
        while (x > 0 && y > 0)
        {
            if (comparer.Equals(seq1[x - 1], seq2[y - 1]))
            {
                lcs.Add((x - 1, y - 1));
                x--;
                y--;
            }
            else if (lengths[x - 1, y] > lengths[x, y - 1])
            {
                x--;
            }
            else
            {
                y--;
            }
        }
        lcs.Reverse();
        return lcs;
    }
}