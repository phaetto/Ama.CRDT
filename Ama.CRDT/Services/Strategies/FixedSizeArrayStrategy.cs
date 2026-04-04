namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Attributes.Strategies.Semantic;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <inheritdoc/>
[CrdtSupportedType(typeof(IList))]
[CrdtSupportedIntent(typeof(SetIndexIntent))]
[Commutative]
[Associative]
[Idempotent]
[StateBased]
public sealed class FixedSizeArrayStrategy(
    ReplicaContext replicaContext,
    IEnumerable<CrdtAotContext> aotContexts) : ICrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;

    /// <inheritdoc/>
    public void GeneratePatch(GeneratePatchContext context)
    {
        var (operations, _, path, property, originalValue, modifiedValue, originalRoot, modifiedRoot, originalMeta, changeTimestamp, clock) = context;

        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(originalMeta);

        if (property.StrategyAttribute is not CrdtFixedSizeArrayStrategyAttribute attr)
        {
            return;
        }

        var originalList = (originalValue as IList)?.Cast<object>().ToList() ?? [];
        var modifiedList = (modifiedValue as IList)?.Cast<object>().ToList() ?? [];

        for (var i = 0; i < attr.Size; i++)
        {
            var elementPath = $"{path}[{i}]";
            var originalElement = i < originalList.Count ? originalList[i] : null;
            var modifiedElement = i < modifiedList.Count ? modifiedList[i] : null;

            if (Equals(originalElement, modifiedElement))
            {
                continue;
            }

            if (!originalMeta.States.TryGetValue(elementPath, out var baseState) || baseState is not CausalTimestamp originalTimestamp || originalTimestamp.Timestamp is null || changeTimestamp.CompareTo(originalTimestamp.Timestamp) >= 0)
            {
                operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, elementPath, OperationType.Upsert, modifiedElement, changeTimestamp, clock));
            }
        }
    }

    /// <inheritdoc/>
    public CrdtOperation GenerateOperation(GenerateOperationContext context)
    {
        if (context.Property.StrategyAttribute is not CrdtFixedSizeArrayStrategyAttribute attr)
        {
            throw new InvalidOperationException($"Property {context.Property.Name} is missing CrdtFixedSizeArrayStrategyAttribute.");
        }

        if (context.Intent is SetIndexIntent setIndexIntent)
        {
            if (setIndexIntent.Index < 0 || setIndexIntent.Index >= attr.Size)
            {
                throw new ArgumentOutOfRangeException(nameof(setIndexIntent.Index), $"Index must be between 0 and {attr.Size - 1}.");
            }

            var elementPath = $"{context.JsonPath}[{setIndexIntent.Index}]";
            return new CrdtOperation(Guid.NewGuid(), replicaId, elementPath, OperationType.Upsert, setIndexIntent.Value, context.Timestamp, context.Clock);
        }

        throw new NotSupportedException($"Intent {context.Intent.GetType().Name} is not supported for {this.GetType().Name}.");
    }

    /// <inheritdoc/>
    public CrdtOperationStatus ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        var resolution = PocoPathHelper.ResolvePath(root, operation.JsonPath, aotContexts);
        var parent = resolution.Parent;
        var property = resolution.Property;
        var index = resolution.FinalSegment;

        if (parent is null || property is null || index is null || property.Getter!(parent) is not IList list)
        {
            return CrdtOperationStatus.PathResolutionFailed;
        }

        if (metadata.States.TryGetValue(operation.JsonPath, out var baseState) && baseState is CausalTimestamp currentTimestamp && currentTimestamp.Timestamp is not null &&
            operation.Timestamp.CompareTo(currentTimestamp.Timestamp) < 0)
        {
            return CrdtOperationStatus.Obsolete;
        }

        var elementType = PocoPathHelper.GetTypeInfo(property.PropertyType, aotContexts).CollectionElementType ?? typeof(object);
        if (elementType is null)
        {
            return CrdtOperationStatus.StrategyApplicationFailed;
        }

        metadata.States[operation.JsonPath] = new CausalTimestamp(operation.Timestamp, operation.ReplicaId, operation.Clock);

        var value = PocoPathHelper.ConvertValue(operation.Value, elementType, aotContexts);
        var elementIndex = (int)index;

        while (list.Count <= elementIndex)
        {
            list.Add(PocoPathHelper.GetDefaultValue(elementType, aotContexts));
        }

        list[elementIndex] = value;

        return CrdtOperationStatus.Success;
    }

    /// <inheritdoc/>
    public void Compact(CompactionContext context)
    {
        // FixedSizeArrayStrategy uses LWW metadata for explicit indices but does not maintain tombstones for deleted elements
        // as the array size is fixed. Therefore, there is no metadata to prune safely.
    }
}