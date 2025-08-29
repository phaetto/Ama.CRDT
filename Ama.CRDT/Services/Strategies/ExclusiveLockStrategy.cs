namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Models;
using Ama.CRDT.Services.Helpers;
using System;
using System.Reflection;
using Ama.CRDT.Services;

[CrdtSupportedType(typeof(object))]
[Commutative]
[Associative]
[Idempotent]
[Mergeable]
public sealed class ExclusiveLockStrategy(ReplicaContext replicaContext) : ICrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;

    public void GeneratePatch(GeneratePatchContext context)
    {
        var (patcher, operations, path, property, originalValue, modifiedValue, originalRoot, modifiedRoot, originalMeta, changeTimestamp) = context;

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
        
        if (currentLock is not null && currentLock.LockHolderId != lockHolderId && lockHolderId is not null)
        {
            return;
        }

        var payload = new ExclusiveLockPayload(modifiedValue, lockHolderId);
        var operation = new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, payload, changeTimestamp);
        operations.Add(operation);
    }

    public void ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        metadata.ExclusiveLocks.TryGetValue(operation.JsonPath, out var currentLock);

        if (currentLock is not null && operation.Timestamp.CompareTo(currentLock.Timestamp) <= 0)
        {
            return;
        }

        var payload = PocoPathHelper.ConvertValue(operation.Value, typeof(ExclusiveLockPayload)) as ExclusiveLockPayload?;
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
}