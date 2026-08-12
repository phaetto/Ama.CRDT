namespace Ama.CRDT.UnitTests.Services.Decorators;

using System;
using System.Threading;
using System.Threading.Tasks;
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Decorators;
using Moq;
using Shouldly;
using Xunit;

public sealed class AsyncCrdtMergerDecoratorBaseTests
{
    [Fact]
    public async Task MergeStateAsync_BeforeBehavior_ShouldShieldInnerMergerFromCancellation()
    {
        // Arrange
        var innerMock = new Mock<IAsyncCrdtMerger>();
        using var cts = new CancellationTokenSource();
        var decorator = new TestMergerDecorator(innerMock.Object, DecoratorBehavior.Before);
        
        var primary = new CrdtDocument<TestModel>(new TestModel());
        var secondary = new CrdtDocument<TestModel>(new TestModel());

        innerMock.Setup(m => m.MergeStateAsync(primary, secondary, CancellationToken.None))
            .Returns(Task.CompletedTask);

        // Act
        await decorator.MergeStateAsync(primary, secondary, cts.Token);

        // Assert
        decorator.CapturedBeforeToken.ShouldBe(cts.Token);
        
        // Verifies the inner merger is shielded from torn commits during Before phase
        innerMock.Verify(m => m.MergeStateAsync(primary, secondary, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task MergeStateAsync_AfterBehavior_ShouldShieldOnAfterMethodFromCancellation()
    {
        // Arrange
        var innerMock = new Mock<IAsyncCrdtMerger>();
        using var cts = new CancellationTokenSource();
        var decorator = new TestMergerDecorator(innerMock.Object, DecoratorBehavior.After);
        
        var primary = new CrdtDocument<TestModel>(new TestModel());
        var secondary = new CrdtDocument<TestModel>(new TestModel());

        innerMock.Setup(m => m.MergeStateAsync(primary, secondary, cts.Token))
            .Returns(() => 
            {
                // Simulate cancellation right after inner logic finishes but before OnAfter
                cts.Cancel(); 
                return Task.CompletedTask;
            });

        // Act
        await decorator.MergeStateAsync(primary, secondary, cts.Token);

        // Assert
        decorator.CapturedAfterToken.ShouldBe(CancellationToken.None);
        decorator.CapturedAfterToken.CanBeCanceled.ShouldBeFalse();
        
        innerMock.Verify(m => m.MergeStateAsync(primary, secondary, cts.Token), Times.Once);
    }

    [Fact]
    public async Task MergeStateAsync_ComplexBehavior_ShouldPropagateCancellationToInner()
    {
        // Arrange
        var innerMock = new Mock<IAsyncCrdtMerger>();
        using var cts = new CancellationTokenSource();
        var decorator = new TestMergerDecorator(innerMock.Object, DecoratorBehavior.Complex);
        
        var primary = new CrdtDocument<TestModel>(new TestModel());
        var secondary = new CrdtDocument<TestModel>(new TestModel());

        innerMock.Setup(m => m.MergeStateAsync(primary, secondary, cts.Token))
            .Returns(Task.CompletedTask);

        // Act
        await decorator.MergeStateAsync(primary, secondary, cts.Token);

        // Assert
        decorator.CapturedComplexToken.ShouldBe(cts.Token);
        innerMock.Verify(m => m.MergeStateAsync(primary, secondary, cts.Token), Times.Once);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenInnerIsNull()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new TestMergerDecorator(null!, DecoratorBehavior.Before));
    }

    private sealed class TestMergerDecorator : AsyncCrdtMergerDecoratorBase
    {
        public CancellationToken CapturedBeforeToken { get; private set; }
        public CancellationToken CapturedAfterToken { get; private set; }
        public CancellationToken CapturedComplexToken { get; private set; }

        public TestMergerDecorator(IAsyncCrdtMerger innerMerger, DecoratorBehavior behavior) 
            : base(innerMerger, behavior)
        {
        }

        protected override Task OnBeforeMergeAsync<T>(CrdtDocument<T> primary, CrdtDocument<T> secondary, CancellationToken cancellationToken)
        {
            CapturedBeforeToken = cancellationToken;
            return Task.CompletedTask;
        }

        protected override Task OnAfterMergeAsync<T>(CrdtDocument<T> primary, CrdtDocument<T> secondary, CancellationToken cancellationToken)
        {
            CapturedAfterToken = cancellationToken;
            return Task.CompletedTask;
        }

        protected override Task OnComplexMergeAsync<T>(IAsyncCrdtMerger inner, CrdtDocument<T> primary, CrdtDocument<T> secondary, CancellationToken cancellationToken)
        {
            CapturedComplexToken = cancellationToken;
            return inner.MergeStateAsync(primary, secondary, cancellationToken);
        }
    }
}