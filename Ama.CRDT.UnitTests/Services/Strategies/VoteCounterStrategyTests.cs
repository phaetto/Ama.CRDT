namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services;
using Ama.CRDT.Services.GarbageCollection;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Strategies;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xunit;

public sealed class VoteCounterStrategyTests : IDisposable
{
    // Local test provider to guarantee strictly increasing timestamps during rapid test execution
    private sealed class TestTimestampProvider : ICrdtTimestampProvider
    {
        private long current = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        private readonly EpochTimestampProvider defaultProvider = new(new ReplicaContext { ReplicaId = "test" });
        
        public ICrdtTimestamp Now() => defaultProvider.Create(Interlocked.Increment(ref current));
        public ICrdtTimestamp Create(long value) => defaultProvider.Create(value);
    }

    internal sealed class Poll
    {
        [CrdtVoteCounterStrategy]
        public IDictionary<string, HashSet<string>> Votes { get; set; } = new Dictionary<string, HashSet<string>>();
    }

    internal sealed class PollWithList
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
            .AddCrdtAotContext<VoteCounterStrategyTestCrdtAotContext>()
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

    private static CrdtPropertyInfo GetPollVotesPropertyInfo() => new CrdtPropertyInfo(
        nameof(Poll.Votes),
        "votes",
        typeof(IDictionary<string, HashSet<string>>),
        true,
        true,
        obj => ((Poll)obj).Votes,
        (obj, val) => ((Poll)obj).Votes = (IDictionary<string, HashSet<string>>)val!,
        null,
        Array.Empty<CrdtStrategyDecoratorAttribute>()
    );

    [Fact]
    public void GeneratePatch_ShouldCreateUpsertForNewVote()
    {
        var original = new Poll();
        var modified = new Poll { Votes = { ["OptionA"] = new HashSet<string> { "Voter1" } } };
        var originalMeta = metadataManagerA.Initialize(original);

        var operations = new List<CrdtOperation>();
        var context = new GeneratePatchContext(
            operations, new List<DifferentiateObjectContext>(), "$.votes", GetPollVotesPropertyInfo(), original.Votes, modified.Votes, original, modified, originalMeta, timestampProvider.Now(), 0);
        
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
            operations, new List<DifferentiateObjectContext>(), "$.votes", GetPollVotesPropertyInfo(), original.Votes, modified.Votes, original, modified, originalMeta, timestampProvider.Now(), 0);
        
        strategyA.GeneratePatch(context);

        operations.Count.ShouldBe(1);
        var op = operations[0];
        op.Type.ShouldBe(OperationType.Upsert);
        var payload = op.Value.ShouldBeOfType<VotePayload>();
        payload.Voter.ShouldBe("Voter1");
        payload.Option.ShouldBe("OptionB");
    }
    
    [Fact]
    public void GenerateOperation_ShouldCreateUpsertForVoteIntent()
    {
        var original = new Poll();
        var originalMeta = metadataManagerA.Initialize(original);
        var intent = new VoteIntent("Voter1", "OptionA");
        
        var context = new GenerateOperationContext(
            original, 
            originalMeta, 
            "$.votes", 
            GetPollVotesPropertyInfo(), 
            intent, 
            timestampProvider.Now(), 
            0);
            
        var operation = strategyA.GenerateOperation(context);

        operation.Type.ShouldBe(OperationType.Upsert);
        operation.JsonPath.ShouldBe("$.votes");
        operation.ReplicaId.ShouldBe("A");
        var payload = operation.Value.ShouldBeOfType<VotePayload>();
        payload.Voter.ShouldBe("Voter1");
        payload.Option.ShouldBe("OptionA");
    }

    [Fact]
    public void GenerateOperation_WithUnsupportedIntent_ShouldThrowNotSupportedException()
    {
        var original = new Poll();
        var originalMeta = metadataManagerA.Initialize(original);
        var intent = new AddIntent("Voter1");
        
        var context = new GenerateOperationContext(
            original, 
            originalMeta, 
            "$.votes", 
            GetPollVotesPropertyInfo(), 
            intent, 
            timestampProvider.Now(), 
            0);
            
        Should.Throw<NotSupportedException>(() => strategyA.GenerateOperation(context));
    }

    [Fact]
    public void ApplyOperation_ShouldApplyNewVote()
    {
        var model = new Poll();
        var metadata = new CrdtMetadata();
        var payload = new VotePayload("Voter1", "OptionA");
        var operation = new CrdtOperation(Guid.NewGuid(), "r1", "$.votes", OperationType.Upsert, payload, timestampProvider.Create(200L), 0);
        var context = new ApplyOperationContext(model, metadata, operation);

        strategyA.ApplyOperation(context);

        model.Votes["OptionA"].ShouldContain("Voter1");
        metadata.Lww["$.votes.['Voter1']"].Timestamp.ShouldBe(timestampProvider.Create(200L));
    }

    [Fact]
    public void ApplyOperation_ShouldApplyNewVote_WithList()
    {
        var model = new PollWithList();
        var metadata = new CrdtMetadata();
        var payload = new VotePayload("Voter1", "OptionA");
        var operation = new CrdtOperation(Guid.NewGuid(), "r1", "$.votes", OperationType.Upsert, payload, timestampProvider.Create(200L), 0);
        var context = new ApplyOperationContext(model, metadata, operation);

        strategyA.ApplyOperation(context);

        model.Votes["OptionA"].ShouldContain("Voter1");
        metadata.Lww["$.votes.['Voter1']"].Timestamp.ShouldBe(timestampProvider.Create(200L));
    }

    [Fact]
    public void ApplyOperation_LwwShouldResolveVoteChange()
    {
        var model = new Poll { Votes = { ["OptionA"] = new HashSet<string> { "Voter1" } } };
        var metadata = metadataManagerA.Initialize(model);
        metadata.Lww["$.votes.['Voter1']"] = new CausalTimestamp(timestampProvider.Create(100L), "r0", 1);

        var oldVoteOp = new CrdtOperation(Guid.NewGuid(), "r1", "$.votes", OperationType.Upsert, new VotePayload("Voter1", "OptionC"), timestampProvider.Create(90L), 0);
        var newVoteOp = new CrdtOperation(Guid.NewGuid(), "r2", "$.votes", OperationType.Upsert, new VotePayload("Voter1", "OptionB"), timestampProvider.Create(110L), 0);

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
        var operation = new CrdtOperation(Guid.NewGuid(), "r1", "$.votes", OperationType.Upsert, new VotePayload("Voter1", "OptionA"), timestampProvider.Create(200L), 0);
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
        var op1 = new CrdtOperation(Guid.NewGuid(), "r1", "$.votes", OperationType.Upsert, new VotePayload("Voter1", "OptionA"), timestampProvider.Create(200L), 0);
        var op2 = new CrdtOperation(Guid.NewGuid(), "r2", "$.votes", OperationType.Upsert, new VotePayload("Voter2", "OptionB"), timestampProvider.Create(201L), 0);

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
        var op1 = new CrdtOperation(Guid.NewGuid(), "r1", "$.votes", OperationType.Upsert, new VotePayload("Voter1", "OptionB"), timestampProvider.Create(300L), 0);
        // Voter1 changes from A -> C (older)
        var op2 = new CrdtOperation(Guid.NewGuid(), "r2", "$.votes", OperationType.Upsert, new VotePayload("Voter1", "OptionC"), timestampProvider.Create(250L), 0);
        
        var initialModel = new Poll { Votes = { ["OptionA"] = new HashSet<string> { "Voter1" } } };
        var initialMeta = metadataManagerA.Initialize(initialModel);
        initialMeta.Lww["$.votes.['Voter1']"] = new CausalTimestamp(timestampProvider.Create(200L), "r0", 1);

        // Scenario 1: op1 then op2
        var model1 = new Poll { Votes = { ["OptionA"] = new HashSet<string> { "Voter1" } } };
        var meta1 = metadataManagerA.Initialize(new Poll { Votes = { ["OptionA"] = new HashSet<string> { "Voter1" } } });
        meta1.Lww["$.votes.['Voter1']"] = new CausalTimestamp(timestampProvider.Create(200L), "r0", 1);

        strategyA.ApplyOperation(new ApplyOperationContext(model1, meta1, op1));
        strategyA.ApplyOperation(new ApplyOperationContext(model1, meta1, op2)); // op2 is older, should be ignored

        // Scenario 2: op2 then op1
        var model2 = new Poll { Votes = { ["OptionA"] = new HashSet<string> { "Voter1" } } };
        var meta2 = metadataManagerA.Initialize(new Poll { Votes = { ["OptionA"] = new HashSet<string> { "Voter1" } } });
        meta2.Lww["$.votes.['Voter1']"] = new CausalTimestamp(timestampProvider.Create(200L), "r0", 1);

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

    [Fact]
    public void GetStartKey_ShouldReturnSmallestVoterOrNull()
    {
        var propInfo = GetPollVotesPropertyInfo();
        
        strategyA.GetStartKey(new Poll(), propInfo).ShouldBeNull();
        
        var doc = new Poll { Votes = { ["Opt1"] = new HashSet<string> { "c", "a", "b" } } };
        strategyA.GetStartKey(doc, propInfo).ShouldBe("a");
    }

    [Fact]
    public void GetKeyFromOperation_ShouldExtractVoterProperly()
    {
        var op = new CrdtOperation(Guid.NewGuid(), "r1", "$.votes", OperationType.Upsert, new VotePayload("myVoter", "Opt"), timestampProvider.Now(), 0);
        
        strategyA.GetKeyFromOperation(op, "$.votes").ShouldBe("myVoter");
        strategyA.GetKeyFromOperation(op, "$.otherPath").ShouldBeNull();
    }

    [Fact]
    public void GetMinimumKey_ShouldReturnCorrectMinValueForVoterType()
    {
        var propInfo = GetPollVotesPropertyInfo();
        strategyA.GetMinimumKey(propInfo).ShouldBe(string.Empty);
    }

    [Fact]
    public void Split_ShouldDivideDataAndMetadataEqually()
    {
        var doc = new Poll();
        var meta = metadataManagerA.Initialize(doc);
        var propInfo = GetPollVotesPropertyInfo();

        strategyA.ApplyOperation(new ApplyOperationContext(doc, meta, new CrdtOperation(Guid.NewGuid(), "r1", "$.votes", OperationType.Upsert, new VotePayload("a", "O1"), timestampProvider.Now(), 0)));
        strategyA.ApplyOperation(new ApplyOperationContext(doc, meta, new CrdtOperation(Guid.NewGuid(), "r1", "$.votes", OperationType.Upsert, new VotePayload("b", "O1"), timestampProvider.Now(), 0)));
        strategyA.ApplyOperation(new ApplyOperationContext(doc, meta, new CrdtOperation(Guid.NewGuid(), "r1", "$.votes", OperationType.Upsert, new VotePayload("c", "O2"), timestampProvider.Now(), 0)));
        strategyA.ApplyOperation(new ApplyOperationContext(doc, meta, new CrdtOperation(Guid.NewGuid(), "r1", "$.votes", OperationType.Upsert, new VotePayload("d", "O2"), timestampProvider.Now(), 0)));

        var result = strategyA.Split(doc, meta, propInfo);

        result.SplitKey.ShouldBe("c");

        var doc1 = (Poll)result.Partition1.Data;
        var doc2 = (Poll)result.Partition2.Data;

        doc1.Votes["O1"].ShouldBe(["a", "b"], ignoreOrder: true);
        doc2.Votes["O2"].ShouldBe(["c", "d"], ignoreOrder: true);

        result.Partition1.Metadata.Lww.Keys.ShouldContain("$.votes.['a']");
        result.Partition2.Metadata.Lww.Keys.ShouldContain("$.votes.['c']");
    }

    [Fact]
    public void Merge_ShouldCombineDataAndMetadata()
    {
        var doc1 = new Poll();
        var meta1 = metadataManagerA.Initialize(doc1);
        var doc2 = new Poll();
        var meta2 = metadataManagerA.Initialize(doc2);
        var propInfo = GetPollVotesPropertyInfo();

        strategyA.ApplyOperation(new ApplyOperationContext(doc1, meta1, new CrdtOperation(Guid.NewGuid(), "r1", "$.votes", OperationType.Upsert, new VotePayload("a", "O1"), timestampProvider.Now(), 0)));
        strategyA.ApplyOperation(new ApplyOperationContext(doc1, meta1, new CrdtOperation(Guid.NewGuid(), "r1", "$.votes", OperationType.Upsert, new VotePayload("b", "O1"), timestampProvider.Now(), 0)));
        
        strategyA.ApplyOperation(new ApplyOperationContext(doc2, meta2, new CrdtOperation(Guid.NewGuid(), "r1", "$.votes", OperationType.Upsert, new VotePayload("c", "O2"), timestampProvider.Now(), 0)));
        strategyA.ApplyOperation(new ApplyOperationContext(doc2, meta2, new CrdtOperation(Guid.NewGuid(), "r1", "$.votes", OperationType.Upsert, new VotePayload("d", "O2"), timestampProvider.Now(), 0)));

        var result = strategyA.Merge(doc1, meta1, doc2, meta2, propInfo);

        var mergedDoc = (Poll)result.Data;
        mergedDoc.Votes["O1"].ShouldBe(["a", "b"], ignoreOrder: true);
        mergedDoc.Votes["O2"].ShouldBe(["c", "d"], ignoreOrder: true);
        result.Metadata.Lww.Keys.Count.ShouldBeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public void Compact_ShouldRemoveLwwTombstones_WhenPolicyAllows()
    {
        // Arrange
        var doc = new Poll { Votes = { ["OptionA"] = new HashSet<string> { "ActiveVoter" } } };
        var meta = new CrdtMetadata();

        var tsActive = timestampProvider.Create(1);
        var tsDeadSafe = timestampProvider.Create(2);
        var tsDeadUnsafe = timestampProvider.Create(3);

        meta.Lww["$.votes.['ActiveVoter']"] = new CausalTimestamp(tsActive, "replica-1", 1);
        meta.Lww["$.votes.['DeadSafeVoter']"] = new CausalTimestamp(tsDeadSafe, "replica-1", 2);
        meta.Lww["$.votes.['DeadUnsafeVoter']"] = new CausalTimestamp(tsDeadUnsafe, "replica-2", 3);

        var mockPolicy = new Mock<ICompactionPolicy>();
        mockPolicy.Setup(p => p.IsSafeToCompact(It.IsAny<CompactionCandidate>()))
            .Returns((CompactionCandidate c) => c.ReplicaId == "replica-1" && c.Version <= 5);

        var context = new CompactionContext(meta, mockPolicy.Object, "Votes", "$.votes", doc);

        // Act
        strategyA.Compact(context);

        // Assert
        meta.Lww.ShouldContainKey("$.votes.['ActiveVoter']");
        meta.Lww.ShouldContainKey("$.votes.['DeadUnsafeVoter']");
        meta.Lww.ShouldNotContainKey("$.votes.['DeadSafeVoter']");

        mockPolicy.Verify(p => p.IsSafeToCompact(It.Is<CompactionCandidate>(c => c.Timestamp == tsActive)), Times.Never);
        mockPolicy.Verify(p => p.IsSafeToCompact(It.Is<CompactionCandidate>(c => c.Timestamp == tsDeadSafe)), Times.Once);
        mockPolicy.Verify(p => p.IsSafeToCompact(It.Is<CompactionCandidate>(c => c.Timestamp == tsDeadUnsafe)), Times.Once);
    }
}