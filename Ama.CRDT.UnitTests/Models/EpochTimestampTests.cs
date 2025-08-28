namespace Ama.CRDT.UnitTests.Models;

using Ama.CRDT.Models;
using Shouldly;
using System;
using Xunit;

public sealed class EpochTimestampTests
{
    [Theory]
    [InlineData(100, 100, 0)]
    [InlineData(101, 100, 1)]
    [InlineData(99, 100, -1)]
    public void CompareTo_ShouldReturnCorrectValue(long a, long b, int expected)
    {
        var tsA = new SequentialTimestamp(a);
        var tsB = new SequentialTimestamp(b);

        tsA.CompareTo(tsB).ShouldBe(expected);
    }

    [Fact]
    public void CompareTo_WithNull_ShouldReturn1()
    {
        var ts = new SequentialTimestamp(100);
        ts.CompareTo(null).ShouldBe(1);
    }
    
    private readonly record struct OtherTimestamp : ICrdtTimestamp
    {
        public int CompareTo(ICrdtTimestamp? other) => 0;
    }

    [Fact]
    public void CompareTo_WithDifferentImplementation_ShouldThrow()
    {
        var ts = new SequentialTimestamp(100);
        var other = new OtherTimestamp();

        Should.Throw<ArgumentException>(() => ts.CompareTo(other));
    }
}