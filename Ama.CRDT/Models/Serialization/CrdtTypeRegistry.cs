namespace Ama.CRDT.Models.Serialization;

using System;
using System.Collections.Concurrent;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Decorators;
using Ama.CRDT.Models.Partitioning;

internal static class CrdtTypeRegistry
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
        Register("avg-reg", typeof(AverageRegisterValue));
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

        // Partitioning
        Register("comp-key", typeof(CompositePartitionKey));
        Register("header-partition", typeof(HeaderPartition));
        Register("data-partition", typeof(DataPartition));

        // Timestamps
        Register("epoch", typeof(EpochTimestamp));
    }

    public static void Register(string discriminator, Type type)
    {
        TypeMap[discriminator] = type;
        DiscriminatorMap[type] = discriminator;
    }

    public static bool TryGetType(string discriminator, out Type type) => TypeMap.TryGetValue(discriminator, out type!);
    public static string? GetDiscriminator(Type type) => DiscriminatorMap.TryGetValue(type, out var discriminator) ? discriminator : null;
}