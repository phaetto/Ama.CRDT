namespace Ama.CRDT.UnitTests.Services.Strategies.Decorators;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Decorators;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Models.Decorators;
using Ama.CRDT.Models.Intents;
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

[CrdtAotType(typeof(ApprovalQuorumStrategyTests.ProposalDocument))]
internal partial class ApprovalQuorumTestCrdtAotContext : CrdtAotContext { }

public sealed class ApprovalQuorumStrategyTests : IDisposable
{
    private static readonly CrdtPropertyInfo ConfigValueProperty = new(
        nameof(ProposalDocument.ConfigValue),
        "configValue",
        typeof(string),
        true,
        true,
        obj => ((ProposalDocument)obj).ConfigValue,
        (obj, val) => ((ProposalDocument)obj).ConfigValue = (string)val!,
        new CrdtLwwStrategyAttribute(),
        new CrdtStrategyDecoratorAttribute[] { new CrdtApprovalQuorumAttribute(2) });

    private readonly IServiceScope scope;
    private readonly ICrdtStrategyProvider strategyProvider;
    private readonly ICrdtPatcher patcher;
    private readonly ICrdtApplicator applicator;
    private readonly ICrdtTimestampProvider timestampProvider;

    public ApprovalQuorumStrategyTests()
    {
        var serviceProvider = new ServiceCollection()
            .AddCrdt()
            .AddCrdtAotContext<ApprovalQuorumTestCrdtAotContext>()
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

    public sealed class ProposalDocument
    {
        [CrdtApprovalQuorum(2)]
        [CrdtLwwStrategy]
        public string ConfigValue { get; set; } = string.Empty;
    }

    [Fact]
    public void Provider_ShouldResolve_DecoratorAndBaseStrategy()
    {
        var property = ConfigValueProperty;
        property.ShouldNotBeNull();

        var strategy = strategyProvider.GetStrategy(typeof(ProposalDocument), property);
        strategy.ShouldBeOfType<ApprovalQuorumStrategy>();

        var baseStrategy = strategyProvider.GetBaseStrategy(typeof(ProposalDocument), property);
        baseStrategy.ShouldBeOfType<LwwStrategy>();
    }

    [Fact]
    public void GeneratePatch_ShouldWrapOperations_InQuorumPayload()
    {
        var fromDoc = new CrdtDocument<ProposalDocument>(new ProposalDocument { ConfigValue = "Old" }, new CrdtMetadata());
        var toState = new ProposalDocument { ConfigValue = "New" };

        var patch = patcher.GeneratePatch(fromDoc, toState);

        patch.Operations.Count.ShouldBe(1);
        var op = patch.Operations[0];
        
        op.JsonPath.ShouldBe("$.configValue");
        op.Value.ShouldBeOfType<QuorumPayload>();

        var payload = (QuorumPayload)op.Value;
        payload.ProposedValue.ShouldBe("New");
    }

    [Fact]
    public void GenerateOperation_Intent_ShouldWrapInQuorumPayload()
    {
        var doc = new CrdtDocument<ProposalDocument>(new ProposalDocument(), new CrdtMetadata());

        var op = patcher.GenerateOperation(doc, x => x.ConfigValue, new SetIntent("Proposed"));

        op.Type.ShouldBe(OperationType.Upsert);
        op.JsonPath.ShouldBe("$.configValue");
        op.Value.ShouldBeOfType<QuorumPayload>();

        var payload = (QuorumPayload)op.Value;
        payload.ProposedValue.ShouldBe("Proposed");
    }

    [Fact]
    public void ApplyOperation_WithoutQuorum_ShouldNotApply_ButTrackApproval()
    {
        var doc = new CrdtDocument<ProposalDocument>(new ProposalDocument { ConfigValue = "Current" }, new CrdtMetadata());

        var op = new CrdtOperation(
            Guid.NewGuid(),
            "ReplicaA",
            "$.configValue",
            OperationType.Upsert,
            new QuorumPayload("ProposedNew"),
            timestampProvider.Now(),
            1);

        applicator.ApplyPatch(doc, new CrdtPatch([op]));

        // Value shouldn't change because Quorum=2, and we only have 1 approval
        doc.Data.ConfigValue.ShouldBe("Current");

        // Metadata should track the approval
        doc.Metadata.States.ShouldContainKey("$.configValue@quorum");
        var approvals = doc.Metadata.States["$.configValue@quorum"].ShouldBeOfType<QuorumState>().Approvals;
        approvals.ShouldContainKey("ProposedNew");
        approvals["ProposedNew"].ShouldContain("ReplicaA");
        approvals["ProposedNew"].Count.ShouldBe(1);
    }

    [Fact]
    public void ApplyOperation_WithSameReplicaVotingTwice_ShouldNotReachQuorum()
    {
        var doc = new CrdtDocument<ProposalDocument>(new ProposalDocument { ConfigValue = "Current" }, new CrdtMetadata());

        var op1 = new CrdtOperation(
            Guid.NewGuid(),
            "ReplicaA",
            "$.configValue",
            OperationType.Upsert,
            new QuorumPayload("ProposedNew"),
            timestampProvider.Now(),
            1);

        var op2 = new CrdtOperation(
            Guid.NewGuid(),
            "ReplicaA", // Same replica voting again
            "$.configValue",
            OperationType.Upsert,
            new QuorumPayload("ProposedNew"),
            timestampProvider.Now(),
            2);

        applicator.ApplyPatch(doc, new CrdtPatch([op1, op2]));

        // Value shouldn't change, we still only have 1 distinct replica approval
        doc.Data.ConfigValue.ShouldBe("Current");
        
        var approvals = doc.Metadata.States["$.configValue@quorum"].ShouldBeOfType<QuorumState>().Approvals;
        approvals["ProposedNew"].Count.ShouldBe(1);
    }

    [Fact]
    public void ApplyOperation_WithMetQuorum_ShouldApplyAndCleanUp()
    {
        var doc = new CrdtDocument<ProposalDocument>(new ProposalDocument { ConfigValue = "Current" }, new CrdtMetadata());

        var op1 = new CrdtOperation(
            Guid.NewGuid(),
            "ReplicaA",
            "$.configValue",
            OperationType.Upsert,
            new QuorumPayload("ProposedNew"),
            timestampProvider.Now(),
            1);

        var op2 = new CrdtOperation(
            Guid.NewGuid(),
            "ReplicaB",
            "$.configValue",
            OperationType.Upsert,
            new QuorumPayload("ProposedNew"),
            timestampProvider.Now(),
            1);

        applicator.ApplyPatch(doc, new CrdtPatch([op1]));
        
        // Quorum not yet met
        doc.Data.ConfigValue.ShouldBe("Current");
        doc.Metadata.States.ShouldContainKey("$.configValue@quorum");

        applicator.ApplyPatch(doc, new CrdtPatch([op2]));

        // Quorum is 2, so it should be met now
        doc.Data.ConfigValue.ShouldBe("ProposedNew");
        
        // Trackers should be cleaned up
        doc.Metadata.States.ShouldNotContainKey("$.configValue@quorum");
    }

    [Fact]
    public void ApplyOperation_WithoutQuorumPayload_ShouldBeIgnored()
    {
        var doc = new CrdtDocument<ProposalDocument>(new ProposalDocument { ConfigValue = "Current" }, new CrdtMetadata());

        // An operation lacking the wrapper payload
        var unconstrainedOp = new CrdtOperation(
            Guid.NewGuid(),
            "ReplicaA",
            "$.configValue",
            OperationType.Upsert,
            "ForcedValue", // Unwrapped value!
            timestampProvider.Now(),
            1);

        applicator.ApplyPatch(doc, new CrdtPatch([unconstrainedOp]));

        // Should NOT apply the value bypassing the Quorum logic
        doc.Data.ConfigValue.ShouldBe("Current");
        
        // Should NOT track any approvals because the operation is invalid for this strategy
        doc.Metadata.States.ShouldNotContainKey("$.configValue@quorum");
    }

    [Fact]
    public void Compact_ShouldNotModifyDecoratorMetadata_AndDelegateToInnerStrategy()
    {
        // Arrange
        var property = ConfigValueProperty;
        var strategy = strategyProvider.GetStrategy(typeof(ProposalDocument), property);
        
        var metadata = new CrdtMetadata();
        metadata.States["$.configValue@quorum"] = new QuorumState(new Dictionary<object, ISet<string>>());
        
        var mockPolicy = new Mock<ICompactionPolicy>();
        mockPolicy.Setup(p => p.IsSafeToCompact(It.IsAny<CompactionCandidate>())).Returns(true);

        var context = new CompactionContext(metadata, mockPolicy.Object, "ConfigValue", "$.configValue", new ProposalDocument());

        // Act
        strategy.Compact(context);

        // Assert
        metadata.States.ShouldContainKey("$.configValue@quorum");
    }
}