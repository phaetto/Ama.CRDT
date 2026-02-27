namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Strategies;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

public sealed class VoteCounterStrategyTests : IDisposable
{
    // Local test provider to guarantee strictly increasing timestamps during rapid test execution
    private sealed class TestTimestampProvider : ICrdtTimestampProvider
    {
        private long current = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        private readonly EpochTimestampProvider defaultProvider = new();
        
        public ICrdtTimestamp Now() => defaultProvider.Create(System.Threading.Interlocked.Increment(ref current));
        public ICrdtTimestamp Create(long value) => defaultProvider.Create(value);
    }

    private sealed class Poll
    {
        [CrdtVoteCounterStrategy]
        public IDictionary<string, HashSet<string>> Votes { get; set; } = new Dictionary<string, HashSet<string>>();
    }

    private sealed class PollWithList
    {
        [CrdtVoteCounterStrategy]
        public IDictionary<string, List<string>> Votes { get; set; } = new Dictionary<string, List<string>>();
    }

    private readonly IServiceScope scopeA;
    private readonly IServiceScope scopeB;
    private readonly VoteCounterStrategy strategyA;
    private readonly ICrdtTimestampProvider timestampProvider;
    private readonly ICrdtMetadataManager metadataManagerA;
    private readonly Mock<ICrdtPatcher> mockPatcher = new();

    public VoteCounterStrategyTests()
    {
        var serviceProvider = new ServiceCollection()
            .AddCrdt()
            .AddSingleton<ICrdtTimestampProvider, TestTimestampProvider>()
            .BuildServiceProvider();

        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();

        scopeA = scopeFactory.CreateScope("A");
        scopeB = scopeFactory.CreateScope("B");

        strategyA = scopeA.ServiceProvider.GetRequiredService<VoteCounterStrategy>();
        timestampProvider = scopeA.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();
        metadataManagerA = scopeA.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();
    }

    public void Dispose()
    {
        scopeA.Dispose();
        scopeB.Dispose();
    }

    [Fact]
    public void GeneratePatch_ShouldCreateUpsertForNewVote()
    {
        var original = new Poll();
        var modified = new Poll { Votes = { ["OptionA"] = new HashSet<string> { "Voter1" } } };
        var originalMeta = metadataManagerA.Initialize(original);

        var operations = new List<CrdtOperation>();
        var context = new GeneratePatchContext(
            mockPatcher.Object, operations, "$.votes", typeof(Poll).GetProperty(nameof(Poll.Votes))!, original.Votes, modified.Votes, original, modified, originalMeta, timestampProvider.Now());
        
        strategyA.GeneratePatch(context);

        operations.Count.ShouldBe(1);
        var op = operations[0];
        op.Type.ShouldBe(OperationType.Upsert);
        var payload = op.Value.ShouldBeOfType<VotePayload>();
        payload.Voter.ShouldBe("Voter1");
        payload.Option.ShouldBe("OptionA");
    }

    [Fact]
    public void GeneratePatch_ShouldCreateUpsertForChangedVote()
    {
        var original = new Poll { Votes = { ["OptionA"] = new HashSet<string> { "Voter1" } } };
        var modified = new Poll { Votes = { ["OptionB"] = new HashSet<string> { "Voter1" } } };
        var originalMeta = metadataManagerA.Initialize(original);

        var operations = new List<CrdtOperation>();
        var context = new GeneratePatchContext(
            mockPatcher.Object, operations, "$.votes", typeof(Poll).GetProperty(nameof(Poll.Votes))!, original.Votes, modified.Votes, original, modified, originalMeta, timestampProvider.Now());
        
        strategyA.GeneratePatch(context);

        operations.Count.ShouldBe(1);
        var op = operations[0];
        op.Type.ShouldBe(OperationType.Upsert);
        var payload = op.Value.ShouldBeOfType<VotePayload>();
        payload.Voter.ShouldBe("Voter1");
        payload.Option.ShouldBe("OptionB");
    }

    [Fact]
    public void ApplyOperation_ShouldApplyNewVote()
    {
        var model = new Poll();
        var metadata = new CrdtMetadata();
        var payload = new VotePayload("Voter1", "OptionA");
        var operation = new CrdtOperation(Guid.NewGuid(), "r1", "$.votes", OperationType.Upsert, payload, timestampProvider.Create(200L));
        var context = new ApplyOperationContext(model, metadata, operation);

        strategyA.ApplyOperation(context);

        model.Votes["OptionA"].ShouldContain("Voter1");
        metadata.Lww["$.votes.['Voter1']"].ShouldBe(timestampProvider.Create(200L));
    }

    [Fact]
    public void ApplyOperation_ShouldApplyNewVote_WithList()
    {
        var model = new PollWithList();
        var metadata = new CrdtMetadata();
        var payload = new VotePayload("Voter1", "OptionA");
        var operation = new CrdtOperation(Guid.NewGuid(), "r1", "$.votes", OperationType.Upsert, payload, timestampProvider.Create(200L));
        var context = new ApplyOperationContext(model, metadata, operation);

        strategyA.ApplyOperation(context);

        model.Votes["OptionA"].ShouldContain("Voter1");
        metadata.Lww["$.votes.['Voter1']"].ShouldBe(timestampProvider.Create(200L));
    }

    [Fact]
    public void ApplyOperation_LwwShouldResolveVoteChange()
    {
        var model = new Poll { Votes = { ["OptionA"] = new HashSet<string> { "Voter1" } } };
        var metadata = metadataManagerA.Initialize(model);
        metadata.Lww["$.votes.['Voter1']"] = timestampProvider.Create(100L);

        var oldVoteOp = new CrdtOperation(Guid.NewGuid(), "r1", "$.votes", OperationType.Upsert, new VotePayload("Voter1", "OptionC"), timestampProvider.Create(90L));
        var newVoteOp = new CrdtOperation(Guid.NewGuid(), "r2", "$.votes", OperationType.Upsert, new VotePayload("Voter1", "OptionB"), timestampProvider.Create(110L));

        strategyA.ApplyOperation(new ApplyOperationContext(model, metadata, oldVoteOp)); // Should be ignored
        model.Votes.ContainsKey("OptionC").ShouldBeFalse();
        model.Votes["OptionA"].ShouldContain("Voter1");

        strategyA.ApplyOperation(new ApplyOperationContext(model, metadata, newVoteOp)); // Should be applied
        model.Votes.ContainsKey("OptionA").ShouldBeFalse();
        model.Votes["OptionB"].ShouldContain("Voter1");
    }
    
    [Fact]
    public void ApplyOperation_IsIdempotent()
    {
        var model = new Poll();
        var metadata = new CrdtMetadata();
        var operation = new CrdtOperation(Guid.NewGuid(), "r1", "$.votes", OperationType.Upsert, new VotePayload("Voter1", "OptionA"), timestampProvider.Create(200L));
        var context = new ApplyOperationContext(model, metadata, operation);
        
        strategyA.ApplyOperation(context);
        var votesAfterFirstApply = new Dictionary<string, HashSet<string>>(model.Votes.ToDictionary(kvp => kvp.Key, kvp => new HashSet<string>(kvp.Value)));
        
        strategyA.ApplyOperation(context);
        
        model.Votes.Count.ShouldBe(1);
        model.Votes["OptionA"].Count.ShouldBe(1);
        model.Votes["OptionA"].ShouldContain("Voter1");
        model.Votes["OptionA"].ShouldBe(votesAfterFirstApply["OptionA"], ignoreOrder: true);
    }
    
    [Fact]
    public void ApplyOperation_IsCommutative()
    {
        var op1 = new CrdtOperation(Guid.NewGuid(), "r1", "$.votes", OperationType.Upsert, new VotePayload("Voter1", "OptionA"), timestampProvider.Create(200L));
        var op2 = new CrdtOperation(Guid.NewGuid(), "r2", "$.votes", OperationType.Upsert, new VotePayload("Voter2", "OptionB"), timestampProvider.Create(201L));

        // Scenario 1: op1 then op2
        var model1 = new Poll();
        var meta1 = new CrdtMetadata();
        strategyA.ApplyOperation(new ApplyOperationContext(model1, meta1, op1));
        strategyA.ApplyOperation(new ApplyOperationContext(model1, meta1, op2));

        // Scenario 2: op2 then op1
        var model2 = new Poll();
        var meta2 = new CrdtMetadata();
        strategyA.ApplyOperation(new ApplyOperationContext(model2, meta2, op2));
        strategyA.ApplyOperation(new ApplyOperationContext(model2, meta2, op1));
        
        model1.Votes["OptionA"].ShouldContain("Voter1");
        model1.Votes["OptionB"].ShouldContain("Voter2");
        
        model2.Votes["OptionA"].ShouldContain("Voter1");
        model2.Votes["OptionB"].ShouldContain("Voter2");
        
        model1.Votes["OptionA"].ShouldBe(model2.Votes["OptionA"], ignoreOrder: true);
        model1.Votes["OptionB"].ShouldBe(model2.Votes["OptionB"], ignoreOrder: true);
    }

    [Fact]
    public void ApplyOperation_ConvergesWithConcurrentVoteChanges()
    {
        // Voter1 changes from A -> B (newer)
        var op1 = new CrdtOperation(Guid.NewGuid(), "r1", "$.votes", OperationType.Upsert, new VotePayload("Voter1", "OptionB"), timestampProvider.Create(300L));
        // Voter1 changes from A -> C (older)
        var op2 = new CrdtOperation(Guid.NewGuid(), "r2", "$.votes", OperationType.Upsert, new VotePayload("Voter1", "OptionC"), timestampProvider.Create(250L));
        
        var initialModel = new Poll { Votes = { ["OptionA"] = new HashSet<string> { "Voter1" } } };
        var initialMeta = metadataManagerA.Initialize(initialModel);
        initialMeta.Lww["$.votes.['Voter1']"] = timestampProvider.Create(200L);

        // Scenario 1: op1 then op2
        var model1 = new Poll { Votes = { ["OptionA"] = new HashSet<string> { "Voter1" } } };
        var meta1 = metadataManagerA.Initialize(new Poll { Votes = { ["OptionA"] = new HashSet<string> { "Voter1" } } });
        meta1.Lww["$.votes.['Voter1']"] = timestampProvider.Create(200L);

        strategyA.ApplyOperation(new ApplyOperationContext(model1, meta1, op1));
        strategyA.ApplyOperation(new ApplyOperationContext(model1, meta1, op2)); // op2 is older, should be ignored

        // Scenario 2: op2 then op1
        var model2 = new Poll { Votes = { ["OptionA"] = new HashSet<string> { "Voter1" } } };
        var meta2 = metadataManagerA.Initialize(new Poll { Votes = { ["OptionA"] = new HashSet<string> { "Voter1" } } });
        meta2.Lww["$.votes.['Voter1']"] = timestampProvider.Create(200L);

        strategyA.ApplyOperation(new ApplyOperationContext(model2, meta2, op2)); // This is applied because timestamp 250 > 200
        strategyA.ApplyOperation(new ApplyOperationContext(model2, meta2, op1)); // op1 is newer (300 > 250), should win
        
        // Assert: Both converge to OptionB, the one with the highest timestamp
        model1.Votes.ContainsKey("OptionA").ShouldBeFalse();
        model1.Votes.ContainsKey("OptionC").ShouldBeFalse();
        model1.Votes["OptionB"].ShouldContain("Voter1");
        
        model2.Votes.ContainsKey("OptionA").ShouldBeFalse();
        model2.Votes.ContainsKey("OptionC").ShouldBeFalse();
        model2.Votes["OptionB"].ShouldContain("Voter1");
    }
}