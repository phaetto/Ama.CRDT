namespace Ama.CRDT.UnitTests.Services.Strategies.Decorators;

using Ama.CRDT.Attributes.Decorators;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Decorators;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Strategies;
using Ama.CRDT.Services.Strategies.Decorators;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System;
using Xunit;

public sealed class ChainedDecoratorsTests : IDisposable
{
    private readonly IServiceScope scope;
    private readonly ICrdtPatcher patcher;
    private readonly ICrdtApplicator applicator;
    private readonly ICrdtTimestampProvider timestampProvider;

    public ChainedDecoratorsTests()
    {
        var serviceProvider = new ServiceCollection()
            .AddCrdt()
            .AddScoped<ICrdtStrategy, ApprovalQuorumStrategy>()
            .BuildServiceProvider();

        scope = serviceProvider.GetRequiredService<ICrdtScopeFactory>().CreateScope("TestReplica");

        patcher = scope.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        applicator = scope.ServiceProvider.GetRequiredService<ICrdtApplicator>();
        timestampProvider = scope.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();
    }

    public void Dispose()
    {
        scope.Dispose();
    }

    public sealed class ChainedDocument
    {
        [CrdtEpochBound]
        [CrdtApprovalQuorum(2)]
        [CrdtLwwStrategy]
        public string Value { get; set; } = string.Empty;
    }

    [Fact]
    public void GeneratePatch_ShouldNestPayloadsFromBothDecorators()
    {
        var doc = new CrdtDocument<ChainedDocument>(new ChainedDocument { Value = "Old" }, new CrdtMetadata());
        var changed = new ChainedDocument { Value = "New" };

        var patch = patcher.GeneratePatch(doc, changed);
        
        patch.Operations.Count.ShouldBe(1);
        var op = patch.Operations[0];
        
        // Ensure both decorators correctly chained their payload wrapping.
        // The nesting order depends on the runtime reflection order of the attributes, so we check both paths.
        if (op.Value is EpochPayload epochPayload)
        {
            epochPayload.Value.ShouldBeOfType<QuorumPayload>();
            ((QuorumPayload)epochPayload.Value).ProposedValue.ShouldBe("New");
        }
        else if (op.Value is QuorumPayload quorumPayload)
        {
            quorumPayload.ProposedValue.ShouldBeOfType<EpochPayload>();
            ((EpochPayload)quorumPayload.ProposedValue).Value.ShouldBe("New");
        }
        else
        {
            Assert.Fail($"Expected nested decorator payloads, but got {op.Value?.GetType().Name}");
        }
    }

    [Fact]
    public void ApplyOperation_ShouldRequireQuorum_And_RespectEpoch()
    {
        var doc = new CrdtDocument<ChainedDocument>(new ChainedDocument { Value = "Initial" }, new CrdtMetadata());
        
        // Generate a properly wrapped intent payload using the patcher chain
        var op = patcher.BuildOperation(doc, x => x.Value).Set("Proposed");

        // Simulate operations coming from two distinct replicas
        var opReplica1 = op with { ReplicaId = "Replica1" };
        var opReplica2 = op with { ReplicaId = "Replica2" };

        // 1. Apply first vote. Quorum not met, value should remain the same.
        applicator.ApplyPatch(doc, new CrdtPatch([opReplica1]));
        doc.Data.Value.ShouldBe("Initial");

        // 2. Apply second vote. Quorum met. Value should update, respecting Epoch payload unwrapping.
        applicator.ApplyPatch(doc, new CrdtPatch([opReplica2]));
        doc.Data.Value.ShouldBe("Proposed");
    }
}