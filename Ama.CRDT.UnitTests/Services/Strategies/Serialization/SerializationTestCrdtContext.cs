namespace Ama.CRDT.UnitTests.Services.Strategies.Serialization;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using System.Collections.Generic;

/// <summary>
/// A dedicated CrdtAotContext for the StrategyPayloadSerialization tests to provide AOT-compatible 
/// property metadata for test-specific models and collections.
/// </summary>
[CrdtAotType(typeof(StrategyPayloadSerializationTests.LwwModel))]
[CrdtAotType(typeof(StrategyPayloadSerializationTests.FwwModel))]
[CrdtAotType(typeof(StrategyPayloadSerializationTests.CounterModel))]
[CrdtAotType(typeof(StrategyPayloadSerializationTests.GCounterModel))]
[CrdtAotType(typeof(StrategyPayloadSerializationTests.BoundedCounterModel))]
[CrdtAotType(typeof(StrategyPayloadSerializationTests.MaxWinsModel))]
[CrdtAotType(typeof(StrategyPayloadSerializationTests.MinWinsModel))]
[CrdtAotType(typeof(StrategyPayloadSerializationTests.AverageRegisterModel))]
[CrdtAotType(typeof(StrategyPayloadSerializationTests.ArrayLcsModel))]
[CrdtAotType(typeof(StrategyPayloadSerializationTests.FixedSizeArrayModel))]
[CrdtAotType(typeof(StrategyPayloadSerializationTests.GSetModel))]
[CrdtAotType(typeof(StrategyPayloadSerializationTests.TwoPhaseSetModel))]
[CrdtAotType(typeof(StrategyPayloadSerializationTests.LwwSetModel))]
[CrdtAotType(typeof(StrategyPayloadSerializationTests.FwwSetModel))]
[CrdtAotType(typeof(StrategyPayloadSerializationTests.OrSetModel))]
[CrdtAotType(typeof(StrategyPayloadSerializationTests.LseqModel))]
[CrdtAotType(typeof(StrategyPayloadSerializationTests.RgaModel))]
[CrdtAotType(typeof(StrategyPayloadSerializationTests.User))]
[CrdtAotType(typeof(StrategyPayloadSerializationTests.SortedSetModel))]
[CrdtAotType(typeof(StrategyPayloadSerializationTests.LwwMapModel))]
[CrdtAotType(typeof(StrategyPayloadSerializationTests.FwwMapModel))]
[CrdtAotType(typeof(StrategyPayloadSerializationTests.OrMapModel))]
[CrdtAotType(typeof(StrategyPayloadSerializationTests.CounterMapModel))]
[CrdtAotType(typeof(StrategyPayloadSerializationTests.MaxWinsMapModel))]
[CrdtAotType(typeof(StrategyPayloadSerializationTests.MinWinsMapModel))]
[CrdtAotType(typeof(StrategyPayloadSerializationTests.VoteCounterModel))]
[CrdtAotType(typeof(StrategyPayloadSerializationTests.Item))]
[CrdtAotType(typeof(StrategyPayloadSerializationTests.PriorityQueueModel))]
[CrdtAotType(typeof(StrategyPayloadSerializationTests.StateMachineModel))]
[CrdtAotType(typeof(StrategyPayloadSerializationTests.GraphModel))]
[CrdtAotType(typeof(StrategyPayloadSerializationTests.TwoPhaseGraphModel))]
[CrdtAotType(typeof(StrategyPayloadSerializationTests.ReplicatedTreeModel))]
[CrdtAotType(typeof(StrategyPayloadSerializationTests.EpochBoundModel))]
[CrdtAotType(typeof(StrategyPayloadSerializationTests.ApprovalQuorumModel))]
[CrdtAotType(typeof(List<string>))]
[CrdtAotType(typeof(List<int>))]
[CrdtAotType(typeof(List<StrategyPayloadSerializationTests.User>))]
[CrdtAotType(typeof(List<StrategyPayloadSerializationTests.Item>))]
[CrdtAotType(typeof(Dictionary<string, int>))]
[CrdtAotType(typeof(Dictionary<string, HashSet<string>>))]
[CrdtAotType(typeof(HashSet<string>))]
[CrdtAotType(typeof(int))]
[CrdtAotType(typeof(Guid))]
[CrdtAotType(typeof(string))]
[CrdtAotType(typeof(IDictionary<object, CausalTimestamp>))]
[CrdtAotType(typeof(Dictionary<object, CausalTimestamp>))]
[CrdtAotType(typeof(IDictionary<object, ICrdtTimestamp>))]
[CrdtAotType(typeof(Dictionary<object, ICrdtTimestamp>))]
[CrdtAotType(typeof(IDictionary<object, ISet<Guid>>))]
[CrdtAotType(typeof(Dictionary<object, ISet<Guid>>))]
[CrdtAotType(typeof(IDictionary<object, IDictionary<Guid, CausalTimestamp>>))]
[CrdtAotType(typeof(Dictionary<object, IDictionary<Guid, CausalTimestamp>>))]
[CrdtAotType(typeof(IDictionary<Guid, CausalTimestamp>))]
[CrdtAotType(typeof(Dictionary<Guid, CausalTimestamp>))]
[CrdtAotType(typeof(IDictionary<object, PnCounterState>))]
[CrdtAotType(typeof(Dictionary<object, PnCounterState>))]
internal partial class SerializationTestCrdtAotContext : CrdtAotContext
{
}