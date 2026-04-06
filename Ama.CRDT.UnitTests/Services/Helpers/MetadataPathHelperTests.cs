namespace Ama.CRDT.UnitTests.Services.Helpers;

using Ama.CRDT.Services.Helpers;
using Shouldly;
using Xunit;

public sealed class MetadataPathHelperTests
{
    [Theory]
    [InlineData("$.users", "Epoch", "$.users|Epoch")]
    [InlineData("$.users[0].name", "Quorum", "$.users[0].name|Quorum")]
    [InlineData("$", "Global", "$|Global")]
    public void GetDecoratorPath_ShouldFormatCorrectly(string jsonPath, string decoratorKey, string expected)
    {
        var result = MetadataPathHelper.GetDecoratorPath(jsonPath, decoratorKey);
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("$.users|Epoch", "$.users")]
    [InlineData("$.users[0].name|Quorum", "$.users[0].name")]
    [InlineData("$.users", "$.users")] // No decorator
    [InlineData("$", "$")] // Root, no decorator
    [InlineData("$|Global", "$")] 
    public void GetBasePath_ShouldStripDecoratorKey(string stateKey, string expected)
    {
        var result = MetadataPathHelper.GetBasePath(stateKey);
        result.ShouldBe(expected);
    }

    [Theory]
    // Self matches
    [InlineData("$.users|Epoch", "$.users", true)]
    [InlineData("$.users", "$.users", true)]
    // Child dot notation
    [InlineData("$.users.count|Epoch", "$.users", true)]
    [InlineData("$.users.count", "$.users", true)]
    // Child array index
    [InlineData("$.users[0]|Epoch", "$.users", true)]
    [InlineData("$.users[0].name|Quorum", "$.users", true)]
    [InlineData("$.users[0]", "$.users", true)]
    // Negative matches (similar names but not children)
    [InlineData("$.usersCount|Epoch", "$.users", false)]
    [InlineData("$.usersCount", "$.users", false)]
    // Negative matches (completely different)
    [InlineData("$.settings|Epoch", "$.users", false)]
    [InlineData("$.settings", "$.users", false)]
    // Root level matches
    [InlineData("$.users|Epoch", "$", true)] // Anything starting with $. is a child of $ because . counts
    public void IsChildOrSelfPath_ShouldCorrectlyIdentifyHierarchy(string stateKey, string targetBasePath, bool expected)
    {
        var result = MetadataPathHelper.IsChildOrSelfPath(stateKey, targetBasePath);
        result.ShouldBe(expected);
    }
}