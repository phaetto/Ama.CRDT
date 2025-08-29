namespace Ama.CRDT.Services.Strategies;
using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Models;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services;

/// <summary>
/// Implements the Max-Wins Register strategy. Conflicts are resolved by choosing the highest value.
/// </summary>
[CrdtSupportedType(typeof(IComparable))]
[Commutative]
[Associative]
[Idempotent]
[Mergeable]
public sealed class MaxWinsStrategy(ReplicaContext replicaContext) : ICrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;

    /// <inheritdoc/>
    public void GeneratePatch(GeneratePatchContext context)
    {
        var (patcher, operations, path, property, originalValue, modifiedValue, originalRoot, modifiedRoot, originalMeta, changeTimestamp) = context;

        if (modifiedValue is null || originalValue is null)
        {
            if (modifiedValue != originalValue)
            {
                var operation = new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, modifiedValue, changeTimestamp);
                operations.Add(operation);
            }
            return;
        }
        
        var originalComparable = (IComparable)originalValue;
        if (originalComparable.CompareTo(modifiedValue) < 0)
        {
            var operation = new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, modifiedValue, changeTimestamp);
            operations.Add(operation);
        }
    }

    /// <inheritdoc/>
    public void ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        if (operation.Type != OperationType.Upsert)
        {
            // This strategy only handles value assignments.
            return;
        }
        
        var currentValue = PocoPathHelper.GetValue(root, operation.JsonPath);
        var incomingValue = operation.Value;

        if (incomingValue is null)
        {
            return; // Max-wins doesn't typically handle nulls; we ignore them.
        }

        if (currentValue is null || ((IComparable)currentValue).CompareTo(incomingValue) < 0)
        {
            PocoPathHelper.SetValue(root, operation.JsonPath, incomingValue);
        }
    }
}