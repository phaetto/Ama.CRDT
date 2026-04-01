namespace Ama.CRDT.UnitTests.Services.Strategies.Decorators;

using Ama.CRDT.Attributes.Decorators;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Decorators;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Models.Intents.Decorators;
using Ama.CRDT.Services;
using Ama.CRDT.Services.GarbageCollection;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Strategies;
using Ama.CRDT.Services.Strategies.Decorators;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using Xunit;

public sealed class EpochBoundStrategyTests : IDisposable
{
    private readonly IServiceScope scope;
    private readonly ICrdtStrategyProvider strategyProvider;
    private readonly ICrdtPatcher patcher;
    private readonly ICrdtApplicator applicator;
    private readonly ICrdtTimestampProvider timestampProvider;

    public EpochBoundStrategyTests()
    {
        var serviceProvider = new ServiceCollection()
            .AddCrdt()
            .BuildServiceProvider();

        scope = serviceProvider.GetRequiredService<ICrdtScopeFactory>().CreateScope("TestReplica");

        strategyProvider = scope.ServiceProvider.GetRequiredService<ICrdtStrategyProvider>();
        patcher = scope.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        applicator = scope.ServiceProvider.GetRequiredService<ICrdtApplicator>();
        timestampProvider = scope.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();
    }

    public void Dispose()
    {
        scope.Dispose();
    }

    public sealed class ShoppingCart
    {
        [CrdtEpochBound]
        [CrdtLwwStrategy]
        public string Status { get; set; } = string.Empty;

        [CrdtEpochBound]
        [CrdtLwwMapStrategy]
        public Dictionary<string, string> Items { get; set; } = new();
    }

    [Fact]
    public void Provider_ShouldResolve_DecoratorAndBaseStrategy()
    {
        var property = typeof(ShoppingCart).GetProperty(nameof(ShoppingCart.Status));
        property.ShouldNotBeNull();

        var strategy = strategyProvider.GetStrategy(property);
        strategy.ShouldBeOfType<EpochBoundStrategy>();

        var baseStrategy = strategyProvider.GetBaseStrategy(property);
        baseStrategy.ShouldBeOfType<LwwStrategy>();
    }

    [Fact]
    public void GeneratePatch_ShouldWrapOperations_InEpochPayload()
    {
        var fromDoc = new CrdtDocument<ShoppingCart>(new ShoppingCart { Status = "Open" }, new CrdtMetadata());
        var toState = new ShoppingCart { Status = "Closed" };

        var patch = patcher.GeneratePatch(fromDoc, toState);

        patch.Operations.Count.ShouldBe(1);
        var op = patch.Operations[0];
        
        op.JsonPath.ShouldBe("$.status");
        op.Value.ShouldBeOfType<EpochPayload>();

        var payload = (EpochPayload)op.Value;
        payload.Epoch.ShouldBe(0); // Default epoch
        payload.Value.ShouldBe("Closed");
    }

    [Fact]
    public void GeneratePatch_ChildProperty_ShouldInheritParentEpoch()
    {
        var fromDoc = new CrdtDocument<ShoppingCart>(new ShoppingCart { Items = new Dictionary<string, string> { { "Key1", "Val1" } } }, new CrdtMetadata());
        fromDoc.Metadata.Epochs["$.items"] = 4; // Parent at epoch 4

        var toState = new ShoppingCart { Items = new Dictionary<string, string> { { "Key1", "Val2" } } };

        var patch = patcher.GeneratePatch(fromDoc, toState);

        patch.Operations.Count.ShouldBe(1);
        var op = patch.Operations[0];
        
        op.JsonPath.ShouldStartWith("$.items"); // Inner strategy may or may not append key based on its own logic
        op.Value.ShouldBeOfType<EpochPayload>();

        var payload = (EpochPayload)op.Value;
        payload.Epoch.ShouldBe(4); // Inherits 4 from parent
    }

    [Fact]
    public void GenerateOperation_ChildPath_ShouldInheritParentEpoch()
    {
        var doc = new CrdtDocument<ShoppingCart>(new ShoppingCart(), new CrdtMetadata());
        doc.Metadata.Epochs["$.items"] = 3; // Parent is at epoch 3

        // Use GenerateOperation on a child path with Set intent
        var op = patcher.GenerateOperation(doc, x => x.Items, new MapSetIntent("Key1", "Val1"));

        op.JsonPath.ShouldStartWith("$.items"); // Inner strategy defines the final generated path layout
        op.Value.ShouldBeOfType<EpochPayload>();

        var payload = (EpochPayload)op.Value;
        payload.Epoch.ShouldBe(3); // Should inherit 3 from parent, not default to 0
    }

    [Fact]
    public void GenerateOperation_EpochClearIntent_ShouldBumpEpoch_AndEmitNullValue()
    {
        var doc = new CrdtDocument<ShoppingCart>(new ShoppingCart(), new CrdtMetadata());
        doc.Metadata.Epochs["$.items"] = 5; // Start at epoch 5

        var op = patcher.GenerateOperation(doc, x => x.Items, new EpochClearIntent());

        op.Type.ShouldBe(OperationType.Remove);
        op.JsonPath.ShouldBe("$.items");
        op.Value.ShouldBeOfType<EpochPayload>();

        var payload = (EpochPayload)op.Value;
        payload.Epoch.ShouldBe(6); // Bumped to 6
        payload.Value.ShouldBeNull(); // Clear intent payload
    }

    [Fact]
    public void ApplyOperation_WithOlderEpoch_ShouldBeDiscarded()
    {
        var doc = new CrdtDocument<ShoppingCart>(new ShoppingCart { Status = "Current" }, new CrdtMetadata());
        doc.Metadata.Epochs["$.status"] = 2; // Local epoch is 2

        var oldOp = new CrdtOperation(
            Guid.NewGuid(),
            Guid.NewGuid().ToString(),
            "$.status",
            OperationType.Upsert,
            new EpochPayload(1, "OldValue"), // Epoch 1 (older)
            timestampProvider.Now(),
            1);

        applicator.ApplyPatch(doc, new CrdtPatch([oldOp]));

        // Status should remain unchanged because epoch 1 < epoch 2
        doc.Data.Status.ShouldBe("Current");
        doc.Metadata.Epochs["$.status"].ShouldBe(2);
    }

    [Fact]
    public void ApplyOperation_WithNewerEpoch_ShouldClearState_AndApply()
    {
        var doc = new CrdtDocument<ShoppingCart>(new ShoppingCart
        {
            Status = "OldStatus"
        }, new CrdtMetadata());
        
        doc.Metadata.Epochs["$.status"] = 1; // Local epoch is 1

        var newEpochOp = new CrdtOperation(
            Guid.NewGuid(),
            Guid.NewGuid().ToString(),
            "$.status",
            OperationType.Upsert,
            new EpochPayload(2, "NewStatus"), // New epoch!
            timestampProvider.Now(),
            1);

        applicator.ApplyPatch(doc, new CrdtPatch([newEpochOp]));

        // State should be cleared of old items and properly bumped, applying the inner payload
        doc.Metadata.Epochs["$.status"].ShouldBe(2);
        doc.Data.Status.ShouldBe("NewStatus");
    }

    [Fact]
    public void ApplyOperation_EpochClearIntent_ShouldClearStateAndBumpLocalEpoch()
    {
        var doc = new CrdtDocument<ShoppingCart>(new ShoppingCart
        {
            Items = new Dictionary<string, string> { { "Key1", "Val1" } }
        }, new CrdtMetadata());
        
        doc.Metadata.Epochs["$.items"] = 1;

        var clearOp = new CrdtOperation(
            Guid.NewGuid(),
            Guid.NewGuid().ToString(),
            "$.items",
            OperationType.Remove,
            new EpochPayload(2, null), // Emitted by EpochClearIntent
            timestampProvider.Now(),
            1);

        applicator.ApplyPatch(doc, new CrdtPatch([clearOp]));

        doc.Metadata.Epochs["$.items"].ShouldBe(2);
        doc.Data.Items.ShouldBeEmpty();
    }

    [Fact]
    public void ApplyOperation_ParentClear_ShouldRemoveLingeringChildEpochs()
    {
        var doc = new CrdtDocument<ShoppingCart>(new ShoppingCart
        {
            Items = new Dictionary<string, string> { { "Key1", "Val1" } }
        }, new CrdtMetadata());
        
        // Simulate child op arriving out-of-order before parent clear
        doc.Metadata.Epochs["$.items['Key1']"] = 1;
        doc.Metadata.Epochs["$.items"] = 1;

        // Now parent clear arrives with Epoch 2
        var clearOp = patcher.GenerateOperation(doc, x => x.Items, new EpochClearIntent());

        applicator.ApplyPatch(doc, new CrdtPatch([clearOp]));

        // Parent should be bumped
        doc.Metadata.Epochs["$.items"].ShouldBe(2);
        
        // Lingering child epoch must be gone, otherwise future operations will evaluate against it!
        doc.Metadata.Epochs.ContainsKey("$.items['Key1']").ShouldBeFalse();

        // Future operation on child with epoch 2 should succeed and NOT trigger a clear
        var childOp = patcher.GenerateOperation(doc, x => x.Items, new MapSetIntent("Key2", "Val2"));

        applicator.ApplyPatch(doc, new CrdtPatch([childOp]));
        
        doc.Data.Items.ContainsKey("Key2").ShouldBeTrue();
        doc.Data.Items["Key2"].ShouldBe("Val2");
    }

    [Fact]
    public void Compact_ShouldNotModifyDecoratorMetadata_AndDelegateToInnerStrategy()
    {
        // Arrange
        var property = typeof(ShoppingCart).GetProperty(nameof(ShoppingCart.Status))!;
        var strategy = strategyProvider.GetStrategy(property);
        
        var metadata = new CrdtMetadata();
        metadata.Epochs["$.status"] = 5;
        
        var mockPolicy = new Mock<ICompactionPolicy>();
        mockPolicy.Setup(p => p.IsSafeToCompact(It.IsAny<CompactionCandidate>())).Returns(true);

        var context = new CompactionContext(metadata, mockPolicy.Object, "Status", "$.status", new ShoppingCart());

        // Act
        strategy.Compact(context);

        // Assert
        metadata.Epochs["$.status"].ShouldBe(5);
    }
}