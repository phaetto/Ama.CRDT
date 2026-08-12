namespace Ama.CRDT.UnitTests.Services.Decorators;

using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Decorators;
using Moq;
using Shouldly;
using Xunit;

public sealed class AsyncCrdtPatcherDecoratorBaseTests
{
    [Fact]
    public async Task GeneratePatchAsync_BeforeBehavior_ShouldPropagateCancellationToInnerPatcher()
    {
        // Arrange
        var innerMock = new Mock<IAsyncCrdtPatcher>();
        using var cts = new CancellationTokenSource();
        var decorator = new TestPatcherDecorator(innerMock.Object, DecoratorBehavior.Before);
        
        var document = new CrdtDocument<TestModel>(new TestModel());
        var changed = new TestModel();
        var expectedPatch = new CrdtPatch(Array.Empty<CrdtOperation>());

        innerMock.Setup(m => m.GeneratePatchAsync(document, changed, cts.Token))
            .ReturnsAsync(expectedPatch);

        // Act
        var result = await decorator.GeneratePatchAsync(document, changed, cts.Token);

        // Assert
        result.ShouldBe(expectedPatch);
        decorator.CapturedBeforeToken.ShouldBe(cts.Token);
        innerMock.Verify(m => m.GeneratePatchAsync(document, changed, cts.Token), Times.Once);
    }

    [Fact]
    public async Task GeneratePatchAsync_AfterBehavior_ShouldShieldOnAfterMethodFromCancellation()
    {
        // Arrange
        var innerMock = new Mock<IAsyncCrdtPatcher>();
        using var cts = new CancellationTokenSource();
        var decorator = new TestPatcherDecorator(innerMock.Object, DecoratorBehavior.After);
        
        var document = new CrdtDocument<TestModel>(new TestModel());
        var changed = new TestModel();
        var expectedPatch = new CrdtPatch(Array.Empty<CrdtOperation>());

        innerMock.Setup(m => m.GeneratePatchAsync(document, changed, cts.Token))
            .ReturnsAsync(() => 
            {
                // Simulate cancellation right after inner logic finishes but before OnAfter
                cts.Cancel(); 
                return expectedPatch;
            });

        // Act
        var result = await decorator.GeneratePatchAsync(document, changed, cts.Token);

        // Assert
        result.ShouldBe(expectedPatch);
        
        decorator.CapturedAfterToken.ShouldBe(CancellationToken.None);
        decorator.CapturedAfterToken.CanBeCanceled.ShouldBeFalse();
        
        innerMock.Verify(m => m.GeneratePatchAsync(document, changed, cts.Token), Times.Once);
    }

    [Fact]
    public async Task GenerateOperationAsync_ComplexBehavior_ShouldPropagateCancellationToInner()
    {
        // Arrange
        var innerMock = new Mock<IAsyncCrdtPatcher>();
        using var cts = new CancellationTokenSource();
        var decorator = new TestPatcherDecorator(innerMock.Object, DecoratorBehavior.Complex);
        
        var document = new CrdtDocument<TestModel>(new TestModel());
        Expression<Func<TestModel, string?>> expression = m => m.Property;
        var intentMock = new Mock<IOperationIntent>();
        var expectedOp = new CrdtOperation { Id = Guid.NewGuid() };

        innerMock.Setup(m => m.GenerateOperationAsync(document, expression, intentMock.Object, cts.Token))
            .ReturnsAsync(expectedOp);

        // Act
        var result = await decorator.GenerateOperationAsync(document, expression, intentMock.Object, cts.Token);

        // Assert
        result.ShouldBe(expectedOp);
        decorator.CapturedComplexToken.ShouldBe(cts.Token);
        innerMock.Verify(m => m.GenerateOperationAsync(document, expression, intentMock.Object, cts.Token), Times.Once);
    }

    [Fact]
    public async Task GenerateOperationAsyncWithTimestamp_AfterBehavior_ShouldShieldOnAfterMethodFromCancellation()
    {
        // Arrange
        var innerMock = new Mock<IAsyncCrdtPatcher>();
        using var cts = new CancellationTokenSource();
        var decorator = new TestPatcherDecorator(innerMock.Object, DecoratorBehavior.After);
        
        var document = new CrdtDocument<TestModel>(new TestModel());
        Expression<Func<TestModel, string?>> expression = m => m.Property;
        var intentMock = new Mock<IOperationIntent>();
        var timestampMock = new Mock<ICrdtTimestamp>();
        var expectedOp = new CrdtOperation { Id = Guid.NewGuid() };

        innerMock.Setup(m => m.GenerateOperationAsync(document, expression, intentMock.Object, timestampMock.Object, cts.Token))
            .ReturnsAsync(() => 
            {
                cts.Cancel(); 
                return expectedOp;
            });

        // Act
        var result = await decorator.GenerateOperationAsync(document, expression, intentMock.Object, timestampMock.Object, cts.Token);

        // Assert
        result.ShouldBe(expectedOp);
        
        decorator.CapturedAfterToken.ShouldBe(CancellationToken.None);
        decorator.CapturedAfterToken.CanBeCanceled.ShouldBeFalse();
        
        innerMock.Verify(m => m.GenerateOperationAsync(document, expression, intentMock.Object, timestampMock.Object, cts.Token), Times.Once);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenInnerIsNull()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new TestPatcherDecorator(null!, DecoratorBehavior.Before));
    }

    private sealed class TestPatcherDecorator : AsyncCrdtPatcherDecoratorBase
    {
        public CancellationToken CapturedBeforeToken { get; private set; }
        public CancellationToken CapturedAfterToken { get; private set; }
        public CancellationToken CapturedComplexToken { get; private set; }

        public TestPatcherDecorator(IAsyncCrdtPatcher innerPatcher, DecoratorBehavior behavior) 
            : base(innerPatcher, behavior)
        {
        }

        protected override Task OnBeforeGeneratePatchAsync<T>(CrdtDocument<T> from, T changed, CancellationToken cancellationToken)
        {
            CapturedBeforeToken = cancellationToken;
            return Task.CompletedTask;
        }

        protected override Task OnAfterGeneratePatchAsync<T>(CrdtDocument<T> from, T changed, CrdtPatch result, CancellationToken cancellationToken)
        {
            CapturedAfterToken = cancellationToken;
            return Task.CompletedTask;
        }

        protected override Task<CrdtPatch> OnComplexGeneratePatchAsync<T>(IAsyncCrdtPatcher inner, CrdtDocument<T> from, T changed, CancellationToken cancellationToken)
        {
            CapturedComplexToken = cancellationToken;
            return inner.GeneratePatchAsync(from, changed, cancellationToken);
        }

        protected override Task OnBeforeGenerateOperationAsync<T, TProp>(CrdtDocument<T> document, Expression<Func<T, TProp>> propertyExpression, IOperationIntent intent, CancellationToken cancellationToken)
        {
            CapturedBeforeToken = cancellationToken;
            return Task.CompletedTask;
        }

        protected override Task OnAfterGenerateOperationAsync<T, TProp>(CrdtDocument<T> document, Expression<Func<T, TProp>> propertyExpression, IOperationIntent intent, CrdtOperation result, CancellationToken cancellationToken)
        {
            CapturedAfterToken = cancellationToken;
            return Task.CompletedTask;
        }

        protected override Task<CrdtOperation> OnComplexGenerateOperationAsync<T, TProp>(IAsyncCrdtPatcher inner, CrdtDocument<T> document, Expression<Func<T, TProp>> propertyExpression, IOperationIntent intent, CancellationToken cancellationToken)
        {
            CapturedComplexToken = cancellationToken;
            return inner.GenerateOperationAsync(document, propertyExpression, intent, cancellationToken);
        }

        protected override Task OnBeforeGenerateOperationAsync<T, TProp>(CrdtDocument<T> document, Expression<Func<T, TProp>> propertyExpression, IOperationIntent intent, ICrdtTimestamp timestamp, CancellationToken cancellationToken)
        {
            CapturedBeforeToken = cancellationToken;
            return Task.CompletedTask;
        }

        protected override Task OnAfterGenerateOperationAsync<T, TProp>(CrdtDocument<T> document, Expression<Func<T, TProp>> propertyExpression, IOperationIntent intent, ICrdtTimestamp timestamp, CrdtOperation result, CancellationToken cancellationToken)
        {
            CapturedAfterToken = cancellationToken;
            return Task.CompletedTask;
        }

        protected override Task<CrdtOperation> OnComplexGenerateOperationAsync<T, TProp>(IAsyncCrdtPatcher inner, CrdtDocument<T> document, Expression<Func<T, TProp>> propertyExpression, IOperationIntent intent, ICrdtTimestamp timestamp, CancellationToken cancellationToken)
        {
            CapturedComplexToken = cancellationToken;
            return inner.GenerateOperationAsync(document, propertyExpression, intent, timestamp, cancellationToken);
        }
    }
}