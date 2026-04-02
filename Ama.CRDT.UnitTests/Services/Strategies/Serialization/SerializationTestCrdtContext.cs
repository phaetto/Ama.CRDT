namespace Ama.CRDT.UnitTests.Services.Strategies.Serialization;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// A dedicated CrdtContext for the StrategyPayloadSerialization tests to provide AOT-compatible 
/// property metadata for test-specific models and collections.
/// </summary>
[CrdtSerializable(typeof(StrategyPayloadSerializationTests.LwwModel))]
[CrdtSerializable(typeof(StrategyPayloadSerializationTests.FwwModel))]
[CrdtSerializable(typeof(StrategyPayloadSerializationTests.CounterModel))]
[CrdtSerializable(typeof(StrategyPayloadSerializationTests.GCounterModel))]
[CrdtSerializable(typeof(StrategyPayloadSerializationTests.BoundedCounterModel))]
[CrdtSerializable(typeof(StrategyPayloadSerializationTests.MaxWinsModel))]
[CrdtSerializable(typeof(StrategyPayloadSerializationTests.MinWinsModel))]
[CrdtSerializable(typeof(StrategyPayloadSerializationTests.AverageRegisterModel))]
[CrdtSerializable(typeof(StrategyPayloadSerializationTests.ArrayLcsModel))]
[CrdtSerializable(typeof(StrategyPayloadSerializationTests.FixedSizeArrayModel))]
[CrdtSerializable(typeof(StrategyPayloadSerializationTests.GSetModel))]
[CrdtSerializable(typeof(StrategyPayloadSerializationTests.TwoPhaseSetModel))]
[CrdtSerializable(typeof(StrategyPayloadSerializationTests.LwwSetModel))]
[CrdtSerializable(typeof(StrategyPayloadSerializationTests.FwwSetModel))]
[CrdtSerializable(typeof(StrategyPayloadSerializationTests.OrSetModel))]
[CrdtSerializable(typeof(StrategyPayloadSerializationTests.LseqModel))]
[CrdtSerializable(typeof(StrategyPayloadSerializationTests.RgaModel))]
[CrdtSerializable(typeof(StrategyPayloadSerializationTests.User))]
[CrdtSerializable(typeof(StrategyPayloadSerializationTests.SortedSetModel))]
[CrdtSerializable(typeof(StrategyPayloadSerializationTests.LwwMapModel))]
[CrdtSerializable(typeof(StrategyPayloadSerializationTests.FwwMapModel))]
[CrdtSerializable(typeof(StrategyPayloadSerializationTests.OrMapModel))]
[CrdtSerializable(typeof(StrategyPayloadSerializationTests.CounterMapModel))]
[CrdtSerializable(typeof(StrategyPayloadSerializationTests.MaxWinsMapModel))]
[CrdtSerializable(typeof(StrategyPayloadSerializationTests.MinWinsMapModel))]
[CrdtSerializable(typeof(StrategyPayloadSerializationTests.VoteCounterModel))]
[CrdtSerializable(typeof(StrategyPayloadSerializationTests.Item))]
[CrdtSerializable(typeof(StrategyPayloadSerializationTests.PriorityQueueModel))]
[CrdtSerializable(typeof(StrategyPayloadSerializationTests.StateMachineModel))]
[CrdtSerializable(typeof(StrategyPayloadSerializationTests.GraphModel))]
[CrdtSerializable(typeof(StrategyPayloadSerializationTests.TwoPhaseGraphModel))]
[CrdtSerializable(typeof(StrategyPayloadSerializationTests.ReplicatedTreeModel))]
[CrdtSerializable(typeof(StrategyPayloadSerializationTests.EpochBoundModel))]
[CrdtSerializable(typeof(StrategyPayloadSerializationTests.ApprovalQuorumModel))]
[CrdtSerializable(typeof(List<string>))]
[CrdtSerializable(typeof(List<int>))]
[CrdtSerializable(typeof(List<StrategyPayloadSerializationTests.User>))]
[CrdtSerializable(typeof(List<StrategyPayloadSerializationTests.Item>))]
[CrdtSerializable(typeof(Dictionary<string, int>))]
[CrdtSerializable(typeof(Dictionary<string, HashSet<string>>))]
[CrdtSerializable(typeof(HashSet<string>))]
[CrdtSerializable(typeof(int))]
[CrdtSerializable(typeof(Guid))]
[CrdtSerializable(typeof(string))]
[CrdtSerializable(typeof(IDictionary<object, CausalTimestamp>))]
[CrdtSerializable(typeof(Dictionary<object, CausalTimestamp>))]
[CrdtSerializable(typeof(IDictionary<object, ICrdtTimestamp>))]
[CrdtSerializable(typeof(Dictionary<object, ICrdtTimestamp>))]
[CrdtSerializable(typeof(IDictionary<object, ISet<Guid>>))]
[CrdtSerializable(typeof(Dictionary<object, ISet<Guid>>))]
[CrdtSerializable(typeof(IDictionary<object, IDictionary<Guid, CausalTimestamp>>))]
[CrdtSerializable(typeof(Dictionary<object, IDictionary<Guid, CausalTimestamp>>))]
[CrdtSerializable(typeof(IDictionary<Guid, CausalTimestamp>))]
[CrdtSerializable(typeof(Dictionary<Guid, CausalTimestamp>))]
[CrdtSerializable(typeof(IDictionary<object, PnCounterState>))]
[CrdtSerializable(typeof(Dictionary<object, PnCounterState>))]
internal partial class SerializationTestCrdtContext : CrdtContext
{
}