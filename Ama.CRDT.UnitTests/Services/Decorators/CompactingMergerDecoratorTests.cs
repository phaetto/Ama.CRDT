namespace Ama.CRDT.UnitTests.Services.Decorators;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Decorators;
using Ama.CRDT.Services.GarbageCollection;
using Moq;
using Shouldly;
using Xunit;

public sealed class CompactingMergerDecoratorTests
{
    private sealed class TestModel { }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenArgumentsAreNull()
    {
        // Arrange
        var mergerMock = new Mock<IAsyncCrdtMerger>();
        var metadataManagerMock = new Mock<ICrdtMetadataManager>();
        var factories = new List<ICompactionPolicyFactory>();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new CompactingMergerDecorator(null!, metadataManagerMock.Object, factories, DecoratorBehavior.After));
        Should.Throw<ArgumentNullException>(() => new CompactingMergerDecorator(mergerMock.Object, null!, factories, DecoratorBehavior.After));
        Should.Throw<ArgumentNullException>(() => new CompactingMergerDecorator(mergerMock.Object, metadataManagerMock.Object, null!, DecoratorBehavior.After));
    }

    [Fact]
    public async Task MergeStateAsync_ShouldCallCompactOnAllPolicies_WhenPoliciesExist()
    {
        // Arrange
        var mergerMock = new Mock<IAsyncCrdtMerger>();
        var metadataManagerMock = new Mock<ICrdtMetadataManager>();
        
        var policy1Mock = new Mock<ICompactionPolicy>();
        var policy2Mock = new Mock<ICompactionPolicy>();

        var factory1Mock = new Mock<ICompactionPolicyFactory>();
        factory1Mock.Setup(f => f.CreatePolicy()).Returns(policy1Mock.Object);

        var factory2Mock = new Mock<ICompactionPolicyFactory>();
        factory2Mock.Setup(f => f.CreatePolicy()).Returns(policy2Mock.Object);

        var factories = new List<ICompactionPolicyFactory> { factory1Mock.Object, factory2Mock.Object };

        var decorator = new CompactingMergerDecorator(mergerMock.Object, metadataManagerMock.Object, factories, DecoratorBehavior.After);

        var primary = new CrdtDocument<TestModel>(new TestModel(), new CrdtMetadata());
        var secondary = new CrdtDocument<TestModel>(new TestModel(), new CrdtMetadata());

        mergerMock.Setup(m => m.MergeStateAsync(primary, secondary, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await decorator.MergeStateAsync(primary, secondary);

        // Assert
        // Verify that CreatePolicy was called on the factories
        factory1Mock.Verify(f => f.CreatePolicy(), Times.Once);
        factory2Mock.Verify(f => f.CreatePolicy(), Times.Once);

        // Verify that Compact was called for each policy on the returned primary document
        metadataManagerMock.Verify(m => m.Compact(primary, policy1Mock.Object), Times.Once);
        metadataManagerMock.Verify(m => m.Compact(primary, policy2Mock.Object), Times.Once);
    }

    [Fact]
    public async Task MergeStateAsync_ShouldNotCallCompact_WhenNoPoliciesExist()
    {
        // Arrange
        var mergerMock = new Mock<IAsyncCrdtMerger>();
        var metadataManagerMock = new Mock<ICrdtMetadataManager>();
        
        var factories = new List<ICompactionPolicyFactory>(); // Empty

        var decorator = new CompactingMergerDecorator(mergerMock.Object, metadataManagerMock.Object, factories, DecoratorBehavior.After);

        var primary = new CrdtDocument<TestModel>(new TestModel(), new CrdtMetadata());
        var secondary = new CrdtDocument<TestModel>(new TestModel(), new CrdtMetadata());

        mergerMock.Setup(m => m.MergeStateAsync(primary, secondary, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await decorator.MergeStateAsync(primary, secondary);

        // Assert
        // Verify that Compact was NEVER called
        metadataManagerMock.Verify(m => m.Compact(It.IsAny<CrdtDocument<TestModel>>(), It.IsAny<ICompactionPolicy>()), Times.Never);
    }
}