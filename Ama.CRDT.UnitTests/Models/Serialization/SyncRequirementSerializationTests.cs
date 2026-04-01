namespace Ama.CRDT.UnitTests.Models.Serialization;

using System.Collections.Generic;
using System.Text.Json;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Serialization;
using Shouldly;
using Xunit;

public sealed class SyncRequirementSerializationTests
{
    [Fact]
    public void OriginSyncRequirement_ShouldSerializeAndDeserialize()
    {
        var req = new OriginSyncRequirement
        {
            TargetContiguousVersion = 5,
            SourceContiguousVersion = 10,
            TargetKnownDots = new HashSet<long> { 6, 8 },
            SourceMissingDots = new HashSet<long> { 12 }
        };

        var json = JsonSerializer.Serialize(req, CrdtJsonContext.DefaultOptions);
        var deserialized = JsonSerializer.Deserialize<OriginSyncRequirement>(json, CrdtJsonContext.DefaultOptions);

        deserialized.ShouldBe(req);
    }

    [Fact]
    public void ReplicaSyncRequirement_ShouldSerializeAndDeserialize()
    {
        var originReq = new OriginSyncRequirement
        {
            TargetContiguousVersion = 5,
            SourceContiguousVersion = 10,
            TargetKnownDots = new HashSet<long> { 6 },
            SourceMissingDots = new HashSet<long> { 11 }
        };

        var req = new ReplicaSyncRequirement
        {
            TargetReplicaId = "T1",
            SourceReplicaId = "S1",
            RequirementsByOrigin = new Dictionary<string, OriginSyncRequirement>
            {
                { "O1", originReq }
            }
        };

        var json = JsonSerializer.Serialize(req, CrdtJsonContext.DefaultOptions);
        var deserialized = JsonSerializer.Deserialize<ReplicaSyncRequirement>(json, CrdtJsonContext.DefaultOptions);

        deserialized.ShouldBe(req);
    }

    [Fact]
    public void BidirectionalSyncRequirements_ShouldSerializeAndDeserialize()
    {
        var reqA = new ReplicaSyncRequirement
        {
            TargetReplicaId = "A",
            SourceReplicaId = "B",
            RequirementsByOrigin = new Dictionary<string, OriginSyncRequirement>()
        };

        var reqB = new ReplicaSyncRequirement
        {
            TargetReplicaId = "B",
            SourceReplicaId = "A",
            RequirementsByOrigin = new Dictionary<string, OriginSyncRequirement>()
        };

        var bidi = new BidirectionalSyncRequirements
        {
            ReplicaANeedsFromB = reqA,
            ReplicaBNeedsFromA = reqB
        };

        var json = JsonSerializer.Serialize(bidi, CrdtJsonContext.DefaultOptions);
        var deserialized = JsonSerializer.Deserialize<BidirectionalSyncRequirements>(json, CrdtJsonContext.DefaultOptions);

        deserialized.ShouldBe(bidi);
    }
}