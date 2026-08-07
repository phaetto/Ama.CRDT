namespace Ama.CRDT.Services;

using System;
using System.Collections.Generic;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Strategies;

/// <inheritdoc/>
public sealed class CrdtMerger(
    ICrdtStrategyProvider strategyProvider,
    IEnumerable<CrdtAotContext> aotContexts) : ICrdtMerger
{
    private readonly record struct MergeObjectContext(
        string Path,
        Type Type,
        object? PrimaryObj,
        object? SecondaryObj,
        object PrimaryRoot,
        object SecondaryRoot,
        CrdtMetadata PrimaryMeta,
        CrdtMetadata SecondaryMeta
    );

    /// <inheritdoc/>
    public void MergeState<T>(CrdtDocument<T> primary, CrdtDocument<T> secondary) where T : class
    {
        ArgumentNullException.ThrowIfNull(primary.Data);
        ArgumentNullException.ThrowIfNull(primary.Metadata);
        ArgumentNullException.ThrowIfNull(secondary.Data);
        ArgumentNullException.ThrowIfNull(secondary.Metadata);

        var queue = new Queue<MergeObjectContext>();
        queue.Enqueue(new MergeObjectContext(
            "$",
            typeof(T),
            primary.Data,
            secondary.Data,
            primary.Data,
            secondary.Data,
            primary.Metadata,
            secondary.Metadata
        ));

        while (queue.Count > 0)
        {
            var context = queue.Dequeue();

            if (context.PrimaryObj is null && context.SecondaryObj is null)
            {
                continue;
            }

            var typeInfo = PocoPathHelper.GetTypeInfo(context.Type, aotContexts);
            var isRoot = context.Path == "$";

            foreach (var propertyInfo in typeInfo.Properties.Values)
            {
                var currentPath = isRoot ? $"$.{propertyInfo.JsonName}" : $"{context.Path}.{propertyInfo.JsonName}";
                var pValue1 = context.PrimaryObj is not null && propertyInfo.CanRead ? propertyInfo.Getter!(context.PrimaryObj) : null;
                var pValue2 = context.SecondaryObj is not null && propertyInfo.CanRead ? propertyInfo.Getter!(context.SecondaryObj) : null;

                var propertyType = propertyInfo.PropertyType;
                var strategy = strategyProvider.GetStrategy(context.Type, propertyInfo);

                var isComplexLww = strategy is LwwStrategy 
                                   && propertyType.IsClass 
                                   && propertyType != typeof(string) 
                                   && !PocoPathHelper.IsCollection(propertyType);

                if (isComplexLww && pValue1 is not null && pValue2 is not null)
                {
                    // Natively recurse into POCO properties since both exist and need deep merging
                    queue.Enqueue(new MergeObjectContext(
                        currentPath, propertyType, pValue1, pValue2, context.PrimaryRoot, context.SecondaryRoot, context.PrimaryMeta, context.SecondaryMeta));
                }
                else if (context.PrimaryObj is not null && context.SecondaryObj is not null)
                {
                    // Delegate merging mathematical bounds to the specific strategy
                    var mergeContext = new MergeStateContext(
                        context.PrimaryObj,
                        context.PrimaryMeta,
                        context.SecondaryObj,
                        context.SecondaryMeta,
                        propertyInfo,
                        currentPath
                    );
                    strategy.MergeState(mergeContext);

                    // Recurse into dictionaries for deep merging of complex values inside the maps
                    if (pValue1 is System.Collections.IDictionary dict1 && pValue2 is System.Collections.IDictionary dict2)
                    {
                        var typeInfoDict = PocoPathHelper.GetTypeInfo(propertyType, aotContexts);
                        var valueType = typeInfoDict.DictionaryValueType;
                        if (valueType != null && valueType.IsClass && valueType != typeof(string))
                        {
                            foreach (System.Collections.DictionaryEntry entry in dict1)
                            {
                                if (dict2.Contains(entry.Key))
                                {
                                    var keyStr = entry.Key.ToString();
                                    var childPath = $"{currentPath}['{keyStr}']";
                                    queue.Enqueue(new MergeObjectContext(
                                        childPath, valueType, entry.Value, dict2[entry.Key], context.PrimaryRoot, context.SecondaryRoot, context.PrimaryMeta, context.SecondaryMeta));
                                }
                            }
                        }
                    }
                }
            }
        }

        MergeCausalHistory(primary.Metadata, secondary.Metadata);
    }

    private static void MergeCausalHistory(CrdtMetadata primary, CrdtMetadata secondary)
    {
        // Merge VersionVectors (element-wise max)
        foreach (var kvp in secondary.VersionVector)
        {
            if (primary.VersionVector.TryGetValue(kvp.Key, out var primaryClock))
            {
                primary.VersionVector[kvp.Key] = Math.Max(primaryClock, kvp.Value);
            }
            else
            {
                primary.VersionVector[kvp.Key] = kvp.Value;
            }
        }

        // Merge SeenExceptions (Union)
        foreach (var op in secondary.SeenExceptions)
        {
            primary.SeenExceptions.Add(op);
        }
    }
}