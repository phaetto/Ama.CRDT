namespace Ama.CRDT.UnitTests.Models.Collections;

using System;
using System.Collections.Generic;
using System.Text.Json;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Collections;
using Ama.CRDT.Services.Pooling;
using Ama.CRDT.UnitTests.Models.Serialization;
using Shouldly;
using Xunit;

public sealed class CompactCollectionsTests
{
    [Fact]
    public void CompactDictionary_Empty_ShouldHaveZeroCapacityAndNotBePromoted()
    {
        // Arrange & Act
        var dict = new CompactDictionary<string, long>();

        // Assert
        dict.Count.ShouldBe(0);
        dict.Capacity.ShouldBe(0);
        dict.IsPromoted.ShouldBeFalse();
    }

    [Fact]
    public void CompactDictionary_WithinLinearThreshold_ShouldStayInFlatArrayAndPoolStringKeys()
    {
        // Arrange
        var pool = new CrdtStringPool();
        var dict = new CompactDictionary<string, int>(0, StringComparer.Ordinal, pool);
        var key1A = new string("$.devices.battery".ToCharArray());
        var key1B = new string("$.devices.battery".ToCharArray());

        // Act
        dict.Add(key1A, 85);
        dict[key1B] = 90;

        // Assert
        dict.Count.ShouldBe(1);
        dict.IsPromoted.ShouldBeFalse();
        dict[key1A].ShouldBe(90);

        foreach (var key in dict.Keys)
        {
            ReferenceEquals(key, pool.GetOrAdd("$.devices.battery")).ShouldBeTrue();
        }
    }

    [Fact]
    public void CompactDictionary_ExceedingLinearThreshold_ShouldPromoteAndDemoteOnTrimExcess()
    {
        // Arrange
        int threshold = CrdtMemoryOptions.LinearScanThreshold;
        var dict = new CompactDictionary<int, string>();

        // Act - Fill up to threshold
        for (int i = 0; i < threshold; i++)
        {
            dict[i] = $"val-{i}";
        }

        dict.IsPromoted.ShouldBeFalse();
        dict.Count.ShouldBe(threshold);

        // Act - Exceed threshold by 1
        dict[threshold] = $"val-{threshold}";

        // Assert - Promoted
        dict.IsPromoted.ShouldBeTrue();
        dict.Count.ShouldBe(threshold + 1);
        for (int i = 0; i <= threshold; i++)
        {
            dict.TryGetValue(i, out var val).ShouldBeTrue();
            val.ShouldBe($"val-{i}");
        }

        // Act - Remove items and TrimExcess to demote back to flat array
        dict.Remove(threshold).ShouldBeTrue();
        dict.TrimExcess();

        // Assert - Demoted back to exact-fit flat array
        dict.IsPromoted.ShouldBeFalse();
        dict.Count.ShouldBe(threshold);
        dict.Capacity.ShouldBe(threshold);
    }

    [Fact]
    public void CompactDictionary_AutoTrimOnEmpty_ShouldReleaseBackingStorageWhenClearedOrEmptied()
    {
        // Arrange
        var dict = new CompactDictionary<string, int>();
        dict["a"] = 1;
        dict["b"] = 2;
        dict.Capacity.ShouldBeGreaterThan(0);

        // Act
        dict.Remove("a").ShouldBeTrue();
        dict.Remove("b").ShouldBeTrue();

        // Assert
        dict.Count.ShouldBe(0);
        dict.Capacity.ShouldBe(0);
        dict.IsPromoted.ShouldBeFalse();
    }

    [Fact]
    public void CompactDictionary_EqualityAndClone_ShouldMatchContentsIndependentOfOrder()
    {
        // Arrange
        var dict1 = new CompactDictionary<string, int>
        {
            ["a"] = 1,
            ["b"] = 2,
            ["c"] = 3
        };

        var dict2 = new CompactDictionary<string, int>
        {
            ["c"] = 3,
            ["a"] = 1,
            ["b"] = 2
        };

        // Act
        var clone = dict1.Clone();

        // Assert
        dict1.Equals(dict2).ShouldBeTrue();
        dict1.GetHashCode().ShouldBe(dict2.GetHashCode());
        dict1.Equals(clone).ShouldBeTrue();
        clone.Capacity.ShouldBe(3);
    }

    [Fact]
    public void CompactSet_Empty_ShouldHaveZeroCapacityAndNotBePromoted()
    {
        // Arrange & Act
        var set = new CompactSet<CrdtOperation>();

        // Assert
        set.Count.ShouldBe(0);
        set.Capacity.ShouldBe(0);
        set.IsPromoted.ShouldBeFalse();
    }

    [Fact]
    public void CompactSet_WithinAndBeyondLinearThreshold_ShouldHandlePromotionsAndSetOperations()
    {
        // Arrange
        int threshold = CrdtMemoryOptions.LinearScanThreshold;
        var set = new CompactSet<int>();

        for (int i = 0; i < threshold; i++)
        {
            set.Add(i).ShouldBeTrue();
        }

        set.Add(0).ShouldBeFalse(); // Duplicate
        set.IsPromoted.ShouldBeFalse();

        // Act - Promote
        set.Add(threshold).ShouldBeTrue();

        // Assert
        set.IsPromoted.ShouldBeTrue();
        set.Count.ShouldBe(threshold + 1);
        set.Contains(threshold).ShouldBeTrue();

        // Act - Set operations
        set.ExceptWith([threshold, threshold - 1]);
        set.Count.ShouldBe(threshold - 1);
        set.TrimExcess();

        // Assert - Demoted to exact-sized array
        set.IsPromoted.ShouldBeFalse();
        set.Capacity.ShouldBe(threshold - 1);
    }

    [Fact]
    public void CompactSet_SetAlgebra_ShouldBehaveCorrectly()
    {
        // Arrange
        var setA = new CompactSet<string>(["a", "b", "c"]);
        var setB = new CompactSet<string>(["b", "c", "d"]);

        // Assert relations
        setA.Overlaps(setB).ShouldBeTrue();
        setA.SetEquals(["c", "b", "a"]).ShouldBeTrue();
        setA.IsSubsetOf(["a", "b", "c", "d"]).ShouldBeTrue();
        setA.IsProperSubsetOf(["a", "b", "c", "d"]).ShouldBeTrue();
        setA.IsSupersetOf(["a", "b"]).ShouldBeTrue();
        setA.IsProperSupersetOf(["a", "b"]).ShouldBeTrue();

        // Act - Intersect
        var copy = setA.Clone();
        copy.IntersectWith(setB);
        copy.SetEquals(["b", "c"]).ShouldBeTrue();

        // Act - SymmetricExcept
        var sym = setA.Clone();
        sym.SymmetricExceptWith(setB);
        sym.SetEquals(["a", "d"]).ShouldBeTrue();

        // Act - Clear all items
        sym.Remove("a").ShouldBeTrue();
        sym.Remove("d").ShouldBeTrue();
        sym.Count.ShouldBe(0);
        sym.Capacity.ShouldBe(0);
    }

    [Fact]
    public void CompactCollections_Serialization_ShouldRoundTripAndDeserializeAsCompactTypes()
    {
        // Arrange
        var options = TestOptionsHelper.GetDefaultOptions();
        var metadata = new CrdtMetadata();
        metadata.States["$.name"] = new CausalTimestamp(new CrdtTimestamp(100), "replica-1", 1);
        metadata.VersionVector["replica-1"] = 5;
        metadata.SeenExceptions.Add(new CrdtOperation(Guid.NewGuid(), "replica-2", "$.name", OperationType.Upsert, "Bob", new CrdtTimestamp(200), 2));

        var directDict = new CompactDictionary<object, CausalTimestamp>
        {
            ["key-1"] = new CausalTimestamp(new CrdtTimestamp(123), "replica-1", 1)
        };
        var directSet = new CompactSet<string>(["replica-1", "replica-2"]);

        // Act
        var metadataJson = JsonSerializer.Serialize(metadata, options);
        var deserializedMetadata = JsonSerializer.Deserialize<CrdtMetadata>(metadataJson, options);

        var dictJson = JsonSerializer.Serialize(directDict, options);
        var deserializedDict = JsonSerializer.Deserialize<CompactDictionary<object, CausalTimestamp>>(dictJson, options);

        var setJson = JsonSerializer.Serialize(directSet, options);
        var deserializedSet = JsonSerializer.Deserialize<CompactSet<string>>(setJson, options);

        // Assert
        deserializedMetadata.ShouldNotBeNull();
        deserializedMetadata.States.ShouldBeOfType<CompactDictionary<string, ICrdtMetadataState>>();
        deserializedMetadata.VersionVector.ShouldBeOfType<CompactDictionary<string, long>>();
        deserializedMetadata.SeenExceptions.ShouldBeOfType<CompactSet<CrdtOperation>>();
        deserializedMetadata.Equals(metadata).ShouldBeTrue();

        deserializedDict.ShouldNotBeNull();
        deserializedDict.Equals(directDict).ShouldBeTrue();

        deserializedSet.ShouldNotBeNull();
        deserializedSet.Equals(directSet).ShouldBeTrue();
    }
}