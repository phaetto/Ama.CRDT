namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies.Semantic;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Partitioning;
using Ama.CRDT.Services.Providers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

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
    ReplicaContext replicaContext,
    IEnumerable<CrdtAotContext> aotContexts) : IPartitionableCrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;

    /// <inheritdoc/>
    public void GeneratePatch(GeneratePatchContext context)
    {
        var (operations, _, path, property, originalValue, modifiedValue, _, _, _, changeTimestamp, clock) = context;

        var originalDict = originalValue as IDictionary;
        var modifiedDict = modifiedValue as IDictionary;

        if (originalDict is null && modifiedDict is null) return;

        var keyType = PocoPathHelper.GetTypeInfo(property.PropertyType, aotContexts).DictionaryKeyType ?? typeof(object);
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
    public CrdtOperationStatus ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        if (operation.Type != OperationType.Upsert) return CrdtOperationStatus.StrategyApplicationFailed;

        var (parent, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath, aotContexts);
        if (parent is null || property is null || property.Getter!(parent) is not IDictionary dict)
        {
            return CrdtOperationStatus.PathResolutionFailed;
        }
        
        var keyType = PocoPathHelper.GetTypeInfo(property.PropertyType, aotContexts).DictionaryKeyType ?? typeof(object);
        var valueType = PocoPathHelper.GetTypeInfo(property.PropertyType, aotContexts).DictionaryValueType ?? typeof(object);

        if (PocoPathHelper.ConvertValue(operation.Value, typeof(KeyValuePair<object, object?>), aotContexts) is not KeyValuePair<object, object?> payload)
        {
            return CrdtOperationStatus.StrategyApplicationFailed;
        }
        
        var itemKey = PocoPathHelper.ConvertValue(payload.Key, keyType, aotContexts);
        if (itemKey is null) return CrdtOperationStatus.StrategyApplicationFailed;

        var incomingValue = PocoPathHelper.ConvertValue(payload.Value, valueType, aotContexts);
        if (incomingValue is not IComparable) return CrdtOperationStatus.StrategyApplicationFailed;
        
        if (dict.Contains(itemKey))
        {
            var currentValue = dict[itemKey];
            if (currentValue is IComparable currentComparable && currentComparable.CompareTo(incomingValue) < 0)
            {
                dict[itemKey] = incomingValue;
            }
            else
            {
                return CrdtOperationStatus.Obsolete;
            }
        }
        else
        {
            dict[itemKey] = incomingValue;
        }

        return CrdtOperationStatus.Success;
    }

    /// <inheritdoc/>
    public void Compact(CompactionContext context)
    {
        // MaxWinsMapStrategy relies solely on value comparisons and does not maintain metadata or tombstones.
    }

    /// <inheritdoc/>
    public IComparable? GetStartKey(object data, CrdtPropertyInfo partitionableProperty)
    {
        var dict = partitionableProperty.Getter!(data) as IDictionary;
        if (dict == null || dict.Count == 0) return null;

        return dict.Keys.Cast<IComparable>().OrderBy(k => k).FirstOrDefault();
    }

    /// <inheritdoc/>
    public IComparable? GetKeyFromOperation(CrdtOperation operation, string partitionablePropertyPath)
    {
        if (!operation.JsonPath.StartsWith(partitionablePropertyPath, StringComparison.Ordinal)) return null;

        var payloadObj = PocoPathHelper.ConvertValue(operation.Value, typeof(KeyValuePair<object, object?>), aotContexts);
        if (payloadObj is KeyValuePair<object, object?> payload)
        {
            return payload.Key as IComparable ?? payload.Key?.ToString() as IComparable;
        }

        return null;
    }

    /// <inheritdoc/>
    public IComparable GetMinimumKey(CrdtPropertyInfo partitionableProperty)
    {
        var keyType = PocoPathHelper.GetTypeInfo(partitionableProperty.PropertyType, aotContexts).DictionaryKeyType ?? typeof(object);
        return GetMinimumKeyForType(keyType, aotContexts);
    }

    /// <inheritdoc/>
    public SplitResult Split(object originalData, CrdtMetadata originalMetadata, CrdtPropertyInfo partitionableProperty)
    {
        var documentType = originalData.GetType();

        var dict = partitionableProperty.Getter!(originalData) as IDictionary;
        if (dict == null || dict.Count < 2)
        {
            throw new InvalidOperationException("Cannot split a partition with less than 2 items.");
        }

        var sortedKeys = dict.Keys.Cast<IComparable>().OrderBy(k => k).ToList();
        var splitIndex = sortedKeys.Count / 2;
        var splitKey = sortedKeys[splitIndex];

        var keys1 = sortedKeys.Take(splitIndex).ToHashSet();
        var keys2 = sortedKeys.Skip(splitIndex).ToHashSet();

        var doc1 = PocoPathHelper.Instantiate(documentType, aotContexts)!;
        var doc2 = PocoPathHelper.Instantiate(documentType, aotContexts)!;

        ReconstructDictionaryForSplitMerge(doc1, dict, keys1, partitionableProperty, aotContexts);
        ReconstructDictionaryForSplitMerge(doc2, dict, keys2, partitionableProperty, aotContexts);

        return new SplitResult(new PartitionContent(doc1, originalMetadata.DeepClone()), new PartitionContent(doc2, originalMetadata.DeepClone()), splitKey);
    }

    /// <inheritdoc/>
    public PartitionContent Merge(object data1, CrdtMetadata meta1, object data2, CrdtMetadata meta2, CrdtPropertyInfo partitionableProperty)
    {
        var documentType = data1.GetType();

        var mergedDoc = PocoPathHelper.Instantiate(documentType, aotContexts)!;
        var mergedMeta = CrdtMetadata.Merge(meta1, meta2);

        var dict1 = partitionableProperty.Getter!(data1) as IDictionary;
        var dict2 = partitionableProperty.Getter!(data2) as IDictionary;

        var mergedDict = (IDictionary)PocoPathHelper.Instantiate(partitionableProperty.PropertyType, aotContexts);

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
        
        partitionableProperty.Setter!(mergedDoc, mergedDict);

        return new PartitionContent(mergedDoc, mergedMeta);
    }

    private static void ReconstructDictionaryForSplitMerge(object root, IDictionary sourceDict, HashSet<IComparable> keysToKeep, CrdtPropertyInfo partitionableProperty, IEnumerable<CrdtAotContext> aotContexts)
    {
        var dict = (IDictionary)PocoPathHelper.Instantiate(partitionableProperty.PropertyType, aotContexts);
        
        foreach (DictionaryEntry entry in sourceDict)
        {
            if (keysToKeep.Contains((IComparable)entry.Key))
            {
                dict[entry.Key] = entry.Value;
            }
        }

        partitionableProperty.Setter!(root, dict);
    }

    private static IComparable GetMinimumKeyForType(Type keyType, IEnumerable<CrdtAotContext> aotContexts)
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
            return (IComparable)PocoPathHelper.GetDefaultValue(keyType, aotContexts)!;
        }

        throw new InvalidOperationException($"Cannot determine minimum key for type {keyType}.");
    }
}