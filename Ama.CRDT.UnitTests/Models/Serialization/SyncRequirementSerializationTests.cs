namespace Ama.CRDT.UnitTests.Models.Serialization;

using System.Collections.Generic;
using System.Text.Json;
using Ama.CRDT.Models;
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

        var options = TestOptionsHelper.GetDefaultOptions();
        var json = JsonSerializer.Serialize(req, options);
        var deserialized = JsonSerializer.Deserialize<OriginSyncRequirement>(json, options);

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

        var options = TestOptionsHelper.GetDefaultOptions();
        var json = JsonSerializer.Serialize(req, options);
        var deserialized = JsonSerializer.Deserialize<ReplicaSyncRequirement>(json, options);

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

        var options = TestOptionsHelper.GetDefaultOptions();
        var json = JsonSerializer.Serialize(bidi, options);
        var deserialized = JsonSerializer.Deserialize<BidirectionalSyncRequirements>(json, options);

        deserialized.ShouldBe(bidi);
    }
}