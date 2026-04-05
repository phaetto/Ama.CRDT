namespace Ama.CRDT.Services.Strategies.Decorators;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Decorators;
using Ama.CRDT.Attributes.Strategies.Semantic;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Models.Decorators;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Providers;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A decorator strategy that tracks pending proposals and requires a quorum of replicas 
/// to propose the exact same operation before it is applied to the underlying data structure.
/// </summary>
[CrdtSupportedType(typeof(object))]
[Commutative]
[Associative]
[Idempotent]
[OperationBased]
public sealed class ApprovalQuorumStrategy(
    IServiceProvider serviceProvider, 
    IElementComparerProvider comparerProvider, 
    IEnumerable<CrdtAotContext> aotContexts) : ICrdtStrategy
{
    private ICrdtStrategy GetInnerStrategy(Type declaringType, CrdtPropertyInfo property)
    {
        // Use IServiceProvider to break circular DI dependency
        var provider = serviceProvider.GetRequiredService<ICrdtStrategyProvider>();
        return provider.GetInnerStrategy(declaringType, property, typeof(ApprovalQuorumStrategy));
    }

    /// <inheritdoc/>
    public void GeneratePatch(GeneratePatchContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Property);

        var root = context.OriginalRoot ?? context.ModifiedRoot;
        var (target, _, _) = PocoPathHelper.ResolvePath(root!, context.Path, aotContexts);
        var declaringType = target?.GetType() ?? typeof(object);

        var innerStrategy = GetInnerStrategy(declaringType, context.Property);
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

        var (target, _, _) = PocoPathHelper.ResolvePath(context.DocumentRoot, context.JsonPath, aotContexts);
        var declaringType = target?.GetType() ?? typeof(object);

        var innerStrategy = GetInnerStrategy(declaringType, context.Property);
        var innerOp = innerStrategy.GenerateOperation(context);
        
        return innerOp with { Value = new QuorumPayload(innerOp.Value) };
    }

    /// <inheritdoc/>
    public CrdtOperationStatus ApplyOperation(ApplyOperationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Property is null)
        {
            return CrdtOperationStatus.PathResolutionFailed;
        }

        // Strict enforcement: Reject operations that are not explicitly wrapped in a QuorumPayload.
        if (context.Operation.Value is not QuorumPayload payload)
        {
            return CrdtOperationStatus.StrategyApplicationFailed;
        }

        var quorumAttr = context.Property.DecoratorAttributes.OfType<CrdtApprovalQuorumAttribute>().FirstOrDefault();
        var requiredQuorum = quorumAttr?.QuorumSize ?? 1;

        var path = context.Operation.JsonPath;
        if (!context.Metadata.States.TryGetValue(path, out var baseState) || baseState is not QuorumState quorumState)
        {
            var comparer = comparerProvider.GetComparer(typeof(object));
            quorumState = new QuorumState(new Dictionary<object, ISet<string>>(comparer));
            context.Metadata.States[path] = quorumState;
        }

        var pathApprovals = quorumState.Approvals;

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
            var declaringType = context.Target?.GetType() ?? typeof(object);
            var innerStrategy = GetInnerStrategy(declaringType, context.Property);
            var innerOp = context.Operation with { Value = payload.ProposedValue };
            var innerContext = context with { Operation = innerOp };
            
            var status = innerStrategy.ApplyOperation(innerContext);

            // Clean up approvals for this value once met to keep metadata compact
            pathApprovals.Remove(keyObject);
            if (pathApprovals.Count == 0)
            {
                context.Metadata.States.Remove(path);
            }

            return status;
        }

        return CrdtOperationStatus.Success;
    }

    /// <inheritdoc/>
    public void Compact(CompactionContext context)
    {
        // For decorators, we must delegate the compaction request down the chain to the inner strategy.
        if (context.Document is null) return;
        
        var (target, property, _) = PocoPathHelper.ResolvePath(context.Document, context.PropertyPath, aotContexts);
        if (property is not null)
        {
            var declaringType = target?.GetType() ?? typeof(object);
            var innerStrategy = GetInnerStrategy(declaringType, property);
            innerStrategy.Compact(context);
        }
    }
}