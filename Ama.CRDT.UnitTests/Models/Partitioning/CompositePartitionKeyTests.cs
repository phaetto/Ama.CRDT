namespace Ama.CRDT.UnitTests.Models.Partitioning;

using Ama.CRDT.Models.Partitioning;
using Shouldly;
using System.Collections.Generic;
using Xunit;

public sealed class CompositePartitionKeyTests
{
    [Fact]
    public void CompareTo_WithDifferentLogicalKeys_ShouldSortByLogicalKey()
    {
        // Arrange
        var key1 = new CompositePartitionKey("tenant-a", "m");
        var key2 = new CompositePartitionKey("tenant-b", "a");

        // Act & Assert
        key1.CompareTo(key2).ShouldBeLessThan(0);
        key2.CompareTo(key1).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void CompareTo_WithSameLogicalKeys_ShouldSortByRangeKey()
    {
        // Arrange
        var key1 = new CompositePartitionKey("tenant-a", "a");
        var key2 = new CompositePartitionKey("tenant-a", "m");

        // Act & Assert
        key1.CompareTo(key2).ShouldBeLessThan(0);
        key2.CompareTo(key1).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void CompareTo_WithNumericStrings_ShouldSortLexicographically()
    {
        // Arrange
        var key1 = new CompositePartitionKey("tenant", "2");
        var key2 = new CompositePartitionKey("tenant", "10");

        // Act & Assert
        // Lexicographically, "10" comes before "2".
        key2.CompareTo(key1).ShouldBeLessThan(0);
        key1.CompareTo(key2).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void CompareTo_WithNullRangeKey_ShouldComeFirst()
    {
        // Arrange
        var key1 = new CompositePartitionKey("tenant-a", null); // Header partition
        var key2 = new CompositePartitionKey("tenant-a", "a");  // First data partition

        // Act & Assert
        key1.CompareTo(key2).ShouldBeLessThan(0);
        key2.CompareTo(key1).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void CompareTo_WithTwoNullRangeKeys_ShouldBeEqual()
    {
        // Arrange
        var key1 = new CompositePartitionKey("tenant-a", null);
        var key2 = new CompositePartitionKey("tenant-a", null);

        // Act & Assert
        key1.CompareTo(key2).ShouldBe(0);
    }

    [Fact]
    public void Equals_ShouldWorkCorrectly()
    {
        // Arrange
        var key1a = new CompositePartitionKey("tenant-a", "1");
        var key1b = new CompositePartitionKey("tenant-a", "1");
        var key2 = new CompositePartitionKey("tenant-a", "2");
        var key3 = new CompositePartitionKey("tenant-b", "1");

        // Act & Assert
        key1a.ShouldBe(key1b);
        key1a.ShouldNotBe(key2);
        key1a.ShouldNotBe(key3);
    }

    [Fact]
    public void ListSort_ShouldUseCompareToCorrectly()
    {
        // Arrange
        var k1 = new CompositePartitionKey("B", "10");
        var k2 = new CompositePartitionKey("A", null); // header for A
        var k3 = new CompositePartitionKey("A", "200");
        var k4 = new CompositePartitionKey("B", null); // header for B
        var k5 = new CompositePartitionKey("A", "50");

        var list = new List<CompositePartitionKey> { k1, k2, k3, k4, k5 };

        // Act
        list.Sort();

        // Assert (Ordinal string comparison: "10" < "200" < "50")
        list[0].ShouldBe(k2); // A, null
        list[1].ShouldBe(k3); // A, "200"
        list[2].ShouldBe(k5); // A, "50"
        list[3].ShouldBe(k4); // B, null
        list[4].ShouldBe(k1); // B, "10"
    }
}