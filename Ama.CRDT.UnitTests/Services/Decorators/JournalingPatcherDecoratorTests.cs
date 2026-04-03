namespace Ama.CRDT.UnitTests.Services.Decorators;

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Decorators;
using Ama.CRDT.Services.Journaling;
using Moq;
using Shouldly;
using Xunit;

public sealed class JournalingPatcherDecoratorTests
{
    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenArgumentsAreNull()
    {
        // Arrange
        var patcherMock = new Mock<IAsyncCrdtPatcher>();
        var journalMock = new Mock<ICrdtOperationJournal>();
        var aotContexts = new[] { new DecoratorsTestCrdtContext() };

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new JournalingPatcherDecorator(null!, journalMock.Object, aotContexts));
        Should.Throw<ArgumentNullException>(() => new JournalingPatcherDecorator(patcherMock.Object, null!, aotContexts));
        Should.Throw<ArgumentNullException>(() => new JournalingPatcherDecorator(patcherMock.Object, journalMock.Object, null!));
    }

    [Fact]
    public async Task GeneratePatchAsync_ShouldJournalOperations_WhenOperationsAreGenerated()
    {
        // Arrange
        var patcherMock = new Mock<IAsyncCrdtPatcher>();
        var journalMock = new Mock<ICrdtOperationJournal>();
        var aotContexts = new[] { new DecoratorsTestCrdtContext() };
        var decorator = new JournalingPatcherDecorator(patcherMock.Object, journalMock.Object, aotContexts);

        var document = new CrdtDocument<TestModel>(new TestModel());
        var changed = new TestModel();

        var op = new CrdtOperation { Id = Guid.NewGuid() };
        var patch = new CrdtPatch(new[] { op });

        patcherMock.Setup(m => m.GeneratePatchAsync(document, changed, It.IsAny<CancellationToken>())).ReturnsAsync(patch);

        // Act
        var result = await decorator.GeneratePatchAsync(document, changed);

        // Assert
        result.ShouldBe(patch);
        journalMock.Verify(m => m.AppendAsync(
            It.IsAny<string>(),
            It.Is<IReadOnlyList<CrdtOperation>>(ops => ops.Count == 1 && ops.Contains(op)), 
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task GeneratePatchAsync_WithTimestamp_ShouldJournalOperations_WhenOperationsAreGenerated()
    {
        // Arrange
        var patcherMock = new Mock<IAsyncCrdtPatcher>();
        var journalMock = new Mock<ICrdtOperationJournal>();
        var aotContexts = new[] { new DecoratorsTestCrdtContext() };
        var decorator = new JournalingPatcherDecorator(patcherMock.Object, journalMock.Object, aotContexts);

        var document = new CrdtDocument<TestModel>(new TestModel());
        var changed = new TestModel();
        var timestampMock = new Mock<ICrdtTimestamp>();

        var op = new CrdtOperation { Id = Guid.NewGuid() };
        var patch = new CrdtPatch(new[] { op });

        patcherMock.Setup(m => m.GeneratePatchAsync(document, changed, timestampMock.Object, It.IsAny<CancellationToken>())).ReturnsAsync(patch);

        // Act
        var result = await decorator.GeneratePatchAsync(document, changed, timestampMock.Object);

        // Assert
        result.ShouldBe(patch);
        journalMock.Verify(m => m.AppendAsync(
            It.IsAny<string>(),
            It.Is<IReadOnlyList<CrdtOperation>>(ops => ops.Count == 1 && ops.Contains(op)), 
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task GeneratePatchAsync_ShouldNotJournal_WhenNoOperationsAreGenerated()
    {
        // Arrange
        var patcherMock = new Mock<IAsyncCrdtPatcher>();
        var journalMock = new Mock<ICrdtOperationJournal>();
        var aotContexts = new[] { new DecoratorsTestCrdtContext() };
        var decorator = new JournalingPatcherDecorator(patcherMock.Object, journalMock.Object, aotContexts);

        var document = new CrdtDocument<TestModel>(new TestModel());
        var changed = new TestModel();

        var patch = new CrdtPatch(Array.Empty<CrdtOperation>());

        patcherMock.Setup(m => m.GeneratePatchAsync(document, changed, It.IsAny<CancellationToken>())).ReturnsAsync(patch);

        // Act
        await decorator.GeneratePatchAsync(document, changed);

        // Assert
        journalMock.Verify(m => m.AppendAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<CrdtOperation>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateOperationAsync_ShouldJournalTheOperation()
    {
        // Arrange
        var patcherMock = new Mock<IAsyncCrdtPatcher>();
        var journalMock = new Mock<ICrdtOperationJournal>();
        var aotContexts = new[] { new DecoratorsTestCrdtContext() };
        var decorator = new JournalingPatcherDecorator(patcherMock.Object, journalMock.Object, aotContexts);

        var document = new CrdtDocument<TestModel>(new TestModel());
        Expression<Func<TestModel, string?>> expression = m => m.Property;
        var intentMock = new Mock<IOperationIntent>();

        var expectedOperation = new CrdtOperation { Id = Guid.NewGuid() };

        patcherMock.Setup(m => m.GenerateOperationAsync(document, expression, intentMock.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedOperation);

        // Act
        var result = await decorator.GenerateOperationAsync(document, expression, intentMock.Object);

        // Assert
        result.ShouldBe(expectedOperation);
        journalMock.Verify(m => m.AppendAsync(
            It.IsAny<string>(),
            It.Is<IReadOnlyList<CrdtOperation>>(ops => ops.Count == 1 && ops.Contains(expectedOperation)), 
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }
}