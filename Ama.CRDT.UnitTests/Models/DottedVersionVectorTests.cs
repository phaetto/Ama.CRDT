namespace Ama.CRDT.UnitTests.Models;

using System.Collections.Generic;
using Ama.CRDT.Models;
using Shouldly;
using Xunit;

public sealed class DottedVersionVectorTests
{
    [Fact]
    public void Constructor_ShouldInitializeEmpty()
    {
        var dvv = new DottedVersionVector();
        
        dvv.Versions.ShouldBeEmpty();
        dvv.Dots.ShouldBeEmpty();
    }

    [Fact]
    public void ConstructorWithState_ShouldCreateDeepCopy()
    {
        var versions = new Dictionary<string, long> { { "A", 5 } };
        var dots = new Dictionary<string, ISet<long>> { { "A", new HashSet<long> { 7, 8 } } };
        
        var dvv = new DottedVersionVector(versions, dots);
        
        dvv.Versions["A"].ShouldBe(5);
        dvv.Dots["A"].ShouldContain(7);
        dvv.Dots["A"].ShouldContain(8);
        
        // Mutate original to ensure deep copy
        versions["A"] = 10;
        dots["A"].Add(9);
        
        dvv.Versions["A"].ShouldBe(5);
        dvv.Dots["A"].ShouldNotContain(9);
    }

    [Fact]
    public void Includes_WithContiguousVersion_ShouldReturnTrue()
    {
        var dvv = new DottedVersionVector();
        dvv.Add("A", 1);
        dvv.Add("A", 2);
        dvv.Add("A", 3);

        dvv.Includes("A", 2).ShouldBeTrue();
        dvv.Includes("A", 3).ShouldBeTrue();
    }

    [Fact]
    public void Includes_WithIsolatedDot_ShouldReturnTrue()
    {
        var dvv = new DottedVersionVector();
        dvv.Add("A", 1);
        dvv.Add("A", 4); // Out of order

        dvv.Includes("A", 4).ShouldBeTrue();
    }

    [Fact]
    public void Includes_WithMissingVersion_ShouldReturnFalse()
    {
        var dvv = new DottedVersionVector();
        dvv.Add("A", 1);
        dvv.Add("A", 4);

        dvv.Includes("A", 2).ShouldBeFalse();
        dvv.Includes("A", 3).ShouldBeFalse();
        dvv.Includes("A", 5).ShouldBeFalse();
        dvv.Includes("B", 1).ShouldBeFalse(); // Unknown replica
    }

    [Fact]
    public void Add_NextContiguousVersion_ShouldAdvanceMax()
    {
        var dvv = new DottedVersionVector();
        
        dvv.Add("A", 1);
        dvv.Versions["A"].ShouldBe(1);
        dvv.Dots.ShouldNotContainKey("A");

        dvv.Add("A", 2);
        dvv.Versions["A"].ShouldBe(2);
    }

    [Fact]
    public void Add_OutOfOrderVersion_ShouldAddDot()
    {
        var dvv = new DottedVersionVector();
        dvv.Add("A", 1);
        
        dvv.Add("A", 3);
        
        dvv.Versions["A"].ShouldBe(1);
        dvv.Dots["A"].ShouldContain(3);
    }

    [Fact]
    public void Add_VersionThatFillsGap_ShouldAdvanceMaxAndCompactDots()
    {
        var dvv = new DottedVersionVector();
        dvv.Add("A", 1);
        dvv.Add("A", 3);
        dvv.Add("A", 4);
        
        // At this point, Versions["A"] is 1, Dots["A"] has 3 and 4
        dvv.Versions["A"].ShouldBe(1);
        dvv.Dots["A"].Count.ShouldBe(2);
        
        // Fill the gap
        dvv.Add("A", 2);
        
        // Now it should compress up to 4
        dvv.Versions["A"].ShouldBe(4);
        dvv.Dots.ShouldNotContainKey("A");
    }

    [Fact]
    public void Add_DuplicateVersion_ShouldIgnore()
    {
        var dvv = new DottedVersionVector();
        dvv.Add("A", 1);
        dvv.Add("A", 3);
        
        dvv.Add("A", 1); // Less than current max contiguous
        dvv.Add("A", 3); // Already in dots
        
        dvv.Versions["A"].ShouldBe(1);
        dvv.Dots["A"].Count.ShouldBe(1);
        dvv.Dots["A"].ShouldContain(3);
    }

    [Fact]
    public void Merge_WithOtherVector_ShouldCombineStateCorrectly()
    {
        var dvv1 = new DottedVersionVector();
        dvv1.Add("A", 1);
        dvv1.Add("A", 2);
        dvv1.Add("A", 5);
        dvv1.Add("B", 1);

        var dvv2 = new DottedVersionVector();
        dvv2.Add("A", 1);
        dvv2.Add("A", 3);
        dvv2.Add("A", 4);
        dvv2.Add("B", 1);
        dvv2.Add("B", 2);
        dvv2.Add("C", 1);

        dvv1.Merge(dvv2);

        // A should have 1, 2, 3, 4, 5 completely contiguous now
        dvv1.Versions["A"].ShouldBe(5);
        dvv1.Dots.ShouldNotContainKey("A");

        // B should have 1, 2
        dvv1.Versions["B"].ShouldBe(2);
        
        // C should have 1
        dvv1.Versions["C"].ShouldBe(1);
    }

    [Fact]
    public void Equals_AndHashCode_ShouldWorkCorrectly()
    {
        var dvv1 = new DottedVersionVector();
        dvv1.Add("A", 1);
        dvv1.Add("A", 3);

        var dvv2 = new DottedVersionVector();
        dvv2.Add("A", 1);
        dvv2.Add("A", 3);

        var dvv3 = new DottedVersionVector();
        dvv3.Add("A", 1);
        dvv3.Add("A", 2);
        dvv3.Add("A", 3); // Contiguous 1,2,3

        dvv1.Equals(dvv2).ShouldBeTrue();
        (dvv1.GetHashCode() == dvv2.GetHashCode()).ShouldBeTrue();

        dvv1.Equals(dvv3).ShouldBeFalse();
        (dvv1.GetHashCode() == dvv3.GetHashCode()).ShouldBeFalse();
        
        dvv1.Equals(null).ShouldBeFalse();
    }

    [Fact]
    public void DeepClone_ShouldReturnEqualButIndependentInstance()
    {
        var original = new DottedVersionVector();
        original.Add("A", 1);
        original.Add("A", 3);
        // Add contiguous versions for B to ensure they map into Versions dictionary instead of Dots
        original.Add("B", 1);
        original.Add("B", 2);

        var clone = original.DeepClone();

        // Ensure clone is logically equal but not the same reference
        clone.ShouldNotBeSameAs(original);
        clone.ShouldBe(original);

        // Mutate original and check clone remains unchanged
        original.Add("A", 2); // This compresses A up to 3 in original
        
        clone.Versions["A"].ShouldBe(1);
        clone.Dots["A"].ShouldContain(3);

        // Mutate clone and check original remains unchanged
        clone.Add("B", 3);
        
        original.Versions["B"].ShouldBe(2);
        original.Dots.ShouldNotContainKey("B");
    }
}