namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Models;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Providers;

[CrdtSupportedType(typeof(CrdtTree))]
[Commutative]
[Associative]
[Idempotent]
[Mergeable]
public sealed class ReplicatedTreeStrategy(
    IElementComparerProvider comparerProvider,
    ReplicaContext replicaContext) : ICrdtStrategy
{
    private readonly string replicaId = replicaContext.ReplicaId;
    
    public void GeneratePatch(GeneratePatchContext context)
    {
        var (_, operations, path, _, originalValue, modifiedValue, _, _, originalMeta, changeTimestamp) = context;

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
            operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, payload, changeTimestamp));
        }

        if (originalMeta.ReplicatedTrees.TryGetValue(path, out var metaState))
        {
            foreach (var id in removedIds)
            {
                if (metaState.Adds.TryGetValue(id, out var tags) && tags.Count > 0)
                {
                    var payload = new TreeRemoveNodePayload(id, new HashSet<Guid>(tags));
                    operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Remove, payload, changeTimestamp));
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
                operations.Add(new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, payload, changeTimestamp));
            }
        }
    }

    public void ApplyOperation(ApplyOperationContext context)
    {
        var (root, metadata, operation) = context;
        
        var treeObj = PocoPathHelper.GetValue(root, operation.JsonPath);
        if (treeObj is not CrdtTree tree) return;
        
        var idType = tree.Nodes.Keys.FirstOrDefault()?.GetType() ?? typeof(object);
        var idComparer = comparerProvider.GetComparer(idType);

        if (!metadata.ReplicatedTrees.TryGetValue(operation.JsonPath, out var state))
        {
            state = (new Dictionary<object, ISet<Guid>>(idComparer), new Dictionary<object, ISet<Guid>>(idComparer));
            metadata.ReplicatedTrees[operation.JsonPath] = state;
        }
        
        object? payload = operation.Value;

        if (payload is null) return;
        
        if (payload is TreeAddNodePayload addPayload)
        {
            var nodeId = addPayload.NodeId;
            ApplyAdd(state, nodeId, addPayload.Tag);
            
            var node = new TreeNode { Id = nodeId, Value = addPayload.Value, ParentId = addPayload.ParentId };
            tree.Nodes[nodeId] = node;
        }
        else if (payload is TreeRemoveNodePayload removePayload)
        {
            ApplyRemove(state, removePayload.NodeId, removePayload.Tags);
        }
        else if (payload is TreeMoveNodePayload movePayload)
        {
            var nodeId = movePayload.NodeId;
            var nodePath = $"{operation.JsonPath}.Nodes.['{nodeId}'].ParentId";

            if (metadata.Lww.TryGetValue(nodePath, out var existingTimestamp) && operation.Timestamp.CompareTo(existingTimestamp) <= 0)
            {
                return;
            }

            if (tree.Nodes.TryGetValue(nodeId, out var nodeToMove))
            {
                nodeToMove.ParentId = movePayload.NewParentId;
                metadata.Lww[nodePath] = operation.Timestamp;
            }
        }
        else
        {
            return;
        }
        
        ReconstructTree(tree, state, idComparer);
    }
    
    private static void ApplyAdd((IDictionary<object, ISet<Guid>> Adds, IDictionary<object, ISet<Guid>> Removes) state, object nodeId, Guid tag)
    {
        if (!state.Adds.TryGetValue(nodeId, out var addTags))
        {
            addTags = new HashSet<Guid>();
            state.Adds[nodeId] = addTags;
        }
        addTags.Add(tag);
    }

    private static void ApplyRemove((IDictionary<object, ISet<Guid>> Adds, IDictionary<object, ISet<Guid>> Removes) state, object nodeId, ISet<Guid> tags)
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

    private static void ReconstructTree(CrdtTree tree, (IDictionary<object, ISet<Guid>> Adds, IDictionary<object, ISet<Guid>> Removes) state, IEqualityComparer<object> idComparer)
    {
        var liveKeys = new HashSet<object>(idComparer);
        foreach (var (key, addTags) in state.Adds)
        {
            var isLive = true;
            if (state.Removes.TryGetValue(key, out var removeTags))
            {
                if (!addTags.Except(removeTags).Any())
                {
                    isLive = false;
                }
            }
            if (isLive)
            {
                liveKeys.Add(key);
            }
        }

        var keysToRemove = tree.Nodes.Keys.Where(k => !liveKeys.Contains(k)).ToList();

        foreach (var key in keysToRemove)
        {
            tree.Nodes.Remove(key);
        }
    }
}