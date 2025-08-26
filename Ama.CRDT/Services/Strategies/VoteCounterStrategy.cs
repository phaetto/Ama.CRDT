namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Models;
using Ama.CRDT.Services.Helpers;
using Microsoft.Extensions.Options;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text.Json;

[Commutative]
[Associative]
[Idempotent]
[Mergeable]
public sealed class VoteCounterStrategy(IOptions<CrdtOptions> options, ICrdtTimestampProvider timestampProvider) : ICrdtStrategy
{
    private readonly string replicaId = options.Value.ReplicaId;
    private static readonly JsonSerializerOptions DefaultJsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public void GeneratePatch([DisallowNull] ICrdtPatcher patcher, [DisallowNull] List<CrdtOperation> operations, [DisallowNull] string path, [DisallowNull] PropertyInfo property, object? originalValue, object? modifiedValue, object? originalRoot, object? modifiedRoot, [DisallowNull] CrdtMetadata originalMeta, [DisallowNull] CrdtMetadata modifiedMeta)
    {
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
            var newTimestamp = timestampProvider.Now();

            if (originalMeta.Lww.TryGetValue(voterMetaPath, out var lastTimestamp) && newTimestamp.CompareTo(lastTimestamp) <= 0)
            {
                continue;
            }

            var payload = new VotePayload(voter, newOption);
            var operation = new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, payload, newTimestamp);
            operations.Add(operation);
            
            modifiedMeta.Lww[voterMetaPath] = newTimestamp;
        }
    }

    public void ApplyOperation([DisallowNull] object root, [DisallowNull] CrdtMetadata metadata, CrdtOperation operation)
    {
        var payload = DeserializePayload(operation.Value);
        var voterMetaPath = $"{operation.JsonPath}.['{GetVoterKey(payload.Voter)}']";

        if (metadata.Lww.TryGetValue(voterMetaPath, out var currentTimestamp) && operation.Timestamp.CompareTo(currentTimestamp) <= 0)
        {
            return;
        }

        var (parentNode, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath);
        
        if (property is null || parentNode is null) return;
        
        if (property.GetValue(parentNode) is not IDictionary dictionary) return;

        var dictKeyType = property.PropertyType.GetGenericArguments()[0];
        var dictValueType = property.PropertyType.GetGenericArguments()[1];
        var voterType = dictValueType.GetGenericArguments()[0];

        var voter = DeserializeObject(payload.Voter, voterType);
        var newOption = DeserializeObject(payload.Option, dictKeyType);

        if (voter is null || newOption is null) return;

        RemoveVoterFromAllOptions(dictionary, voter);
        AddVoterToOption(dictionary, voter, newOption, dictValueType);
        
        metadata.Lww[voterMetaPath] = operation.Timestamp;
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
                    count = (int)(countProperty.GetValue(voterCollection) ?? -1);
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

    private VotePayload DeserializePayload(object? value)
    {
        return value switch
        {
            VotePayload p => p,
            JsonElement e => e.Deserialize<VotePayload>(DefaultJsonSerializerOptions),
            _ => throw new InvalidCastException($"Unsupported payload type for VoteCounterStrategy: {value?.GetType().Name}")
        };
    }
    
    private object? DeserializeObject(object? obj, Type targetType)
    {
        if (obj is JsonElement je)
        {
            return je.Deserialize(targetType, DefaultJsonSerializerOptions);
        }
        return obj;
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
        return voter switch
        {
            string s => s,
            JsonElement { ValueKind: JsonValueKind.String } je => je.GetString() ?? "",
            JsonElement je => je.GetRawText(),
            _ => JsonSerializer.Serialize(voter, DefaultJsonSerializerOptions)
        };
    }
}