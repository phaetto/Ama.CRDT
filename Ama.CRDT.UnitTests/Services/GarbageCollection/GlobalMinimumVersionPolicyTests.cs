namespace Ama.CRDT.UnitTests.Services.GarbageCollection;

using System;
using System.Collections.Generic;
using Ama.CRDT.Services.GarbageCollection;
using Shouldly;
using Xunit;

public sealed class GlobalMinimumVersionPolicyTests
{
    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenGlobalMinimumVersionsIsNull()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new GlobalMinimumVersionPolicy(null!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void IsSafeToCompact_ShouldReturnFalse_WhenReplicaIdIsNullOrWhitespace(string? replicaId)
    {
        // Arrange
        var policy = new GlobalMinimumVersionPolicy(new Dictionary<string, long> { { "replica1", 10 } });
        var candidate = new CompactionCandidate(ReplicaId: replicaId, Version: 5);

        // Act
        var result = policy.IsSafeToCompact(candidate);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsSafeToCompact_ShouldReturnFalse_WhenVersionIsNull()
    {
        // Arrange
        var policy = new GlobalMinimumVersionPolicy(new Dictionary<string, long> { { "replica1", 10 } });
        var candidate = new CompactionCandidate(ReplicaId: "replica1", Version: null);

        // Act
        var result = policy.IsSafeToCompact(candidate);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsSafeToCompact_ShouldReturnFalse_WhenReplicaIdNotInDictionary()
    {
        // Arrange
        var policy = new GlobalMinimumVersionPolicy(new Dictionary<string, long> { { "replica1", 10 } });
        var candidate = new CompactionCandidate(ReplicaId: "replica2", Version: 5);

        // Act
        var result = policy.IsSafeToCompact(candidate);

        // Assert
        result.ShouldBeFalse();
    }

    [Theory]
    [InlineData(5, 10, true)]
    [InlineData(10, 10, true)]
    [InlineData(11, 10, false)]
    public void IsSafeToCompact_ShouldCompareVersionCorrectly(long candidateVersion, long minVersion, bool expectedResult)
    {
        // Arrange
        var policy = new GlobalMinimumVersionPolicy(new Dictionary<string, long> { { "replica1", minVersion } });
        var candidate = new CompactionCandidate(ReplicaId: "replica1", Version: candidateVersion);

        // Act
        var result = policy.IsSafeToCompact(candidate);

        // Assert
        result.ShouldBe(expectedResult);
    }
}