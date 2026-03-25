namespace Ama.CRDT.UnitTests.Services.Adapters;

using System;
using System.Threading;
using System.Threading.Tasks;
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Adapters;
using Moq;
using Shouldly;
using Xunit;

public sealed class AsyncCrdtApplicatorAdapterTests
{
    private sealed class TestModel
    {
        public string? Property { get; set; }
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenInnerApplicatorIsNull()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new AsyncCrdtApplicatorAdapter(null!));
    }

    [Fact]
    public async Task ApplyPatchAsync_ShouldCallInnerApplicatorAndReturnResult()
    {
        // Arrange
        var innerMock = new Mock<ICrdtApplicator>();
        var adapter = new AsyncCrdtApplicatorAdapter(innerMock.Object);
        
        var document = new CrdtDocument<TestModel>(new TestModel());
        var patch = new CrdtPatch(Array.Empty<CrdtOperation>());
        var expectedResult = new ApplyPatchResult<TestModel>(document.Data!, Array.Empty<UnappliedOperation>());

        innerMock.Setup(m => m.ApplyPatch(document, patch)).Returns(expectedResult);

        // Act
        var result = await adapter.ApplyPatchAsync(document, patch);

        // Assert
        result.ShouldBe(expectedResult);
        innerMock.Verify(m => m.ApplyPatch(document, patch), Times.Once);
    }

    [Fact]
    public async Task ApplyPatchAsync_ShouldThrowOperationCanceledException_WhenCancellationRequested()
    {
        // Arrange
        var innerMock = new Mock<ICrdtApplicator>();
        var adapter = new AsyncCrdtApplicatorAdapter(innerMock.Object);
        
        var document = new CrdtDocument<TestModel>(new TestModel());
        var patch = new CrdtPatch(Array.Empty<CrdtOperation>());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(() => adapter.ApplyPatchAsync(document, patch, cts.Token));
        
        innerMock.Verify(m => m.ApplyPatch(It.IsAny<CrdtDocument<TestModel>>(), It.IsAny<CrdtPatch>()), Times.Never);
    }
}