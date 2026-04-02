namespace Ama.CRDT.Models.Aot;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Decorators;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Models.Intents.Decorators;
using Ama.CRDT.Models.Partitioning;

/// <summary>
/// An internal AOT-generated context containing metadata for all core CRDT models.
/// This is automatically registered into the DI container by <see cref="Extensions.ServiceCollectionExtensions.AddCrdt"/>.
/// </summary>
[CrdtSerializable(typeof(ApplyPatchResult<object>))]
[CrdtSerializable(typeof(AverageRegisterValue))]
[CrdtSerializable(typeof(BidirectionalSyncRequirements))]
[CrdtSerializable(typeof(CausalTimestamp))]
[CrdtSerializable(typeof(CrdtDocument<object>))]
[CrdtSerializable(typeof(CrdtGraph))]
[CrdtSerializable(typeof(CrdtMetadata))]
[CrdtSerializable(typeof(CrdtOperation))]
[CrdtSerializable(typeof(CrdtPatch))]
[CrdtSerializable(typeof(CrdtTree))]
[CrdtSerializable(typeof(DottedVersionVector))]
[CrdtSerializable(typeof(Edge))]
[CrdtSerializable(typeof(EpochTimestamp))]
[CrdtSerializable(typeof(GraphEdgePayload))]
[CrdtSerializable(typeof(GraphVertexPayload))]
[CrdtSerializable(typeof(JournaledOperation))]
[CrdtSerializable(typeof(LseqIdentifier))]
[CrdtSerializable(typeof(LseqItem))]
[CrdtSerializable(typeof(LseqPathSegment))]
[CrdtSerializable(typeof(LwwSetState))]
[CrdtSerializable(typeof(OrMapAddItem))]
[CrdtSerializable(typeof(OrMapRemoveItem))]
[CrdtSerializable(typeof(OrSetAddItem))]
[CrdtSerializable(typeof(OrSetRemoveItem))]
[CrdtSerializable(typeof(OrSetState))]
[CrdtSerializable(typeof(OriginSyncRequirement))]
[CrdtSerializable(typeof(PnCounterState))]
[CrdtSerializable(typeof(PositionalIdentifier))]
[CrdtSerializable(typeof(PositionalItem))]
[CrdtSerializable(typeof(ReplicaSyncRequirement))]
[CrdtSerializable(typeof(RgaIdentifier))]
[CrdtSerializable(typeof(RgaItem))]
[CrdtSerializable(typeof(TreeAddNodePayload))]
[CrdtSerializable(typeof(TreeMoveNodePayload))]
[CrdtSerializable(typeof(TreeNode))]
[CrdtSerializable(typeof(TreeRemoveNodePayload))]
[CrdtSerializable(typeof(TwoPhaseGraphState))]
[CrdtSerializable(typeof(TwoPhaseSetState))]
[CrdtSerializable(typeof(UnappliedOperation))]
[CrdtSerializable(typeof(VotePayload))]
[CrdtSerializable(typeof(EpochPayload))]
[CrdtSerializable(typeof(QuorumPayload))]
[CrdtSerializable(typeof(CompositePartitionKey))]
[CrdtSerializable(typeof(DataPartition))]
[CrdtSerializable(typeof(HeaderPartition))]
[CrdtSerializable(typeof(PartitionContent))]
[CrdtSerializable(typeof(SplitResult))]
[CrdtSerializable(typeof(AddEdgeIntent))]
[CrdtSerializable(typeof(AddIntent))]
[CrdtSerializable(typeof(AddNodeIntent))]
[CrdtSerializable(typeof(AddVertexIntent))]
[CrdtSerializable(typeof(ClearIntent))]
[CrdtSerializable(typeof(EpochClearIntent))]
[CrdtSerializable(typeof(IncrementIntent))]
[CrdtSerializable(typeof(InsertIntent))]
[CrdtSerializable(typeof(MapIncrementIntent))]
[CrdtSerializable(typeof(MapRemoveIntent))]
[CrdtSerializable(typeof(MapSetIntent))]
[CrdtSerializable(typeof(MoveNodeIntent))]
[CrdtSerializable(typeof(RemoveEdgeIntent))]
[CrdtSerializable(typeof(RemoveIntent))]
[CrdtSerializable(typeof(RemoveNodeIntent))]
[CrdtSerializable(typeof(RemoveValueIntent))]
[CrdtSerializable(typeof(RemoveVertexIntent))]
[CrdtSerializable(typeof(SetIndexIntent))]
[CrdtSerializable(typeof(SetIntent))]
[CrdtSerializable(typeof(VoteIntent))]
internal partial class InternalCrdtContext : CrdtContext
{
}