namespace Ama.CRDT.UnitTests.Services.Strategies.Serialization;

using Ama.CRDT.Attributes.Decorators;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Decorators;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.UnitTests.Models.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Threading;
using Xunit;

public sealed class StrategyPayloadSerializationTests : IDisposable
{
    // Local test provider to guarantee strictly increasing timestamps during rapid test execution
    internal sealed class TestTimestampProvider : ICrdtTimestampProvider
    {
        private long current = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        private readonly EpochTimestampProvider defaultProvider = new(new ReplicaContext { ReplicaId = "test" });
        
        public ICrdtTimestamp Now() => defaultProvider.Create(Interlocked.Increment(ref current));
        public ICrdtTimestamp Create(long value) => defaultProvider.Create(value);
    }

    #region Models and Helpers

    // LwwStrategy
    internal sealed class LwwModel { [CrdtLwwStrategy] public int Value { get; set; } }

    // FwwStrategy
    internal sealed class FwwModel { [CrdtFwwStrategy] public int Value { get; set; } }

    // CounterStrategy
    internal sealed class CounterModel { [CrdtCounterStrategy] public int Value { get; set; } }

    // GCounterStrategy
    internal sealed class GCounterModel { [CrdtGCounterStrategy] public int Value { get; set; } }

    // BoundedCounterStrategy
    internal sealed class BoundedCounterModel { [CrdtBoundedCounterStrategy(0, 100)] public int Value { get; set; } }

    // MaxWinsStrategy
    internal sealed class MaxWinsModel { [CrdtMaxWinsStrategy] public int Value { get; set; } }

    // MinWinsStrategy
    internal sealed class MinWinsModel { [CrdtMinWinsStrategy] public int Value { get; set; } }

    // AverageRegisterStrategy
    internal sealed class AverageRegisterModel { [CrdtAverageRegisterStrategy] public decimal Value { get; set; } }

    // ArrayLcsStrategy
    internal sealed class ArrayLcsModel { [CrdtArrayLcsStrategy] public List<string> Items { get; set; } = new(); }

    // FixedSizeArrayStrategy
    internal sealed class FixedSizeArrayModel { [CrdtFixedSizeArrayStrategy(3)] public List<int> Items { get; set; } = new(); }

    // GSetStrategy
    internal sealed class GSetModel { [CrdtGSetStrategy] public List<string> Items { get; set; } = new(); }

    // TwoPhaseSetStrategy
    internal sealed class TwoPhaseSetModel { [CrdtTwoPhaseSetStrategy] public List<string> Items { get; set; } = new(); }

    // LwwSetStrategy
    internal sealed class LwwSetModel { [CrdtLwwSetStrategy] public List<string> Items { get; set; } = new(); }

    // FwwSetStrategy
    internal sealed class FwwSetModel { [CrdtFwwSetStrategy] public List<string> Items { get; set; } = new(); }

    // OrSetStrategy
    internal sealed class OrSetModel { [CrdtOrSetStrategy] public List<string> Items { get; set; } = new(); }

    // LseqStrategy
    internal sealed class LseqModel { [CrdtLseqStrategy] public List<string> Items { get; set; } = new(); }

    // RgaStrategy
    internal sealed class RgaModel { [CrdtRgaStrategy] public List<string> Items { get; set; } = new(); }

    // SortedSetStrategy
    internal sealed record User(Guid Id, string Name) : IComparable<User>
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
    internal sealed class SortedSetModel { [CrdtSortedSetStrategy(nameof(User.Name))] public List<User> Users { get; set; } = new(); }

    // LwwMapStrategy
    internal sealed class LwwMapModel { [CrdtLwwMapStrategy] public Dictionary<string, int> Map { get; set; } = new(); }

    // FwwMapStrategy
    internal sealed class FwwMapModel { [CrdtFwwMapStrategy] public Dictionary<string, int> Map { get; set; } = new(); }

    // OrMapStrategy
    internal sealed class OrMapModel { [CrdtOrMapStrategy] public Dictionary<string, int> Map { get; set; } = new(); }

    // CounterMapStrategy
    internal sealed class CounterMapModel { [CrdtCounterMapStrategy] public Dictionary<string, int> Map { get; set; } = new(); }

    // MaxWinsMapStrategy
    internal sealed class MaxWinsMapModel { [CrdtMaxWinsMapStrategy] public Dictionary<string, int> Map { get; set; } = new(); }

    // MinWinsMapStrategy
    internal sealed class MinWinsMapModel { [CrdtMinWinsMapStrategy] public Dictionary<string, int> Map { get; set; } = new(); }

    // VoteCounterStrategy
    internal sealed class VoteCounterModel { [CrdtVoteCounterStrategy] public Dictionary<string, HashSet<string>> Votes { get; set; } = new(); }

    // PriorityQueueStrategy
    internal sealed record Item(string Id, int Priority);
    internal sealed class ItemComparer : IElementComparer
    {
        public bool CanCompare([DisallowNull] Type type) => type == typeof(Item);
        public new bool Equals(object? x, object? y) => (x as Item)?.Id == (y as Item)?.Id;
        public int GetHashCode(object obj) => (obj as Item)?.Id?.GetHashCode() ?? 0;
    }
    internal sealed class PriorityQueueModel { [CrdtPriorityQueueStrategy(nameof(Item.Priority))] public List<Item> Items { get; set; } = new(); }

    // StateMachineStrategy
    internal sealed class OrderStatusStateMachine : IStateMachine<string>
    {
        public bool IsValidTransition(string from, string to) => (from, to) switch
        {
            (null, "PENDING") => true,
            ("PENDING", "PROCESSING") => true,
            _ => false
        };
    }
    internal sealed class StateMachineModel { [CrdtStateMachineStrategy(typeof(OrderStatusStateMachine))] public string Status { get; set; } }

    // GraphStrategy
    internal sealed class GraphModel { [CrdtGraphStrategy] public CrdtGraph Graph { get; set; } = new(); }

    // TwoPhaseGraphStrategy
    internal sealed class TwoPhaseGraphModel { [CrdtTwoPhaseGraphStrategy] public CrdtGraph Graph { get; set; } = new(); }

    // ReplicatedTreeStrategy
    internal sealed class ReplicatedTreeModel { [CrdtReplicatedTreeStrategy] public CrdtTree Tree { get; set; } = new(); }

    // EpochBoundStrategy
    internal sealed class EpochBoundModel { [CrdtEpochBound] [CrdtLwwStrategy] public int Value { get; set; } }

    // ApprovalQuorumStrategy
    internal sealed class ApprovalQuorumModel { [CrdtApprovalQuorum(2)] [CrdtLwwStrategy] public int Value { get; set; } }

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
            .AddCrdtAotContext<SerializationTestCrdtAotContext>()
            .AddCrdtTimestampProvider<TestTimestampProvider>()
            .AddCrdtComparer<ItemComparer>()
            .AddSingleton<OrderStatusStateMachine>()
            .AddCrdtSerializableType<User>("test-user")
            .AddCrdtSerializableType<Item>("test-item");

        var serviceProvider = services.BuildServiceProvider();

        scope = serviceProvider.GetRequiredService<ICrdtScopeFactory>().CreateScope("serialization-test");
        patcher = scope.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        applicator = scope.ServiceProvider.GetRequiredService<ICrdtApplicator>();
        metadataManager = scope.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();

        jsonSerializerOptions = TestOptionsHelper.GetDefaultOptions();
    }

    public void Dispose() => scope.Dispose();

    [Fact] public void LwwStrategy_Payload_ShouldBeSerializable() => TestStrategy(new LwwModel { Value = 10 }, new LwwModel { Value = 20 });
    
    [Fact]
    public void FwwStrategy_Payload_ShouldBeSerializable()
    {
        var initial = new FwwModel { Value = 10 };
        var initialJson = JsonSerializer.Serialize(initial, jsonSerializerOptions);
        var initialForTarget = JsonSerializer.Deserialize<FwwModel>(initialJson, jsonSerializerOptions)!;

        var metadata = metadataManager.Initialize(initial, new EpochTimestamp(1));
        var targetDoc = new CrdtDocument<FwwModel>(initialForTarget, metadata.DeepClone());

        // Create an operation with an OLDER timestamp so it wins (FWW)
        var olderTimestamp = new EpochTimestamp(0);
        var op = new CrdtOperation(Guid.NewGuid(), "test", "$.value", OperationType.Upsert, 20, olderTimestamp, 0);
        var patch = new CrdtPatch(new List<CrdtOperation> { op });

        var deserializedPatch = SerializeAndDeserialize(patch);
        deserializedPatch.Operations.Count.ShouldBe(1);

        applicator.ApplyPatch(targetDoc, deserializedPatch);

        targetDoc.Data!.Value.ShouldBe(20);
    }

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
    [Fact] public void FwwSetStrategy_Payload_ShouldBeSerializable() => TestStrategy(new FwwSetModel { Items = { "A" } }, new FwwSetModel { Items = { "A", "B" } });
    [Fact] public void OrSetStrategy_Payload_ShouldBeSerializable() => TestStrategy(new OrSetModel { Items = { "A" } }, new OrSetModel { Items = { "A", "B" } });
    [Fact] public void LseqStrategy_Payload_ShouldBeSerializable() => TestStrategy(new LseqModel { Items = { "A", "C" } }, new LseqModel { Items = { "A", "B", "C" } });
    [Fact] public void RgaStrategy_Payload_ShouldBeSerializable() => TestStrategy(new RgaModel { Items = { "A", "C" } }, new RgaModel { Items = { "A", "B", "C" } });
    
    [Fact]
    public void SortedSetStrategy_Payload_ShouldBeSerializable()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        TestStrategy(
            new SortedSetModel { Users = { new User(id1, "A") } }, 
            new SortedSetModel { Users = { new User(id1, "A"), new User(id2, "B") } });
    }
    
    [Fact] public void LwwMapStrategy_Payload_ShouldBeSerializable() => TestStrategy(new LwwMapModel { Map = { { "A", 1 } } }, new LwwMapModel { Map = { { "A", 2 } } });
    
    [Fact]
    public void FwwMapStrategy_Payload_ShouldBeSerializable()
    {
        var initial = new FwwMapModel { Map = { { "A", 1 } } };
        var initialJson = JsonSerializer.Serialize(initial, jsonSerializerOptions);
        var initialForTarget = JsonSerializer.Deserialize<FwwMapModel>(initialJson, jsonSerializerOptions)!;

        var metadata = metadataManager.Initialize(initial, new EpochTimestamp(1));
        var targetDoc = new CrdtDocument<FwwMapModel>(initialForTarget, metadata.DeepClone());

        // Create an operation with an OLDER timestamp so it wins (FWW)
        var olderTimestamp = new EpochTimestamp(0);
        var payload = new KeyValuePair<object, object?>("A", 2);
        var op = new CrdtOperation(Guid.NewGuid(), "test", "$.map", OperationType.Upsert, payload, olderTimestamp, 0);
        var patch = new CrdtPatch(new List<CrdtOperation> { op });

        var deserializedPatch = SerializeAndDeserialize(patch);
        deserializedPatch.Operations.Count.ShouldBe(1);

        applicator.ApplyPatch(targetDoc, deserializedPatch);

        targetDoc.Data!.Map["A"].ShouldBe(2);
    }

    [Fact] public void OrMapStrategy_Payload_ShouldBeSerializable() => TestStrategy(new OrMapModel { Map = { { "A", 1 } } }, new OrMapModel { Map = { { "A", 1 }, { "B", 2 } } });
    [Fact] public void CounterMapStrategy_Payload_ShouldBeSerializable() => TestStrategy(new CounterMapModel { Map = { { "A", 10 } } }, new CounterMapModel { Map = { { "A", 15 } } });
    [Fact] public void MaxWinsMapStrategy_Payload_ShouldBeSerializable() => TestStrategy(new MaxWinsMapModel { Map = { { "A", 10 } } }, new MaxWinsMapModel { Map = { { "A", 20 } } });
    [Fact] public void MinWinsMapStrategy_Payload_ShouldBeSerializable() => TestStrategy(new MinWinsMapModel { Map = { { "A", 20 } } }, new MinWinsMapModel { Map = { { "A", 10 } } });
    [Fact] public void PriorityQueueStrategy_Payload_ShouldBeSerializable() => TestStrategy(new PriorityQueueModel { Items = { new Item("A", 10) } }, new PriorityQueueModel { Items = { new Item("A", 10), new Item("B", 5) } });
    [Fact] public void StateMachineStrategy_Payload_ShouldBeSerializable() => TestStrategy(new StateMachineModel { Status = "PENDING" }, new StateMachineModel { Status = "PROCESSING" });
    [Fact] public void EpochBoundStrategy_Payload_ShouldBeSerializable() => TestStrategy(new EpochBoundModel { Value = 10 }, new EpochBoundModel { Value = 20 });
    
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

        var metadata = metadataManager.Initialize(initial);
        var doc = new CrdtDocument<VoteCounterModel>(initial, metadata);
        var targetDoc = new CrdtDocument<VoteCounterModel>(initialForTarget, metadata.DeepClone());

        var patch = patcher.GeneratePatch(doc, modified);
        patch.Operations.ShouldNotBeEmpty();

        var deserializedPatch = SerializeAndDeserialize(patch);

        applicator.ApplyPatch(targetDoc, deserializedPatch);

        targetDoc.Data!.Votes.Count.ShouldBe(1);
        targetDoc.Data.Votes.ShouldContainKey("OptionB");
        targetDoc.Data.Votes["OptionB"].ShouldHaveSingleItem().ShouldBe("Voter1");
    }

    [Fact]
    public void ApprovalQuorumStrategy_Payload_ShouldBeSerializable()
    {
        var initial = new ApprovalQuorumModel { Value = 10 };
        var modified = new ApprovalQuorumModel { Value = 20 };

        var initialJson = JsonSerializer.Serialize(initial, jsonSerializerOptions);
        var initialForTarget = JsonSerializer.Deserialize<ApprovalQuorumModel>(initialJson, jsonSerializerOptions)!;

        var metadata = metadataManager.Initialize(initial);
        var doc = new CrdtDocument<ApprovalQuorumModel>(initial, metadata);
        var targetDoc = new CrdtDocument<ApprovalQuorumModel>(initialForTarget, metadata.DeepClone());

        var patch = patcher.GeneratePatch(doc, modified);
        patch.Operations.ShouldNotBeEmpty();

        var deserializedPatch = SerializeAndDeserialize(patch);

        applicator.ApplyPatch(targetDoc, deserializedPatch);

        // The value shouldn't change yet because quorum is 2, but metadata should have the proposal
        targetDoc.Data!.Value.ShouldBe(10);
        targetDoc.Metadata!.States.ShouldNotBeEmpty();
    }

    [Fact] public void GraphEdgePayload_ShouldSerializeAndDeserialize() => TestPayloadSerialization<GraphEdgePayload>();
    [Fact] public void GraphVertexPayload_ShouldSerializeAndDeserialize() => TestPayloadSerialization<GraphVertexPayload>();
    [Fact] public void OrMapAddItem_ShouldSerializeAndDeserialize() => TestPayloadSerialization<OrMapAddItem>();
    [Fact] public void OrMapRemoveItem_ShouldSerializeAndDeserialize() => TestPayloadSerialization<OrMapRemoveItem>();
    [Fact] public void OrSetAddItem_ShouldSerializeAndDeserialize() => TestPayloadSerialization<OrSetAddItem>();
    [Fact] public void OrSetRemoveItem_ShouldSerializeAndDeserialize() => TestPayloadSerialization<OrSetRemoveItem>();
    [Fact] public void PositionalItem_ShouldSerializeAndDeserialize() => TestPayloadSerialization<PositionalItem>();
    [Fact] public void RgaIdentifier_ShouldSerializeAndDeserialize() => TestPayloadSerialization<RgaIdentifier>();
    [Fact] public void RgaItem_ShouldSerializeAndDeserialize() => TestPayloadSerialization<RgaItem>();
    [Fact] public void TreeAddNodePayload_ShouldSerializeAndDeserialize() => TestPayloadSerialization<TreeAddNodePayload>();
    [Fact] public void TreeMoveNodePayload_ShouldSerializeAndDeserialize() => TestPayloadSerialization<TreeMoveNodePayload>();
    [Fact] public void TreeRemoveNodePayload_ShouldSerializeAndDeserialize() => TestPayloadSerialization<TreeRemoveNodePayload>();
    [Fact] public void VotePayload_ShouldSerializeAndDeserialize() => TestPayloadSerialization<VotePayload>();
    [Fact] public void EpochPayload_ShouldSerializeAndDeserialize() => TestPayloadSerialization<EpochPayload>();
    [Fact] public void QuorumPayload_ShouldSerializeAndDeserialize() => TestPayloadSerialization<QuorumPayload>();

    private void TestPayloadSerialization<T>()
    {
        var type = typeof(T);
        var obj = default(T);
        var json = JsonSerializer.Serialize(obj, type, jsonSerializerOptions);
        var deserialized = JsonSerializer.Deserialize(json, type, jsonSerializerOptions);
        
        if (obj != null)
        {
            deserialized.ShouldBe(obj);
        }
        else
        {
            deserialized.ShouldBeNull();
        }
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

        var metadata = metadataManager.Initialize(initial);
        var doc = new CrdtDocument<TModel>(initial, metadata);
        var targetDoc = new CrdtDocument<TModel>(initialForTarget, metadata.DeepClone());

        var patch = patcher.GeneratePatch(doc, modified);
        patch.Operations.ShouldNotBeEmpty($"Patch should have operations for model {typeof(TModel).Name}");

        var deserializedPatch = SerializeAndDeserialize(patch);
        deserializedPatch.Operations.Count.ShouldBe(patch.Operations.Count);

        applicator.ApplyPatch(targetDoc, deserializedPatch);

        JsonSerializer.Serialize(targetDoc.Data, jsonSerializerOptions).ShouldBe(JsonSerializer.Serialize(modified, jsonSerializerOptions));
    }
}