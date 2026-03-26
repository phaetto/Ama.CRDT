namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies.Semantic;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.GarbageCollection;
using System;
using System.Collections.Generic;
using System.Linq;

[CrdtSupportedType(typeof(CrdtTree))]
[CrdtSupportedIntent(typeof(AddNodeIntent))]
[CrdtSupportedIntent(typeof(RemoveNodeIntent))]
[CrdtSupportedIntent(typeof(MoveNodeIntent))]
[Commutative]
[Associative]
[Idempotent]
[StateBased]
public sealed class ReplicatedTreeStrategy(
    IElementComparerProvider comparerProvider,
    ReplicaContext replicaContext) : ICrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;

    public void GeneratePatch(GeneratePatchContext context)
    {
        var (operations, _, path, _, originalValue, modifiedValue, _, _, originalMeta, changeTimestamp, clock) = context;

        if (originalValue is not CrdtTree originalTree || modifiedValue is not CrdtTree modifiedTree) return;

        var originalNodes = originalTree.Nodes;
        var modifiedNodes = modifiedTree.Nodes;

        var addedIds = modifiedNodes.Keys.Except(originalNodes.Keys);
        var removedIds = originalNodes.Keys.Except(modifiedNodes.Keys);
        var commonIds = originalNodes.Keys.Intersect(modifiedNodes.Keys);

        foreach (var id in addedIds)
        {
            var node = modifiedNodes[id];
            var payload = new TreeAddNodePayload(node.Id, node.Value, node.ParentId, Guid.NewGuid());
            operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, payload, changeTimestamp, clock));
        }

        if (originalMeta.ReplicatedTrees.TryGetValue(path, out var metaState))
        {
            foreach (var id in removedIds)
            {
                if (metaState.Adds.TryGetValue(id, out var tags) && tags.Count > 0)
                {
                    var payload = new TreeRemoveNodePayload(id, new HashSet<Guid>(tags));
                    operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Remove, payload, changeTimestamp, clock));
                }
            }
        }
        
        foreach (var id in commonIds)
        {
            var originalNode = originalNodes[id];
            var modifiedNode = modifiedNodes[id];

            if (!Equals(originalNode.ParentId, modifiedNode.ParentId))
            {
                var payload = new TreeMoveNodePayload(modifiedNode.Id, modifiedNode.ParentId);
                operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, payload, changeTimestamp, clock));
            }
        }
    }

    public CrdtOperation GenerateOperation(GenerateOperationContext context)
    {
        return context.Intent switch
        {
            AddNodeIntent addIntent => new CrdtOperation(
                Guid.NewGuid(),
                replicaId,
                context.JsonPath,
                OperationType.Upsert,
                new TreeAddNodePayload(addIntent.Node.Id, addIntent.Node.Value, addIntent.Node.ParentId, Guid.NewGuid()),
                context.Timestamp,
                context.Clock),

            RemoveNodeIntent removeIntent => GenerateRemoveOperation(context, removeIntent),

            MoveNodeIntent moveIntent => new CrdtOperation(
                Guid.NewGuid(),
                replicaId,
                context.JsonPath,
                OperationType.Upsert,
                new TreeMoveNodePayload(moveIntent.NodeId, moveIntent.NewParentId),
                context.Timestamp,
                context.Clock),

            _ => throw new NotSupportedException($"Intent {context.Intent.GetType().Name} is not supported by {nameof(ReplicatedTreeStrategy)}.")
        };
    }

    private CrdtOperation GenerateRemoveOperation(GenerateOperationContext context, RemoveNodeIntent intent)
    {
        var tags = new HashSet<Guid>();
        if (context.Metadata.ReplicatedTrees.TryGetValue(context.JsonPath, out var state) &&
            state.Adds.TryGetValue(intent.NodeId, out var addedTags))
        {
            tags = new HashSet<Guid>(addedTags);
        }

        return new CrdtOperation(
            Guid.NewGuid(),
            replicaId,
            context.JsonPath,
            OperationType.Remove,
            new TreeRemoveNodePayload(intent.NodeId, tags),
            context.Timestamp,
            context.Clock);
    }

    public CrdtOperationStatus ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;
        
        var treeObj = PocoPathHelper.GetValue(root, operation.JsonPath);
        if (treeObj is not CrdtTree tree)
        {
            return CrdtOperationStatus.PathResolutionFailed;
        }
        
        var idType = tree.Nodes.Keys.FirstOrDefault()?.GetType() ?? typeof(object);
        var idComparer = comparerProvider.GetComparer(idType);

        if (!metadata.ReplicatedTrees.TryGetValue(operation.JsonPath, out var state))
        {
            state = new OrSetState(new Dictionary<object, ISet<Guid>>(idComparer), new Dictionary<object, ISet<Guid>>(idComparer));
            metadata.ReplicatedTrees[operation.JsonPath] = state;
        }
        
        object? payload = operation.Value;

        if (payload is null)
        {
            return CrdtOperationStatus.StrategyApplicationFailed;
        }

        if (payload is IDictionary<string, object> dict)
        {
            if (dict.ContainsKey("Tag"))
                payload = PocoPathHelper.ConvertValue(dict, typeof(TreeAddNodePayload));
            else if (dict.ContainsKey("Tags"))
                payload = PocoPathHelper.ConvertValue(dict, typeof(TreeRemoveNodePayload));
            else if (dict.ContainsKey("NewParentId"))
                payload = PocoPathHelper.ConvertValue(dict, typeof(TreeMoveNodePayload));
        }

        if (payload is TreeAddNodePayload addPayload)
        {
            var nodeId = addPayload.NodeId;
            ApplyAdd(state, nodeId, addPayload.Tag);
            
            bool isLive = true;
            if (state.Removes.TryGetValue(nodeId, out var rmTags) && state.Adds.TryGetValue(nodeId, out var addTags))
            {
                isLive = addTags.Except(rmTags).Any();
            }

            if (isLive)
            {
                var node = new TreeNode { Id = nodeId, Value = addPayload.Value, ParentId = addPayload.ParentId };
                tree.Nodes[nodeId] = node;
            }
        }
        else if (payload is TreeRemoveNodePayload removePayload)
        {
            ApplyRemove(state, removePayload.NodeId, removePayload.Tags);

            bool isLive = false;
            if (state.Adds.TryGetValue(removePayload.NodeId, out var addTags))
            {
                if (!state.Removes.TryGetValue(removePayload.NodeId, out var rmTags) || addTags.Except(rmTags).Any())
                {
                    isLive = true;
                }
            }

            if (!isLive)
            {
                tree.Nodes.Remove(removePayload.NodeId);
            }
        }
        else if (payload is TreeMoveNodePayload movePayload)
        {
            var nodeId = movePayload.NodeId;
            var nodePath = $"{operation.JsonPath}.Nodes.['{nodeId}'].ParentId";

            if (metadata.Lww.TryGetValue(nodePath, out var existingTimestamp) && operation.Timestamp.CompareTo(existingTimestamp) <= 0)
            {
                return CrdtOperationStatus.Obsolete;
            }

            if (tree.Nodes.TryGetValue(nodeId, out var nodeToMove))
            {
                nodeToMove.ParentId = movePayload.NewParentId;
                metadata.Lww[nodePath] = operation.Timestamp;
            }
        }
        else
        {
            return CrdtOperationStatus.StrategyApplicationFailed;
        }

        return CrdtOperationStatus.Success;
    }

    public void Compact(CompactionContext context)
    {
        // ReplicatedTreeStrategy uses an OrSetState pattern mapping Nodes via Guids rather than ICrdtTimestamps.
        // Also it uses LWW for tree movement properties. While tree moves could theoretically be pruned,
        // pruning the tags requires timestamp tracking we do not currently possess in this structure.
    }
    
    private static void ApplyAdd(OrSetState state, object nodeId, Guid tag)
    {
        if (!state.Adds.TryGetValue(nodeId, out var addTags))
        {
            addTags = new HashSet<Guid>();
            state.Adds[nodeId] = addTags;
        }
        addTags.Add(tag);
    }

    private static void ApplyRemove(OrSetState state, object nodeId, ISet<Guid> tags)
    {
        if (!state.Removes.TryGetValue(nodeId, out var removeTags))
        {
            removeTags = new HashSet<Guid>();
            state.Removes[nodeId] = removeTags;
        }
        foreach (var tag in tags)
        {
            removeTags.Add(tag);
        }
    }
}