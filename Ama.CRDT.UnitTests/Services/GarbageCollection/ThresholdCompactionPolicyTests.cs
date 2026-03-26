namespace Ama.CRDT.UnitTests.Services.GarbageCollection;

using System;
using Ama.CRDT.Models;
using Ama.CRDT.Services.GarbageCollection;
using Shouldly;
using Xunit;

public sealed class ThresholdCompactionPolicyTests
{
    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenTimestampThresholdIsNull()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new ThresholdCompactionPolicy((ICrdtTimestamp)null!));
        Should.Throw<ArgumentNullException>(() => new ThresholdCompactionPolicy(null!, 100));
    }

    [Theory]
    [InlineData(50, 100, true)]
    [InlineData(100, 100, true)]
    [InlineData(150, 100, false)]
    public void IsSafeToCompact_WithTimestampOnly_ShouldCompareCorrectly(long candidateTime, long thresholdTime, bool expectedResult)
    {
        // Arrange
        var policy = new ThresholdCompactionPolicy(new EpochTimestamp(thresholdTime));
        var candidate = new CompactionCandidate(Timestamp: new EpochTimestamp(candidateTime));

        // Act
        var result = policy.IsSafeToCompact(candidate);

        // Assert
        result.ShouldBe(expectedResult);
    }

    [Fact]
    public void IsSafeToCompact_WithTimestampOnly_ShouldReturnFalse_WhenCandidateHasNoTimestamp()
    {
        // Arrange
        var policy = new ThresholdCompactionPolicy(new EpochTimestamp(100));
        var candidate = new CompactionCandidate(Version: 50);

        // Act
        var result = policy.IsSafeToCompact(candidate);

        // Assert
        result.ShouldBeFalse();
    }

    [Theory]
    [InlineData(5, 10, true)]
    [InlineData(10, 10, true)]
    [InlineData(15, 10, false)]
    public void IsSafeToCompact_WithVersionOnly_ShouldCompareCorrectly(long candidateVer, long thresholdVer, bool expectedResult)
    {
        // Arrange
        var policy = new ThresholdCompactionPolicy(thresholdVer);
        var candidate = new CompactionCandidate(Version: candidateVer);

        // Act
        var result = policy.IsSafeToCompact(candidate);

        // Assert
        result.ShouldBe(expectedResult);
    }

    [Fact]
    public void IsSafeToCompact_WithVersionOnly_ShouldReturnFalse_WhenCandidateHasNoVersion()
    {
        // Arrange
        var policy = new ThresholdCompactionPolicy(10);
        var candidate = new CompactionCandidate(Timestamp: new EpochTimestamp(5));

        // Act
        var result = policy.IsSafeToCompact(candidate);

        // Assert
        result.ShouldBeFalse();
    }

    [Theory]
    [InlineData(50, 100, 15, 10, true)] // Timestamp safe, Version not safe -> true
    [InlineData(150, 100, 5, 10, true)] // Timestamp not safe, Version safe -> true
    [InlineData(50, 100, 5, 10, true)]  // Both safe -> true
    [InlineData(150, 100, 15, 10, false)] // Both not safe -> false
    public void IsSafeToCompact_WithBothThresholds_ShouldCompareCorrectly(
        long candidateTime, long thresholdTime,
        long candidateVer, long thresholdVer,
        bool expectedResult)
    {
        // Arrange
        var policy = new ThresholdCompactionPolicy(new EpochTimestamp(thresholdTime), thresholdVer);
        var candidate = new CompactionCandidate(Timestamp: new EpochTimestamp(candidateTime), Version: candidateVer);

        // Act
        var result = policy.IsSafeToCompact(candidate);

        // Assert
        result.ShouldBe(expectedResult);
    }
}