namespace Ama.CRDT.UnitTests.Services;

using Ama.CRDT.Attributes;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
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
            new CrdtOperation(Guid.NewGuid(), "test-replica-2", "$.level1.level2.message", OperationType.Upsert, "Instantiated dynamically", _timestampProvider.Now()),
            new CrdtOperation(Guid.NewGuid(), "test-replica-2", "$.level1.level2.count", OperationType.Upsert, 99, _timestampProvider.Now())
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
        fromMeta.Lww["$.level1"] = _timestampProvider.Create(1L);

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
}