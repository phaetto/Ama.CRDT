namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services.Helpers;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Ama.CRDT.Services;

/// <inheritdoc/>
[CrdtSupportedType(typeof(IList))]
[CrdtSupportedIntent(typeof(SetIndexIntent))]
[Commutative]
[Associative]
[Idempotent]
[StateBased]
public sealed class FixedSizeArrayStrategy(
    ReplicaContext replicaContext) : ICrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;

    /// <inheritdoc/>
    public void GeneratePatch(GeneratePatchContext context)
    {
        var (operations, _, path, property, originalValue, modifiedValue, _, _, originalMeta, changeTimestamp) = context;

        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(originalMeta);

        if (property.GetCustomAttribute<CrdtFixedSizeArrayStrategyAttribute>() is not { } attr)
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

            if (!originalMeta.Lww.TryGetValue(elementPath, out var originalTimestamp) || changeTimestamp.CompareTo(originalTimestamp) >= 0)
            {
                operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, elementPath, OperationType.Upsert, modifiedElement, changeTimestamp));
            }
        }
    }

    /// <inheritdoc/>
    public CrdtOperation GenerateOperation(GenerateOperationContext context)
    {
        if (context.Property.GetCustomAttribute<CrdtFixedSizeArrayStrategyAttribute>() is not { } attr)
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
            return new CrdtOperation(Guid.NewGuid(), context.ReplicaId, elementPath, OperationType.Upsert, setIndexIntent.Value, context.Timestamp);
        }

        throw new NotSupportedException($"Intent {context.Intent.GetType().Name} is not supported for {this.GetType().Name}.");
    }

    /// <inheritdoc/>
    public void ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        var (parent, property, index) = PocoPathHelper.ResolvePath(root, operation.JsonPath);

        if (parent is null || property is null || index is null || PocoPathHelper.GetAccessor(property).Getter(parent) is not IList list)
        {
            return;
        }

        if (metadata.Lww.TryGetValue(operation.JsonPath, out var currentTimestamp) &&
            operation.Timestamp.CompareTo(currentTimestamp) < 0)
        {
            return;
        }

        var elementType = PocoPathHelper.GetCollectionElementType(property);
        if (elementType is null)
        {
            return;
        }

        metadata.Lww[operation.JsonPath] = operation.Timestamp;

        var value = PocoPathHelper.ConvertValue(operation.Value, elementType);
        var elementIndex = (int)index;

        while (list.Count <= elementIndex)
        {
            list.Add(elementType.IsValueType ? Activator.CreateInstance(elementType) : null);
        }

        list[elementIndex] = value;
    }
}