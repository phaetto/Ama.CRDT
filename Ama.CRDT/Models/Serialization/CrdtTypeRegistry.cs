namespace Ama.CRDT.Models.Serialization;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Decorators;
using Ama.CRDT.Models.Partitioning;

/// <summary>
/// A centralized registry mapping discriminator strings to CRDT serialization types.
/// This registry feeds the native System.Text.Json polymorphism engine and custom payload wrappers.
/// </summary>
public static class CrdtTypeRegistry
{
    private static readonly ConcurrentDictionary<string, Type> TypeMap = new();
    private static readonly ConcurrentDictionary<Type, string> DiscriminatorMap = new();

    static CrdtTypeRegistry()
    {
        // Primitives
        Register("bool", typeof(bool));
        Register("byte", typeof(byte));
        Register("sbyte", typeof(sbyte));
        Register("char", typeof(char));
        Register("decimal", typeof(decimal));
        Register("double", typeof(double));
        Register("float", typeof(float));
        Register("int", typeof(int));
        Register("uint", typeof(uint));
        Register("long", typeof(long));
        Register("ulong", typeof(ulong));
        Register("short", typeof(short));
        Register("ushort", typeof(ushort));
        Register("string", typeof(string));
        Register("guid", typeof(Guid));
        Register("datetime", typeof(DateTime));
        Register("datetimeoffset", typeof(DateTimeOffset));

        // Core CRDT Models
        Register("kvp", typeof(KeyValuePair<object, object>));
        Register("patch", typeof(CrdtPatch));
        Register("op", typeof(CrdtOperation));
        Register("edge", typeof(Edge));
        Register("graph-edge-payload", typeof(GraphEdgePayload));
        Register("graph-vertex-payload", typeof(GraphVertexPayload));
        Register("lseq-id", typeof(LseqIdentifier));
        Register("lseq-item", typeof(LseqItem));
        Register("lseq-segment", typeof(LseqPathSegment));
        Register("ormap-add", typeof(OrMapAddItem));
        Register("ormap-remove", typeof(OrMapRemoveItem));
        Register("orset-add", typeof(OrSetAddItem));
        Register("orset-remove", typeof(OrSetRemoveItem));
        Register("pos-id", typeof(PositionalIdentifier));
        Register("pos-item", typeof(PositionalItem));
        Register("rga-id", typeof(RgaIdentifier));
        Register("rga-item", typeof(RgaItem));
        Register("tree-add", typeof(TreeAddNodePayload));
        Register("tree-remove", typeof(TreeRemoveNodePayload));
        Register("tree-move", typeof(TreeMoveNodePayload));
        Register("vote-payload", typeof(VotePayload));
        Register("epoch-payload", typeof(EpochPayload));
        Register("quorum-payload", typeof(QuorumPayload));
        Register("tree-node", typeof(TreeNode));

        // Partitioning
        Register("comp-key", typeof(CompositePartitionKey));
        Register("header-partition", typeof(HeaderPartition));
        Register("data-partition", typeof(DataPartition));

        // Timestamps
        Register("epoch", typeof(EpochTimestamp));

        // States
        Register("causal-ts", typeof(CausalTimestamp));
        Register("fww-ts", typeof(FwwTimestamp));
        Register("pn-counter-state", typeof(PnCounterState));
        Register("lww-set-state", typeof(LwwSetState));
        Register("fww-set-state", typeof(FwwSetState));
        Register("or-set-state", typeof(OrSetState));
        Register("2p-set-state", typeof(TwoPhaseSetState));
        Register("2p-graph-state", typeof(TwoPhaseGraphState));
        Register("avg-reg", typeof(AverageRegisterValue));
        Register("avg-reg-state", typeof(AverageRegisterState));
        Register("pos-state", typeof(PositionalState));
        Register("lseq-state", typeof(LseqState));
        Register("rga-state", typeof(RgaState));
        Register("lww-map-state", typeof(LwwMapState));
        Register("fww-map-state", typeof(FwwMapState));
        Register("counter-map-state", typeof(CounterMapState));
        Register("epoch-state", typeof(EpochState));
        Register("quorum-state", typeof(QuorumState));
    }

    /// <summary>
    /// Registers a type with a unique discriminator for polymorphic serialization.
    /// Exposing this publicly allows plugin packages (like Streams) to inject their own specific model types.
    /// </summary>
    public static void Register(string discriminator, Type type)
    {
        if (string.IsNullOrWhiteSpace(discriminator))
        {
            throw new ArgumentException("Discriminator cannot be null or whitespace.", nameof(discriminator));
        }

        TypeMap[discriminator] = type;
        DiscriminatorMap[type] = discriminator;
    }

    internal static bool TryGetType(string discriminator, out Type type) => TypeMap.TryGetValue(discriminator, out type!);
    internal static string? GetDiscriminator(Type type) => DiscriminatorMap.TryGetValue(type, out var discriminator) ? discriminator : null;
    internal static IEnumerable<KeyValuePair<string, Type>> GetAll() => TypeMap;
}