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

public sealed class AsyncCrdtApplicatorDecoratorBaseTests
{
    [Fact]
    public async Task ApplyPatchAsync_BeforeBehavior_ShouldShieldInnerApplicatorFromCancellation()
    {
        // Arrange
        var innerMock = new Mock<IAsyncCrdtApplicator>();
        using var cts = new CancellationTokenSource();
        var decorator = new TestApplicatorDecorator(innerMock.Object, DecoratorBehavior.Before);
        
        var document = new CrdtDocument<TestModel>(new TestModel());
        var patch = new CrdtPatch(Array.Empty<CrdtOperation>());
        var expectedResult = new ApplyPatchResult<TestModel>(document, Array.Empty<UnappliedOperation>());

        innerMock.Setup(m => m.ApplyPatchAsync(document, patch, CancellationToken.None))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await decorator.ApplyPatchAsync(document, patch, cts.Token);

        // Assert
        result.ShouldBe(expectedResult);
        decorator.CapturedBeforeToken.ShouldBe(cts.Token);
        
        // Verifies the inner applicator is shielded from torn commits during Before phase
        innerMock.Verify(m => m.ApplyPatchAsync(document, patch, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task ApplyPatchAsync_AfterBehavior_ShouldShieldOnAfterMethodFromCancellation()
    {
        // Arrange
        var innerMock = new Mock<IAsyncCrdtApplicator>();
        using var cts = new CancellationTokenSource();
        var decorator = new TestApplicatorDecorator(innerMock.Object, DecoratorBehavior.After);
        
        var document = new CrdtDocument<TestModel>(new TestModel());
        var patch = new CrdtPatch(Array.Empty<CrdtOperation>());
        var expectedResult = new ApplyPatchResult<TestModel>(document, Array.Empty<UnappliedOperation>());

        innerMock.Setup(m => m.ApplyPatchAsync(document, patch, cts.Token))
            .ReturnsAsync(() => 
            {
                // Simulate cancellation right after inner logic finishes but before OnAfter
                cts.Cancel(); 
                return expectedResult;
            });

        // Act
        var result = await decorator.ApplyPatchAsync(document, patch, cts.Token);

        // Assert
        result.ShouldBe(expectedResult);
        
        // Ensure OnAfter is called even if token was cancelled
        decorator.CapturedAfterToken.ShouldBe(CancellationToken.None);
        decorator.CapturedAfterToken.CanBeCanceled.ShouldBeFalse();
        
        innerMock.Verify(m => m.ApplyPatchAsync(document, patch, cts.Token), Times.Once);
    }

    [Fact]
    public async Task ApplyPatchAsync_ComplexBehavior_ShouldPropagateCancellationToInner()
    {
        // Arrange
        var innerMock = new Mock<IAsyncCrdtApplicator>();
        using var cts = new CancellationTokenSource();
        var decorator = new TestApplicatorDecorator(innerMock.Object, DecoratorBehavior.Complex);
        
        var document = new CrdtDocument<TestModel>(new TestModel());
        var patch = new CrdtPatch(Array.Empty<CrdtOperation>());
        var expectedResult = new ApplyPatchResult<TestModel>(document, Array.Empty<UnappliedOperation>());

        innerMock.Setup(m => m.ApplyPatchAsync(document, patch, cts.Token))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await decorator.ApplyPatchAsync(document, patch, cts.Token);

        // Assert
        result.ShouldBe(expectedResult);
        decorator.CapturedComplexToken.ShouldBe(cts.Token);
        innerMock.Verify(m => m.ApplyPatchAsync(document, patch, cts.Token), Times.Once);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenInnerIsNull()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new TestApplicatorDecorator(null!, DecoratorBehavior.Before));
    }

    private sealed class TestApplicatorDecorator : AsyncCrdtApplicatorDecoratorBase
    {
        public CancellationToken CapturedBeforeToken { get; private set; }
        public CancellationToken CapturedAfterToken { get; private set; }
        public CancellationToken CapturedComplexToken { get; private set; }

        public TestApplicatorDecorator(IAsyncCrdtApplicator innerApplicator, DecoratorBehavior behavior) 
            : base(innerApplicator, behavior)
        {
        }

        protected override Task OnBeforeApplyAsync<TDoc>(CrdtDocument<TDoc> document, CrdtPatch patch, CancellationToken cancellationToken)
        {
            CapturedBeforeToken = cancellationToken;
            return Task.CompletedTask;
        }

        protected override Task OnAfterApplyAsync<TDoc>(CrdtDocument<TDoc> document, CrdtPatch patch, ApplyPatchResult<TDoc> result, CancellationToken cancellationToken)
        {
            CapturedAfterToken = cancellationToken;
            return Task.CompletedTask;
        }

        protected override Task<ApplyPatchResult<TDoc>> OnComplexApplyAsync<TDoc>(IAsyncCrdtApplicator inner, CrdtDocument<TDoc> document, CrdtPatch patch, CancellationToken cancellationToken)
        {
            CapturedComplexToken = cancellationToken;
            return inner.ApplyPatchAsync(document, patch, cancellationToken);
        }
    }
}