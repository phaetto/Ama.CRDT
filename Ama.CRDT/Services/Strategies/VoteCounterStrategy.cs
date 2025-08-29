namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Models;
using Ama.CRDT.Services.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Ama.CRDT.Services;

[CrdtSupportedType(typeof(IDictionary))]
[Commutative]
[Associative]
[Idempotent]
[Mergeable]
public sealed class VoteCounterStrategy(ReplicaContext replicaContext) : ICrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;

    public void GeneratePatch(GeneratePatchContext context)
    {
        var (patcher, operations, path, property, originalValue, modifiedValue, originalRoot, modifiedRoot, originalMeta, changeTimestamp) = context;

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
        
        if (property.GetValue(parentNode) is not IDictionary dictionary) return;

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
}