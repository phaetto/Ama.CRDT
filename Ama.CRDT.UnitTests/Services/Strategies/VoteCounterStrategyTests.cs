namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Strategies;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xunit;

public sealed class VoteCounterStrategyTests
{
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

    private readonly VoteCounterStrategy strategy;
    private readonly Mock<ICrdtTimestampProvider> mockTimestampProvider = new();
    private readonly Mock<ICrdtPatcher> mockPatcher = new();
    private readonly CrdtMetadataManager metadataManager;
    private long timestampCounter = 100L;
    
    public VoteCounterStrategyTests()
    {
        mockTimestampProvider.Setup(p => p.Now()).Returns(() => new EpochTimestamp(Interlocked.Increment(ref timestampCounter)));
        strategy = new VoteCounterStrategy(new ReplicaContext { ReplicaId = "replica-A" }, mockTimestampProvider.Object);
        
        var mockStrategyManager = new Mock<ICrdtStrategyProvider>();
        mockStrategyManager.Setup(m => m.GetStrategy(It.IsAny<System.Reflection.PropertyInfo>())).Returns(strategy);
        
        var mockElementComparerProvider = new Mock<IElementComparerProvider>();

        metadataManager = new CrdtMetadataManager(mockStrategyManager.Object, mockTimestampProvider.Object, mockElementComparerProvider.Object);
    }
    
    [Fact]
    public void GeneratePatch_ShouldCreateUpsertForNewVote()
    {
        var original = new Poll();
        var modified = new Poll { Votes = { ["OptionA"] = new HashSet<string> { "Voter1" } } };
        var originalMeta = metadataManager.Initialize(original);
        var modifiedMeta = metadataManager.Initialize(modified);

        var operations = new List<CrdtOperation>();
        strategy.GeneratePatch(mockPatcher.Object, operations, "$.votes", typeof(Poll).GetProperty(nameof(Poll.Votes))!, original.Votes, modified.Votes, original, modified, originalMeta, modifiedMeta);

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
        var originalMeta = metadataManager.Initialize(original);
        var modifiedMeta = metadataManager.Initialize(modified);

        var operations = new List<CrdtOperation>();
        strategy.GeneratePatch(mockPatcher.Object, operations, "$.votes", typeof(Poll).GetProperty(nameof(Poll.Votes))!, original.Votes, modified.Votes, original, modified, originalMeta, modifiedMeta);

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
        var operation = new CrdtOperation(Guid.NewGuid(), "r1", "$.votes", OperationType.Upsert, payload, new EpochTimestamp(200L));

        strategy.ApplyOperation(model, metadata, operation);

        model.Votes["OptionA"].ShouldContain("Voter1");
        metadata.Lww["$.votes.['Voter1']"].ShouldBe(new EpochTimestamp(200L));
    }

    [Fact]
    public void ApplyOperation_ShouldApplyNewVote_WithList()
    {
        var model = new PollWithList();
        var metadata = new CrdtMetadata();
        var payload = new VotePayload("Voter1", "OptionA");
        var operation = new CrdtOperation(Guid.NewGuid(), "r1", "$.votes", OperationType.Upsert, payload, new EpochTimestamp(200L));

        strategy.ApplyOperation(model, metadata, operation);

        model.Votes["OptionA"].ShouldContain("Voter1");
        metadata.Lww["$.votes.['Voter1']"].ShouldBe(new EpochTimestamp(200L));
    }

    [Fact]
    public void ApplyOperation_LwwShouldResolveVoteChange()
    {
        var model = new Poll { Votes = { ["OptionA"] = new HashSet<string> { "Voter1" } } };
        var metadata = metadataManager.Initialize(model, new EpochTimestamp(100L));

        var oldVoteOp = new CrdtOperation(Guid.NewGuid(), "r1", "$.votes", OperationType.Upsert, new VotePayload("Voter1", "OptionC"), new EpochTimestamp(90L));
        var newVoteOp = new CrdtOperation(Guid.NewGuid(), "r2", "$.votes", OperationType.Upsert, new VotePayload("Voter1", "OptionB"), new EpochTimestamp(110L));

        strategy.ApplyOperation(model, metadata, oldVoteOp); // Should be ignored
        model.Votes.ContainsKey("OptionC").ShouldBeFalse();
        model.Votes["OptionA"].ShouldContain("Voter1");

        strategy.ApplyOperation(model, metadata, newVoteOp); // Should be applied
        model.Votes.ContainsKey("OptionA").ShouldBeFalse();
        model.Votes["OptionB"].ShouldContain("Voter1");
    }
    
    [Fact]
    public void ApplyOperation_IsIdempotent()
    {
        var model = new Poll();
        var metadata = new CrdtMetadata();
        var operation = new CrdtOperation(Guid.NewGuid(), "r1", "$.votes", OperationType.Upsert, new VotePayload("Voter1", "OptionA"), new EpochTimestamp(200L));
        
        strategy.ApplyOperation(model, metadata, operation);
        var votesAfterFirstApply = new Dictionary<string, HashSet<string>>(model.Votes.ToDictionary(kvp => kvp.Key, kvp => new HashSet<string>(kvp.Value)));
        
        strategy.ApplyOperation(model, metadata, operation);
        
        model.Votes.Count.ShouldBe(1);
        model.Votes["OptionA"].Count.ShouldBe(1);
        model.Votes["OptionA"].ShouldContain("Voter1");
        model.Votes["OptionA"].ShouldBe(votesAfterFirstApply["OptionA"], ignoreOrder: true);
    }
    
    [Fact]
    public void ApplyOperation_IsCommutative()
    {
        var op1 = new CrdtOperation(Guid.NewGuid(), "r1", "$.votes", OperationType.Upsert, new VotePayload("Voter1", "OptionA"), new EpochTimestamp(200L));
        var op2 = new CrdtOperation(Guid.NewGuid(), "r2", "$.votes", OperationType.Upsert, new VotePayload("Voter2", "OptionB"), new EpochTimestamp(201L));

        // Scenario 1: op1 then op2
        var model1 = new Poll();
        var meta1 = new CrdtMetadata();
        strategy.ApplyOperation(model1, meta1, op1);
        strategy.ApplyOperation(model1, meta1, op2);

        // Scenario 2: op2 then op1
        var model2 = new Poll();
        var meta2 = new CrdtMetadata();
        strategy.ApplyOperation(model2, meta2, op2);
        strategy.ApplyOperation(model2, meta2, op1);
        
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
        var op1 = new CrdtOperation(Guid.NewGuid(), "r1", "$.votes", OperationType.Upsert, new VotePayload("Voter1", "OptionB"), new EpochTimestamp(300L));
        // Voter1 changes from A -> C (older)
        var op2 = new CrdtOperation(Guid.NewGuid(), "r2", "$.votes", OperationType.Upsert, new VotePayload("Voter1", "OptionC"), new EpochTimestamp(250L));
        
        var initialMeta = new CrdtMetadata();
        metadataManager.Initialize(initialMeta, new EpochTimestamp(200L));

        // Scenario 1: op1 then op2
        var model1 = new Poll { Votes = { ["OptionA"] = new HashSet<string> { "Voter1" } } };
        var meta1 = metadataManager.Initialize(new Poll { Votes = { ["OptionA"] = new HashSet<string> { "Voter1" } } }, new EpochTimestamp(200L));
        strategy.ApplyOperation(model1, meta1, op1);
        strategy.ApplyOperation(model1, meta1, op2); // op2 is older, should be ignored

        // Scenario 2: op2 then op1
        var model2 = new Poll { Votes = { ["OptionA"] = new HashSet<string> { "Voter1" } } };
        var meta2 = metadataManager.Initialize(new Poll { Votes = { ["OptionA"] = new HashSet<string> { "Voter1" } } }, new EpochTimestamp(200L));
        strategy.ApplyOperation(model2, meta2, op2); // This is applied because timestamp 250 > 200
        strategy.ApplyOperation(model2, meta2, op1); // op1 is newer (300 > 250), should win
        
        // Assert: Both converge to OptionB, the one with the highest timestamp
        model1.Votes.ContainsKey("OptionA").ShouldBeFalse();
        model1.Votes.ContainsKey("OptionC").ShouldBeFalse();
        model1.Votes["OptionB"].ShouldContain("Voter1");
        
        model2.Votes.ContainsKey("OptionA").ShouldBeFalse();
        model2.Votes.ContainsKey("OptionC").ShouldBeFalse();
        model2.Votes["OptionB"].ShouldContain("Voter1");
    }
}