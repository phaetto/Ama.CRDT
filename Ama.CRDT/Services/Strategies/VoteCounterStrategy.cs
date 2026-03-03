namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Partitioning;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ama.CRDT.Services;

[CrdtSupportedType(typeof(IDictionary))]
[CrdtSupportedIntent(typeof(VoteIntent))]
[Commutative]
[Associative]
[Idempotent]
[StateBased]
public sealed class VoteCounterStrategy(ReplicaContext replicaContext) : IPartitionableCrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;

    public void GeneratePatch(GeneratePatchContext context)
    {
        var (operations, _, path, _, originalValue, modifiedValue, _, _, originalMeta, changeTimestamp) = context;

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
            
            if (originalMeta.Lww.TryGetValue(voterMetaPath, out var lastTimestamp) && changeTimestamp.CompareTo(lastTimestamp) <= 0)
            {
                continue;
            }

            var payload = new VotePayload(voter, newOption);
            var operation = new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, payload, changeTimestamp);
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
                context.ReplicaId, 
                context.JsonPath, 
                OperationType.Upsert, 
                payload, 
                context.Timestamp);
        }

        throw new NotSupportedException($"Explicit operation generation for intent '{context.Intent?.GetType().Name}' is not supported for {this.GetType().Name}.");
    }

    public void ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        if (PocoPathHelper.ConvertValue(operation.Value, typeof(VotePayload)) is not VotePayload payload)
        {
            return;
        }
        var voterMetaPath = $"{operation.JsonPath}.['{GetVoterKey(payload.Voter)}']";

        if (metadata.Lww.TryGetValue(voterMetaPath, out var currentTimestamp) && operation.Timestamp.CompareTo(currentTimestamp) <= 0)
        {
            return;
        }

        var (parentNode, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath);
        
        if (property is null || parentNode is null) return;
        
        if (PocoPathHelper.GetAccessor(property).Getter(parentNode) is not IDictionary dictionary) return;

        var dictKeyType = PocoPathHelper.GetDictionaryKeyType(property);
        var dictValueType = PocoPathHelper.GetDictionaryValueType(property);
        var voterType = dictValueType.GetGenericArguments()[0];

        var voter = PocoPathHelper.ConvertValue(payload.Voter, voterType);
        var newOption = PocoPathHelper.ConvertValue(payload.Option, dictKeyType);

        if (voter is null || newOption is null) return;

        RemoveVoterFromAllOptions(dictionary, voter);
        AddVoterToOption(dictionary, voter, newOption, dictValueType);
        
        metadata.Lww[voterMetaPath] = operation.Timestamp;
    }

    /// <inheritdoc/>
    public IComparable? GetStartKey(object data, PropertyInfo partitionableProperty)
    {
        var dict = PocoPathHelper.GetAccessor(partitionableProperty).Getter(data) as IDictionary;
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

        var payloadObj = PocoPathHelper.ConvertValue(operation.Value, typeof(VotePayload));
        if (payloadObj is VotePayload vote)
        {
            return vote.Voter as IComparable ?? vote.Voter?.ToString() as IComparable;
        }
        return null;
    }

    /// <inheritdoc/>
    public IComparable GetMinimumKey(PropertyInfo partitionableProperty)
    {
        var dictValueType = PocoPathHelper.GetDictionaryValueType(partitionableProperty);
        var voterType = dictValueType.GetGenericArguments()[0];
        return GetMinimumKeyForType(voterType);
    }

    /// <inheritdoc/>
    public SplitResult Split(object originalData, CrdtMetadata originalMetadata, PropertyInfo partitionableProperty)
    {
        var documentType = partitionableProperty.DeclaringType!;
        var path = $"$.{char.ToLowerInvariant(partitionableProperty.Name[0])}{partitionableProperty.Name[1..]}";

        var dict = PocoPathHelper.GetAccessor(partitionableProperty).Getter(originalData) as IDictionary;
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

        var doc1 = Activator.CreateInstance(documentType)!;
        var doc2 = Activator.CreateInstance(documentType)!;

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
            ReconstructDictionaryForSplitMerge(doc1, path, dict, voters1, partitionableProperty);
            ReconstructDictionaryForSplitMerge(doc2, path, dict, voters2, partitionableProperty);
        }

        return new SplitResult(new PartitionContent(doc1, meta1), new PartitionContent(doc2, meta2), splitKey);
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
            Type dictType = property.PropertyType;
            if (dictType.IsInterface)
            {
                dictType = typeof(Dictionary<,>).MakeGenericType(
                    PocoPathHelper.GetDictionaryKeyType(partitionableProperty),
                    PocoPathHelper.GetDictionaryValueType(partitionableProperty)
                );
            }
            var mergedDict = (IDictionary)Activator.CreateInstance(dictType)!;
            var dictValueType = PocoPathHelper.GetDictionaryValueType(partitionableProperty);

            if (dict1 != null)
            {
                foreach (DictionaryEntry entry in dict1)
                {
                    if (entry.Value is IEnumerable vList)
                    {
                        foreach (var v in vList) AddVoterToOption(mergedDict, v, entry.Key, dictValueType);
                    }
                }
            }
            if (dict2 != null)
            {
                foreach (DictionaryEntry entry in dict2)
                {
                    if (entry.Value is IEnumerable vList)
                    {
                        foreach (var v in vList) AddVoterToOption(mergedDict, v, entry.Key, dictValueType);
                    }
                }
            }
            PocoPathHelper.GetAccessor(property).Setter(parent, mergedDict);
        }

        return new PartitionContent(mergedDoc, mergedMeta);
    }

    private static void ReconstructDictionaryForSplitMerge(object root, string path, IDictionary sourceDict, HashSet<IComparable> votersToKeep, PropertyInfo partitionableProperty)
    {
        var (parent, property, _) = PocoPathHelper.ResolvePath(root, path);
        if (parent is null || property is null) return;

        Type dictType = property.PropertyType;
        if (dictType.IsInterface)
        {
            dictType = typeof(Dictionary<,>).MakeGenericType(
                PocoPathHelper.GetDictionaryKeyType(partitionableProperty),
                PocoPathHelper.GetDictionaryValueType(partitionableProperty)
            );
        }
        var dict = (IDictionary)Activator.CreateInstance(dictType)!;
        var dictValueType = PocoPathHelper.GetDictionaryValueType(partitionableProperty);

        foreach (DictionaryEntry entry in sourceDict)
        {
            if (entry.Value is IEnumerable vList)
            {
                foreach (var v in vList)
                {
                    var comparableVoter = v as IComparable ?? v?.ToString() as IComparable;
                    if (comparableVoter != null && votersToKeep.Contains(comparableVoter))
                    {
                        AddVoterToOption(dict, v!, entry.Key, dictValueType);
                    }
                }
            }
        }
        PocoPathHelper.GetAccessor(property).Setter(parent, dict);
    }

    private static void RemoveVoterFromAllOptions(IDictionary dictionary, object voter)
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
            else // For other collections (like HashSet<T>), use reflection.
            {
                var collectionType = voterCollection.GetType();
                var removeMethod = collectionType.GetMethod("Remove", [voter.GetType()]);
                if (removeMethod is null)
                {
                    removeMethod = collectionType.GetMethods().FirstOrDefault(m =>
                        m.Name == "Remove" &&
                        m.GetParameters().Length == 1 &&
                        m.GetParameters()[0].ParameterType.IsAssignableFrom(voter.GetType()));
                }

                if (removeMethod is not null)
                {
                    var result = removeMethod.Invoke(voterCollection, [voter]);
                    if (result is bool b)
                    {
                        removed = b;
                    }
                }
            }
            
            if (removed)
            {
                var countProperty = voterCollection.GetType().GetProperty("Count");
                var count = -1;
                if (countProperty?.CanRead == true && countProperty.PropertyType == typeof(int))
                {
                    count = (int)(PocoPathHelper.GetAccessor(countProperty).Getter(voterCollection) ?? -1);
                }

                if (count == 0 || (count == -1 && !voterCollection.Cast<object>().Any()))
                {
                    dictionary.Remove(key);
                }
                
                return; // A voter can only be in one set at a time.
            }
        }
    }

    private static void AddVoterToOption(IDictionary dictionary, object voter, object newOption, Type dictValueType)
    {
        if (!dictionary.Contains(newOption))
        {
            dictionary[newOption] = Activator.CreateInstance(dictValueType);
        }

        var voterCollection = dictionary[newOption];
        if (voterCollection is null) return;

        // Handle IList-based collections (like List<T>) that are not fixed-size arrays.
        if (voterCollection is IList list && !voterCollection.GetType().IsArray)
        {
            list.Add(voter);
            return;
        }
        
        // For other collections (like HashSet<T>), use reflection to find and invoke the Add method.
        // First, try for an exact match on the voter's type.
        var addMethod = voterCollection.GetType().GetMethod("Add", [voter.GetType()]);
        if (addMethod is not null)
        {
            addMethod.Invoke(voterCollection, [voter]);
            return;
        }

        // As a fallback, search for an Add method where the parameter is assignable from the voter's type.
        // This handles cases like adding a concrete type to a collection of a base type.
        addMethod = voterCollection.GetType().GetMethods().FirstOrDefault(m =>
            m.Name == "Add" &&
            m.GetParameters().Length == 1 &&
            m.GetParameters()[0].ParameterType.IsAssignableFrom(voter.GetType()));
        
        addMethod?.Invoke(voterCollection, [voter]);
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