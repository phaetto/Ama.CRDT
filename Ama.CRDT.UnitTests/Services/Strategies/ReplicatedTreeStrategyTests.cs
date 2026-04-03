namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services;
using Ama.CRDT.Services.GarbageCollection;
using Ama.CRDT.Services.Providers;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

public sealed class ReplicatedTreeStrategyTests : IDisposable
{
    internal sealed class TestModel
    {
        [CrdtReplicatedTreeStrategy]
        public CrdtTree Tree { get; set; } = new();
    }

    private readonly IServiceScope scopeA;
    private readonly IServiceScope scopeB;
    private readonly ICrdtPatcher patcherA;
    private readonly ICrdtPatcher patcherB;
    private readonly ICrdtApplicator applicator;
    private readonly ICrdtMetadataManager metadataManager;
    private readonly ICrdtTimestampProvider timestampProvider;

    public ReplicatedTreeStrategyTests()
    {
        var serviceProvider = new ServiceCollection()
            .AddCrdt()
            .AddCrdtAotContext<ReplicatedTreeStrategyTestCrdtAotContext>()
            .BuildServiceProvider();

        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();

        scopeA = scopeFactory.CreateScope("A");
        scopeB = scopeFactory.CreateScope("B");

        patcherA = scopeA.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        patcherB = scopeB.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        applicator = scopeA.ServiceProvider.GetRequiredService<ICrdtApplicator>();
        metadataManager = scopeA.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();
        timestampProvider = scopeA.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();
    }

    public void Dispose()
    {
        scopeA.Dispose();
        scopeB.Dispose();
    }

    [Fact]
    public void ApplyPatch_WithAddNodeOperation_IsIdempotent()
    {
        // Arrange
        var ancestor = new TestModel();
        var docAncestor = new CrdtDocument<TestModel>(ancestor, metadataManager.Initialize(ancestor));

        var nodeId = Guid.NewGuid();
        var replicaAState = new TestModel();
        replicaAState.Tree.Nodes.Add(nodeId, new TreeNode { Id = nodeId, Value = "A" });
        var patch = patcherA.GeneratePatch(docAncestor, replicaAState);

        var target = new TestModel();
        var targetDocument = new CrdtDocument<TestModel>(target, metadataManager.Initialize(target));

        // Act
        applicator.ApplyPatch(targetDocument, patch);
        var nodesAfterFirstApply = new Dictionary<object, TreeNode>(target.Tree.Nodes);

        applicator.ApplyPatch(targetDocument, patch); // Apply second time

        // Assert
        target.Tree.Nodes.Count.ShouldBe(1);
        target.Tree.Nodes.Select(x => x.Value.ToString())
            .ShouldBe(nodesAfterFirstApply.Select(x => x.Value.ToString()));
    }

    [Fact]
    public void ApplyPatch_WithConcurrentAdds_IsCommutative()
    {
        // Arrange
        var ancestor = new TestModel();
        var docAncestor = new CrdtDocument<TestModel>(ancestor, metadataManager.Initialize(ancestor));

        var nodeAId = Guid.NewGuid();
        var stateA = new TestModel();
        stateA.Tree.Nodes.Add(nodeAId, new TreeNode { Id = nodeAId, Value = "A" });
        var patchA = patcherA.GeneratePatch(docAncestor, stateA);

        var nodeBId = Guid.NewGuid();
        var stateB = new TestModel();
        stateB.Tree.Nodes.Add(nodeBId, new TreeNode { Id = nodeBId, Value = "B" });
        var patchB = patcherB.GeneratePatch(docAncestor, stateB);

        // Act: Scenario 1 (A then B)
        var model1 = new TestModel();
        var doc1 = new CrdtDocument<TestModel>(model1, metadataManager.Initialize(model1));
        applicator.ApplyPatch(doc1, patchA);
        applicator.ApplyPatch(doc1, patchB);

        // Act: Scenario 2 (B then A)
        var model2 = new TestModel();
        var doc2 = new CrdtDocument<TestModel>(model2, metadataManager.Initialize(model2));
        applicator.ApplyPatch(doc2, patchB);
        applicator.ApplyPatch(doc2, patchA);

        // Assert
        model1.Tree.Nodes.Count.ShouldBe(2);
        model1.Tree.Nodes.Keys.ShouldContain(nodeAId);
        model1.Tree.Nodes.Keys.ShouldContain(nodeBId);

        model2.Tree.Nodes.Count.ShouldBe(2);
        model2.Tree.Nodes.Keys.ShouldBe(model1.Tree.Nodes.Keys, ignoreOrder: true);
    }

    [Fact]
    public void Converge_ConcurrentMove_ResolvesWithLwwAndIsCommutative()
    {
        // Arrange
        var rootId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var parentAId = Guid.NewGuid();
        var parentBId = Guid.NewGuid();

        var ancestor = new TestModel();
        ancestor.Tree.Nodes.Add(rootId, new TreeNode { Id = rootId, Value = "Root" });
        ancestor.Tree.Nodes.Add(childId, new TreeNode { Id = childId, Value = "Child", ParentId = rootId });
        ancestor.Tree.Nodes.Add(parentAId, new TreeNode { Id = parentAId, Value = "ParentA", ParentId = rootId });
        ancestor.Tree.Nodes.Add(parentBId, new TreeNode { Id = parentBId, Value = "ParentB", ParentId = rootId });
        var docAncestor = new CrdtDocument<TestModel>(ancestor, metadataManager.Initialize(ancestor));

        // Replica A moves Child to ParentA
        var stateA = new TestModel { Tree = { Nodes = new Dictionary<object, TreeNode>(ancestor.Tree.Nodes) } };
        stateA.Tree.Nodes[childId] = new TreeNode { Id = childId, Value = "Child", ParentId = parentAId };
        var patchA = patcherA.GeneratePatch(docAncestor, stateA, timestampProvider.Create(1)); // Explicit timestamp

        // Replica B moves Child to ParentB (with a later timestamp)
        var stateB = new TestModel { Tree = { Nodes = new Dictionary<object, TreeNode>(ancestor.Tree.Nodes) } };
        stateB.Tree.Nodes[childId] = new TreeNode { Id = childId, Value = "Child", ParentId = parentBId };
        var patchB = patcherB.GeneratePatch(docAncestor, stateB, timestampProvider.Create(3)); // Explicit later timestamp

        // Act: Scenario 1 (A then B)
        var model1 = new TestModel { Tree = { Nodes = new Dictionary<object, TreeNode>(ancestor.Tree.Nodes) } };
        var doc1 = new CrdtDocument<TestModel>(model1, docAncestor.Metadata.DeepClone());
        applicator.ApplyPatch(doc1, patchA);
        applicator.ApplyPatch(doc1, patchB);

        // Act: Scenario 2 (B then A)
        var model2 = new TestModel { Tree = { Nodes = new Dictionary<object, TreeNode>(ancestor.Tree.Nodes) } };
        var doc2 = new CrdtDocument<TestModel>(model2, docAncestor.Metadata.DeepClone());
        applicator.ApplyPatch(doc2, patchB);
        applicator.ApplyPatch(doc2, patchA);

        // Assert
        doc1.Data.Tree.Nodes[childId].ParentId.ShouldBe(parentBId);
        doc2.Data.Tree.Nodes[childId].ParentId.ShouldBe(parentBId);
    }
    
    [Fact]
    public void Converge_ConcurrentAddAndRemove_AllowsReAddingNode()
    {
        // Arrange
        var nodeId = Guid.NewGuid();
        var ancestor = new TestModel();
        ancestor.Tree.Nodes.Add(nodeId, new TreeNode { Id = nodeId, Value = "A" });
        var docAncestor = new CrdtDocument<TestModel>(ancestor, metadataManager.Initialize(ancestor));

        // A removes node
        var patchRemove = patcherA.GeneratePatch(docAncestor, new TestModel());

        // B adds the same node again (concurrently, based on empty state)
        var patchAdd = patcherB.GeneratePatch(new CrdtDocument<TestModel>(new TestModel(), metadataManager.Initialize(new TestModel())), ancestor);
        
        // Act: Scenario 1 (Remove then Add)
        var model1 = new TestModel { Tree = { Nodes = new Dictionary<object, TreeNode>(ancestor.Tree.Nodes) } };
        var doc1 = new CrdtDocument<TestModel>(model1, docAncestor.Metadata.DeepClone());
        applicator.ApplyPatch(doc1, patchRemove);
        applicator.ApplyPatch(doc1, patchAdd);

        // Act: Scenario 2 (Add then Remove)
        var model2 = new TestModel { Tree = { Nodes = new Dictionary<object, TreeNode>(ancestor.Tree.Nodes) } };
        var doc2 = new CrdtDocument<TestModel>(model2, docAncestor.Metadata.DeepClone());
        applicator.ApplyPatch(doc2, patchAdd);
        applicator.ApplyPatch(doc2, patchRemove);
        
        // Assert
        doc1.Data.Tree.Nodes.Count.ShouldBe(1);
        doc1.Data.Tree.Nodes.Keys.ShouldContain(nodeId);

        doc2.Data.Tree.Nodes.Count.ShouldBe(1);
        doc2.Data.Tree.Nodes.Keys.ShouldContain(nodeId);
    }

    [Fact]
    public void GenerateOperation_AddNodeIntent_GeneratesAndAppliesCorrectly()
    {
        // Arrange
        var model = new TestModel();
        var doc = new CrdtDocument<TestModel>(model, metadataManager.Initialize(model));
        var nodeId = Guid.NewGuid();
        var node = new TreeNode { Id = nodeId, Value = "NewNode", ParentId = null };
        var intent = new AddNodeIntent(node);

        // Act
        var operation = patcherA.GenerateOperation(doc, m => m.Tree, intent);
        var patch = new CrdtPatch(new List<CrdtOperation> { operation });
        applicator.ApplyPatch(doc, patch);

        // Assert
        model.Tree.Nodes.ShouldContainKey(nodeId);
        model.Tree.Nodes[nodeId].Value.ShouldBe("NewNode");
    }

    [Fact]
    public void GenerateOperation_RemoveNodeIntent_GeneratesAndAppliesCorrectly()
    {
        // Arrange
        var model = new TestModel();
        var doc = new CrdtDocument<TestModel>(model, metadataManager.Initialize(model));
        var nodeId = Guid.NewGuid();
        var node = new TreeNode { Id = nodeId, Value = "To Remove", ParentId = null };
        
        var addOp = patcherA.GenerateOperation(doc, m => m.Tree, new AddNodeIntent(node));
        applicator.ApplyPatch(doc, new CrdtPatch(new List<CrdtOperation> { addOp }));
        
        // Act
        var intent = new RemoveNodeIntent(nodeId);
        var removeOp = patcherA.GenerateOperation(doc, m => m.Tree, intent);
        applicator.ApplyPatch(doc, new CrdtPatch(new List<CrdtOperation> { removeOp }));

        // Assert
        model.Tree.Nodes.ShouldNotContainKey(nodeId);
    }

    [Fact]
    public void GenerateOperation_MoveNodeIntent_GeneratesAndAppliesCorrectly()
    {
        // Arrange
        var model = new TestModel();
        var doc = new CrdtDocument<TestModel>(model, metadataManager.Initialize(model));
        var nodeId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var node = new TreeNode { Id = nodeId, Value = "To Move", ParentId = null };
        
        var addOp = patcherA.GenerateOperation(doc, m => m.Tree, new AddNodeIntent(node));
        applicator.ApplyPatch(doc, new CrdtPatch(new List<CrdtOperation> { addOp }));
        
        // Act
        var intent = new MoveNodeIntent(nodeId, parentId);
        var moveOp = patcherA.GenerateOperation(doc, m => m.Tree, intent);
        applicator.ApplyPatch(doc, new CrdtPatch(new List<CrdtOperation> { moveOp }));

        // Assert
        model.Tree.Nodes[nodeId].ParentId.ShouldBe(parentId);
    }

    [Fact]
    public void Compact_ShouldRemoveTombstones_WhenPolicyAllows()
    {
        // Arrange
        var mockPolicy = new Mock<ICompactionPolicy>();
        // Return true if ReplicaId == "replica-1" and Version <= 5
        mockPolicy.Setup(p => p.IsSafeToCompact(It.Is<CompactionCandidate>(c => c.ReplicaId == "replica-1" && c.Version <= 5))).Returns(true);
        mockPolicy.Setup(p => p.IsSafeToCompact(It.Is<CompactionCandidate>(c => c.ReplicaId != "replica-1" || c.Version > 5))).Returns(false);
        
        var doc = new TestModel();
        var meta = new CrdtMetadata();
        var strategy = scopeA.ServiceProvider.GetServices<CRDT.Services.Strategies.ICrdtStrategy>().OfType<CRDT.Services.Strategies.ReplicatedTreeStrategy>().Single();

        var nodeId = Guid.NewGuid();
        var tagSafe = Guid.NewGuid();
        var tagUnsafe = Guid.NewGuid();
        var tagOther = Guid.NewGuid();

        var adds = new Dictionary<object, ISet<Guid>> { { nodeId, new HashSet<Guid> { tagSafe, tagUnsafe, tagOther } } };
        
        var removes = new Dictionary<object, IDictionary<Guid, CausalTimestamp>>
        {
            {
                nodeId, new Dictionary<Guid, CausalTimestamp>
                {
                    { tagSafe, new CausalTimestamp(timestampProvider.Create(100), "replica-1", 5) },
                    { tagUnsafe, new CausalTimestamp(timestampProvider.Create(200), "replica-1", 10) },
                    { tagOther, new CausalTimestamp(timestampProvider.Create(150), "replica-2", 3) }
                }
            }
        };

        meta.ReplicatedTrees["$.tree"] = new OrSetState(adds, removes);

        var context = new CRDT.Services.Strategies.CompactionContext(meta, mockPolicy.Object, "Tree", "$.tree", doc);

        // Act
        strategy.Compact(context);

        // Assert
        var state = meta.ReplicatedTrees["$.tree"];
        state.Removes[nodeId].ShouldNotContainKey(tagSafe); // Compacted
        state.Removes[nodeId].ShouldContainKey(tagUnsafe); // Not compacted (Version > 5)
        state.Removes[nodeId].ShouldContainKey(tagOther); // Not compacted (Wrong replica)

        state.Adds[nodeId].ShouldNotContain(tagSafe); // Add tag removed along with remove tag
        state.Adds[nodeId].ShouldContain(tagUnsafe);
        state.Adds[nodeId].ShouldContain(tagOther);
    }
}