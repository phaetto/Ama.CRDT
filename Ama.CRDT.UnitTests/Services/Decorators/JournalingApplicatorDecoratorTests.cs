namespace Ama.CRDT.UnitTests.Services.Decorators;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Decorators;
using Ama.CRDT.Services.Journaling;
using Moq;
using Shouldly;
using Xunit;

public sealed class JournalingApplicatorDecoratorTests
{
    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenArgumentsAreNull()
    {
        // Arrange
        var applicatorMock = new Mock<IAsyncCrdtApplicator>();
        var journalMock = new Mock<ICrdtOperationJournal>();
        var aotContexts = new[] { new DecoratorsTestCrdtAotContext() };

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new JournalingApplicatorDecorator(null!, journalMock.Object, aotContexts));
        Should.Throw<ArgumentNullException>(() => new JournalingApplicatorDecorator(applicatorMock.Object, null!, aotContexts));
        Should.Throw<ArgumentNullException>(() => new JournalingApplicatorDecorator(applicatorMock.Object, journalMock.Object, null!));
    }

    [Fact]
    public async Task ApplyPatchAsync_ShouldJournalOnlySuccessfullyAppliedOperations()
    {
        // Arrange
        var applicatorMock = new Mock<IAsyncCrdtApplicator>();
        var journalMock = new Mock<ICrdtOperationJournal>();
        var aotContexts = new[] { new DecoratorsTestCrdtAotContext() };
        var decorator = new JournalingApplicatorDecorator(applicatorMock.Object, journalMock.Object, aotContexts);

        var document = new CrdtDocument<TestModel>(new TestModel(), new CrdtMetadata());
        
        var appliedOp = CreateOperationWithId(Guid.NewGuid());
        var unappliedOp = CreateOperationWithId(Guid.NewGuid());

        var patch = new CrdtPatch(new[] { appliedOp, unappliedOp });

        var unappliedOperationData = new UnappliedOperation(unappliedOp, default);

        var expectedResult = new ApplyPatchResult<TestModel>(document, new[] { unappliedOperationData });

        applicatorMock.Setup(m => m.ApplyPatchAsync(document, patch, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await decorator.ApplyPatchAsync(document, patch);

        // Assert
        result.ShouldBe(expectedResult);
        
        journalMock.Verify(m => m.AppendAsync(
            It.IsAny<string>(),
            It.Is<IReadOnlyList<CrdtOperation>>(ops => ops.Count == 1 && ops.Contains(appliedOp)), 
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task ApplyPatchAsync_ShouldNotJournal_WhenNoOperationsWereSuccessful()
    {
        // Arrange
        var applicatorMock = new Mock<IAsyncCrdtApplicator>();
        var journalMock = new Mock<ICrdtOperationJournal>();
        var aotContexts = new[] { new DecoratorsTestCrdtAotContext() };
        var decorator = new JournalingApplicatorDecorator(applicatorMock.Object, journalMock.Object, aotContexts);

        var document = new CrdtDocument<TestModel>(new TestModel(), new CrdtMetadata());
        
        var failedOp = CreateOperationWithId(Guid.NewGuid());

        var patch = new CrdtPatch(new[] { failedOp });

        var unappliedOperationData = new UnappliedOperation(failedOp, default);

        var expectedResult = new ApplyPatchResult<TestModel>(document, new[] { unappliedOperationData });

        applicatorMock.Setup(m => m.ApplyPatchAsync(document, patch, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await decorator.ApplyPatchAsync(document, patch);

        // Assert
        result.ShouldBe(expectedResult);
        journalMock.Verify(m => m.AppendAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<CrdtOperation>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApplyPatchAsync_ShouldNotJournal_WhenPatchHasNoOperations()
    {
        // Arrange
        var applicatorMock = new Mock<IAsyncCrdtApplicator>();
        var journalMock = new Mock<ICrdtOperationJournal>();
        var aotContexts = new[] { new DecoratorsTestCrdtAotContext() };
        var decorator = new JournalingApplicatorDecorator(applicatorMock.Object, journalMock.Object, aotContexts);

        var document = new CrdtDocument<TestModel>(new TestModel(), new CrdtMetadata());
        
        var patch = new CrdtPatch(Array.Empty<CrdtOperation>());

        var expectedResult = new ApplyPatchResult<TestModel>(document, Array.Empty<UnappliedOperation>());

        applicatorMock.Setup(m => m.ApplyPatchAsync(document, patch, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await decorator.ApplyPatchAsync(document, patch);

        // Assert
        result.ShouldBe(expectedResult);
        journalMock.Verify(m => m.AppendAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<CrdtOperation>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static CrdtOperation CreateOperationWithId(Guid id)
    {
        return new CrdtOperation { Id = id };
    }
}