namespace Ama.CRDT.Services.Strategies;

using System;
using System.Collections.Generic;
using System.Linq;
using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies.Semantic;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services.Helpers;

/// <summary>
/// Implements an Average Register strategy. Each replica contributes a value, and the property converges to the average of all contributions.
/// </summary>
[CrdtSupportedType(typeof(decimal))]
[CrdtSupportedType(typeof(double))]
[CrdtSupportedType(typeof(float))]
[CrdtSupportedType(typeof(int))]
[CrdtSupportedType(typeof(long))]
[CrdtSupportedIntent(typeof(SetIntent))]
[Commutative]
[Associative]
[Idempotent]
[StateBased]
public sealed class AverageRegisterStrategy(ReplicaContext replicaContext) : ICrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;

    /// <inheritdoc/>
    public void GeneratePatch(GeneratePatchContext context)
    {
        var (operations, _, path, property, originalValue, modifiedValue, _, _, _, changeTimestamp, clock) = context;
        if (Equals(originalValue, modifiedValue))
        {
            return;
        }

        var operation = new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, modifiedValue, changeTimestamp, clock);
        operations.Add(operation);
    }

    /// <inheritdoc/>
    public CrdtOperation GenerateOperation(GenerateOperationContext context)
    {
        if (context.Intent is SetIntent setIntent)
        {
            return new CrdtOperation(
                Guid.NewGuid(),
                replicaId,
                context.JsonPath,
                OperationType.Upsert,
                setIntent.Value,
                context.Timestamp,
                context.Clock);
        }

        throw new NotSupportedException($"Intent '{context.Intent.GetType().Name}' is not supported by {nameof(AverageRegisterStrategy)}.");
    }

    /// <inheritdoc/>
    public void ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;

        ArgumentNullException.ThrowIfNull(operation.ReplicaId);
        
        if (operation.Type != OperationType.Upsert)
        {
            return;
        }

        if (!metadata.AverageRegisters.TryGetValue(operation.JsonPath, out var contributions))
        {
            contributions = new Dictionary<string, AverageRegisterValue>();
            metadata.AverageRegisters[operation.JsonPath] = contributions;
        }
        
        if (contributions.TryGetValue(operation.ReplicaId, out var existing) && operation.Timestamp.CompareTo(existing.Timestamp) <= 0)
        {
            // Incoming operation is older or same, ignore.
            return;
        }
        
        var incomingValue = PocoPathHelper.ConvertTo<decimal>(operation.Value);
        contributions[operation.ReplicaId] = new AverageRegisterValue(incomingValue, operation.Timestamp);

        RecalculateAndApplyAverage(root, operation.JsonPath, contributions);
    }

    private static void RecalculateAndApplyAverage(object root, string jsonPath, IDictionary<string, AverageRegisterValue> contributions)
    {
        if (contributions.Count == 0)
        {
            return;
        }
        
        var sum = contributions.Values.Sum(c => c.Value);
        var average = sum / contributions.Count;

        PocoPathHelper.SetValue<decimal>(root, jsonPath, average);
    }
}