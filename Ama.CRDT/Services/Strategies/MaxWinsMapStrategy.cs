namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Partitioning;
using Ama.CRDT.Services.Providers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ama.CRDT.Services;
using Ama.CRDT.Attributes.Strategies.Semantic;

/// <summary>
/// Implements a Max-Wins Map strategy. For each key, conflicts are resolved by choosing the highest value.
/// This strategy makes the map's keys grow-only; removals are not propagated.
/// </summary>
[CrdtSupportedType(typeof(IDictionary))]
[CrdtSupportedIntent(typeof(MapSetIntent))]
[Commutative]
[Associative]
[Idempotent]
[StateBased]
public sealed class MaxWinsMapStrategy(
    IElementComparerProvider comparerProvider,
    ReplicaContext replicaContext) : IPartitionableCrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;

    /// <inheritdoc/>
    public void GeneratePatch(GeneratePatchContext context)
    {
        var (operations, _, path, property, originalValue, modifiedValue, _, _, _, changeTimestamp, clock) = context;

        var originalDict = originalValue as IDictionary;
        var modifiedDict = modifiedValue as IDictionary;

        if (originalDict is null && modifiedDict is null) return;

        var keyType = PocoPathHelper.GetDictionaryKeyType(property);
        var comparer = comparerProvider.GetComparer(keyType);

        var allKeys = (originalDict?.Keys.Cast<object>() ?? Enumerable.Empty<object>())
            .Union(modifiedDict?.Keys.Cast<object>() ?? Enumerable.Empty<object>(), comparer)
            .ToHashSet(comparer);
        
        foreach (var key in allKeys)
        {
            var originalExists = originalDict?.Contains(key) ?? false;
            var modifiedExists = modifiedDict?.Contains(key) ?? false;

            if (originalExists && !modifiedExists) continue;

            var originalItemValue = originalExists ? originalDict![key] : null;
            var modifiedItemValue = modifiedExists ? modifiedDict![key] : null;

            if (modifiedExists && (originalItemValue is null || (originalItemValue is IComparable o && o.CompareTo(modifiedItemValue) < 0)))
            {
                var payload = new KeyValuePair<object, object?>(key, modifiedItemValue);
                operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, payload, changeTimestamp, clock));
            }
        }
    }

    /// <inheritdoc/>
    public CrdtOperation GenerateOperation(GenerateOperationContext context)
    {
        if (context.Intent is MapSetIntent mapSetIntent)
        {
            var payload = new KeyValuePair<object, object?>(mapSetIntent.Key, mapSetIntent.Value);
            return new CrdtOperation(
                Guid.NewGuid(),
                replicaId,
                context.JsonPath,
                OperationType.Upsert,
                payload,
                context.Timestamp,
                context.Clock);
        }

        throw new NotSupportedException($"Intent {context.Intent.GetType().Name} is not supported by {nameof(MaxWinsMapStrategy)}.");
    }

    /// <inheritdoc/>
    public void ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        if (operation.Type != OperationType.Upsert) return;

        var (parent, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath);
        if (parent is null || property is null || PocoPathHelper.GetAccessor(property).Getter(parent) is not IDictionary dict) return;
        
        var keyType = PocoPathHelper.GetDictionaryKeyType(property);
        var valueType = PocoPathHelper.GetDictionaryValueType(property);

        if (PocoPathHelper.ConvertValue(operation.Value, typeof(KeyValuePair<object, object?>)) is not KeyValuePair<object, object?> payload)
        {
            return;
        }
        
        var itemKey = PocoPathHelper.ConvertValue(payload.Key, keyType);
        if (itemKey is null) return;

        var incomingValue = PocoPathHelper.ConvertValue(payload.Value, valueType);
        if (incomingValue is not IComparable) return;
        
        if (dict.Contains(itemKey))
        {
            var currentValue = dict[itemKey];
            if (currentValue is IComparable currentComparable && currentComparable.CompareTo(incomingValue) < 0)
            {
                dict[itemKey] = incomingValue;
            }
        }
        else
        {
            dict[itemKey] = incomingValue;
        }
    }

    /// <inheritdoc/>
    public IComparable? GetStartKey(object data, PropertyInfo partitionableProperty)
    {
        var dict = PocoPathHelper.GetAccessor(partitionableProperty).Getter(data) as IDictionary;
        if (dict == null || dict.Count == 0) return null;

        return dict.Keys.Cast<IComparable>().OrderBy(k => k).FirstOrDefault();
    }

    /// <inheritdoc/>
    public IComparable? GetKeyFromOperation(CrdtOperation operation, string partitionablePropertyPath)
    {
        if (!operation.JsonPath.StartsWith(partitionablePropertyPath, StringComparison.Ordinal)) return null;

        var payloadObj = PocoPathHelper.ConvertValue(operation.Value, typeof(KeyValuePair<object, object?>));
        if (payloadObj is KeyValuePair<object, object?> payload)
        {
            return payload.Key as IComparable ?? payload.Key?.ToString() as IComparable;
        }

        return null;
    }

    /// <inheritdoc/>
    public IComparable GetMinimumKey(PropertyInfo partitionableProperty)
    {
        var keyType = PocoPathHelper.GetDictionaryKeyType(partitionableProperty);
        return GetMinimumKeyForType(keyType);
    }

    /// <inheritdoc/>
    public SplitResult Split(object originalData, CrdtMetadata originalMetadata, PropertyInfo partitionableProperty)
    {
        var documentType = partitionableProperty.DeclaringType!;
        var path = $"$.{char.ToLowerInvariant(partitionableProperty.Name[0])}{partitionableProperty.Name[1..]}";

        var dict = PocoPathHelper.GetAccessor(partitionableProperty).Getter(originalData) as IDictionary;
        if (dict == null || dict.Count < 2)
        {
            throw new InvalidOperationException("Cannot split a partition with less than 2 items.");
        }

        var sortedKeys = dict.Keys.Cast<IComparable>().OrderBy(k => k).ToList();
        var splitIndex = sortedKeys.Count / 2;
        var splitKey = sortedKeys[splitIndex];

        var keys1 = sortedKeys.Take(splitIndex).ToHashSet();
        var keys2 = sortedKeys.Skip(splitIndex).ToHashSet();

        var doc1 = Activator.CreateInstance(documentType)!;
        var doc2 = Activator.CreateInstance(documentType)!;

        ReconstructDictionaryForSplitMerge(doc1, path, dict, keys1, partitionableProperty);
        ReconstructDictionaryForSplitMerge(doc2, path, dict, keys2, partitionableProperty);

        return new SplitResult(new PartitionContent(doc1, originalMetadata.DeepClone()), new PartitionContent(doc2, originalMetadata.DeepClone()), splitKey);
    }

    /// <inheritdoc/>
    public PartitionContent Merge(object data1, CrdtMetadata meta1, object data2, CrdtMetadata meta2, PropertyInfo partitionableProperty)
    {
        var documentType = partitionableProperty.DeclaringType!;
        var path = $"$.{char.ToLowerInvariant(partitionableProperty.Name[0])}{partitionableProperty.Name[1..]}";

        var mergedDoc = Activator.CreateInstance(documentType)!;
        var mergedMeta = CrdtMetadata.Merge(meta1, meta2);

        var dict1 = PocoPathHelper.GetAccessor(partitionableProperty).Getter(data1) as IDictionary;
        var dict2 = PocoPathHelper.GetAccessor(partitionableProperty).Getter(data2) as IDictionary;

        var (parent, property, _) = PocoPathHelper.ResolvePath(mergedDoc, path);
        if (parent is not null && property is not null)
        {
            var dictType = typeof(Dictionary<,>).MakeGenericType(PocoPathHelper.GetDictionaryKeyType(partitionableProperty), PocoPathHelper.GetDictionaryValueType(partitionableProperty));
            var mergedDict = (IDictionary)Activator.CreateInstance(dictType)!;

            if (dict1 != null)
            {
                foreach (DictionaryEntry entry in dict1) mergedDict[entry.Key] = entry.Value;
            }
            if (dict2 != null)
            {
                foreach (DictionaryEntry entry in dict2)
                {
                    if (mergedDict.Contains(entry.Key))
                    {
                        var v1 = mergedDict[entry.Key] as IComparable;
                        var v2 = entry.Value as IComparable;
                        if (v1 != null && v2 != null && v2.CompareTo(v1) > 0)
                        {
                            mergedDict[entry.Key] = entry.Value;
                        }
                    }
                    else
                    {
                        mergedDict[entry.Key] = entry.Value;
                    }
                }
            }
            PocoPathHelper.GetAccessor(property).Setter(parent, mergedDict);
        }

        return new PartitionContent(mergedDoc, mergedMeta);
    }

    private static void ReconstructDictionaryForSplitMerge(object root, string path, IDictionary sourceDict, HashSet<IComparable> keysToKeep, PropertyInfo partitionableProperty)
    {
        var (parent, property, _) = PocoPathHelper.ResolvePath(root, path);
        if (parent is null || property is null) return;

        var dictType = typeof(Dictionary<,>).MakeGenericType(PocoPathHelper.GetDictionaryKeyType(partitionableProperty), PocoPathHelper.GetDictionaryValueType(partitionableProperty));
        var dict = (IDictionary)Activator.CreateInstance(dictType)!;
        
        foreach (DictionaryEntry entry in sourceDict)
        {
            if (keysToKeep.Contains((IComparable)entry.Key))
            {
                dict[entry.Key] = entry.Value;
            }
        }

        PocoPathHelper.GetAccessor(property).Setter(parent, dict);
    }

    private static IComparable GetMinimumKeyForType(Type keyType)
    {
        if (keyType == typeof(string)) return string.Empty;
        if (keyType == typeof(int)) return int.MinValue;
        if (keyType == typeof(long)) return long.MinValue;
        if (keyType == typeof(short)) return short.MinValue;
        if (keyType == typeof(byte)) return byte.MinValue;
        if (keyType == typeof(Guid)) return Guid.Empty;
        if (keyType == typeof(DateTime)) return DateTime.MinValue;
        if (keyType == typeof(DateTimeOffset)) return DateTimeOffset.MinValue;
        if (keyType == typeof(char)) return char.MinValue;
        if (keyType == typeof(double)) return double.MinValue;
        if (keyType == typeof(float)) return float.MinValue;
        if (keyType == typeof(decimal)) return decimal.MinValue;

        if (keyType.IsValueType)
        {
            return (IComparable)Activator.CreateInstance(keyType)!;
        }

        throw new InvalidOperationException($"Cannot determine minimum key for type {keyType}.");
    }
}