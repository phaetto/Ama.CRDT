namespace Ama.CRDT.UnitTests.Extensions;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Moq;
using Shouldly;
using Xunit;

public class AsyncCrdtApplicatorExtensionsTests
{
    private sealed record TestModel(string Name);

    [Fact]
    public async Task ApplyOperationsAsync_NullApplicator_ThrowsArgumentNullException()
    {
        IAsyncCrdtApplicator applicator = null!;
        var doc = new CrdtDocument<TestModel>(new TestModel("A"), new CrdtMetadata());
        var ops = EmptyAsyncEnumerable<JournaledOperation>();

        await Should.ThrowAsync<ArgumentNullException>(() => applicator.ApplyOperationsAsync(doc, ops));
    }

    [Fact]
    public async Task ApplyOperationsAsync_NullMissingOperations_ThrowsArgumentNullException()
    {
        var applicatorMock = new Mock<IAsyncCrdtApplicator>();
        var doc = new CrdtDocument<TestModel>(new TestModel("A"), new CrdtMetadata());

        await Should.ThrowAsync<ArgumentNullException>(() => applicatorMock.Object.ApplyOperationsAsync(doc, null!));
    }

    [Fact]
    public async Task ApplyOperationsAsync_EmptyOperations_ReturnsOriginalDocumentAndDoesNotApplyPatch()
    {
        var applicatorMock = new Mock<IAsyncCrdtApplicator>();
        var doc = new CrdtDocument<TestModel>(new TestModel("A"), new CrdtMetadata());
        var ops = EmptyAsyncEnumerable<JournaledOperation>();

        var result = await applicatorMock.Object.ApplyOperationsAsync(doc, ops);

        result.Document.ShouldBe(doc);
        result.UnappliedOperations.ShouldBeEmpty();
        applicatorMock.Verify(a => a.ApplyPatchAsync(It.IsAny<CrdtDocument<TestModel>>(), It.IsAny<CrdtPatch>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApplyOperationsAsync_WithOperations_AppliesPatchCorrectly()
    {
        var applicatorMock = new Mock<IAsyncCrdtApplicator>();
        var doc = new CrdtDocument<TestModel>(new TestModel("A"), new CrdtMetadata());
        var op1 = new CrdtOperation(Guid.NewGuid(), "R1", "Name", OperationType.Upsert, "B", new EpochTimestamp(1));
        var op2 = new CrdtOperation(Guid.NewGuid(), "R1", "Name", OperationType.Upsert, "C", new EpochTimestamp(2));
        
        var ops = ToAsyncEnumerable(new[] 
        { 
            new JournaledOperation("doc1", op1), 
            new JournaledOperation("doc1", op2) 
        });

        var expectedResult = new ApplyPatchResult<TestModel>(
            new CrdtDocument<TestModel>(new TestModel("C"), new CrdtMetadata()), 
            Array.Empty<UnappliedOperation>());

        CrdtPatch? appliedPatch = null;
        applicatorMock
            .Setup(a => a.ApplyPatchAsync(doc, It.IsAny<CrdtPatch>(), It.IsAny<CancellationToken>()))
            .Callback<CrdtDocument<TestModel>, CrdtPatch, CancellationToken>((d, p, c) => appliedPatch = p)
            .ReturnsAsync(expectedResult);

        var result = await applicatorMock.Object.ApplyOperationsAsync(doc, ops);

        result.ShouldBe(expectedResult);
        appliedPatch.ShouldNotBeNull();
        appliedPatch.Value.Operations.Count.ShouldBe(2);
        appliedPatch.Value.Operations[0].ShouldBe(op1);
        appliedPatch.Value.Operations[1].ShouldBe(op2);

        applicatorMock.Verify(a => a.ApplyPatchAsync(doc, It.IsAny<CrdtPatch>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static async IAsyncEnumerable<T> EmptyAsyncEnumerable<T>()
    {
        yield break;
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            yield return item;
            await Task.Yield(); // Simulate async streaming
        }
    }
}