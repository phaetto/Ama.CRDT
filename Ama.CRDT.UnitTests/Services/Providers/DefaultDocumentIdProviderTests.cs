namespace Ama.CRDT.UnitTests.Services.Providers;

using System;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Services.Providers;
using Shouldly;
using Xunit;

[CrdtAotType(typeof(ModelWithId))]
[CrdtAotType(typeof(ModelWithoutId))]
[CrdtAotType(typeof(ModelWithNullId))]
[CrdtAotType(typeof(ModelWithEmptyId))]
internal partial class DocumentIdTestCrdtContext : CrdtAotContext { }

public class ModelWithId
{
    public Guid Id { get; set; }
}

public class ModelWithoutId
{
    public string? Name { get; set; }
}

public class ModelWithNullId
{
    public string? Id { get; set; }
}

public class ModelWithEmptyId
{
    public string Id { get; set; } = string.Empty;
}

public class DefaultDocumentIdProviderTests
{
    private readonly DefaultDocumentIdProvider provider;

    public DefaultDocumentIdProviderTests()
    {
        provider = new DefaultDocumentIdProvider([new DocumentIdTestCrdtContext()]);
    }

    [Fact]
    public void GetDocumentId_ShouldReturnId_WhenIdPropertyExistsAndIsValid()
    {
        // Arrange
        var id = Guid.NewGuid();
        var model = new ModelWithId { Id = id };

        // Act
        var result = provider.GetDocumentId(model);

        // Assert
        result.ShouldBe(id.ToString());
    }

    [Fact]
    public void GetDocumentId_ShouldThrowArgumentNullException_WhenObjectIsNull()
    {
        // Arrange
        ModelWithId? model = null;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => provider.GetDocumentId(model));
    }

    [Fact]
    public void GetDocumentId_ShouldThrowInvalidOperationException_WhenIdPropertyIsMissing()
    {
        // Arrange
        var model = new ModelWithoutId { Name = "Test" };

        // Act & Assert
        var ex = Should.Throw<InvalidOperationException>(() => provider.GetDocumentId(model));
        ex.Message.ShouldContain("does not have a readable 'Id' property");
    }

    [Fact]
    public void GetDocumentId_ShouldThrowInvalidOperationException_WhenIdPropertyIsNull()
    {
        // Arrange
        var model = new ModelWithNullId { Id = null };

        // Act & Assert
        var ex = Should.Throw<InvalidOperationException>(() => provider.GetDocumentId(model));
        ex.Message.ShouldContain("evaluated to null");
    }

    [Fact]
    public void GetDocumentId_ShouldThrowInvalidOperationException_WhenIdPropertyIsEmpty()
    {
        // Arrange
        var model = new ModelWithEmptyId { Id = "   " };

        // Act & Assert
        var ex = Should.Throw<InvalidOperationException>(() => provider.GetDocumentId(model));
        ex.Message.ShouldContain("evaluated to an empty or whitespace string");
    }
}