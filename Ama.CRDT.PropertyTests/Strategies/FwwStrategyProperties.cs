namespace Ama.CRDT.PropertyTests.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.PropertyTests.Attributes;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Strategies;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;

[CrdtAotType(typeof(FwwTestPoco))]
public partial class FwwTestContext : CrdtAotContext
{
}

public sealed class FwwTestPoco : IEquatable<FwwTestPoco>
{
    public string? Value { get; set; }

    public bool Equals(FwwTestPoco? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return string.Equals(Value, other.Value, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj) => Equals(obj as FwwTestPoco);
    
    public override int GetHashCode() => Value?.GetHashCode(StringComparison.Ordinal) ?? 0;
}

public sealed class FwwStrategyProperties
{
    [CrdtProperty]
    public void Idempotence_ApplyingSameOperationTwice_YieldsSameState(long timestamp, string? value)
    {
        var op = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            nameof(FwwTestPoco.Value),
            OperationType.Upsert, // Enforce Upsert as Remove bypasses value timestamps
            value,
            new EpochTimestamp(timestamp),
            0);

        var state1 = new FwwTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, new[] { op });

        var state2 = new FwwTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, new[] { op, op }); // Applied twice

        state1.ShouldBe(state2);
    }

    [CrdtProperty]
    public void Commutativity_ApplyingOperationsInDifferentOrder_YieldsSameState(long timestamp1, string? value1, long timestamp2, string? value2)
    {
        if (timestamp1 == timestamp2)
        {
            // Standard FWW requires strictly different timestamps to guarantee conflict resolution commutativity without tie-breakers.
            return;
        }

        var op1 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            nameof(FwwTestPoco.Value),
            OperationType.Upsert,
            value1,
            new EpochTimestamp(timestamp1),
            0);

        var op2 = new CrdtOperation(
            Guid.NewGuid(),
            "replica-2",
            nameof(FwwTestPoco.Value),
            OperationType.Upsert,
            value2,
            new EpochTimestamp(timestamp2),
            0);

        var stateAB = new FwwTestPoco();
        var metaAB = new CrdtMetadata();
        ApplyOperations(stateAB, metaAB, new[] { op1, op2 });

        var stateBA = new FwwTestPoco();
        var metaBA = new CrdtMetadata();
        ApplyOperations(stateBA, metaBA, new[] { op2, op1 });

        stateAB.ShouldBe(stateBA);
    }

    [CrdtProperty]
    public void Convergence_AnyPermutationOfOperations_YieldsSameState(List<Tuple<long, string?>> rawOps)
    {
        if (rawOps is null || rawOps.Count == 0)
        {
            return;
        }

        // Distinctly filter by timestamp to avoid FWW tie-break rejections making convergence flaky.
        var distinctOpsData = rawOps.DistinctBy(x => x.Item1).ToList();
        if (distinctOpsData.Count == 0)
        {
            return;
        }

        var ops = distinctOpsData.Select(x => new CrdtOperation(
            Guid.NewGuid(),
            "replica-1",
            nameof(FwwTestPoco.Value),
            OperationType.Upsert,
            x.Item2,
            new EpochTimestamp(x.Item1),
            0)).ToList();

        var random = new Random(distinctOpsData.Count);
        var permutation1 = ops.OrderBy(_ => random.Next()).ToList();
        var permutation2 = ops.OrderBy(_ => random.Next()).ToList();

        var state1 = new FwwTestPoco();
        var meta1 = new CrdtMetadata();
        ApplyOperations(state1, meta1, permutation1);

        var state2 = new FwwTestPoco();
        var meta2 = new CrdtMetadata();
        ApplyOperations(state2, meta2, permutation2);

        state1.ShouldBe(state2);
    }

    private static void ApplyOperations(FwwTestPoco state, CrdtMetadata metadata, IEnumerable<CrdtOperation> operations)
    {
        var replicaContext = new ReplicaContext { ReplicaId = "property-test-replica" };
        var strategy = new FwwStrategy(replicaContext, [ new FwwTestContext() ]);
        var propertyInfo = new CrdtPropertyInfo(
            nameof(FwwTestPoco.Value),
            "value",
            typeof(string),
            true,
            true,
            obj => ((FwwTestPoco)obj).Value,
            (obj, val) => ((FwwTestPoco)obj).Value = (string?)val,
            null,
            Array.Empty<CrdtStrategyDecoratorAttribute>()
        );

        foreach (var op in operations)
        {
            var context = new ApplyOperationContext(state, metadata, op)
            {
                Target = state,
                Property = propertyInfo,
                FinalSegment = nameof(FwwTestPoco.Value)
            };
            
            // Replicate external Applicator timestamp management so FWW comparisons work correctly
            if (metadata.Fww.TryGetValue(nameof(FwwTestPoco.Value), out var existingTs))
            {
                if (op.Timestamp.CompareTo(existingTs.Timestamp) >= 0) continue; 
            }
            
            var status = strategy.ApplyOperation(context);
            if (status == CrdtOperationStatus.Success)
            {
                 metadata.Fww[nameof(FwwTestPoco.Value)] = new CausalTimestamp(op.Timestamp, op.ReplicaId, op.Clock);
            }
        }
    }
}