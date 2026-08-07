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

public sealed class AsyncCrdtMergerAdapterTests
{
    private sealed class TestModel
    {
        public string? Property { get; set; }
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenInnerMergerIsNull()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new AsyncCrdtMergerAdapter(null!));
    }

    [Fact]
    public async Task MergeStateAsync_ShouldCallInnerMerger()
    {
        // Arrange
        var innerMock = new Mock<ICrdtMerger>();
        var adapter = new AsyncCrdtMergerAdapter(innerMock.Object);
        
        var primary = new CrdtDocument<TestModel>(new TestModel(), new CrdtMetadata());
        var secondary = new CrdtDocument<TestModel>(new TestModel(), new CrdtMetadata());

        // Act
        await adapter.MergeStateAsync(primary, secondary);

        // Assert
        innerMock.Verify(m => m.MergeState(primary, secondary), Times.Once);
    }

    [Fact]
    public async Task MergeStateAsync_ShouldThrowOperationCanceledException_WhenCancellationRequested()
    {
        // Arrange
        var innerMock = new Mock<ICrdtMerger>();
        var adapter = new AsyncCrdtMergerAdapter(innerMock.Object);
        
        var primary = new CrdtDocument<TestModel>(new TestModel(), new CrdtMetadata());
        var secondary = new CrdtDocument<TestModel>(new TestModel(), new CrdtMetadata());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(() => adapter.MergeStateAsync(primary, secondary, cts.Token));
        
        innerMock.Verify(m => m.MergeState(It.IsAny<CrdtDocument<TestModel>>(), It.IsAny<CrdtDocument<TestModel>>()), Times.Never);
    }
}