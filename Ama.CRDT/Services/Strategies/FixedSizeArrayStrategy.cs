namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
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

/// <inheritdoc/>
[Commutative]
[Associative]
[Idempotent]
[Mergeable]
public sealed class FixedSizeArrayStrategy(
    ICrdtTimestampProvider timestampProvider,
    IOptions<CrdtOptions> options) : ICrdtStrategy
{
    private readonly string replicaId = options.Value.ReplicaId;

    /// <inheritdoc/>
    public void GeneratePatch([DisallowNull] ICrdtPatcher patcher, [DisallowNull] List<CrdtOperation> operations, [DisallowNull] string path, [DisallowNull] PropertyInfo property, object? originalValue, object? modifiedValue, [DisallowNull] CrdtMetadata originalMeta, [DisallowNull] CrdtMetadata modifiedMeta)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(originalMeta);
        ArgumentNullException.ThrowIfNull(modifiedMeta);

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

            var now = timestampProvider.Now();
            if (!originalMeta.Lww.TryGetValue(elementPath, out var originalTimestamp) || now.CompareTo(originalTimestamp) >= 0)
            {
                operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, elementPath, OperationType.Upsert, modifiedElement, now));
                modifiedMeta.Lww[elementPath] = now;
            }
        }
    }

    /// <inheritdoc/>
    public void ApplyOperation([DisallowNull] object root, [DisallowNull] CrdtMetadata metadata, CrdtOperation operation)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(metadata);

        var (parent, property, index) = PocoPathHelper.ResolvePath(root, operation.JsonPath);

        if (parent is null || property is null || index is null || property.GetValue(parent) is not IList list)
        {
            return;
        }

        if (metadata.Lww.TryGetValue(operation.JsonPath, out var currentTimestamp) &&
            operation.Timestamp.CompareTo(currentTimestamp) < 0)
        {
            return;
        }

        metadata.Lww[operation.JsonPath] = operation.Timestamp;

        var elementType = list.GetType().IsGenericType ? list.GetType().GetGenericArguments()[0] : typeof(object);
        var value = DeserializeItemValue(operation.Value, elementType);

        var elementIndex = (int)index;

        if (elementIndex < list.Count)
        {
            list[elementIndex] = value;
        }
        else
        {
            // Pad the list with defaults if the index is out of bounds.
            for (var i = list.Count; i < elementIndex; i++)
            {
                list.Add(elementType.IsValueType ? Activator.CreateInstance(elementType) : null);
            }
            list.Add(value);
        }
    }

    private static object? DeserializeItemValue(object? value, Type targetType)
    {
        if (value is null)
        {
            return null;
        }
        if (targetType.IsInstanceOfType(value))
        {
            return value;
        }

        if (value is JsonElement jsonElement)
        {
            return JsonSerializer.Deserialize(jsonElement.GetRawText(), targetType);
        }

        try
        {
            return Convert.ChangeType(value, targetType);
        }
        catch (Exception)
        {
            return null;
        }
    }
}