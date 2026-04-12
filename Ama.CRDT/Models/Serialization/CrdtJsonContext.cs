namespace Ama.CRDT.Models.Serialization;

using Ama.CRDT.Models;
using Ama.CRDT.Models.Decorators;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Models.Intents.Decorators;
using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Models.Serialization.Converters;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

/// <summary>
/// Provides pre-configured, AOT-compatible <see cref="JsonSerializerOptions"/> for serializing CRDT models.
/// For generic data models like <c>CrdtDocument&lt;MyClass&gt;</c>, ensure you create a custom <see cref="JsonSerializerContext"/> 
/// that derives from your custom types and register it in your application.
/// </summary>
[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]
[JsonSerializable(typeof(ICrdtMetadataState))]
[JsonSerializable(typeof(CrdtPatch))]
[JsonSerializable(typeof(CrdtMetadata))]
[JsonSerializable(typeof(CrdtDocument<object>))]
[JsonSerializable(typeof(ApplyPatchResult<object>))]
[JsonSerializable(typeof(JournaledOperation))]
[JsonSerializable(typeof(UnappliedOperation))]
[JsonSerializable(typeof(DottedVersionVector))]
[JsonSerializable(typeof(CompositePartitionKey))]
[JsonSerializable(typeof(PartitionContent))]
[JsonSerializable(typeof(HeaderPartition))]
[JsonSerializable(typeof(DataPartition))]
[JsonSerializable(typeof(EpochTimestamp))]
[JsonSerializable(typeof(AverageRegisterValue))]
[JsonSerializable(typeof(AverageRegisterState))]
[JsonSerializable(typeof(CausalTimestamp))]
[JsonSerializable(typeof(FwwTimestamp))]
[JsonSerializable(typeof(PnCounterState))]
[JsonSerializable(typeof(LwwSetState))]
[JsonSerializable(typeof(FwwSetState))]
[JsonSerializable(typeof(OrSetState))]
[JsonSerializable(typeof(TwoPhaseSetState))]
[JsonSerializable(typeof(TwoPhaseGraphState))]
[JsonSerializable(typeof(PositionalState))]
[JsonSerializable(typeof(LseqState))]
[JsonSerializable(typeof(RgaState))]
[JsonSerializable(typeof(LwwMapState))]
[JsonSerializable(typeof(FwwMapState))]
[JsonSerializable(typeof(CounterMapState))]
[JsonSerializable(typeof(EpochState))]
[JsonSerializable(typeof(QuorumState))]
[JsonSerializable(typeof(KeyValuePair<object, object>))]
[JsonSerializable(typeof(CrdtOperation))]
[JsonSerializable(typeof(Edge))]
[JsonSerializable(typeof(GraphEdgePayload))]
[JsonSerializable(typeof(GraphVertexPayload))]
[JsonSerializable(typeof(LseqIdentifier))]
[JsonSerializable(typeof(LseqItem))]
[JsonSerializable(typeof(LseqPathSegment))]
[JsonSerializable(typeof(OrMapAddItem))]
[JsonSerializable(typeof(OrMapRemoveItem))]
[JsonSerializable(typeof(OrSetAddItem))]
[JsonSerializable(typeof(OrSetRemoveItem))]
[JsonSerializable(typeof(PositionalIdentifier))]
[JsonSerializable(typeof(PositionalItem))]
[JsonSerializable(typeof(RgaIdentifier))]
[JsonSerializable(typeof(RgaItem))]
[JsonSerializable(typeof(TreeAddNodePayload))]
[JsonSerializable(typeof(TreeRemoveNodePayload))]
[JsonSerializable(typeof(TreeMoveNodePayload))]
[JsonSerializable(typeof(TreeNode))]
[JsonSerializable(typeof(CrdtTree))]
[JsonSerializable(typeof(CrdtGraph))]
[JsonSerializable(typeof(VotePayload))]
[JsonSerializable(typeof(EpochPayload))]
[JsonSerializable(typeof(QuorumPayload))]
[JsonSerializable(typeof(OriginSyncRequirement))]
[JsonSerializable(typeof(ReplicaSyncRequirement))]
[JsonSerializable(typeof(BidirectionalSyncRequirements))]
[JsonSerializable(typeof(JournalSyncResult))]
[JsonSerializable(typeof(SplitResult))]
[JsonSerializable(typeof(AddIntent))]
[JsonSerializable(typeof(AddEdgeIntent))]
[JsonSerializable(typeof(AddNodeIntent))]
[JsonSerializable(typeof(AddVertexIntent))]
[JsonSerializable(typeof(ClearIntent))]
[JsonSerializable(typeof(IncrementIntent))]
[JsonSerializable(typeof(InsertIntent))]
[JsonSerializable(typeof(MapIncrementIntent))]
[JsonSerializable(typeof(MapRemoveIntent))]
[JsonSerializable(typeof(MapSetIntent))]
[JsonSerializable(typeof(MoveNodeIntent))]
[JsonSerializable(typeof(RemoveEdgeIntent))]
[JsonSerializable(typeof(RemoveIntent))]
[JsonSerializable(typeof(RemoveNodeIntent))]
[JsonSerializable(typeof(RemoveValueIntent))]
[JsonSerializable(typeof(RemoveVertexIntent))]
[JsonSerializable(typeof(SetIndexIntent))]
[JsonSerializable(typeof(SetIntent))]
[JsonSerializable(typeof(VoteIntent))]
[JsonSerializable(typeof(EpochClearIntent))]
[SuppressMessage("ApiDesign", "RS0016:Add public types and members to the declared API", Justification = "System.Text.Json source generator emits public properties that do not need to be tracked in the public API.")]
[SuppressMessage("ApiDesign", "RS0041:Symbol uses some oblivious reference types", Justification = "System.Text.Json source generator does not emit nullable annotations for its properties.")]
public partial class CrdtJsonContext : JsonSerializerContext
{
    private static readonly Lazy<JsonSerializerOptions> _defaultOptions = new(CreateDefaultOptions);
    public static JsonSerializerOptions DefaultOptions => _defaultOptions.Value;

    private static readonly Lazy<JsonSerializerOptions> _metadataCompactOptions = new(CreateMetadataCompactOptions);
    public static JsonSerializerOptions MetadataCompactOptions => _metadataCompactOptions.Value;

    private static JsonSerializerOptions CreateDefaultOptions()
    {
        var options = new JsonSerializerOptions
        {
            TypeInfoResolver = Default.WithAddedModifier(CrdtJsonTypeInfoResolver.ApplyCrdtModifiers)
        };
        AddCrdtConverters(options);
        return options;
    }

    private static JsonSerializerOptions CreateMetadataCompactOptions()
    {
        var options = new JsonSerializerOptions
        {
            TypeInfoResolver = Default
                .WithAddedModifier(CrdtJsonTypeInfoResolver.ApplyCrdtModifiers)
                .WithAddedModifier(CrdtMetadataJsonResolver.ApplyMetadataModifiers)
        };
        AddCrdtConverters(options);
        return options;
    }

    private static void AddCrdtConverters(JsonSerializerOptions options)
    {
        // The Payload converter factory strictly handles polymorphism for 'object' and 'IComparable' globally.
        // This acts as a net to catch deep values inside collections like List<IComparable> and Dictionary<object, ...>
        options.Converters.Add(CrdtPayloadJsonConverterFactory.Instance);
        options.Converters.Add(new ObjectKeyDictionaryJsonConverter());
    }
}