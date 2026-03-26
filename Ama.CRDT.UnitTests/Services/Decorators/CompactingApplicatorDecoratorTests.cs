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

public sealed class CompactingApplicatorDecoratorTests
{
    private sealed class TestModel
    {
        public string? Property { get; set; }
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenArgumentsAreNull()
    {
        // Arrange
        var applicatorMock = new Mock<IAsyncCrdtApplicator>();
        var metadataManagerMock = new Mock<ICrdtMetadataManager>();
        var factories = new List<ICompactionPolicyFactory>();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new CompactingApplicatorDecorator(null!, metadataManagerMock.Object, factories));
        Should.Throw<ArgumentNullException>(() => new CompactingApplicatorDecorator(applicatorMock.Object, null!, factories));
        Should.Throw<ArgumentNullException>(() => new CompactingApplicatorDecorator(applicatorMock.Object, metadataManagerMock.Object, null!));
    }

    [Fact]
    public async Task ApplyPatchAsync_ShouldCallCompactOnAllPolicies_WhenPoliciesExist()
    {
        // Arrange
        var applicatorMock = new Mock<IAsyncCrdtApplicator>();
        var metadataManagerMock = new Mock<ICrdtMetadataManager>();
        
        var policy1Mock = new Mock<ICompactionPolicy>();
        var policy2Mock = new Mock<ICompactionPolicy>();

        var factory1Mock = new Mock<ICompactionPolicyFactory>();
        factory1Mock.Setup(f => f.CreatePolicy()).Returns(policy1Mock.Object);

        var factory2Mock = new Mock<ICompactionPolicyFactory>();
        factory2Mock.Setup(f => f.CreatePolicy()).Returns(policy2Mock.Object);

        var factories = new List<ICompactionPolicyFactory> { factory1Mock.Object, factory2Mock.Object };

        var decorator = new CompactingApplicatorDecorator(applicatorMock.Object, metadataManagerMock.Object, factories);

        var document = new CrdtDocument<TestModel>(new TestModel());
        var patch = new CrdtPatch(Array.Empty<CrdtOperation>());
        var expectedResult = new ApplyPatchResult<TestModel>(document.Data!, Array.Empty<UnappliedOperation>());

        applicatorMock.Setup(m => m.ApplyPatchAsync(document, patch, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await decorator.ApplyPatchAsync(document, patch);

        // Assert
        result.ShouldBe(expectedResult);

        // Verify that CreatePolicy was called on the factories
        factory1Mock.Verify(f => f.CreatePolicy(), Times.Once);
        factory2Mock.Verify(f => f.CreatePolicy(), Times.Once);

        // Verify that Compact was called for each policy on the returned document
        metadataManagerMock.Verify(m => m.Compact(document, policy1Mock.Object), Times.Once);
        metadataManagerMock.Verify(m => m.Compact(document, policy2Mock.Object), Times.Once);
    }

    [Fact]
    public async Task ApplyPatchAsync_ShouldNotCallCompact_WhenNoPoliciesExist()
    {
        // Arrange
        var applicatorMock = new Mock<IAsyncCrdtApplicator>();
        var metadataManagerMock = new Mock<ICrdtMetadataManager>();
        
        var factories = new List<ICompactionPolicyFactory>(); // Empty

        var decorator = new CompactingApplicatorDecorator(applicatorMock.Object, metadataManagerMock.Object, factories);

        var document = new CrdtDocument<TestModel>(new TestModel());
        var patch = new CrdtPatch(Array.Empty<CrdtOperation>());
        var expectedResult = new ApplyPatchResult<TestModel>(document.Data!, Array.Empty<UnappliedOperation>());

        applicatorMock.Setup(m => m.ApplyPatchAsync(document, patch, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await decorator.ApplyPatchAsync(document, patch);

        // Assert
        result.ShouldBe(expectedResult);

        // Verify that Compact was NEVER called
        metadataManagerMock.Verify(m => m.Compact(It.IsAny<CrdtDocument<TestModel>>(), It.IsAny<ICompactionPolicy>()), Times.Never);
    }
}