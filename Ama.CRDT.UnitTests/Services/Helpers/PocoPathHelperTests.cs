namespace Ama.CRDT.UnitTests.Services.Helpers;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ama.CRDT.Services.Helpers;
using Shouldly;
using Xunit;

public sealed class PocoPathHelperTests
{
    private readonly TestRoot rootObject;

    public PocoPathHelperTests()
    {
        rootObject = new TestRoot
        {
            SimpleProp = "value",
            SpecialNameProp = "special",
            User = new TestUser
            {
                Name = "John",
                Age = 30,
                Address = new TestAddress { Street = "123 Main St" }
            },
            Users =
            [
                new() { Name = "Alice", Age = 25 },
                new() { Name = "Bob", Age = 28 }
            ],
            Tags = ["tag1", "tag2"]
        };
    }

    [Theory]
    [InlineData(null, new string[0])]
    [InlineData("", new string[0])]
    [InlineData("$", new string[0])]
    [InlineData("$.name", new[] { "name" })]
    [InlineData("name", new[] { "name" })]
    [InlineData("$.user.name", new[] { "user", "name" })]
    [InlineData("user.name", new[] { "user", "name" })]
    [InlineData("$.users[0]", new[] { "users", "0" })]
    [InlineData("$.users['first-name']", new[] { "users", "first-name" })]
    [InlineData("$.users[0].name", new[] { "users", "0", "name" })]
    [InlineData("$.a.b['c-d'][0].e", new[] { "a", "b", "c-d", "0", "e" })]
    [InlineData("a.b[0]", new[] { "a", "b", "0" })]
    [InlineData("[0]", new[] { "0" })]
    [InlineData("['name']", new[] { "name" })]
    public void ParsePath_ShouldReturnCorrectSegments(string path, string[] expected)
    {
        // Act
        var result = PocoPathHelper.ParsePath(path);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void ResolvePath_ShouldResolveSimpleProperty()
    {
        // Arrange
        var path = "$.simpleProp";

        // Act
        var (parent, property, finalSegment) = PocoPathHelper.ResolvePath(rootObject, path);

        // Assert
        parent.ShouldBe(rootObject);
        property.ShouldNotBeNull();
        property.Name.ShouldBe(nameof(TestRoot.SimpleProp));
        finalSegment.ShouldBe("simpleProp");
    }

    [Fact]
    public void ResolvePath_ShouldResolveNestedProperty()
    {
        // Arrange
        var path = "$.user.name";

        // Act
        var (parent, property, finalSegment) = PocoPathHelper.ResolvePath(rootObject, path);

        // Assert
        parent.ShouldBe(rootObject.User);
        property.ShouldNotBeNull();
        property.Name.ShouldBe(nameof(TestUser.Name));
        finalSegment.ShouldBe("name");
    }

    [Fact]
    public void ResolvePath_ShouldResolveArrayElement()
    {
        // Arrange
        var path = "$.users[1]";

        // Act
        var (parent, property, finalSegment) = PocoPathHelper.ResolvePath(rootObject, path);

        // Assert
        parent.ShouldBe(rootObject);
        property.ShouldNotBeNull();
        property.Name.ShouldBe(nameof(TestRoot.Users));
        finalSegment.ShouldBe(1);
    }

    [Fact]
    public void ResolvePath_ShouldResolvePropertyOfArrayElement()
    {
        // Arrange
        var path = "$.users[0].age";

        // Act
        var (parent, property, finalSegment) = PocoPathHelper.ResolvePath(rootObject, path);

        // Assert
        parent.ShouldBe(rootObject.Users[0]);
        property.ShouldNotBeNull();
        property.Name.ShouldBe(nameof(TestUser.Age));
        finalSegment.ShouldBe("age");
    }

    [Fact]
    public void ResolvePath_ShouldHandleJsonPropertyNameAttribute()
    {
        // Arrange
        var path = "$['special-name']";

        // Act
        var (parent, property, finalSegment) = PocoPathHelper.ResolvePath(rootObject, path);

        // Assert
        parent.ShouldBe(rootObject);
        property.ShouldNotBeNull();
        property.Name.ShouldBe(nameof(TestRoot.SpecialNameProp));
        finalSegment.ShouldBe("special-name");
    }

    [Fact]
    public void ResolvePath_ShouldHandlePascalCasePropertyName()
    {
        // Arrange
        var path = "$.SimpleProp";

        // Act
        var (parent, property, finalSegment) = PocoPathHelper.ResolvePath(rootObject, path);

        // Assert
        parent.ShouldBe(rootObject);
        property.ShouldNotBeNull();
        property.Name.ShouldBe(nameof(TestRoot.SimpleProp));
        finalSegment.ShouldBe("SimpleProp");
    }

    [Fact]
    public void ResolvePath_ShouldReturnNullsForInvalidProperty()
    {
        // Arrange
        var path = "$.nonExistent";

        // Act
        var (parent, property, finalSegment) = PocoPathHelper.ResolvePath(rootObject, path);

        // Assert
        parent.ShouldBeNull();
        property.ShouldBeNull();
        finalSegment.ShouldBeNull();
    }

    [Fact]
    public void ResolvePath_ShouldReturnNullsForIndexOutOfBounds()
    {
        // Arrange
        var path = "$.users[99]";

        // Act
        var (parent, property, finalSegment) = PocoPathHelper.ResolvePath(rootObject, path);

        // Assert
        parent.ShouldBeNull();
        property.ShouldBeNull();
        finalSegment.ShouldBeNull();
    }

    [Fact]
    public void ResolvePath_ShouldReturnNullsForInvalidIndexAccessOnObject()
    {
        // Arrange
        var path = "$.user[0]";

        // Act
        var (parent, property, finalSegment) = PocoPathHelper.ResolvePath(rootObject, path);

        // Assert
        parent.ShouldBeNull();
        property.ShouldBeNull();
        finalSegment.ShouldBeNull();
    }

    [Fact]
    public void ResolvePath_ShouldReturnNullsForEmptyPath()
    {
        // Arrange
        var path = "$";

        // Act
        var (parent, property, finalSegment) = PocoPathHelper.ResolvePath(rootObject, path);

        // Assert
        parent.ShouldBeNull();
        property.ShouldBeNull();
        finalSegment.ShouldBeNull();
    }

    [Fact]
    public void ConvertValue_ShouldReturnNull_WhenValueIsNull()
    {
        // Act
        var result = PocoPathHelper.ConvertValue(null, typeof(int));

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ConvertValue_ShouldReturnSameValue_WhenTypeIsCorrect()
    {
        // Arrange
        var value = "test";

        // Act
        var result = PocoPathHelper.ConvertValue(value, typeof(string));

        // Assert
        result.ShouldBe(value);
    }

    [Fact]
    public void ConvertValue_ShouldDeserializeJsonElementToObject()
    {
        // Arrange
        var user = new TestUser { Name = "Test", Age = 99 };
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var jsonElement = JsonSerializer.SerializeToElement(user, options);

        // Act
        var result = PocoPathHelper.ConvertValue(jsonElement, typeof(TestUser));

        // Assert
        result.ShouldBeOfType<TestUser>();
        var convertedUser = (TestUser)result;
        convertedUser.Name.ShouldBe(user.Name);
        convertedUser.Age.ShouldBe(user.Age);
    }

    [Fact]
    public void ConvertValue_ShouldDeserializeJsonElementToPrimitive()
    {
        // Arrange
        var jsonElement = JsonSerializer.SerializeToElement(123);

        // Act
        var result = PocoPathHelper.ConvertValue(jsonElement, typeof(int));

        // Assert
        result.ShouldBe(123);
    }

    [Fact]
    public void ConvertValue_ShouldChangeTypeForPrimitives()
    {
        // Arrange
        long value = 123L;

        // Act
        var result = PocoPathHelper.ConvertValue(value, typeof(int));

        // Assert
        result.ShouldBe(123);
    }

    [Fact]
    public void ConvertValue_ShouldHandleNullableTypes()
    {
        // Arrange
        int value = 42;

        // Act
        var result = PocoPathHelper.ConvertValue(value, typeof(int?));

        // Assert
        result.ShouldBe(42);
    }

    [Fact]
    public void ConvertValue_ShouldReturnOriginalValue_OnFailedConversion()
    {
        // Arrange
        var value = "not-a-number";

        // Act
        var result = PocoPathHelper.ConvertValue(value, typeof(int));

        // Assert
        result.ShouldBe(value);
    }

    private sealed class TestAddress
    {
        public string? Street { get; set; }
    }

    private sealed class TestUser
    {
        public string? Name { get; set; }
        public int Age { get; set; }
        public TestAddress? Address { get; set; }
    }

    private sealed class TestRoot
    {
        public string? SimpleProp { get; set; }
        public TestUser? User { get; set; }
        public List<TestUser>? Users { get; set; }
        public List<string>? Tags { get; set; }
        [JsonPropertyName("special-name")]
        public string? SpecialNameProp { get; set; }
    }
}