namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Partitioning;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Ama.CRDT.Services;
using Ama.CRDT.Attributes.Strategies.Semantic;
using Ama.CRDT.Services.GarbageCollection;

[CrdtSupportedType(typeof(IDictionary))]
[CrdtSupportedIntent(typeof(VoteIntent))]
[Commutative]
[Associative]
[Idempotent]
[StateBased]
public sealed class VoteCounterStrategy(
    ReplicaContext replicaContext, 
    IEnumerable<CrdtAotContext> aotContexts) : IPartitionableCrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;

    public void GeneratePatch(GeneratePatchContext context)
    {
        var (operations, _, path, _, originalValue, modifiedValue, _, _, originalMeta, changeTimestamp, clock) = context;

        if (Equals(originalValue, modifiedValue))
        {
            return;
        }

        var oldVoterMap = FlattenVotes(originalValue);
        var newVoterMap = FlattenVotes(modifiedValue);

        foreach (var (voter, newOption) in newVoterMap)
        {
            if (oldVoterMap.TryGetValue(voter, out var oldOption) && Equals(oldOption, newOption))
            {
                continue;
            }

            var voterMetaPath = $"{path}.['{GetVoterKey(voter)}']";
            
            if (originalMeta.Lww.TryGetValue(voterMetaPath, out var lastTimestamp) && changeTimestamp.CompareTo(lastTimestamp.Timestamp) <= 0)
            {
                continue;
            }

            var payload = new VotePayload(voter, newOption);
            var operation = new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, payload, changeTimestamp, clock);
            operations.Add(operation);
        }
    }

    public CrdtOperation GenerateOperation(GenerateOperationContext context)
    {
        if (context.Intent is VoteIntent voteIntent)
        {
            if (voteIntent.Voter is null || voteIntent.Option is null)
            {
                throw new ArgumentException("Voter and Option must not be null for VoteIntent.");
            }

            var payload = new VotePayload(voteIntent.Voter, voteIntent.Option);
            return new CrdtOperation(
                Guid.NewGuid(), 
                replicaId, 
                context.JsonPath, 
                OperationType.Upsert, 
                payload, 
                context.Timestamp,
                context.Clock);
        }

        throw new NotSupportedException($"Explicit operation generation for intent '{context.Intent?.GetType().Name}' is not supported for {this.GetType().Name}.");
    }

    public CrdtOperationStatus ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        if (PocoPathHelper.ConvertValue(operation.Value, typeof(VotePayload), aotContexts) is not VotePayload payload)
        {
            return CrdtOperationStatus.StrategyApplicationFailed;
        }
        var voterMetaPath = $"{operation.JsonPath}.['{GetVoterKey(payload.Voter)}']";

        if (metadata.Lww.TryGetValue(voterMetaPath, out var currentTimestamp) && operation.Timestamp.CompareTo(currentTimestamp.Timestamp) <= 0)
        {
            return CrdtOperationStatus.Obsolete;
        }

        var (parentNode, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath, aotContexts);
        
        if (property is null || parentNode is null)
        {
            return CrdtOperationStatus.PathResolutionFailed;
        }
        
        if (property.Getter!(parentNode) is not IDictionary dictionary)
        {
            return CrdtOperationStatus.PathResolutionFailed;
        }

        var propTypeInfo = PocoPathHelper.GetTypeInfo(property.PropertyType, aotContexts);
        var dictKeyType = propTypeInfo.DictionaryKeyType ?? typeof(object);
        var dictValueType = propTypeInfo.DictionaryValueType ?? typeof(object);
        var voterType = PocoPathHelper.GetTypeInfo(dictValueType, aotContexts).CollectionElementType ?? typeof(object);

        var voter = PocoPathHelper.ConvertValue(payload.Voter, voterType, aotContexts);
        var newOption = PocoPathHelper.ConvertValue(payload.Option, dictKeyType, aotContexts);

        if (voter is null || newOption is null)
        {
            return CrdtOperationStatus.StrategyApplicationFailed;
        }

        RemoveVoterFromAllOptions(dictionary, voter, aotContexts);
        AddVoterToOption(dictionary, voter, newOption, dictValueType, aotContexts);
        
        metadata.Lww[voterMetaPath] = new CausalTimestamp(operation.Timestamp, operation.ReplicaId, operation.Clock);

        return CrdtOperationStatus.Success;
    }

    public void Compact(CompactionContext context)
    {
        if (context.Document is null) return;

        var (parent, property, _) = PocoPathHelper.ResolvePath(context.Document, context.PropertyPath, aotContexts);
        if (parent is null || property is null || property.Getter!(parent) is not IDictionary dict)
        {
            return;
        }

        var prefix = context.PropertyPath + ".['";
        var keysToRemove = new List<string>();

        var currentVoters = new HashSet<string>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in dict)
        {
            if (entry.Value is IEnumerable voters)
            {
                foreach (var voter in voters)
                {
                    if (voter != null)
                    {
                        currentVoters.Add(GetVoterKey(voter));
                    }
                }
            }
        }

        foreach (var kvp in context.Metadata.Lww)
        {
            if (kvp.Key.StartsWith(prefix, StringComparison.Ordinal) && kvp.Key.EndsWith("']", StringComparison.Ordinal))
            {
                var voterKey = kvp.Key.Substring(prefix.Length, kvp.Key.Length - prefix.Length - 2);
                
                if (!currentVoters.Contains(voterKey) && context.Policy.IsSafeToCompact(new CompactionCandidate(Timestamp: kvp.Value.Timestamp, ReplicaId: kvp.Value.ReplicaId, Version: kvp.Value.Clock)))
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
        }

        foreach (var key in keysToRemove)
        {
            context.Metadata.Lww.Remove(key);
        }
    }

    /// <inheritdoc/>
    public IComparable? GetStartKey(object data, CrdtPropertyInfo partitionableProperty)
    {
        var dict = partitionableProperty.Getter!(data) as IDictionary;
        if (dict == null) return null;

        var voters = new HashSet<IComparable>();
        foreach (DictionaryEntry entry in dict)
        {
            if (entry.Value is IEnumerable vList)
            {
                foreach (var v in vList)
                {
                    if (v is IComparable comp) voters.Add(comp);
                    else if (v != null) voters.Add(v.ToString()!);
                }
            }
        }
        return voters.OrderBy(v => v).FirstOrDefault();
    }

    /// <inheritdoc/>
    public IComparable? GetKeyFromOperation(CrdtOperation operation, string partitionablePropertyPath)
    {
        if (!operation.JsonPath.StartsWith(partitionablePropertyPath, StringComparison.Ordinal)) return null;

        var payloadObj = PocoPathHelper.ConvertValue(operation.Value, typeof(VotePayload), aotContexts);
        if (payloadObj is VotePayload vote)
        {
            return vote.Voter as IComparable ?? vote.Voter?.ToString() as IComparable;
        }
        return null;
    }

    /// <inheritdoc/>
    public IComparable GetMinimumKey(CrdtPropertyInfo partitionableProperty)
    {
        var typeInfo = PocoPathHelper.GetTypeInfo(partitionableProperty.PropertyType, aotContexts);
        var dictValueType = typeInfo.DictionaryValueType ?? typeof(object);
        var voterType = PocoPathHelper.GetTypeInfo(dictValueType, aotContexts).CollectionElementType ?? typeof(object);
        return GetMinimumKeyForType(voterType, aotContexts);
    }

    /// <inheritdoc/>
    public SplitResult Split(object originalData, CrdtMetadata originalMetadata, CrdtPropertyInfo partitionableProperty)
    {
        var documentType = originalData.GetType();
        var path = $"$.{char.ToLowerInvariant(partitionableProperty.Name[0])}{partitionableProperty.Name[1..]}";

        var dict = partitionableProperty.Getter!(originalData) as IDictionary;
        var allVoters = new HashSet<IComparable>();
        
        if (dict != null)
        {
            foreach (DictionaryEntry entry in dict)
            {
                if (entry.Value is IEnumerable vList)
                {
                    foreach (var v in vList)
                    {
                        if (v is IComparable comp) allVoters.Add(comp);
                        else if (v != null) allVoters.Add(v.ToString()!);
                    }
                }
            }
        }

        if (allVoters.Count < 2)
        {
            throw new InvalidOperationException("Cannot split a partition with less than 2 items.");
        }

        var sortedVoters = allVoters.OrderBy(v => v).ToList();
        var splitIndex = sortedVoters.Count / 2;
        var splitKey = sortedVoters[splitIndex];

        var voters1 = sortedVoters.Take(splitIndex).ToHashSet();
        var voters2 = sortedVoters.Skip(splitIndex).ToHashSet();

        var doc1 = PocoPathHelper.Instantiate(documentType, aotContexts)!;
        var doc2 = PocoPathHelper.Instantiate(documentType, aotContexts)!;

        var meta1 = originalMetadata.DeepClone();
        var meta2 = originalMetadata.DeepClone();

        var keysToRemove1 = meta1.Lww.Keys.Where(k => k.StartsWith(path + ".['")).ToList();
        foreach (var k in keysToRemove1) meta1.Lww.Remove(k);
        
        var keysToRemove2 = meta2.Lww.Keys.Where(k => k.StartsWith(path + ".['")).ToList();
        foreach (var k in keysToRemove2) meta2.Lww.Remove(k);

        foreach (var voter in voters1)
        {
            var voterMetaPath = $"{path}.['{GetVoterKey(voter)}']";
            if (originalMetadata.Lww.TryGetValue(voterMetaPath, out var ts)) meta1.Lww[voterMetaPath] = ts;
        }

        foreach (var voter in voters2)
        {
            var voterMetaPath = $"{path}.['{GetVoterKey(voter)}']";
            if (originalMetadata.Lww.TryGetValue(voterMetaPath, out var ts)) meta2.Lww[voterMetaPath] = ts;
        }

        if (dict != null)
        {
            ReconstructDictionaryForSplitMerge(doc1, path, dict, voters1, partitionableProperty, aotContexts);
            ReconstructDictionaryForSplitMerge(doc2, path, dict, voters2, partitionableProperty, aotContexts);
        }

        return new SplitResult(new PartitionContent(doc1, meta1), new PartitionContent(doc2, meta2), splitKey);
    }

    /// <inheritdoc/>
    public PartitionContent Merge(object data1, CrdtMetadata meta1, object data2, CrdtMetadata meta2, CrdtPropertyInfo partitionableProperty)
    {
        var documentType = data1.GetType();
        var path = $"$.{char.ToLowerInvariant(partitionableProperty.Name[0])}{partitionableProperty.Name[1..]}";

        var mergedDoc = PocoPathHelper.Instantiate(documentType, aotContexts)!;
        var mergedMeta = CrdtMetadata.Merge(meta1, meta2);

        var dict1 = partitionableProperty.Getter!(data1) as IDictionary;
        var dict2 = partitionableProperty.Getter!(data2) as IDictionary;

        var (parent, property, _) = PocoPathHelper.ResolvePath(mergedDoc, path, aotContexts);
        if (parent is not null && property is not null)
        {
            var propTypeInfo = PocoPathHelper.GetTypeInfo(partitionableProperty.PropertyType, aotContexts);
            var dictValueType = propTypeInfo.DictionaryValueType ?? typeof(object);
            
            var existingDict = property.Getter!(parent) as IDictionary;
            var concreteDictType = existingDict?.GetType() ?? dict1?.GetType() ?? dict2?.GetType() ?? property.PropertyType;
            var mergedDict = existingDict ?? (IDictionary)PocoPathHelper.Instantiate(concreteDictType, aotContexts);

            if (dict1 != null)
            {
                foreach (DictionaryEntry entry in dict1)
                {
                    if (entry.Value is IEnumerable vList)
                    {
                        foreach (var v in vList) AddVoterToOption(mergedDict, v, entry.Key, dictValueType, aotContexts);
                    }
                }
            }
            if (dict2 != null)
            {
                foreach (DictionaryEntry entry in dict2)
                {
                    if (entry.Value is IEnumerable vList)
                    {
                        foreach (var v in vList) AddVoterToOption(mergedDict, v, entry.Key, dictValueType, aotContexts);
                    }
                }
            }
            property.Setter!(parent, mergedDict);
        }

        return new PartitionContent(mergedDoc, mergedMeta);
    }

    private void ReconstructDictionaryForSplitMerge(object root, string path, IDictionary sourceDict, HashSet<IComparable> votersToKeep, CrdtPropertyInfo partitionableProperty, IEnumerable<CrdtAotContext> aotContexts)
    {
        var (parent, property, _) = PocoPathHelper.ResolvePath(root, path, aotContexts);
        if (parent is null || property is null) return;

        var existingDict = property.Getter!(parent) as IDictionary;
        var concreteDictType = existingDict?.GetType() ?? sourceDict.GetType();
        var dict = existingDict ?? (IDictionary)PocoPathHelper.Instantiate(concreteDictType, aotContexts);

        var propTypeInfo = PocoPathHelper.GetTypeInfo(partitionableProperty.PropertyType, aotContexts);
        var dictValueType = propTypeInfo.DictionaryValueType ?? typeof(object);

        foreach (DictionaryEntry entry in sourceDict)
        {
            if (entry.Value is IEnumerable vList)
            {
                foreach (var v in vList)
                {
                    var comparableVoter = v as IComparable ?? v?.ToString() as IComparable;
                    if (comparableVoter != null && votersToKeep.Contains(comparableVoter))
                    {
                        AddVoterToOption(dict, v!, entry.Key, dictValueType, aotContexts);
                    }
                }
            }
        }
        property.Setter!(parent, dict);
    }

    private static void RemoveVoterFromAllOptions(IDictionary dictionary, object voter, IEnumerable<CrdtAotContext> aotContexts)
    {
        var keys = dictionary.Keys.Cast<object>().ToList();
        foreach (var key in keys)
        {
            if (dictionary[key] is not IEnumerable voterCollection || voterCollection is string)
            {
                continue;
            }

            bool removed = false;

            // Handle IList-based collections (like List<T>) that are not fixed-size arrays for performance.
            if (voterCollection is IList list && !list.IsFixedSize)
            {
                if (list.Contains(voter))
                {
                    list.Remove(voter);
                    removed = true;
                }
            }
            else // Let PocoPathHelper handle AOT collection removal
            {
                if (voterCollection is IEnumerable enumCol && enumCol.Cast<object>().Contains(voter))
                {
                    PocoPathHelper.RemoveFromCollection(voterCollection, voter, aotContexts);
                    removed = true;
                }
            }
            
            if (removed)
            {
                var count = -1;
                if (voterCollection is ICollection col)
                {
                    count = col.Count;
                }
                else
                {
                    var typeInfo = PocoPathHelper.GetTypeInfo(voterCollection.GetType(), aotContexts);
                    if (typeInfo.Properties.TryGetValue("Count", out var countProp) && countProp.CanRead && countProp.PropertyType == typeof(int))
                    {
                        count = (int)(countProp.Getter!(voterCollection) ?? -1);
                    }
                }

                if (count == 0 || (count == -1 && !voterCollection.Cast<object>().Any()))
                {
                    dictionary.Remove(key);
                }
                
                return; // A voter can only be in one set at a time.
            }
        }
    }

    private static void AddVoterToOption(IDictionary dictionary, object voter, object newOption, Type dictValueType, IEnumerable<CrdtAotContext> aotContexts)
    {
        if (!dictionary.Contains(newOption))
        {
            dictionary[newOption] = PocoPathHelper.Instantiate(dictValueType, aotContexts);
        }

        var voterCollection = dictionary[newOption];
        if (voterCollection is null) return;

        // Handle IList-based collections (like List<T>) that are not fixed-size arrays.
        if (voterCollection is IList list && !voterCollection.GetType().IsArray)
        {
            list.Add(voter);
            return;
        }

        // Delegate to PocoPathHelper for AOT collection adding
        PocoPathHelper.AddToCollection(voterCollection, voter, aotContexts);
    }
    
    private IDictionary<object, object> FlattenVotes(object? value)
    {
        var map = new Dictionary<object, object>();
        if (value is not IDictionary dictionary) return map;

        foreach (DictionaryEntry entry in dictionary)
        {
            if (entry.Key is null || entry.Value is not IEnumerable voters) continue;
            foreach (var voter in voters)
            {
                if (voter != null)
                {
                    map[voter] = entry.Key;
                }
            }
        }
        return map;
    }

    private string GetVoterKey(object voter)
    {
        return voter.ToString() ?? "";
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