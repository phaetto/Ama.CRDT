namespace Ama.CRDT.UnitTests.Models.Collections;

using System;
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
        var pool = CrdtStringPool.Shared;
        var dict = new CompactDictionary<string, int>(0, StringComparer.Ordinal);
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
        int threshold = CrdtCollectionMemoryOptions.LinearScanThreshold;
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
        int threshold = CrdtCollectionMemoryOptions.LinearScanThreshold;
        var set1 = new CompactSet<int>();
        for (int i = 0; i < threshold; i++)
        {
            set1.Add(i).ShouldBeTrue();
        }

        set1.IsPromoted.ShouldBeFalse();
        set1.Add(threshold).ShouldBeTrue();
        set1.IsPromoted.ShouldBeTrue();

        var set2 = new CompactSet<int> { 0, 1, 2, 100 };

        // Act & Assert
        set1.Overlaps(set2).ShouldBeTrue();
        set2.IsSubsetOf(set1).ShouldBeFalse();

        set1.IntersectWith(set2);
        set1.Count.ShouldBe(3);
        set1.TrimExcess();
        set1.IsPromoted.ShouldBeFalse();
        set1.Capacity.ShouldBe(3);

        var clone = set1.Clone();
        clone.Equals(set1).ShouldBeTrue();
        clone.Capacity.ShouldBe(3);
    }

    [Fact]
    public void CompactCollections_JsonRoundTrip_ShouldPreserveValuesAndTierLayout()
    {
        // Arrange
        var options = TestOptionsHelper.GetDefaultOptions();
        var metadata = new CrdtMetadata();
        metadata.VersionVector["replica-1"] = 10;
        metadata.VersionVector["replica-2"] = 20;
        metadata.States["$.title"] = new CausalTimestamp(new CrdtTimestamp(1, 100, 0, "replica-1"), "replica-1", 1);

        // Act
        var json = JsonSerializer.Serialize(metadata, options);
        var deserialized = JsonSerializer.Deserialize<CrdtMetadata>(json, options);

        // Assert
        deserialized.ShouldNotBeNull();
        deserialized.Equals(metadata).ShouldBeTrue();
        deserialized.VersionVector.ShouldBeOfType<CompactDictionary<string, long>>();
        ((CompactDictionary<string, long>)deserialized.VersionVector).IsPromoted.ShouldBeFalse();
    }

    [Fact]
    public void CompactCollections_CustomInitialCapacityAndLinearGrowthStep_ShouldGrowByExactStep()
    {
        var originalInit = CrdtCollectionMemoryOptions.InitialFlatCapacity;
        var originalFactor = CrdtCollectionMemoryOptions.GrowthFactor;
        var originalStep = CrdtCollectionMemoryOptions.MinimumGrowthStep;

        try
        {
            CrdtCollectionMemoryOptions.InitialFlatCapacity = 1;
            CrdtCollectionMemoryOptions.GrowthFactor = 1.0;
            CrdtCollectionMemoryOptions.MinimumGrowthStep = 3;

            var dict = new CompactDictionary<int, int>();
            dict[1] = 10;
            dict.Capacity.ShouldBe(1);

            dict[2] = 20;
            dict.Capacity.ShouldBe(4); // 1 + 3
        }
        finally
        {
            CrdtCollectionMemoryOptions.InitialFlatCapacity = originalInit;
            CrdtCollectionMemoryOptions.GrowthFactor = originalFactor;
            CrdtCollectionMemoryOptions.MinimumGrowthStep = originalStep;
        }
    }

    [Fact]
    public void CompactCollections_AutoDemoteToFlat_ShouldDemoteWhenCountReachesHalfThreshold()
    {
        var originalThreshold = CrdtCollectionMemoryOptions.LinearScanThreshold;
        var originalAutoDemote = CrdtCollectionMemoryOptions.AutoDemoteToFlat;

        try
        {
            CrdtCollectionMemoryOptions.LinearScanThreshold = 4;
            CrdtCollectionMemoryOptions.AutoDemoteToFlat = true;

            var set = new CompactSet<int>();
            for (var i = 0; i < 6; i++)
            {
                set.Add(i);
            }

            set.IsPromoted.ShouldBeTrue();

            // Remove down to 3 (> 4 / 2), should stay promoted
            set.Remove(5);
            set.Remove(4);
            set.Remove(3);
            set.Count.ShouldBe(3);
            set.IsPromoted.ShouldBeTrue();

            // Remove down to 2 (<= 4 / 2), should auto-demote to flat array
            set.Remove(2);
            set.Count.ShouldBe(2);
            set.IsPromoted.ShouldBeFalse();
            set.Capacity.ShouldBe(2);
            set.Contains(0).ShouldBeTrue();
            set.Contains(1).ShouldBeTrue();
        }
        finally
        {
            CrdtCollectionMemoryOptions.LinearScanThreshold = originalThreshold;
            CrdtCollectionMemoryOptions.AutoDemoteToFlat = originalAutoDemote;
        }
    }

    [Fact]
    public void CompactCollections_CustomAutoDemoteRatio_And_StringPoolingFlag_ShouldBeRespected()
    {
        var originalThreshold = CrdtCollectionMemoryOptions.LinearScanThreshold;
        var originalAutoDemote = CrdtCollectionMemoryOptions.AutoDemoteToFlat;
        var originalRatio = CrdtCollectionMemoryOptions.AutoDemoteRatio;
        var originalPooling = CrdtCollectionMemoryOptions.EnableAutomaticStringPooling;

        try
        {
            CrdtCollectionMemoryOptions.LinearScanThreshold = 8;
            CrdtCollectionMemoryOptions.AutoDemoteToFlat = true;
            CrdtCollectionMemoryOptions.AutoDemoteRatio = 0.75; // Demote at <= 6 items
            CrdtCollectionMemoryOptions.EnableAutomaticStringPooling = false;

            var dict = new CompactDictionary<string, int>();
            for (var i = 0; i < 9; i++)
            {
                dict[$"key_{i}"] = i;
            }

            dict.IsPromoted.ShouldBeTrue();

            // Remove down to 7 (> 6), stays promoted
            dict.Remove("key_8");
            dict.Remove("key_7");
            dict.Count.ShouldBe(7);
            dict.IsPromoted.ShouldBeTrue();

            // Remove down to 6 (<= 8 * 0.75), demotes to flat
            dict.Remove("key_6");
            dict.Count.ShouldBe(6);
            dict.IsPromoted.ShouldBeFalse();

            // Copy constructor fast-path
            var copy = new CompactDictionary<string, int>(dict);
            copy.Count.ShouldBe(6);
            copy.IsPromoted.ShouldBeFalse();
            copy.Capacity.ShouldBe(6);
            copy.Equals(dict).ShouldBeTrue();
        }
        finally
        {
            CrdtCollectionMemoryOptions.LinearScanThreshold = originalThreshold;
            CrdtCollectionMemoryOptions.AutoDemoteToFlat = originalAutoDemote;
            CrdtCollectionMemoryOptions.AutoDemoteRatio = originalRatio;
            CrdtCollectionMemoryOptions.EnableAutomaticStringPooling = originalPooling;
        }
    }
}