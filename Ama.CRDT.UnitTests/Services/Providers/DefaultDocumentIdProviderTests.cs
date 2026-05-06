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
[CrdtAotType(typeof(ModelWithStringId))]
[CrdtAotType(typeof(ModelWithIntId))]
[CrdtAotType(typeof(ModelWithReadOnlyId))]
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

public class ModelWithStringId
{
    public string? Id { get; set; }
}

public class ModelWithIntId
{
    public int Id { get; set; }
}

public class ModelWithReadOnlyId
{
    public string Id { get; } = "readonly";
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

    [Fact]
    public void SetDocumentId_ShouldSetGuidId()
    {
        // Arrange
        var id = Guid.NewGuid();
        var model = new ModelWithId();

        // Act
        provider.SetDocumentId(model, id.ToString());

        // Assert
        model.Id.ShouldBe(id);
    }

    [Fact]
    public void SetDocumentId_ShouldSetStringId()
    {
        // Arrange
        var id = "doc-123";
        var model = new ModelWithStringId();

        // Act
        provider.SetDocumentId(model, id);

        // Assert
        model.Id.ShouldBe(id);
    }

    [Fact]
    public void SetDocumentId_ShouldSetIntId()
    {
        // Arrange
        var model = new ModelWithIntId();

        // Act
        provider.SetDocumentId(model, "42");

        // Assert
        model.Id.ShouldBe(42);
    }

    [Fact]
    public void SetDocumentId_ShouldThrowArgumentNullException_WhenObjectIsNull()
    {
        ModelWithId? model = null;
        Should.Throw<ArgumentNullException>(() => provider.SetDocumentId(model!, "id"));
    }

    [Fact]
    public void SetDocumentId_ShouldThrowArgumentException_WhenIdIsNullOrWhitespace()
    {
        var model = new ModelWithId();
        Should.Throw<ArgumentException>(() => provider.SetDocumentId(model, "  "));
    }

    [Fact]
    public void SetDocumentId_ShouldThrowInvalidOperationException_WhenIdPropertyIsMissing()
    {
        var model = new ModelWithoutId();
        var ex = Should.Throw<InvalidOperationException>(() => provider.SetDocumentId(model, "id"));
        ex.Message.ShouldContain("does not have a writable 'Id' property");
    }

    [Fact]
    public void SetDocumentId_ShouldThrowInvalidOperationException_WhenIdPropertyIsReadOnly()
    {
        var model = new ModelWithReadOnlyId();
        var ex = Should.Throw<InvalidOperationException>(() => provider.SetDocumentId(model, "id"));
        ex.Message.ShouldContain("does not have a writable 'Id' property");
    }

    [Fact]
    public void CreateDocumentWithId_ShouldCreateAndSetGuidId()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var model = provider.CreateDocumentWithId<ModelWithId>(id.ToString());

        // Assert
        model.ShouldNotBeNull();
        model.Id.ShouldBe(id);
    }

    [Fact]
    public void CreateDocumentWithId_ShouldCreateAndSetStringId()
    {
        // Arrange
        var id = "doc-123";

        // Act
        var model = provider.CreateDocumentWithId<ModelWithStringId>(id);

        // Assert
        model.ShouldNotBeNull();
        model.Id.ShouldBe(id);
    }

    [Fact]
    public void CreateDocumentWithId_ShouldThrowArgumentException_WhenIdIsNullOrWhitespace()
    {
        Should.Throw<ArgumentException>(() => provider.CreateDocumentWithId<ModelWithId>(""));
    }

    [Fact]
    public void CreateDocumentWithId_ShouldThrowInvalidOperationException_WhenIdPropertyIsMissing()
    {
        var ex = Should.Throw<InvalidOperationException>(() => provider.CreateDocumentWithId<ModelWithoutId>("id"));
        ex.Message.ShouldContain("does not have a writable 'Id' property");
    }
}