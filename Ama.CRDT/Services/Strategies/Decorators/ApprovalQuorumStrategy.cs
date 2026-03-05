namespace Ama.CRDT.Services.Strategies.Decorators;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Decorators;
using Ama.CRDT.Attributes.Strategies.Semantic;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Decorators;
using Ama.CRDT.Services.Providers;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// A decorator strategy that tracks pending proposals and requires a quorum of replicas 
/// to propose the exact same operation before it is applied to the underlying data structure.
/// </summary>
[CrdtSupportedType(typeof(object))]
[Commutative]
[Associative]
[Idempotent]
[OperationBased]
public sealed class ApprovalQuorumStrategy(IServiceProvider serviceProvider, IElementComparerProvider comparerProvider) : ICrdtStrategy
{
    private ICrdtStrategy GetInnerStrategy(PropertyInfo property)
    {
        // Use IServiceProvider to break circular DI dependency
        var provider = serviceProvider.GetRequiredService<ICrdtStrategyProvider>();
        return provider.GetInnerStrategy(property, typeof(ApprovalQuorumStrategy));
    }

    /// <inheritdoc/>
    public void GeneratePatch(GeneratePatchContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Property);

        var innerStrategy = GetInnerStrategy(context.Property);
        var innerOps = new List<CrdtOperation>();
        var innerContext = context with { Operations = innerOps };
        
        innerStrategy.GeneratePatch(innerContext);
        
        foreach (var op in innerOps)
        {
            context.Operations.Add(op with { Value = new QuorumPayload(op.Value) });
        }
    }

    /// <inheritdoc/>
    public CrdtOperation GenerateOperation(GenerateOperationContext context)
    {
        ArgumentNullException.ThrowIfNull(context.Property);

        var innerStrategy = GetInnerStrategy(context.Property);
        var innerOp = innerStrategy.GenerateOperation(context);
        
        return innerOp with { Value = new QuorumPayload(innerOp.Value) };
    }

    /// <inheritdoc/>
    public void ApplyOperation(ApplyOperationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Operation.Value is not QuorumPayload payload)
        {
            if (context.Property is not null)
            {
                var inner = GetInnerStrategy(context.Property);
                inner.ApplyOperation(context);
            }
            return;
        }

        if (context.Property is null)
        {
            return;
        }

        var quorumAttr = context.Property.GetCustomAttribute<CrdtApprovalQuorumAttribute>();
        var requiredQuorum = quorumAttr?.QuorumSize ?? 1;

        var path = context.Operation.JsonPath;
        if (!context.Metadata.QuorumApprovals.TryGetValue(path, out var pathApprovals))
        {
            var comparer = comparerProvider.GetComparer(typeof(object));
            pathApprovals = new Dictionary<object, ISet<string>>(comparer);
            context.Metadata.QuorumApprovals[path] = pathApprovals;
        }

        // Handle null proposed values cleanly using a constant proxy if needed, as Dictionaries reject null keys.
        var keyObject = payload.ProposedValue ?? "$null";

        if (!pathApprovals.TryGetValue(keyObject, out var voters))
        {
            voters = new HashSet<string>();
            pathApprovals[keyObject] = voters;
        }

        voters.Add(context.Operation.ReplicaId);

        if (voters.Count >= requiredQuorum)
        {
            // Quorum met. Apply inner operation.
            var innerStrategy = GetInnerStrategy(context.Property);
            var innerOp = context.Operation with { Value = payload.ProposedValue };
            var innerContext = context with { Operation = innerOp };
            
            innerStrategy.ApplyOperation(innerContext);

            // Clean up approvals for this value once met to keep metadata compact
            pathApprovals.Remove(keyObject);
            if (pathApprovals.Count == 0)
            {
                context.Metadata.QuorumApprovals.Remove(path);
            }
        }
    }
}