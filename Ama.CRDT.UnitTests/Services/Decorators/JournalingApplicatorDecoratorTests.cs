namespace Ama.CRDT.UnitTests.Services.Decorators;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Decorators;
using Ama.CRDT.Services.Journaling;
using Ama.CRDT.Services.Providers;
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
        var providerMock = new Mock<IDocumentIdProvider>();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new JournalingApplicatorDecorator(null!, journalMock.Object, providerMock.Object, DecoratorBehavior.Before));
        Should.Throw<ArgumentNullException>(() => new JournalingApplicatorDecorator(applicatorMock.Object, null!, providerMock.Object, DecoratorBehavior.Before));
        Should.Throw<ArgumentNullException>(() => new JournalingApplicatorDecorator(applicatorMock.Object, journalMock.Object, null!, DecoratorBehavior.Before));
    }

    [Fact]
    public async Task ApplyPatchAsync_ShouldJournalAllOperationsBeforeApplying()
    {
        // Arrange
        var applicatorMock = new Mock<IAsyncCrdtApplicator>();
        var journalMock = new Mock<ICrdtOperationJournal>();
        var providerMock = new Mock<IDocumentIdProvider>();
        
        providerMock.Setup(p => p.GetDocumentId(It.IsAny<TestModel>())).Returns((IComparable)"test-doc-id");
        
        var decorator = new JournalingApplicatorDecorator(applicatorMock.Object, journalMock.Object, providerMock.Object, DecoratorBehavior.Before);

        var document = new CrdtDocument<TestModel>(new TestModel(), new CrdtMetadata());
        
        var op1 = CreateOperationWithId(Guid.NewGuid());
        var op2 = CreateOperationWithId(Guid.NewGuid());

        var patch = new CrdtPatch(new[] { op1, op2 });

        var expectedResult = new ApplyPatchResult<TestModel>(document, Array.Empty<UnappliedOperation>());

        applicatorMock.Setup(m => m.ApplyPatchAsync(document, patch, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await decorator.ApplyPatchAsync(document, patch);

        // Assert
        result.ShouldBe(expectedResult);
        
        journalMock.Verify(m => m.AppendAsync(
            (IComparable)"test-doc-id",
            It.Is<IReadOnlyList<CrdtOperation>>(ops => ops.Count == 2 && ops.Contains(op1) && ops.Contains(op2)), 
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task ApplyPatchAsync_ShouldNotJournal_WhenPatchHasNoOperations()
    {
        // Arrange
        var applicatorMock = new Mock<IAsyncCrdtApplicator>();
        var journalMock = new Mock<ICrdtOperationJournal>();
        var providerMock = new Mock<IDocumentIdProvider>();
        
        var decorator = new JournalingApplicatorDecorator(applicatorMock.Object, journalMock.Object, providerMock.Object, DecoratorBehavior.Before);

        var document = new CrdtDocument<TestModel>(new TestModel(), new CrdtMetadata());
        
        var patch = new CrdtPatch(Array.Empty<CrdtOperation>());

        var expectedResult = new ApplyPatchResult<TestModel>(document, Array.Empty<UnappliedOperation>());

        applicatorMock.Setup(m => m.ApplyPatchAsync(document, patch, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await decorator.ApplyPatchAsync(document, patch);

        // Assert
        result.ShouldBe(expectedResult);
        journalMock.Verify(m => m.AppendAsync(It.IsAny<IComparable>(), It.IsAny<IReadOnlyList<CrdtOperation>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static CrdtOperation CreateOperationWithId(Guid id)
    {
        return new CrdtOperation { Id = id };
    }
}