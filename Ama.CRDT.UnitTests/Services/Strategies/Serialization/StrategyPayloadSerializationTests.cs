namespace Ama.CRDT.UnitTests.Services.Strategies.Serialization;

using Ama.CRDT.Attributes;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Serialization;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Providers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Xunit;

public sealed class StrategyPayloadSerializationTests : IDisposable
{
    #region Models and Helpers

    // LwwStrategy
    private sealed class LwwModel { [CrdtLwwStrategy] public int Value { get; set; } }

    // CounterStrategy
    private sealed class CounterModel { [CrdtCounterStrategy] public int Value { get; set; } }

    // GCounterStrategy
    private sealed class GCounterModel { [CrdtGCounterStrategy] public int Value { get; set; } }

    // BoundedCounterStrategy
    private sealed class BoundedCounterModel { [CrdtBoundedCounterStrategy(0, 100)] public int Value { get; set; } }

    // MaxWinsStrategy
    private sealed class MaxWinsModel { [CrdtMaxWinsStrategy] public int Value { get; set; } }

    // MinWinsStrategy
    private sealed class MinWinsModel { [CrdtMinWinsStrategy] public int Value { get; set; } }

    // AverageRegisterStrategy
    private sealed class AverageRegisterModel { [CrdtAverageRegisterStrategy] public decimal Value { get; set; } }

    // ArrayLcsStrategy
    private sealed class ArrayLcsModel { [CrdtArrayLcsStrategy] public List<string> Items { get; set; } = new(); }

    // FixedSizeArrayStrategy
    private sealed class FixedSizeArrayModel { [CrdtFixedSizeArrayStrategy(3)] public List<int> Items { get; set; } = new(); }

    // GSetStrategy
    private sealed class GSetModel { [CrdtGSetStrategy] public List<string> Items { get; set; } = new(); }

    // TwoPhaseSetStrategy
    private sealed class TwoPhaseSetModel { [CrdtTwoPhaseSetStrategy] public List<string> Items { get; set; } = new(); }

    // LwwSetStrategy
    private sealed class LwwSetModel { [CrdtLwwSetStrategy] public List<string> Items { get; set; } = new(); }

    // OrSetStrategy
    private sealed class OrSetModel { [CrdtOrSetStrategy] public List<string> Items { get; set; } = new(); }

    // LseqStrategy
    private sealed class LseqModel { [CrdtLseqStrategy] public List<string> Items { get; set; } = new(); }

    // SortedSetStrategy
    private sealed record User(Guid Id, string Name) : IComparable<User>
    {
        public int CompareTo(User? other)
        {
            if (other is null)
            {
                return 1;
            }
            return string.Compare(Name, other.Name, StringComparison.Ordinal);
        }
    }
    private sealed class SortedSetModel { [CrdtSortedSetStrategy(nameof(User.Name))] public List<User> Users { get; set; } = new(); }

    // LwwMapStrategy
    private sealed class LwwMapModel { [CrdtLwwMapStrategy] public Dictionary<string, int> Map { get; set; } = new(); }

    // OrMapStrategy
    private sealed class OrMapModel { [CrdtOrMapStrategy] public Dictionary<string, int> Map { get; set; } = new(); }

    // CounterMapStrategy
    private sealed class CounterMapModel { [CrdtCounterMapStrategy] public Dictionary<string, int> Map { get; set; } = new(); }

    // MaxWinsMapStrategy
    private sealed class MaxWinsMapModel { [CrdtMaxWinsMapStrategy] public Dictionary<string, int> Map { get; set; } = new(); }

    // MinWinsMapStrategy
    private sealed class MinWinsMapModel { [CrdtMinWinsMapStrategy] public Dictionary<string, int> Map { get; set; } = new(); }

    // VoteCounterStrategy
    private sealed class VoteCounterModel { [CrdtVoteCounterStrategy] public Dictionary<string, HashSet<string>> Votes { get; set; } = new(); }

    // PriorityQueueStrategy
    private sealed record Item(string Id, int Priority);
    private sealed class ItemComparer : IElementComparer
    {
        public bool CanCompare([DisallowNull] Type type) => type == typeof(Item);
        public new bool Equals(object? x, object? y) => (x as Item)?.Id == (y as Item)?.Id;
        public int GetHashCode(object obj) => (obj as Item)?.Id?.GetHashCode() ?? 0;
    }
    private sealed class PriorityQueueModel { [CrdtPriorityQueueStrategy(nameof(Item.Priority))] public List<Item> Items { get; set; } = new(); }

    // StateMachineStrategy
    private sealed class OrderStatusStateMachine : IStateMachine<string>
    {
        public bool IsValidTransition(string from, string to) => (from, to) switch
        {
            (null, "PENDING") => true,
            ("PENDING", "PROCESSING") => true,
            _ => false
        };
    }
    private sealed class StateMachineModel { [CrdtStateMachineStrategy(typeof(OrderStatusStateMachine))] public string Status { get; set; } }

    // ExclusiveLockStrategy
    private sealed class ExclusiveLockModel { public string UserId { get; set; } [CrdtExclusiveLockStrategy("$.userId")] public string LockedValue { get; set; } }

    // GraphStrategy
    private sealed class GraphModel { [CrdtGraphStrategy] public CrdtGraph Graph { get; set; } = new(); }

    // TwoPhaseGraphStrategy
    private sealed class TwoPhaseGraphModel { [CrdtTwoPhaseGraphStrategy] public CrdtGraph Graph { get; set; } = new(); }

    // ReplicatedTreeStrategy
    private sealed class ReplicatedTreeModel { [CrdtReplicatedTreeStrategy] public CrdtTree Tree { get; set; } = new(); }

    #endregion

    private readonly IServiceScope scope;
    private readonly ICrdtPatcher patcher;
    private readonly ICrdtApplicator applicator;
    private readonly ICrdtMetadataManager metadataManager;
    private readonly JsonSerializerOptions jsonSerializerOptions;

    public StrategyPayloadSerializationTests()
    {
        var services = new ServiceCollection()
            .AddCrdt()
            .AddCrdtComparer<ItemComparer>()
            .AddSingleton<OrderStatusStateMachine>()
            .AddCrdtSerializableType<User>("test-user")
            .AddCrdtSerializableType<Item>("test-item");

        var serviceProvider = services.BuildServiceProvider();

        scope = serviceProvider.GetRequiredService<ICrdtScopeFactory>().CreateScope("serialization-test");
        patcher = scope.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        applicator = scope.ServiceProvider.GetRequiredService<ICrdtApplicator>();
        metadataManager = scope.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();

        jsonSerializerOptions = new JsonSerializerOptions(CrdtJsonContext.DefaultOptions);
    }

    public void Dispose() => scope.Dispose();

    [Fact] public void LwwStrategy_Payload_ShouldBeSerializable() => TestStrategy(new LwwModel { Value = 10 }, new LwwModel { Value = 20 });
    [Fact] public void CounterStrategy_Payload_ShouldBeSerializable() => TestStrategy(new CounterModel { Value = 10 }, new CounterModel { Value = 15 });
    [Fact] public void GCounterStrategy_Payload_ShouldBeSerializable() => TestStrategy(new GCounterModel { Value = 10 }, new GCounterModel { Value = 15 });
    [Fact] public void BoundedCounterStrategy_Payload_ShouldBeSerializable() => TestStrategy(new BoundedCounterModel { Value = 50 }, new BoundedCounterModel { Value = 60 });
    [Fact] public void MaxWinsStrategy_Payload_ShouldBeSerializable() => TestStrategy(new MaxWinsModel { Value = 100 }, new MaxWinsModel { Value = 200 });
    [Fact] public void MinWinsStrategy_Payload_ShouldBeSerializable() => TestStrategy(new MinWinsModel { Value = 200 }, new MinWinsModel { Value = 100 });
    [Fact] public void AverageRegisterStrategy_Payload_ShouldBeSerializable() => TestStrategy(new AverageRegisterModel { Value = 10.0m }, new AverageRegisterModel { Value = 20.0m });
    [Fact] public void ArrayLcsStrategy_Payload_ShouldBeSerializable() => TestStrategy(new ArrayLcsModel { Items = { "A", "C" } }, new ArrayLcsModel { Items = { "A", "B", "C" } });
    [Fact] public void FixedSizeArrayStrategy_Payload_ShouldBeSerializable() => TestStrategy(new FixedSizeArrayModel { Items = { 1, 2, 3 } }, new FixedSizeArrayModel { Items = { 1, 99, 3 } });
    [Fact] public void GSetStrategy_Payload_ShouldBeSerializable() => TestStrategy(new GSetModel { Items = { "A" } }, new GSetModel { Items = { "A", "B" } });
    [Fact] public void TwoPhaseSetStrategy_Payload_ShouldBeSerializable() => TestStrategy(new TwoPhaseSetModel { Items = { "A", "B" } }, new TwoPhaseSetModel { Items = { "A" } });
    [Fact] public void LwwSetStrategy_Payload_ShouldBeSerializable() => TestStrategy(new LwwSetModel { Items = { "A" } }, new LwwSetModel { Items = { "A", "B" } });
    [Fact] public void OrSetStrategy_Payload_ShouldBeSerializable() => TestStrategy(new OrSetModel { Items = { "A" } }, new OrSetModel { Items = { "A", "B" } });
    [Fact] public void LseqStrategy_Payload_ShouldBeSerializable() => TestStrategy(new LseqModel { Items = { "A", "C" } }, new LseqModel { Items = { "A", "B", "C" } });
    [Fact] public void SortedSetStrategy_Payload_ShouldBeSerializable() => TestStrategy(new SortedSetModel { Users = { new User(Guid.NewGuid(), "A") } }, new SortedSetModel { Users = { new User(Guid.NewGuid(), "A"), new User(Guid.NewGuid(), "B") } });
    [Fact] public void LwwMapStrategy_Payload_ShouldBeSerializable() => TestStrategy(new LwwMapModel { Map = { { "A", 1 } } }, new LwwMapModel { Map = { { "A", 2 } } });
    [Fact] public void OrMapStrategy_Payload_ShouldBeSerializable() => TestStrategy(new OrMapModel { Map = { { "A", 1 } } }, new OrMapModel { Map = { { "A", 1 }, { "B", 2 } } });
    [Fact] public void CounterMapStrategy_Payload_ShouldBeSerializable() => TestStrategy(new CounterMapModel { Map = { { "A", 10 } } }, new CounterMapModel { Map = { { "A", 15 } } });
    [Fact] public void MaxWinsMapStrategy_Payload_ShouldBeSerializable() => TestStrategy(new MaxWinsMapModel { Map = { { "A", 10 } } }, new MaxWinsMapModel { Map = { { "A", 20 } } });
    [Fact] public void MinWinsMapStrategy_Payload_ShouldBeSerializable() => TestStrategy(new MinWinsMapModel { Map = { { "A", 20 } } }, new MinWinsMapModel { Map = { { "A", 10 } } });
    [Fact] public void PriorityQueueStrategy_Payload_ShouldBeSerializable() => TestStrategy(new PriorityQueueModel { Items = { new Item("A", 10) } }, new PriorityQueueModel { Items = { new Item("A", 10), new Item("B", 5) } });
    [Fact] public void StateMachineStrategy_Payload_ShouldBeSerializable() => TestStrategy(new StateMachineModel { Status = "PENDING" }, new StateMachineModel { Status = "PROCESSING" });
    [Fact] public void ExclusiveLockStrategy_Payload_ShouldBeSerializable() => TestStrategy(new ExclusiveLockModel { LockedValue = "A" }, new ExclusiveLockModel { UserId = "user1", LockedValue = "B" });

    [Fact]
    public void GraphStrategy_Payload_ShouldBeSerializable()
    {
        var initial = new GraphModel();
        initial.Graph.Vertices.Add("A");
        var modified = new GraphModel();
        modified.Graph.Vertices.Add("A");
        modified.Graph.Vertices.Add("B");
        modified.Graph.Edges.Add(new Edge("A", "B", null));
        TestStrategy(initial, modified);
    }

    [Fact]
    public void TwoPhaseGraphStrategy_Payload_ShouldBeSerializable()
    {
        var initial = new TwoPhaseGraphModel();
        initial.Graph.Vertices.Add("A");
        var modified = new TwoPhaseGraphModel();
        modified.Graph.Vertices.Add("A");
        modified.Graph.Vertices.Add("B");
        TestStrategy(initial, modified);
    }

    [Fact]
    public void ReplicatedTreeStrategy_Payload_ShouldBeSerializable()
    {
        var node1Id = Guid.NewGuid();
        var node2Id = Guid.NewGuid();
        var initial = new ReplicatedTreeModel();
        initial.Tree.Nodes.Add(node1Id, new TreeNode { Id = node1Id, Value = "A" });
        var modified = new ReplicatedTreeModel();
        modified.Tree.Nodes.Add(node1Id, new TreeNode { Id = node1Id, Value = "A" });
        modified.Tree.Nodes.Add(node2Id, new TreeNode { Id = node2Id, Value = "B", ParentId = node1Id });
        TestStrategy(initial, modified);
    }
    
    [Fact]
    public void VoteCounterStrategy_Payload_ShouldBeSerializable()
    {
        var initial = new VoteCounterModel { Votes = { ["OptionA"] = new HashSet<string> { "Voter1" } } };
        var modified = new VoteCounterModel { Votes = { ["OptionB"] = new HashSet<string> { "Voter1" } } };
        
        var initialJson = JsonSerializer.Serialize(initial, jsonSerializerOptions);
        var initialForTarget = JsonSerializer.Deserialize<VoteCounterModel>(initialJson, jsonSerializerOptions)!;

        var doc = new CrdtDocument<VoteCounterModel>(initial, metadataManager.Initialize(initial));
        var targetDoc = new CrdtDocument<VoteCounterModel>(initialForTarget, metadataManager.Initialize(initialForTarget));

        var patch = patcher.GeneratePatch(doc, modified);
        patch.Operations.ShouldNotBeEmpty();

        var deserializedPatch = SerializeAndDeserialize(patch);

        applicator.ApplyPatch(targetDoc, deserializedPatch);

        targetDoc.Data.Votes.Count.ShouldBe(1);
        targetDoc.Data.Votes.ShouldContainKey("OptionB");
        targetDoc.Data.Votes["OptionB"].ShouldHaveSingleItem().ShouldBe("Voter1");
    }

    private CrdtPatch SerializeAndDeserialize(CrdtPatch patch)
    {
        var json = JsonSerializer.Serialize(patch, jsonSerializerOptions);
        return JsonSerializer.Deserialize<CrdtPatch>(json, jsonSerializerOptions)!;
    }

    private void TestStrategy<TModel>(TModel initial, TModel modified) where TModel : class, new()
    {
        var initialJson = JsonSerializer.Serialize(initial, jsonSerializerOptions);
        var initialForTarget = JsonSerializer.Deserialize<TModel>(initialJson, jsonSerializerOptions)!;

        var doc = new CrdtDocument<TModel>(initial, metadataManager.Initialize(initial));
        var targetDoc = new CrdtDocument<TModel>(initialForTarget, metadataManager.Initialize(initialForTarget));

        var patch = patcher.GeneratePatch(doc, modified);
        patch.Operations.ShouldNotBeEmpty($"Patch should have operations for model {typeof(TModel).Name}");

        var deserializedPatch = SerializeAndDeserialize(patch);
        deserializedPatch.Operations.Count.ShouldBe(patch.Operations.Count);

        applicator.ApplyPatch(targetDoc, deserializedPatch);

        JsonSerializer.Serialize(targetDoc.Data, jsonSerializerOptions).ShouldBe(JsonSerializer.Serialize(modified, jsonSerializerOptions));
    }
}