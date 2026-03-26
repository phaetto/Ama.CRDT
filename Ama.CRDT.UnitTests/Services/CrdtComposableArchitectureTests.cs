namespace Ama.CRDT.UnitTests.Services;

using Ama.CRDT.Attributes.Decorators;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Models.Intents.Decorators;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Providers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

public sealed class CrdtComposableArchitectureTests : IDisposable
{
    private readonly IServiceScope _scope;
    private readonly ICrdtPatcher _patcher;
    private readonly ICrdtApplicator _applicator;
    private readonly ICrdtMetadataManager _metadataManager;
    private readonly ICrdtTimestampProvider _timestampProvider;

    public CrdtComposableArchitectureTests()
    {
        var services = new ServiceCollection();
        services.AddCrdt();
        services.AddSingleton<ICrdtTimestampProvider, EpochTimestampProvider>();
        
        // Register the state machine validator used in the complex composition tests
        services.AddSingleton<DocStatusValidator>();

        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<ICrdtScopeFactory>();
        
        _scope = scopeFactory.CreateScope("test-replica-1");
        _patcher = _scope.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        _applicator = _scope.ServiceProvider.GetRequiredService<ICrdtApplicator>();
        _metadataManager = _scope.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();
        _timestampProvider = _scope.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();
    }

    public void Dispose()
    {
        _scope.Dispose();
    }

    #region Test Models

    private sealed class TestRoot
    {
        public TestLevel1? Level1 { get; set; }
        
        [CrdtSortedSetStrategy(nameof(TestTag.Id))]
        public List<TestTag> Tags { get; set; } = new();
    }

    private sealed class TestLevel1
    {
        public TestLevel2? Level2 { get; set; }
    }

    private sealed class TestLevel2
    {
        public string? Message { get; set; }
        public int Count { get; set; }
    }

    private sealed record TestTag(string Id, string Value);

    public enum DocStatus { Draft, Published, Archived }

    public sealed class DocStatusValidator : IStateMachine<DocStatus>
    {
        public bool IsValidTransition(DocStatus from, DocStatus to)
        {
            if (from == DocStatus.Draft && to == DocStatus.Published) return true;
            if (from == DocStatus.Published && to == DocStatus.Archived) return true;
            if (from == to) return true;
            return false;
        }
    }

    private sealed class ComplexDocument
    {
        public string? Title { get; set; }

        [CrdtMinWinsMapStrategy]
        public Dictionary<string, int> Metrics { get; set; } = new();

        [CrdtLseqStrategy]
        public List<string> Log { get; set; } = new();

        [CrdtStateMachineStrategy(typeof(DocStatusValidator))]
        public DocStatus Status { get; set; } = DocStatus.Draft;

        [CrdtGraphStrategy]
        public CrdtGraph Network { get; set; } = new();

        public NestedConfig? Config { get; set; }
    }

    private sealed class NestedConfig
    {
        public string? SettingA { get; set; }
        
        [CrdtLseqStrategy]
        public List<string> SubLog { get; set; } = new();
    }

    private sealed class DecoratedDocument
    {
        [CrdtEpochBound]
        [CrdtApprovalQuorum(2)]
        public string? ProtectedSecret { get; set; }
    }

    #endregion

    [Fact]
    public void Patcher_ShouldTraverseNestedObjects_AndGenerateLeafOperations()
    {
        // Arrange
        var fromModel = new TestRoot();
        var fromMeta = _metadataManager.Initialize(fromModel);
        var fromDoc = new CrdtDocument<TestRoot>(fromModel, fromMeta);

        var toModel = new TestRoot
        {
            Level1 = new TestLevel1
            {
                Level2 = new TestLevel2
                {
                    Message = "Hello from leaf",
                    Count = 42
                }
            }
        };

        // Act
        var patch = _patcher.GeneratePatch(fromDoc, toModel);

        // Assert
        // Recursion should generate the two leaf-node primitive operations
        patch.Operations.Count.ShouldBe(2);

        var messageOp = patch.Operations.Single(o => o.JsonPath == "$.level1.level2.message");
        messageOp.Type.ShouldBe(OperationType.Upsert);
        messageOp.Value.ShouldBe("Hello from leaf");

        var countOp = patch.Operations.Single(o => o.JsonPath == "$.level1.level2.count");
        countOp.Type.ShouldBe(OperationType.Upsert);
        countOp.Value.ShouldBe(42);
    }

    [Fact]
    public void Applicator_ShouldCreateMissingIntermediateObjects_AndApplyLeafOperations()
    {
        // Arrange
        var rootModel = new TestRoot(); 
        var meta = _metadataManager.Initialize(rootModel);
        var doc = new CrdtDocument<TestRoot>(rootModel, meta);

        var operations = new List<CrdtOperation>
        {
            new CrdtOperation(Guid.NewGuid(), "test-replica-2", "$.level1.level2.message", OperationType.Upsert, "Instantiated dynamically", _timestampProvider.Now(), 1),
            new CrdtOperation(Guid.NewGuid(), "test-replica-2", "$.level1.level2.count", OperationType.Upsert, 99, _timestampProvider.Now(), 2)
        };
        var patch = new CrdtPatch(operations);

        // Act
        _applicator.ApplyPatch(doc, patch);

        // Assert
        rootModel.Level1.ShouldNotBeNull();
        rootModel.Level1.Level2.ShouldNotBeNull();
        
        rootModel.Level1.Level2.Message.ShouldBe("Instantiated dynamically");
        rootModel.Level1.Level2.Count.ShouldBe(99);
    }

    [Fact]
    public void Patcher_WhenNestedObjectSetToNull_ShouldGenerateOptimizedRemoveOperationForParent()
    {
        // Arrange
        var fromModel = new TestRoot
        {
            Level1 = new TestLevel1
            {
                Level2 = new TestLevel2
                {
                    Message = "To be removed",
                    Count = 10
                }
            }
        };
        var fromMeta = _metadataManager.Initialize(fromModel);
        // Explicitly set the parent object's initialization timestamp into the past 
        // to prevent it from identically matching the change generation timestamp 
        // within the same millisecond and causing conflict suppression.
        fromMeta.Lww["$.level1"] = new CausalTimestamp(_timestampProvider.Create(1L), "test-replica-1", 1);

        var fromDoc = new CrdtDocument<TestRoot>(fromModel, fromMeta);
        var toModel = new TestRoot { Level1 = null };

        // Act
        var patch = _patcher.GeneratePatch(fromDoc, toModel);

        // Assert
        patch.Operations.Count.ShouldBe(1);
        
        // When setting a whole POCO to null, instead of tracking a billion leaf node deletes, 
        // the patcher natively optimizes it to a single parent Remove operation.
        var parentRemoveOp = patch.Operations.Single(o => o.JsonPath == "$.level1");
        parentRemoveOp.Type.ShouldBe(OperationType.Remove);
        parentRemoveOp.Value.ShouldBeNull();
    }

    [Fact]
    public void ComplexComposition_PatcherAndApplicator_ShouldSynchronizeAllStrategies()
    {
        // Arrange - Setup initial document
        var model1 = new ComplexDocument();
        var meta1 = _metadataManager.Initialize(model1);
        var doc1 = new CrdtDocument<ComplexDocument>(model1, meta1);

        // Create a heavily modified state leveraging all configured strategies
        var modified = new ComplexDocument
        {
            Title = "Complex Sync",
            Status = DocStatus.Published, // Valid transition (Draft -> Published)
            Config = new NestedConfig { SettingA = "Enabled" }
        };
        modified.Metrics["cpu"] = 10;
        modified.Metrics["mem"] = 512;
        modified.Log.Add("init");
        modified.Log.Add("started");
        modified.Network.Vertices.Add("NodeA");
        modified.Network.Vertices.Add("NodeB");
        modified.Config.SubLog.Add("sub-init");

        // Act - Generate a patch capturing the entire complex delta
        var patch = _patcher.GeneratePatch(doc1, modified);

        // Prepare an empty document to simulate replica 2 receiving the patch
        var model2 = new ComplexDocument();
        var meta2 = _metadataManager.Initialize(model2);
        var doc2 = new CrdtDocument<ComplexDocument>(model2, meta2);

        _applicator.ApplyPatch(doc2, patch);

        // Assert - Verify that the applicator successfully resolved all strategies deeply
        model2.Title.ShouldBe("Complex Sync");
        model2.Status.ShouldBe(DocStatus.Published);
        model2.Metrics["cpu"].ShouldBe(10);
        model2.Metrics["mem"].ShouldBe(512);
        model2.Log.ShouldBe(new[] { "init", "started" });
        model2.Network.Vertices.ShouldContain("NodeA");
        model2.Network.Vertices.ShouldContain("NodeB");
        model2.Config.ShouldNotBeNull();
        model2.Config.SettingA.ShouldBe("Enabled");
        model2.Config.SubLog.ShouldBe(new[] { "sub-init" });
    }

    [Fact]
    public void ComplexComposition_IntentGeneration_ShouldApplyCorrectlyToDeepPaths()
    {
        // Arrange
        var model = new ComplexDocument { Config = new NestedConfig() };
        var meta = _metadataManager.Initialize(model);
        var doc = new CrdtDocument<ComplexDocument>(model, meta);

        // Act - Explicitly generate intent-based operations addressing deep strategies
        // We apply them individually to accurately simulate distinct sequential user actions, 
        // updating the internal version vector to reflect the correct monotonically increasing clock.
        var op1 = _patcher.GenerateOperation(doc, d => d.Metrics, new MapSetIntent("cpu", 5));
        _applicator.ApplyPatch(doc, new CrdtPatch([op1]));

        var op2 = _patcher.GenerateOperation(doc, d => d.Log, new AddIntent("first entry"));
        _applicator.ApplyPatch(doc, new CrdtPatch([op2]));

        var op3 = _patcher.GenerateOperation(doc, d => d.Network, new AddVertexIntent("V1"));
        _applicator.ApplyPatch(doc, new CrdtPatch([op3]));

        var op4 = _patcher.GenerateOperation(doc, d => d.Status, new SetIntent(DocStatus.Published));
        _applicator.ApplyPatch(doc, new CrdtPatch([op4]));

        var op5 = _patcher.GenerateOperation(doc, d => d.Config!.SubLog, new AddIntent("deep entry"));
        _applicator.ApplyPatch(doc, new CrdtPatch([op5]));

        // Assert - Verify the intent executions correctly mapped to the underlying strategies
        model.Metrics["cpu"].ShouldBe(5);
        model.Log.ShouldContain("first entry");
        model.Network.Vertices.ShouldContain("V1");
        model.Status.ShouldBe(DocStatus.Published);
        model.Config.ShouldNotBeNull();
        model.Config.SubLog.ShouldContain("deep entry");
    }

    [Fact]
    public void ComplexComposition_StateMachine_ShouldRejectInvalidTransitionsDuringPatchGeneration()
    {
        // Arrange
        var model = new ComplexDocument { Status = DocStatus.Draft };
        var meta = _metadataManager.Initialize(model);
        var doc = new CrdtDocument<ComplexDocument>(model, meta);

        // Attempting an invalid transition (Draft -> Archived directly)
        var modified = new ComplexDocument { Status = DocStatus.Archived };

        // Act
        var patch = _patcher.GeneratePatch(doc, modified);

        // Assert - The StateMachine strategy must discard the invalid transition
        patch.Operations.ShouldNotContain(o => o.JsonPath == "$.status");
    }

    [Fact]
    public void ComplexComposition_MinWinsMap_ShouldConvergeToLowestValue()
    {
        using var scope2 = _scope.ServiceProvider.GetRequiredService<ICrdtScopeFactory>().CreateScope("test-replica-2");
        var patcher2 = scope2.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        var applicator2 = scope2.ServiceProvider.GetRequiredService<ICrdtApplicator>();
        var metadataManager2 = scope2.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();

        // Arrange - Shared Initial State
        var model1 = new ComplexDocument();
        model1.Metrics["latency"] = 50;
        var meta1 = _metadataManager.Initialize(model1);
        var doc1 = new CrdtDocument<ComplexDocument>(model1, meta1);
        
        var model2 = new ComplexDocument();
        model2.Metrics["latency"] = 50;
        var meta2 = metadataManager2.Initialize(model2);
        var doc2 = new CrdtDocument<ComplexDocument>(model2, meta2);

        // Act - Concurrent Conflicting Changes
        // Replica 1 improves latency down to 40
        var mod1 = new ComplexDocument();
        mod1.Metrics["latency"] = 40;
        var patch1 = _patcher.GeneratePatch(doc1, mod1);
        _applicator.ApplyPatch(doc1, patch1); // Applying locally shifts Replica 1 to 40

        // Replica 2 radically improves latency down to 20
        var mod2 = new ComplexDocument();
        mod2.Metrics["latency"] = 20;
        var patch2 = patcher2.GeneratePatch(doc2, mod2);
        applicator2.ApplyPatch(doc2, patch2); // Applying locally shifts Replica 2 to 20

        // Exchange - Network sync
        _applicator.ApplyPatch(doc1, patch2);
        applicator2.ApplyPatch(doc2, patch1); // 40 is ignored because 20 is mathematically lower

        // Assert - Both replicas converge to the mathematically lowest value dynamically
        model1.Metrics["latency"].ShouldBe(20);
        model2.Metrics["latency"].ShouldBe(20);
    }

    [Fact]
    public void ComplexComposition_Decorators_ShouldWrapOperationsAndExecuteCorrectly()
    {
        using var scope2 = _scope.ServiceProvider.GetRequiredService<ICrdtScopeFactory>().CreateScope("test-replica-2");
        var patcher2 = scope2.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        var applicator2 = scope2.ServiceProvider.GetRequiredService<ICrdtApplicator>();
        var metadataManager2 = scope2.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();

        // Arrange - Setup replica 1
        var model1 = new DecoratedDocument();
        var meta1 = _metadataManager.Initialize(model1);
        var doc1 = new CrdtDocument<DecoratedDocument>(model1, meta1);

        // Arrange - Setup replica 2
        var model2 = new DecoratedDocument();
        var meta2 = metadataManager2.Initialize(model2);
        var doc2 = new CrdtDocument<DecoratedDocument>(model2, meta2);

        // Act - Replica 1 proposes a change
        var modified1 = new DecoratedDocument { ProtectedSecret = "ApprovedSecret" };
        var patch1 = _patcher.GeneratePatch(doc1, modified1);
        
        // Replica 1 applies its own proposal - Value should remain null because quorum is 2
        _applicator.ApplyPatch(doc1, patch1);
        model1.ProtectedSecret.ShouldBeNull();

        // Replica 2 generates identical proposal
        var modified2 = new DecoratedDocument { ProtectedSecret = "ApprovedSecret" };
        var patch2 = patcher2.GeneratePatch(doc2, modified2);

        // Replica 2 applies its own proposal - Value should remain null because quorum is 2
        applicator2.ApplyPatch(doc2, patch2);
        model2.ProtectedSecret.ShouldBeNull();

        // Exchange proposals
        _applicator.ApplyPatch(doc1, patch2); // Replica 1 gets Replica 2's vote
        applicator2.ApplyPatch(doc2, patch1); // Replica 2 gets Replica 1's vote

        // Assert - Both replicas reached quorum (2 proposals), unwrapped the payload, and applied the inner LWW strategy
        model1.ProtectedSecret.ShouldBe("ApprovedSecret");
        model2.ProtectedSecret.ShouldBe("ApprovedSecret");

        // Act - Explicit EpochClear intent to bump epoch and reset state
        var clearOp = _patcher.GenerateOperation(doc1, d => d.ProtectedSecret, new EpochClearIntent());
        
        // Even the EpochClear requires a quorum because it is wrapped by the outer ApprovalQuorum!
        _applicator.ApplyPatch(doc1, new CrdtPatch([clearOp]));
        model1.ProtectedSecret.ShouldBe("ApprovedSecret"); // Not cleared yet
        
        // Replica 2 sends the same clear intent
        var clearOp2 = patcher2.GenerateOperation(doc2, d => d.ProtectedSecret, new EpochClearIntent());
        
        // Replica 2 applies its own clear intent
        applicator2.ApplyPatch(doc2, new CrdtPatch([clearOp2]));
        model2.ProtectedSecret.ShouldBe("ApprovedSecret"); // Not cleared yet

        // Exchange clear intents
        _applicator.ApplyPatch(doc1, new CrdtPatch([clearOp2]));
        applicator2.ApplyPatch(doc2, new CrdtPatch([clearOp]));

        // Assert - Quorum met for clear intent, Epoch bumps, and state clears
        model1.ProtectedSecret.ShouldBeNull();
        model2.ProtectedSecret.ShouldBeNull();
    }
}