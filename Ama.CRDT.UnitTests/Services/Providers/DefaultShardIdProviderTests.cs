namespace Ama.CRDT.UnitTests.Services.Providers;

using System;
using Ama.CRDT.Models;
using Ama.CRDT.Services.Providers;
using Shouldly;
using Xunit;

public sealed class DefaultShardIdProviderTests
{
    private sealed class TestModel
    {
        public string Id { get; set; } = string.Empty;
    }

    private readonly DefaultShardIdProvider provider = new();

    [Fact]
    public void GetShardId_WithDocument_ShouldReturnDefault()
    {
        // Arrange
        var doc = new CrdtDocument<TestModel>(new TestModel { Id = "doc-1" }, new CrdtMetadata());

        // Act
        var shardId = provider.GetShardId(doc);

        // Assert
        shardId.ShouldBe("default");
    }

    [Fact]
    public void GetShardId_GenericWithDocumentId_ShouldReturnDefault()
    {
        // Act
        var shardId = provider.GetShardId<TestModel>("doc-1");

        // Assert
        shardId.ShouldBe("default");
    }

    [Fact]
    public void GetShardId_GenericWithNullDocumentId_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => provider.GetShardId<TestModel>((IComparable)null!));
    }

    [Fact]
    public void GetShardId_WithDocumentIdAndType_ShouldReturnDefault()
    {
        // Act
        var shardId = provider.GetShardId("doc-1", typeof(TestModel));

        // Assert
        shardId.ShouldBe("default");
    }

    [Fact]
    public void GetShardId_WithNullDocumentIdAndType_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => provider.GetShardId(null!, typeof(TestModel)));
    }

    [Fact]
    public void GetShardId_WithDocumentIdAndNullType_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => provider.GetShardId("doc-1", null!));
    }
}