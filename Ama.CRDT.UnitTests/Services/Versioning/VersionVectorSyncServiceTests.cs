using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Versioning;
using Shouldly;

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
    public void CalculateRequirement_InvalidReplicaId_ThrowsArgumentException(string invalidId)
    {
        var validContext = new ReplicaContext { ReplicaId = "Valid" };
        var invalidContext = new ReplicaContext { ReplicaId = invalidId };

        Should.Throw<ArgumentException>(() => _sut.CalculateRequirement(invalidContext, validContext));
        Should.Throw<ArgumentException>(() => _sut.CalculateRequirement(validContext, invalidContext));
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
    public void CalculateRequirement_NullDVVs_HandledGracefully()
    {
        var target = new ReplicaContext { ReplicaId = "Target", GlobalVersionVector = null! };
        var source = new ReplicaContext { ReplicaId = "Source", GlobalVersionVector = null! };

        var result = _sut.CalculateRequirement(target, source);

        result.IsBehind.ShouldBeFalse();
        result.RequirementsByOrigin.ShouldBeEmpty();
    }

    [Fact]
    public void CalculateGlobalMinimumVersionVector_NullClusterVectors_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => _sut.CalculateGlobalMinimumVersionVector(null!));
    }

    [Fact]
    public void CalculateGlobalMinimumVersionVector_EmptyClusterVectors_ReturnsEmptyDictionary()
    {
        var result = _sut.CalculateGlobalMinimumVersionVector(Enumerable.Empty<DottedVersionVector>());
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

    private static DottedVersionVector CreateDvv(IDictionary<string, long> versions, IDictionary<string, ISet<long>>? dots)
    {
        return new DottedVersionVector(versions, dots ?? new Dictionary<string, ISet<long>>());
    }
}