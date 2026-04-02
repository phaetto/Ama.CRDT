namespace Ama.CRDT.Services.Strategies.Decorators;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies.Semantic;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Decorators;
using Ama.CRDT.Models.Intents.Decorators;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Providers;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

/// <summary>
/// A decorator strategy that wraps another strategy with an epoch generation counter.
/// It drops incoming operations that have an epoch lower than the local epoch, enabling
/// "Clear-Wins" or "Reset" semantics for structures like Shopping Carts.
/// </summary>
[CrdtSupportedType(typeof(object))]
[CrdtSupportedIntent(typeof(EpochClearIntent))]
[Commutative]
[Associative]
[Idempotent]
[OperationBased]
public sealed class EpochBoundStrategy(IServiceProvider serviceProvider, ReplicaContext replicaContext) : ICrdtStrategy
{
    private ICrdtStrategy GetInnerStrategy(PropertyInfo property)
    {
        // We use IServiceProvider directly here to break a circular DI dependency
        // where EpochBoundStrategy requires ICrdtStrategyProvider, but ICrdtStrategyProvider 
        // constructs a dictionary of all registered ICrdtStrategy implementations.
        var provider = serviceProvider.GetRequiredService<ICrdtStrategyProvider>();
        return provider.GetInnerStrategy(property, typeof(EpochBoundStrategy));
    }

    /// <inheritdoc/>
    public void GeneratePatch(GeneratePatchContext context)
    {
        ArgumentNullException.ThrowIfNull(context.Property);

        var innerStrategy = GetInnerStrategy(context.Property);
        var localEpoch = GetEpochForPath(context.OriginalMeta, context.Path, out _);
        
        var innerOps = new List<CrdtOperation>();
        var innerContext = context with { Operations = innerOps };
        
        innerStrategy.GeneratePatch(innerContext);
        
        foreach (var op in innerOps)
        {
            context.Operations.Add(op with { Value = new EpochPayload(localEpoch, op.Value) });
        }
    }

    /// <inheritdoc/>
    public CrdtOperation GenerateOperation(GenerateOperationContext context)
    {
        ArgumentNullException.ThrowIfNull(context.Property);

        var localEpoch = GetEpochForPath(context.Metadata, context.JsonPath, out _);
        
        if (context.Intent is EpochClearIntent)
        {
            return new CrdtOperation(
                Guid.NewGuid(),
                replicaContext.ReplicaId,
                context.JsonPath,
                OperationType.Remove,
                new EpochPayload(localEpoch + 1, null),
                context.Timestamp,
                context.Clock);
        }
        
        var innerStrategy = GetInnerStrategy(context.Property);
        var innerOp = innerStrategy.GenerateOperation(context);
        
        return innerOp with { Value = new EpochPayload(localEpoch, innerOp.Value) };
    }

    /// <inheritdoc/>
    public CrdtOperationStatus ApplyOperation(ApplyOperationContext context)
    {
        if (context.Operation.Value is not EpochPayload payload)
        {
            if (context.Property is not null)
            {
                var inner = GetInnerStrategy(context.Property);
                return inner.ApplyOperation(context);
            }
            return CrdtOperationStatus.PathResolutionFailed;
        }

        var localEpoch = GetEpochForPath(context.Metadata, context.Operation.JsonPath, out var basePath);

        if (payload.Epoch < localEpoch)
        {
            // Ghost operation from an obsolete generation. Discard.
            return CrdtOperationStatus.Obsolete;
        }

        if (payload.Epoch > localEpoch)
        {
            // We entered a new epoch. Clear all metadata and local state.
            // MUST clear metadata before setting the new epoch so we don't clear the new value.
            ClearMetadataForPath(context.Metadata, basePath);
            context.Metadata.Epochs[basePath] = payload.Epoch;

            var propVal = PocoPathHelper.GetValue(context.Root, basePath);
            if (propVal is System.Collections.IList list && !list.IsFixedSize)
            {
                list.Clear();
            }
            else if (propVal is System.Collections.IDictionary dict)
            {
                dict.Clear();
            }
            else
            {
                var (_, bProperty, _) = PocoPathHelper.ResolvePath(context.Root, basePath, createMissing: false);
                if (bProperty?.CanWrite == true)
                {
                    PocoPathHelper.SetValue(context.Root, basePath, null);
                }
            }
        }

        // If it was just an EpochClearIntent, we already bumped the epoch and cleared the state.
        if (context.Operation.Type == OperationType.Remove && payload.Value is null)
        {
            return CrdtOperationStatus.Success;
        }

        // We must re-resolve the target property because we might have cleared it and it needs recreation
        var (target, property, finalSegment) = PocoPathHelper.ResolvePath(context.Root, context.Operation.JsonPath, createMissing: true);
        
        // Use context.Property, as `property` will be null if we resolved an element of a collection
        if (context.Property is null)
        {
            return CrdtOperationStatus.PathResolutionFailed;
        }

        var innerStrategy = GetInnerStrategy(context.Property);
        var innerOp = context.Operation with { Value = payload.Value };
        
        var innerContext = context with 
        { 
            Operation = innerOp,
            Target = target,
            Property = context.Property, // Retain the correct property info to resolve base strategy
            FinalSegment = finalSegment
        };
        
        return innerStrategy.ApplyOperation(innerContext);
    }

    /// <inheritdoc/>
    public void Compact(CompactionContext context)
    {
        // For decorators, we must delegate the compaction request down the chain to the inner strategy.
        if (context.Document is null) return;
        
        var (_, property, _) = PocoPathHelper.ResolvePath(context.Document, context.PropertyPath);
        if (property is not null)
        {
            var innerStrategy = GetInnerStrategy(property);
            innerStrategy.Compact(context);
        }
    }

    private static int GetEpochForPath(CrdtMetadata metadata, string fullPath, out string matchingPath)
    {
        int maxEpoch = 0;
        matchingPath = fullPath;
        
        foreach (var kvp in metadata.Epochs)
        {
            if (fullPath == kvp.Key || fullPath.StartsWith(kvp.Key + ".") || fullPath.StartsWith(kvp.Key + "["))
            {
                if (maxEpoch == 0 || kvp.Key.Length > matchingPath.Length)
                {
                    maxEpoch = kvp.Value;
                    matchingPath = kvp.Key;
                }
            }
        }
        
        return maxEpoch;
    }

    private static void ClearMetadataForPath(CrdtMetadata metadata, string path)
    {
        var prefix1 = path + ".";
        var prefix2 = path + "[";
        var isMatch = (string k) => k == path || k.StartsWith(prefix1) || k.StartsWith(prefix2);

        // Crucial: We must clear epochs for child paths as well, otherwise a lingering child 
        // will override the parent's new higher epoch during future operations!
        RemoveKeys(metadata.Epochs, isMatch);
        RemoveKeys(metadata.Lww, isMatch);
        RemoveKeys(metadata.Fww, isMatch);
        RemoveKeys(metadata.PositionalTrackers, isMatch);
        RemoveKeys(metadata.AverageRegisters, isMatch);
        RemoveKeys(metadata.TwoPhaseSets, isMatch);
        RemoveKeys(metadata.LwwSets, isMatch);
        RemoveKeys(metadata.FwwSets, isMatch);
        RemoveKeys(metadata.OrSets, isMatch);
        RemoveKeys(metadata.PriorityQueues, isMatch);
        RemoveKeys(metadata.LseqTrackers, isMatch);
        RemoveKeys(metadata.RgaTrackers, isMatch);
        RemoveKeys(metadata.LwwMaps, isMatch);
        RemoveKeys(metadata.FwwMaps, isMatch);
        RemoveKeys(metadata.OrMaps, isMatch);
        RemoveKeys(metadata.CounterMaps, isMatch);
        RemoveKeys(metadata.TwoPhaseGraphs, isMatch);
        RemoveKeys(metadata.ReplicatedTrees, isMatch);
    }

    private static void RemoveKeys<TValue>(IDictionary<string, TValue> dict, Func<string, bool> predicate)
    {
        var keysToRemove = dict.Keys.Where(predicate).ToList();
        foreach (var key in keysToRemove)
        {
            dict.Remove(key);
        }
    }
}