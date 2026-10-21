namespace Ama.CRDT.UnitTests.Services.Adapters;

using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Adapters;
using Moq;
using Shouldly;
using Xunit;

public sealed class AsyncCrdtPatcherAdapterTests
{
    private sealed class TestModel
    {
        public string? Property { get; set; }
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenInnerPatcherIsNull()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new AsyncCrdtPatcherAdapter(null!));
    }

    [Fact]
    public async Task GeneratePatchAsync_WithoutTimestamp_ShouldCallInnerPatcher()
    {
        // Arrange
        var innerMock = new Mock<ICrdtPatcher>();
        var adapter = new AsyncCrdtPatcherAdapter(innerMock.Object);

        var document = new CrdtDocument<TestModel>(new TestModel());
        var changed = new TestModel();
        var expectedPatch = new CrdtPatch(Array.Empty<CrdtOperation>());

        innerMock.Setup(m => m.GeneratePatch(document, changed)).Returns(expectedPatch);

        // Act
        var result = await adapter.GeneratePatchAsync(document, changed);

        // Assert
        result.ShouldBe(expectedPatch);
        innerMock.Verify(m => m.GeneratePatch(document, changed), Times.Once);
    }

    [Fact]
    public async Task GeneratePatchAsync_WithTimestamp_ShouldCallInnerPatcher()
    {
        // Arrange
        var innerMock = new Mock<ICrdtPatcher>();
        var adapter = new AsyncCrdtPatcherAdapter(innerMock.Object);

        var document = new CrdtDocument<TestModel>(new TestModel());
        var changed = new TestModel();
        var timestamp = new CrdtTimestamp(123L);
        var expectedPatch = new CrdtPatch(Array.Empty<CrdtOperation>());

        innerMock.Setup(m => m.GeneratePatch(document, changed, timestamp)).Returns(expectedPatch);

        // Act
        var result = await adapter.GeneratePatchAsync(document, changed, timestamp);

        // Assert
        result.ShouldBe(expectedPatch);
        innerMock.Verify(m => m.GeneratePatch(document, changed, timestamp), Times.Once);
    }

    [Fact]
    public async Task GenerateOperationAsync_WithoutTimestamp_ShouldCallInnerPatcher()
    {
        // Arrange
        var innerMock = new Mock<ICrdtPatcher>();
        var adapter = new AsyncCrdtPatcherAdapter(innerMock.Object);

        var document = new CrdtDocument<TestModel>(new TestModel());
        Expression<Func<TestModel, string?>> expression = m => m.Property;
        var intentMock = new Mock<IOperationIntent>();
        var expectedOperation = new CrdtOperation { Id = Guid.NewGuid() };

        innerMock.Setup(m => m.GenerateOperation(document, expression, intentMock.Object)).Returns(expectedOperation);

        // Act
        var result = await adapter.GenerateOperationAsync(document, expression, intentMock.Object);

        // Assert
        result.ShouldBe(expectedOperation);
        innerMock.Verify(m => m.GenerateOperation(document, expression, intentMock.Object), Times.Once);
    }

    [Fact]
    public async Task GenerateOperationAsync_WithTimestamp_ShouldCallInnerPatcher()
    {
        // Arrange
        var innerMock = new Mock<ICrdtPatcher>();
        var adapter = new AsyncCrdtPatcherAdapter(innerMock.Object);

        var document = new CrdtDocument<TestModel>(new TestModel());
        Expression<Func<TestModel, string?>> expression = m => m.Property;
        var intentMock = new Mock<IOperationIntent>();
        var timestamp = new CrdtTimestamp(123L);
        var expectedOperation = new CrdtOperation { Id = Guid.NewGuid() };

        innerMock.Setup(m => m.GenerateOperation(document, expression, intentMock.Object, timestamp)).Returns(expectedOperation);

        // Act
        var result = await adapter.GenerateOperationAsync(document, expression, intentMock.Object, timestamp);

        // Assert
        result.ShouldBe(expectedOperation);
        innerMock.Verify(m => m.GenerateOperation(document, expression, intentMock.Object, timestamp), Times.Once);
    }

    [Fact]
    public async Task AdapterMethods_ShouldThrowOperationCanceledException_WhenCancellationRequested()
    {
        // Arrange
        var innerMock = new Mock<ICrdtPatcher>();
        var adapter = new AsyncCrdtPatcherAdapter(innerMock.Object);

        var document = new CrdtDocument<TestModel>(new TestModel());
        var changed = new TestModel();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(() => adapter.GeneratePatchAsync(document, changed, cts.Token));
        
        innerMock.Verify(m => m.GeneratePatch(It.IsAny<CrdtDocument<TestModel>>(), It.IsAny<TestModel>()), Times.Never);
    }
}