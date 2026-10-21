namespace Ama.CRDT.UnitTests.Models;

using Ama.CRDT.Models;
using Shouldly;
using System;
using Xunit;

public sealed class CrdtTimestampTests
{
    [Theory]
    [InlineData(100, 100, 0)]
    [InlineData(101, 100, 1)]
    [InlineData(99, 100, -1)]
    public void CompareTo_Ticks_ShouldReturnCorrectValue(long a, long b, int expected)
    {
        var tsA = new CrdtTimestamp(a);
        var tsB = new CrdtTimestamp(b);

        Math.Sign(tsA.CompareTo(tsB)).ShouldBe(expected);
    }

    [Theory]
    [InlineData(1, 1, 0)]
    [InlineData(2, 1, 1)]
    [InlineData(1, 2, -1)]
    public void CompareTo_Epoch_ShouldTakePrecedenceOverTicks(int epochA, int epochB, int expected)
    {
        // Even if Ticks in A is smaller than B, higher Epoch should win
        var tsA = new CrdtTimestamp(Ticks: 100, Counter: 10, Epoch: epochA, ReplicaId: "A");
        var tsB = new CrdtTimestamp(Ticks: epochA == epochB ? 100 : 200, Counter: 10, Epoch: epochB, ReplicaId: "A");

        Math.Sign(tsA.CompareTo(tsB)).ShouldBe(expected);
    }

    [Theory]
    [InlineData(10, 10, 0)]
    [InlineData(11, 10, 1)]
    [InlineData(9, 10, -1)]
    public void CompareTo_Counter_ShouldBreakTiesWhenEpochAndTicksEqual(int counterA, int counterB, int expected)
    {
        var tsA = new CrdtTimestamp(Ticks: 100, Counter: counterA, Epoch: 1, ReplicaId: "A");
        var tsB = new CrdtTimestamp(Ticks: 100, Counter: counterB, Epoch: 1, ReplicaId: "A");

        Math.Sign(tsA.CompareTo(tsB)).ShouldBe(expected);
    }

    [Theory]
    [InlineData("replica-1", "replica-1", 0)]
    [InlineData("replica-2", "replica-1", 1)]
    [InlineData("replica-1", "replica-2", -1)]
    [InlineData(null, "", 0)]
    [InlineData("", null, 0)]
    [InlineData(null, null, 0)]
    [InlineData("a", null, 1)]
    [InlineData(null, "a", -1)]
    [InlineData("b", "B", 1)] // Ordinal comparison ('b' > 'B')
    public void CompareTo_ReplicaId_ShouldBreakTiesWhenEpochTicksAndCounterEqual(string? replicaA, string? replicaB, int expected)
    {
        var tsA = new CrdtTimestamp(Ticks: 100, Counter: 5, Epoch: 1, ReplicaId: replicaA);
        var tsB = new CrdtTimestamp(Ticks: 100, Counter: 5, Epoch: 1, ReplicaId: replicaB);

        Math.Sign(tsA.CompareTo(tsB)).ShouldBe(expected);
    }

    [Fact]
    public void CompareTo_HierarchyPrecedence_ShouldFollowEpochThenTicksThenCounterThenReplicaId()
    {
        var baseTs = new CrdtTimestamp(Ticks: 100, Counter: 5, Epoch: 2, ReplicaId: "replica-b");

        // Higher Epoch wins despite lower Ticks, Counter, ReplicaId
        var higherEpoch = new CrdtTimestamp(Ticks: 50, Counter: 1, Epoch: 3, ReplicaId: "replica-a");
        higherEpoch.CompareTo(baseTs).ShouldBeGreaterThan(0);

        // Same Epoch, Higher Ticks wins despite lower Counter, ReplicaId
        var higherTicks = new CrdtTimestamp(Ticks: 101, Counter: 1, Epoch: 2, ReplicaId: "replica-a");
        higherTicks.CompareTo(baseTs).ShouldBeGreaterThan(0);

        // Same Epoch and Ticks, Higher Counter wins despite lower ReplicaId
        var higherCounter = new CrdtTimestamp(Ticks: 100, Counter: 6, Epoch: 2, ReplicaId: "replica-a");
        higherCounter.CompareTo(baseTs).ShouldBeGreaterThan(0);

        // Same Epoch, Ticks, Counter, Higher ReplicaId wins
        var higherReplica = new CrdtTimestamp(Ticks: 100, Counter: 5, Epoch: 2, ReplicaId: "replica-c");
        higherReplica.CompareTo(baseTs).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void CompareTo_Object_WithNull_ShouldReturn1()
    {
        var ts = new CrdtTimestamp(100);
        ts.CompareTo(null).ShouldBe(1);
    }

    [Fact]
    public void CompareTo_Object_WithValidCrdtTimestamp_ShouldReturnCorrectComparison()
    {
        var tsA = new CrdtTimestamp(100, 1, 1, "r1");
        object tsB = new CrdtTimestamp(100, 2, 1, "r1");
        object tsC = new CrdtTimestamp(100, 1, 1, "r1");

        tsA.CompareTo(tsB).ShouldBeLessThan(0);
        tsA.CompareTo(tsC).ShouldBe(0);
    }

    [Fact]
    public void CompareTo_Object_WithInvalidType_ShouldThrowArgumentException()
    {
        var ts = new CrdtTimestamp(100);

        var exception = Should.Throw<ArgumentException>(() => ts.CompareTo("invalid-object"));
        exception.ParamName.ShouldBe("obj");
    }

    [Fact]
    public void RelationalOperators_ShouldEvaluateCorrectly()
    {
        var smaller = new CrdtTimestamp(100, 1, 1, "a");
        var larger = new CrdtTimestamp(100, 2, 1, "a");
        var equalToSmaller = new CrdtTimestamp(100, 1, 1, "a");

        (smaller < larger).ShouldBeTrue();
        (larger < smaller).ShouldBeFalse();
        (smaller < equalToSmaller).ShouldBeFalse();

        (smaller <= larger).ShouldBeTrue();
        (smaller <= equalToSmaller).ShouldBeTrue();
        (larger <= smaller).ShouldBeFalse();

        (larger > smaller).ShouldBeTrue();
        (smaller > larger).ShouldBeFalse();
        (smaller > equalToSmaller).ShouldBeFalse();

        (larger >= smaller).ShouldBeTrue();
        (smaller >= equalToSmaller).ShouldBeTrue();
        (smaller >= larger).ShouldBeFalse();
    }

    [Fact]
    public void Equality_ShouldConsiderAllComponents()
    {
        var ts1 = new CrdtTimestamp(100, 1, 2, "replica-1");
        var ts2 = new CrdtTimestamp(100, 1, 2, "replica-1");
        var diffTicks = new CrdtTimestamp(101, 1, 2, "replica-1");
        var diffCounter = new CrdtTimestamp(100, 2, 2, "replica-1");
        var diffEpoch = new CrdtTimestamp(100, 1, 3, "replica-1");
        var diffReplica = new CrdtTimestamp(100, 1, 2, "replica-2");

        ts1.ShouldBe(ts2);
        (ts1 == ts2).ShouldBeTrue();
        (ts1 != ts2).ShouldBeFalse();
        ts1.GetHashCode().ShouldBe(ts2.GetHashCode());

        ts1.ShouldNotBe(diffTicks);
        ts1.ShouldNotBe(diffCounter);
        ts1.ShouldNotBe(diffEpoch);
        ts1.ShouldNotBe(diffReplica);
    }

    [Fact]
    public void MinValue_ShouldHaveExpectedDefaultPropertiesAndOrderBeforePositiveTimestamps()
    {
        CrdtTimestamp.MinValue.Ticks.ShouldBe(0);
        CrdtTimestamp.MinValue.Counter.ShouldBe(0);
        CrdtTimestamp.MinValue.Epoch.ShouldBe(0);
        CrdtTimestamp.MinValue.ReplicaId.ShouldBe(string.Empty);

        var defaultConstructed = new CrdtTimestamp(0);
        // MinValue has string.Empty, defaultConstructed has null ReplicaId, which CompareTo treats as equal
        CrdtTimestamp.MinValue.CompareTo(defaultConstructed).ShouldBe(0);

        var positiveTs = new CrdtTimestamp(1, 0, 0, "a");
        (CrdtTimestamp.MinValue < positiveTs).ShouldBeTrue();
    }
}