namespace Ama.CRDT.UnitTests.Services.Decorators;

using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Decorators;
using Ama.CRDT.Services.LargerThanMemory;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

public sealed class LargerThanMemoryApplicatorDecoratorTests
{
    private readonly Mock<IAsyncCrdtApplicator> mockInnerApplicator;
    private readonly Mock<IVirtualDocumentPatchHandler<TestModel>> mockHandler;
    private readonly LargerThanMemoryApplicatorDecorator decorator;

    public LargerThanMemoryApplicatorDecoratorTests()
    {
        mockInnerApplicator = new Mock<IAsyncCrdtApplicator>();
        mockHandler = new Mock<IVirtualDocumentPatchHandler<TestModel>>();

        var services = new ServiceCollection();
        services.AddSingleton(mockHandler.Object);
        var serviceProvider = services.BuildServiceProvider();

        decorator = new LargerThanMemoryApplicatorDecorator(
            mockInnerApplicator.Object,
            serviceProvider,
            DecoratorBehavior.Complex);
    }

    [Fact]
    public async Task ApplyPatchAsync_ShouldDelegateToHandler_WhenHandlerReturnsResult()
    {
        // Arrange
        var doc = new CrdtDocument<TestModel>(new TestModel(), new CrdtMetadata());
        var patch = new CrdtPatch(new List<CrdtOperation>());
        var expectedResult = new ApplyPatchResult<TestModel>(doc, new List<UnappliedOperation>());

        mockHandler.Setup(x => x.TryApplyPatchAsync(mockInnerApplicator.Object, doc, patch, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await decorator.ApplyPatchAsync(doc, patch);

        // Assert
        Assert.Equal(expectedResult, result);
        mockInnerApplicator.Verify(x => x.ApplyPatchAsync(It.IsAny<CrdtDocument<TestModel>>(), It.IsAny<CrdtPatch>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApplyPatchAsync_ShouldDelegateToInnerApplicator_WhenNoHandlerProcessesIt()
    {
        // Arrange
        var doc = new CrdtDocument<TestModel>(new TestModel(), new CrdtMetadata());
        var patch = new CrdtPatch(new List<CrdtOperation>());
        var expectedResult = new ApplyPatchResult<TestModel>(doc, new List<UnappliedOperation>());

        mockHandler.Setup(x => x.TryApplyPatchAsync(mockInnerApplicator.Object, doc, patch, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApplyPatchResult<TestModel>?)null);

        mockInnerApplicator.Setup(x => x.ApplyPatchAsync(doc, patch, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await decorator.ApplyPatchAsync(doc, patch);

        // Assert
        Assert.Equal(expectedResult, result);
        mockInnerApplicator.Verify(x => x.ApplyPatchAsync(doc, patch, It.IsAny<CancellationToken>()), Times.Once);
    }
}