namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies.Semantic;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services.GarbageCollection;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Providers;
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
    ReplicaContext replicaContext,
    IEnumerable<CrdtAotContext> aotContexts) : ICrdtStrategy
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

        if (originalMeta.States.TryGetValue(path, out var baseState) && baseState is OrSetState metaState)
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
        if (context.Metadata.States.TryGetValue(context.JsonPath, out var baseState) && baseState is OrSetState state &&
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
        
        var treeObj = PocoPathHelper.GetValue(root, operation.JsonPath, aotContexts);
        if (treeObj is not CrdtTree tree)
        {
            return CrdtOperationStatus.PathResolutionFailed;
        }
        
        var idType = tree.Nodes.Keys.FirstOrDefault()?.GetType() ?? typeof(object);
        var idComparer = comparerProvider.GetComparer(idType);

        if (!metadata.States.TryGetValue(operation.JsonPath, out var baseState) || baseState is not OrSetState state)
        {
            state = new OrSetState(new Dictionary<object, ISet<Guid>>(idComparer), new Dictionary<object, IDictionary<Guid, CausalTimestamp>>(idComparer));
            metadata.States[operation.JsonPath] = state;
        }
        
        object? payload = operation.Value;

        if (payload is null)
        {
            return CrdtOperationStatus.StrategyApplicationFailed;
        }

        if (payload is IDictionary<string, object> dict)
        {
            if (dict.ContainsKey("Tag"))
                payload = PocoPathHelper.ConvertValue(dict, typeof(TreeAddNodePayload), aotContexts);
            else if (dict.ContainsKey("Tags"))
                payload = PocoPathHelper.ConvertValue(dict, typeof(TreeRemoveNodePayload), aotContexts);
            else if (dict.ContainsKey("NewParentId"))
                payload = PocoPathHelper.ConvertValue(dict, typeof(TreeMoveNodePayload), aotContexts);
        }

        if (payload is TreeAddNodePayload addPayload)
        {
            var nodeId = addPayload.NodeId;
            ApplyAdd(state, nodeId, addPayload.Tag);
            
            bool isLive = true;
            if (state.Removes.TryGetValue(nodeId, out var rmTags) && state.Adds.TryGetValue(nodeId, out var addTags))
            {
                isLive = addTags.Except(rmTags.Keys).Any();
            }

            if (isLive)
            {
                var node = new TreeNode { Id = nodeId, Value = addPayload.Value, ParentId = addPayload.ParentId };
                tree.Nodes[nodeId] = node;
            }
        }
        else if (payload is TreeRemoveNodePayload removePayload)
        {
            var causalTs = new CausalTimestamp(operation.Timestamp, operation.ReplicaId, operation.Clock);
            ApplyRemove(state, removePayload.NodeId, removePayload.Tags, causalTs);

            bool isLive = false;
            if (state.Adds.TryGetValue(removePayload.NodeId, out var addTags))
            {
                if (!state.Removes.TryGetValue(removePayload.NodeId, out var rmTags) || addTags.Except(rmTags.Keys).Any())
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
            var causalOpTs = new CausalTimestamp(operation.Timestamp, operation.ReplicaId, operation.Clock);

            if (metadata.States.TryGetValue(nodePath, out var existingState) && existingState is CausalTimestamp existingTimestamp && causalOpTs.CompareTo(existingTimestamp) <= 0)
            {
                return CrdtOperationStatus.Obsolete;
            }

            if (tree.Nodes.TryGetValue(nodeId, out var nodeToMove))
            {
                nodeToMove.ParentId = movePayload.NewParentId;
                metadata.States[nodePath] = causalOpTs;
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
        if (!context.Metadata.States.TryGetValue(context.PropertyPath, out var baseState) || baseState is not OrSetState state) return;

        var nodesToRemove = new List<object>();

        foreach (var kvp in state.Removes)
        {
            var nodeId = kvp.Key;
            var removeDict = kvp.Value;
            
            var tagsToRemove = new List<Guid>();

            foreach (var tagKvp in removeDict)
            {
                var tag = tagKvp.Key;
                var causalTs = tagKvp.Value;
                
                var candidate = new CompactionCandidate(causalTs.Timestamp, causalTs.ReplicaId, causalTs.Clock);
                if (context.Policy.IsSafeToCompact(candidate))
                {
                    tagsToRemove.Add(tag);
                }
            }

            foreach (var tag in tagsToRemove)
            {
                removeDict.Remove(tag);
                if (state.Adds.TryGetValue(nodeId, out var addTags))
                {
                    addTags.Remove(tag);
                }
            }

            if (removeDict.Count == 0 && (!state.Adds.TryGetValue(nodeId, out var remainingAdds) || remainingAdds.Count == 0))
            {
                nodesToRemove.Add(nodeId);
            }
        }

        foreach (var nodeId in nodesToRemove)
        {
            state.Removes.Remove(nodeId);
            state.Adds.Remove(nodeId);
        }
    }
    
    /// <inheritdoc/>
    public void MergeAsStateCrdt(object data1, CrdtMetadata meta1, object data2, CrdtMetadata meta2, CrdtPropertyInfo property)
    {
        var path = $"$.{char.ToLowerInvariant(property.Name[0])}{property.Name[1..]}";

        var (parent1, prop1, _) = PocoPathHelper.ResolvePath(data1, path, aotContexts);
        var (parent2, prop2, _) = PocoPathHelper.ResolvePath(data2, path, aotContexts);

        if (parent1 is null || prop1 is null || parent2 is null || prop2 is null) return;

        var tree1 = prop1.Getter!(parent1) as CrdtTree;
        var tree2 = prop2.Getter!(parent2) as CrdtTree;

        if (tree1 is null)
        {
            tree1 = new CrdtTree();
            prop1.Setter!(parent1, tree1);
        }
        if (tree2 is null)
        {
            tree2 = new CrdtTree();
        }

        meta1.States.TryGetValue(path, out var baseState1);
        meta2.States.TryGetValue(path, out var baseState2);

        var idType = tree1.Nodes.Keys.FirstOrDefault()?.GetType() ?? tree2.Nodes.Keys.FirstOrDefault()?.GetType() ?? typeof(object);
        var idComparer = comparerProvider.GetComparer(idType);

        var state1 = baseState1 as OrSetState ?? new OrSetState(new Dictionary<object, ISet<Guid>>(idComparer), new Dictionary<object, IDictionary<Guid, CausalTimestamp>>(idComparer));
        var state2 = baseState2 as OrSetState ?? new OrSetState(new Dictionary<object, ISet<Guid>>(idComparer), new Dictionary<object, IDictionary<Guid, CausalTimestamp>>(idComparer));

        foreach (var kvp in state2.Adds)
        {
            if (!state1.Adds.TryGetValue(kvp.Key, out var tags1))
            {
                tags1 = new HashSet<Guid>();
                state1.Adds[kvp.Key] = tags1;
            }
            foreach (var tag in kvp.Value)
            {
                tags1.Add(tag);
            }
        }

        foreach (var kvp in state2.Removes)
        {
            if (!state1.Removes.TryGetValue(kvp.Key, out var rm1))
            {
                rm1 = new Dictionary<Guid, CausalTimestamp>();
                state1.Removes[kvp.Key] = rm1;
            }
            foreach (var rmTag in kvp.Value)
            {
                if (!rm1.TryGetValue(rmTag.Key, out var existingCausal) || rmTag.Value.CompareTo(existingCausal) > 0)
                {
                    rm1[rmTag.Key] = rmTag.Value;
                }
            }
        }

        meta1.States[path] = state1;

        foreach (var kvp in tree2.Nodes)
        {
            if (!tree1.Nodes.TryGetValue(kvp.Key, out var node1))
            {
                node1 = new TreeNode { Id = kvp.Value.Id, Value = kvp.Value.Value, ParentId = kvp.Value.ParentId };
                tree1.Nodes[kvp.Key] = node1;
            }

            var nodePath = $"{path}.Nodes.['{kvp.Key}'].ParentId";
            meta1.States.TryGetValue(nodePath, out var ts1Obj);
            meta2.States.TryGetValue(nodePath, out var ts2Obj);

            if (ts2Obj is CausalTimestamp ts2)
            {
                if (ts1Obj is not CausalTimestamp ts1 || ts2.CompareTo(ts1) > 0)
                {
                    meta1.States[nodePath] = ts2;
                    node1.ParentId = kvp.Value.ParentId;
                }
            }
        }

        var nodesToRemove = new List<object>();
        foreach (var kvp in tree1.Nodes)
        {
            var nodeId = kvp.Key;
            bool isLive = false;
            if (state1.Adds.TryGetValue(nodeId, out var addTags))
            {
                if (!state1.Removes.TryGetValue(nodeId, out var rmTags) || addTags.Except(rmTags.Keys).Any())
                {
                    isLive = true;
                }
            }
            if (!isLive)
            {
                nodesToRemove.Add(nodeId);
            }
        }
        
        foreach (var nodeId in nodesToRemove)
        {
            tree1.Nodes.Remove(nodeId);
        }
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

    private static void ApplyRemove(OrSetState state, object nodeId, ISet<Guid> tags, CausalTimestamp causalTs)
    {
        if (!state.Removes.TryGetValue(nodeId, out var removeTags))
        {
            removeTags = new Dictionary<Guid, CausalTimestamp>();
            state.Removes[nodeId] = removeTags;
        }
        foreach (var tag in tags)
        {
            removeTags[tag] = causalTs;
        }
    }
}