namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Models;
using Ama.CRDT.Services.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using Ama.CRDT.Services;

[CrdtSupportedType(typeof(object))]
[Commutative]
[Associative]
[Idempotent]
[Mergeable]
public sealed class ExclusiveLockStrategy(ReplicaContext replicaContext) : ICrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;

    public void GeneratePatch(ICrdtPatcher patcher, List<CrdtOperation> operations, string path, PropertyInfo property, object? originalValue, object? modifiedValue, object? originalRoot, object? modifiedRoot, CrdtMetadata originalMeta, CrdtMetadata modifiedMeta)
    {
        if (modifiedRoot is null || property.GetCustomAttribute<CrdtExclusiveLockStrategyAttribute>() is not { } attr)
        {
            return;
        }

        var (_, prop, value) = PocoPathHelper.ResolvePath(modifiedRoot, attr.LockHolderPropertyPath);
        if (prop is not null)
        {
            value = prop.GetValue(modifiedRoot);
        }

        var lockHolderId = value?.ToString();

        originalMeta.ExclusiveLocks.TryGetValue(path, out var currentLock);

        var valueChanged = !Equals(originalValue, modifiedValue);
        var lockChanged = (currentLock?.LockHolderId != lockHolderId) && !(currentLock is null && lockHolderId is null);

        if (!valueChanged && !lockChanged)
        {
            return;
        }

        modifiedMeta.Lww.TryGetValue(path, out var modifiedTimestamp);
        if (modifiedTimestamp is null)
        {
            return;
        }
        
        if (currentLock is not null && currentLock.LockHolderId != lockHolderId && lockHolderId is not null)
        {
            return;
        }

        var payload = new ExclusiveLockPayload(modifiedValue, lockHolderId);
        var operation = new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, payload, modifiedTimestamp);
        operations.Add(operation);

        if (lockHolderId is not null)
        {
            modifiedMeta.ExclusiveLocks[path] = new LockInfo(lockHolderId, modifiedTimestamp);
        }
        else
        {
            modifiedMeta.ExclusiveLocks[path] = null;
        }
    }

    public void ApplyOperation([DisallowNull] object root, [DisallowNull] CrdtMetadata metadata, CrdtOperation operation)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(metadata);

        metadata.ExclusiveLocks.TryGetValue(operation.JsonPath, out var currentLock);

        if (currentLock is not null && operation.Timestamp.CompareTo(currentLock.Timestamp) <= 0)
        {
            return;
        }

        var payload = DeserializePayload(operation.Value);
        if (payload is null) return;
        
        var (parent, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath);
        if (parent is null || property is null || !property.CanWrite) return;
        
        var value = PocoPathHelper.ConvertValue(payload.Value.Value, property.PropertyType);
        property.SetValue(parent, value);

        if (payload.Value.LockHolderId is not null)
        {
            metadata.ExclusiveLocks[operation.JsonPath] = new LockInfo(payload.Value.LockHolderId, operation.Timestamp);
        }
        else
        {
            metadata.ExclusiveLocks[operation.JsonPath] = null;
        }
    }

    private static ExclusiveLockPayload? DeserializePayload(object? rawPayload)
    {
        return rawPayload switch
        {
            null => null,
            JsonElement jsonElement => jsonElement.Deserialize<ExclusiveLockPayload>(),
            ExclusiveLockPayload payload => payload,
            _ => null
        };
    }
}