using System.Text.Json;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Serialization;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Versioning;
using Shouldly;
using Xunit;

namespace Ama.CRDT.UnitTests.Services.Versioning;

public class VersionVectorSyncServiceTests
{
    private readonly VersionVectorSyncService _sut;

    public VersionVectorSyncServiceTests()
    {
        _sut = new VersionVectorSyncService();
    }

    [Fact]
    public void CalculateRequirement_NullArguments_ThrowsArgumentNullException()
    {
        var context = new ReplicaContext { ReplicaId = "A" };

        Should.Throw<ArgumentNullException>(() => _sut.CalculateRequirement(null!, context));
        Should.Throw<ArgumentNullException>(() => _sut.CalculateRequirement(context, null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void CalculateRequirement_StringArgs_InvalidReplicaId_ThrowsArgumentException(string? invalidId)
    {
        var dvv = new DottedVersionVector();

        Should.Throw<ArgumentException>(() => _sut.CalculateRequirement(invalidId, dvv, "Valid", dvv));
        Should.Throw<ArgumentException>(() => _sut.CalculateRequirement("Valid", dvv, invalidId, dvv));
    }

    [Fact]
    public void CalculateRequirement_StringArgs_NullVector_ThrowsArgumentNullException()
    {
        var dvv = new DottedVersionVector();

        Should.Throw<ArgumentNullException>(() => _sut.CalculateRequirement("Target", null!, "Source", dvv));
        Should.Throw<ArgumentNullException>(() => _sut.CalculateRequirement("Target", dvv, "Source", null!));
    }

    [Fact]
    public void CalculateRequirement_EmptyDVVs_ReturnsNoRequirements()
    {
        var target = new ReplicaContext { ReplicaId = "Target" };
        var source = new ReplicaContext { ReplicaId = "Source" };

        var result = _sut.CalculateRequirement(target, source);

        result.TargetReplicaId.ShouldBe("Target");
        result.SourceReplicaId.ShouldBe("Source");
        result.IsBehind.ShouldBeFalse();
        result.RequirementsByOrigin.ShouldBeEmpty();
    }

    [Fact]
    public void CalculateRequirement_TargetAhead_ReturnsNoRequirements()
    {
        var target = new ReplicaContext { ReplicaId = "Target", GlobalVersionVector = CreateDvv(new Dictionary<string, long> { { "Origin1", 5 } }, null) };
        var source = new ReplicaContext { ReplicaId = "Source", GlobalVersionVector = CreateDvv(new Dictionary<string, long> { { "Origin1", 2 } }, null) };

        var result = _sut.CalculateRequirement(target, source);

        result.IsBehind.ShouldBeFalse();
        result.RequirementsByOrigin.ShouldBeEmpty();
    }

    [Fact]
    public void CalculateRequirement_SourceAheadContiguous_ReturnsContiguousRequirement()
    {
        var target = new ReplicaContext { ReplicaId = "Target", GlobalVersionVector = CreateDvv(new Dictionary<string, long> { { "Origin1", 2 } }, null) };
        var source = new ReplicaContext { ReplicaId = "Source", GlobalVersionVector = CreateDvv(new Dictionary<string, long> { { "Origin1", 5 } }, null) };

        var result = _sut.CalculateRequirement(target, source);

        result.IsBehind.ShouldBeTrue();
        result.RequirementsByOrigin.Count.ShouldBe(1);
        
        var req = result.RequirementsByOrigin["Origin1"];
        req.TargetContiguousVersion.ShouldBe(2);
        req.SourceContiguousVersion.ShouldBe(5);
        req.TargetKnownDots.ShouldBeEmpty();
        req.SourceMissingDots.ShouldBeEmpty();
        req.HasMissingData.ShouldBeTrue();
    }

    [Fact]
    public void CalculateRequirement_SourceHasDotsTargetMissing_ReturnsMissingDots()
    {
        var target = new ReplicaContext { ReplicaId = "Target", GlobalVersionVector = CreateDvv(new Dictionary<string, long> { { "Origin1", 2 } }, null) };
        var source = new ReplicaContext 
        { 
            ReplicaId = "Source", 
            GlobalVersionVector = CreateDvv(
                new Dictionary<string, long> { { "Origin1", 2 } }, 
                new Dictionary<string, ISet<long>> { { "Origin1", new HashSet<long> { 5, 7 } } }) 
        };

        var result = _sut.CalculateRequirement(target, source);

        result.IsBehind.ShouldBeTrue();
        
        var req = result.RequirementsByOrigin["Origin1"];
        req.TargetContiguousVersion.ShouldBe(2);
        req.SourceContiguousVersion.ShouldBe(2);
        req.TargetKnownDots.ShouldBeEmpty();
        req.SourceMissingDots.ShouldBe(new[] { 5L, 7L });
        req.HasMissingData.ShouldBeTrue();
    }

    [Fact]
    public void CalculateRequirement_TargetHasDotsInMissingContiguousRange_ReturnsKnownDots()
    {
        // Target has 1..2 and dot 4
        var target = new ReplicaContext 
        { 
            ReplicaId = "Target", 
            GlobalVersionVector = CreateDvv(
                new Dictionary<string, long> { { "Origin1", 2 } }, 
                new Dictionary<string, ISet<long>> { { "Origin1", new HashSet<long> { 4 } } }) 
        };
        // Source has 1..5 contiguous
        var source = new ReplicaContext 
        { 
            ReplicaId = "Source", 
            GlobalVersionVector = CreateDvv(new Dictionary<string, long> { { "Origin1", 5 } }, null) 
        };

        var result = _sut.CalculateRequirement(target, source);

        result.IsBehind.ShouldBeTrue();
        
        var req = result.RequirementsByOrigin["Origin1"];
        req.TargetContiguousVersion.ShouldBe(2);
        req.SourceContiguousVersion.ShouldBe(5);
        
        // The target knows about 4, so source shouldn't send it.
        req.TargetKnownDots.ShouldBe(new[] { 4L });
        req.SourceMissingDots.ShouldBeEmpty();
    }

    [Fact]
    public void CalculateRequirement_MultipleOrigins_CalculatesCorrectly()
    {
        var target = new ReplicaContext 
        { 
            ReplicaId = "Target", 
            GlobalVersionVector = CreateDvv(
                new Dictionary<string, long> { { "OriginA", 5 }, { "OriginB", 2 } }, 
                null) 
        };
        
        var source = new ReplicaContext 
        { 
            ReplicaId = "Source", 
            GlobalVersionVector = CreateDvv(
                new Dictionary<string, long> { { "OriginA", 3 }, { "OriginB", 5 }, { "OriginC", 1 } }, 
                null) 
        };

        var result = _sut.CalculateRequirement(target, source);

        result.IsBehind.ShouldBeTrue();
        result.RequirementsByOrigin.Count.ShouldBe(2); // Only for B and C, since Target is ahead on A
        
        result.RequirementsByOrigin["OriginB"].TargetContiguousVersion.ShouldBe(2);
        result.RequirementsByOrigin["OriginB"].SourceContiguousVersion.ShouldBe(5);
        
        result.RequirementsByOrigin["OriginC"].TargetContiguousVersion.ShouldBe(0);
        result.RequirementsByOrigin["OriginC"].SourceContiguousVersion.ShouldBe(1);
    }

    [Fact]
    public void CalculateBidirectionalRequirements_NullArguments_ThrowsArgumentNullException()
    {
        var context = new ReplicaContext { ReplicaId = "A" };

        Should.Throw<ArgumentNullException>(() => _sut.CalculateBidirectionalRequirements(null!, context));
        Should.Throw<ArgumentNullException>(() => _sut.CalculateBidirectionalRequirements(context, null!));
    }

    [Fact]
    public void CalculateBidirectionalRequirements_CalculatesBothWays()
    {
        var replicaA = new ReplicaContext 
        { 
            ReplicaId = "A", 
            GlobalVersionVector = CreateDvv(new Dictionary<string, long> { { "OriginX", 5 }, { "OriginY", 2 } }, null) 
        };
        
        var replicaB = new ReplicaContext 
        { 
            ReplicaId = "B", 
            GlobalVersionVector = CreateDvv(new Dictionary<string, long> { { "OriginX", 3 }, { "OriginY", 5 } }, null) 
        };

        var result = _sut.CalculateBidirectionalRequirements(replicaA, replicaB);

        // A needs from B
        result.ReplicaANeedsFromB.IsBehind.ShouldBeTrue();
        result.ReplicaANeedsFromB.RequirementsByOrigin.Count.ShouldBe(1);
        result.ReplicaANeedsFromB.RequirementsByOrigin["OriginY"].TargetContiguousVersion.ShouldBe(2);
        result.ReplicaANeedsFromB.RequirementsByOrigin["OriginY"].SourceContiguousVersion.ShouldBe(5);

        // B needs from A
        result.ReplicaBNeedsFromA.IsBehind.ShouldBeTrue();
        result.ReplicaBNeedsFromA.RequirementsByOrigin.Count.ShouldBe(1);
        result.ReplicaBNeedsFromA.RequirementsByOrigin["OriginX"].TargetContiguousVersion.ShouldBe(3);
        result.ReplicaBNeedsFromA.RequirementsByOrigin["OriginX"].SourceContiguousVersion.ShouldBe(5);
    }

    [Fact]
    public void CalculateGlobalMinimumVersionVector_NullClusterVectors_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => _sut.CalculateGlobalMinimumVersionVector(null!));
    }

    [Fact]
    public void CalculateGlobalMinimumVersionVector_EmptyClusterVectors_ReturnsEmptyDictionary()
    {
        var result = _sut.CalculateGlobalMinimumVersionVector([]);
        result.ShouldBeEmpty();
    }

    [Fact]
    public void CalculateGlobalMinimumVersionVector_CalculatesCorrectly()
    {
        var vectors = new[]
        {
            CreateDvv(new Dictionary<string, long> { { "A", 5 }, { "B", 3 }, { "C", 2 } }, null),
            CreateDvv(new Dictionary<string, long> { { "A", 4 }, { "B", 4 }, { "C", 2 } }, null),
            CreateDvv(new Dictionary<string, long> { { "A", 6 }, { "B", 3 } }, null) // Missing C
        };

        var result = _sut.CalculateGlobalMinimumVersionVector(vectors);

        result.Count.ShouldBe(2); // Only A and B should be present, C is 0 because third replica misses it
        result["A"].ShouldBe(4);
        result["B"].ShouldBe(3);
        result.ContainsKey("C").ShouldBeFalse();
    }

    [Fact]
    public void CalculateGlobalMaximumVersionVector_NullClusterVectors_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => _sut.CalculateGlobalMaximumVersionVector(null!));
    }

    [Fact]
    public void CalculateGlobalMaximumVersionVector_EmptyClusterVectors_ReturnsEmpty()
    {
        var result = _sut.CalculateGlobalMaximumVersionVector([]);
        result.Versions.ShouldBeEmpty();
        result.Dots.ShouldBeEmpty();
    }

    [Fact]
    public void CalculateGlobalMaximumVersionVector_CalculatesCorrectly()
    {
        var vectors = new[]
        {
            CreateDvv(new Dictionary<string, long> { { "A", 5 }, { "B", 3 } }, new Dictionary<string, ISet<long>> { { "A", new HashSet<long> { 7 } } }),
            CreateDvv(new Dictionary<string, long> { { "A", 4 }, { "B", 4 } }, new Dictionary<string, ISet<long>> { { "B", new HashSet<long> { 6 } } }),
            CreateDvv(new Dictionary<string, long> { { "C", 2 } }, null)
        };

        var result = _sut.CalculateGlobalMaximumVersionVector(vectors);

        result.Versions.Count.ShouldBe(3);
        result.Versions["A"].ShouldBe(5);
        result.Versions["B"].ShouldBe(4);
        result.Versions["C"].ShouldBe(2);

        result.Dots.Count.ShouldBe(2);
        result.Dots["A"].ShouldBe([7L]);
        result.Dots["B"].ShouldBe([6L]);
    }

    [Fact]
    public void CalculateGlobalMaximumVersionVector_PrunesCoveredDots()
    {
        var vectors = new[]
        {
            CreateDvv(new Dictionary<string, long> { { "A", 3 } }, new Dictionary<string, ISet<long>> { { "A", new HashSet<long> { 5, 6 } } }),
            CreateDvv(new Dictionary<string, long> { { "A", 5 } }, null)
        };

        var result = _sut.CalculateGlobalMaximumVersionVector(vectors);

        result.Versions["A"].ShouldBe(5);
        result.Dots.Count.ShouldBe(1);
        result.Dots["A"].ShouldBe([6L]); // 5 was pruned because Max version is 5
    }

    [Fact]
    public void RemoveEvictedReplicas_NullVector_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => _sut.RemoveEvictedReplicas(null!, []));
    }

    [Fact]
    public void RemoveEvictedReplicas_NullEvictedReplicaIds_ThrowsArgumentNullException()
    {
        var dvv = new DottedVersionVector();
        Should.Throw<ArgumentNullException>(() => _sut.RemoveEvictedReplicas(dvv, null!));
    }

    [Fact]
    public void RemoveEvictedReplicas_EmptyEvictedList_ReturnsEquivalentVector()
    {
        var dvv = CreateDvv(
            new Dictionary<string, long> { { "A", 1 }, { "B", 2 } },
            new Dictionary<string, ISet<long>> { { "A", new HashSet<long> { 3 } } }
        );

        var result = _sut.RemoveEvictedReplicas(dvv, []);

        result.ShouldNotBeSameAs(dvv);
        result.Versions.ShouldBe(dvv.Versions);
        
        result.Dots.Keys.ShouldBe(dvv.Dots.Keys);
        foreach (var key in dvv.Dots.Keys)
        {
            result.Dots[key].ShouldBe(dvv.Dots[key]);
        }
    }

    [Fact]
    public void RemoveEvictedReplicas_ValidEvictedIds_RemovesCorrectly()
    {
        var dvv = CreateDvv(
            new Dictionary<string, long> { { "A", 1 }, { "B", 2 }, { "C", 3 } },
            new Dictionary<string, ISet<long>> { 
                { "A", new HashSet<long> { 5 } }, 
                { "B", new HashSet<long> { 6 } },
                { "C", new HashSet<long> { 7 } }
            }
        );

        var result = _sut.RemoveEvictedReplicas(dvv, ["B", "C", "D"]); // Includes an ID that's not in the DVV

        result.Versions.Count.ShouldBe(1);
        result.Versions.ContainsKey("A").ShouldBeTrue();
        result.Versions["A"].ShouldBe(1);

        result.Dots.Count.ShouldBe(1);
        result.Dots.ContainsKey("A").ShouldBeTrue();
        result.Dots["A"].ShouldBe([5L]);
    }

    [Fact]
    public async Task EvaluateJournalCompletionAsync_NullArguments_ThrowsArgumentNullException()
    {
        var requirement = new ReplicaSyncRequirement();
        var emptyStream = AsAsyncEnumerable(Array.Empty<JournaledOperation>());

        await Should.ThrowAsync<ArgumentNullException>(() => _sut.EvaluateJournalCompletionAsync(null!, requirement));
    }

    [Fact]
    public async Task EvaluateJournalCompletionAsync_NotBehind_ReturnsEmptyNotTruncated()
    {
        var requirement = new ReplicaSyncRequirement
        {
            RequirementsByOrigin = new Dictionary<string, OriginSyncRequirement>() // Empty means IsBehind is false
        };
        var emptyStream = AsAsyncEnumerable(Array.Empty<JournaledOperation>());

        var result = await _sut.EvaluateJournalCompletionAsync(emptyStream, requirement);

        result.SnapshotRequired.ShouldBeFalse();
        result.Operations.ShouldBeEmpty();
    }

    [Fact]
    public async Task EvaluateJournalCompletionAsync_AllMissingContiguousOpsAvailable_ReturnsOperations_NotTruncated()
    {
        var requirement = new ReplicaSyncRequirement
        {
            RequirementsByOrigin = new Dictionary<string, OriginSyncRequirement>
            {
                { "A", new OriginSyncRequirement { TargetContiguousVersion = 1, SourceContiguousVersion = 3 } }
            }
        };

        var availableOps = AsAsyncEnumerable(new[]
        {
            CreateJOp("A", 2),
            CreateJOp("A", 3)
        });

        var result = await _sut.EvaluateJournalCompletionAsync(availableOps, requirement);

        result.SnapshotRequired.ShouldBeFalse();
        result.Operations.Count.ShouldBe(2);
    }

    [Fact]
    public async Task EvaluateJournalCompletionAsync_MissingContiguousOp_ReturnsTruncated()
    {
        var requirement = new ReplicaSyncRequirement
        {
            RequirementsByOrigin = new Dictionary<string, OriginSyncRequirement>
            {
                { "A", new OriginSyncRequirement { TargetContiguousVersion = 1, SourceContiguousVersion = 3 } }
            }
        };

        // Stream only has 3, but is missing 2. This implies truncation.
        var availableOps = AsAsyncEnumerable(new[]
        {
            CreateJOp("A", 3)
        });

        var result = await _sut.EvaluateJournalCompletionAsync(availableOps, requirement);

        result.SnapshotRequired.ShouldBeTrue();
        result.Operations.ShouldBeEmpty(); // When truncated, operations are empty to trigger full sync
    }

    [Fact]
    public async Task EvaluateJournalCompletionAsync_AllMissingDotsAvailable_ReturnsOperations_NotTruncated()
    {
        var requirement = new ReplicaSyncRequirement
        {
            RequirementsByOrigin = new Dictionary<string, OriginSyncRequirement>
            {
                { "A", new OriginSyncRequirement { TargetContiguousVersion = 2, SourceContiguousVersion = 2, SourceMissingDots = new HashSet<long> { 5 } } }
            }
        };

        var availableOps = AsAsyncEnumerable(new[]
        {
            CreateJOp("A", 5)
        });

        var result = await await Task.FromResult(_sut.EvaluateJournalCompletionAsync(availableOps, requirement));

        result.SnapshotRequired.ShouldBeFalse();
        result.Operations.Count.ShouldBe(1);
    }

    [Fact]
    public async Task EvaluateJournalCompletionAsync_MissingDot_ReturnsTruncated()
    {
        var requirement = new ReplicaSyncRequirement
        {
            RequirementsByOrigin = new Dictionary<string, OriginSyncRequirement>
            {
                { "A", new OriginSyncRequirement { TargetContiguousVersion = 2, SourceContiguousVersion = 2, SourceMissingDots = new HashSet<long> { 5, 7 } } }
            }
        };

        // Stream has dot 5, but missing 7.
        var availableOps = AsAsyncEnumerable(new[]
        {
            CreateJOp("A", 5)
        });

        var result = await _sut.EvaluateJournalCompletionAsync(availableOps, requirement);

        result.SnapshotRequired.ShouldBeTrue();
        result.Operations.ShouldBeEmpty();
    }

    [Fact]
    public async Task EvaluateJournalCompletionAsync_WithTargetKnownDots_SkipsKnown_NotTruncated()
    {
        var requirement = new ReplicaSyncRequirement
        {
            RequirementsByOrigin = new Dictionary<string, OriginSyncRequirement>
            {
                { "A", new OriginSyncRequirement { TargetContiguousVersion = 1, SourceContiguousVersion = 4, TargetKnownDots = new HashSet<long> { 2, 3 } } }
            }
        };

        // Target needs 4. Stream has 4.
        var availableOps = AsAsyncEnumerable(new[]
        {
            CreateJOp("A", 4)
        });

        var result = await _sut.EvaluateJournalCompletionAsync(availableOps, requirement);

        result.SnapshotRequired.ShouldBeFalse();
        result.Operations.Count.ShouldBe(1);
    }

    [Fact]
    public async Task EvaluateJournalCompletionAsync_WithTargetKnownDots_MissingSubsequent_ReturnsTruncated()
    {
        var requirement = new ReplicaSyncRequirement
        {
            RequirementsByOrigin = new Dictionary<string, OriginSyncRequirement>
            {
                { "A", new OriginSyncRequirement { TargetContiguousVersion = 1, SourceContiguousVersion = 4, TargetKnownDots = new HashSet<long> { 2 } } }
            }
        };

        // Target needs 3 and 4. Stream only has 4.
        var availableOps = AsAsyncEnumerable(new[]
        {
            CreateJOp("A", 4)
        });

        var result = await _sut.EvaluateJournalCompletionAsync(availableOps, requirement);

        result.SnapshotRequired.ShouldBeTrue();
        result.Operations.ShouldBeEmpty();
    }

    [Fact]
    public async Task EvaluateJournalCompletionAsync_ComplexMultiOrigin_TruncatesFast()
    {
        var requirement = new ReplicaSyncRequirement
        {
            RequirementsByOrigin = new Dictionary<string, OriginSyncRequirement>
            {
                { "A", new OriginSyncRequirement { TargetContiguousVersion = 1, SourceContiguousVersion = 2 } },
                { "B", new OriginSyncRequirement { TargetContiguousVersion = 5, SourceContiguousVersion = 5, SourceMissingDots = new HashSet<long> { 8 } } }
            }
        };

        // Has A:2, but missing B:8
        var availableOps = AsAsyncEnumerable(new[]
        {
            CreateJOp("A", 2)
        });

        var result = await _sut.EvaluateJournalCompletionAsync(availableOps, requirement);

        result.SnapshotRequired.ShouldBeTrue();
        result.Operations.ShouldBeEmpty();
    }

    private static DottedVersionVector CreateDvv(IDictionary<string, long> versions, IDictionary<string, ISet<long>>? dots)
    {
        return new DottedVersionVector(versions, dots ?? new Dictionary<string, ISet<long>>());
    }

    /// <summary>
    /// Forges a structurally valid JournaledOperation bypassing ambiguous Native AOT constructors safely through JSON.
    /// </summary>
    private static JournaledOperation CreateJOp(string origin, long clock)
    {
        var json = $$"""{"ReplicaId":"{{origin}}","GlobalClock":{{clock}},"OperationType":1,"PropertyPath":""}""";
        var op = JsonSerializer.Deserialize(json, CrdtJsonContext.Default.CrdtOperation)!;
        return new JournaledOperation("doc1", op);
    }

    private static async IAsyncEnumerable<JournaledOperation> AsAsyncEnumerable(IEnumerable<JournaledOperation> items)
    {
        foreach (var item in items)
        {
            await Task.Yield(); // Force asynchronous execution naturally
            yield return item;
        }
    }
}