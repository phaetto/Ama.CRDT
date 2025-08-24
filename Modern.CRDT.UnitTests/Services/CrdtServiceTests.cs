namespace Modern.CRDT.UnitTests.Services;

using Modern.CRDT.Models;
using Modern.CRDT.Services;
using Moq;
using Shouldly;
using System.Collections.Generic;
using Xunit;

public sealed class CrdtServiceTests
{
    private sealed record TestModel(string Name);

    private readonly Mock<ICrdtPatcher> patcherMock = new();
    private readonly Mock<ICrdtApplicator> applicatorMock = new();
    private readonly CrdtService service;
    
    public CrdtServiceTests()
    {
        service = new CrdtService(patcherMock.Object, applicatorMock.Object);
    }

    [Fact]
    public void CreatePatch_ShouldDelegateToPatcher()
    {
        // Arrange
        var original = new CrdtDocument<TestModel>(new TestModel("original"), null);
        var modified = new CrdtDocument<TestModel>(new TestModel("modified"), null);
        var expectedPatch = new CrdtPatch(new List<CrdtOperation>());
        patcherMock.Setup(p => p.GeneratePatch(original, modified)).Returns(expectedPatch);

        // Act
        var result = service.CreatePatch(original, modified);

        // Assert
        result.ShouldBe(expectedPatch);
        patcherMock.Verify(p => p.GeneratePatch(original, modified), Times.Once);
    }

    [Fact]
    public void Merge_ShouldDelegateToApplicator()
    {
        // Arrange
        var model = new TestModel("original");
        var patch = new CrdtPatch(new List<CrdtOperation>());
        var metadata = new CrdtMetadata();
        var expectedModel = new TestModel("merged");

        applicatorMock.Setup(a => a.ApplyPatch(model, patch, metadata)).Returns(expectedModel);
        
        // Act
        var result = service.Merge(model, patch, metadata);

        // Assert
        result.ShouldBe(expectedModel);
        applicatorMock.Verify(a => a.ApplyPatch(model, patch, metadata), Times.Once);
    }
}