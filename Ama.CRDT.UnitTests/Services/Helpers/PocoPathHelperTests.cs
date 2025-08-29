namespace Ama.CRDT.UnitTests.Services.Helpers;

using System.Collections.Generic;
using Ama.CRDT.Services.Helpers;
using Shouldly;
using Xunit;
using System.Collections;

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
            Tags = ["tag1", "tag2"],
            Scores = new[] { 100, 200 },
            Settings = new Dictionary<string, string> { { "theme", "dark" } },
            UntypedList = ["one", 2]
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
    public void ParsePath_ShouldReturnCorrectSegments(string? path, string[] expected)
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
    public void ResolvePath_ShouldReturnParentForIndexOutOfBounds()
    {
        // Arrange
        var path = "$.users[99]";

        // Act
        var (parent, property, finalSegment) = PocoPathHelper.ResolvePath(rootObject, path);

        // Assert
        parent.ShouldNotBeNull();
        property.ShouldNotBeNull();
        finalSegment.ShouldNotBeNull();
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

    [Fact]
    public void GetValue_ShouldReturnSimplePropertyValue()
    {
        // Act
        var value = PocoPathHelper.GetValue(rootObject, "$.simpleProp");

        // Assert
        value.ShouldBe("value");
    }

    [Fact]
    public void GetValue_ShouldReturnNestedPropertyValue()
    {
        // Act
        var value = PocoPathHelper.GetValue(rootObject, "$.user.name");

        // Assert
        value.ShouldBe("John");
    }

    [Fact]
    public void GetValue_ShouldReturnArrayElementObject()
    {
        // Act
        var value = PocoPathHelper.GetValue(rootObject, "$.users[1]");

        // Assert
        value.ShouldBe(rootObject.Users[1]);
        ((TestUser)value!).Name.ShouldBe("Bob");
    }

    [Fact]
    public void GetValue_ShouldReturnArrayElementPrimitive()
    {
        // Act
        var value = PocoPathHelper.GetValue(rootObject, "$.tags[0]");

        // Assert
        value.ShouldBe("tag1");
    }

    [Fact]
    public void GetValue_ShouldReturnNullForInvalidProperty()
    {
        // Act
        var value = PocoPathHelper.GetValue(rootObject, "$.user.invalidProp");

        // Assert
        value.ShouldBeNull();
    }

    [Fact]
    public void GetValue_ShouldReturnNullForIndexOutOfBounds()
    {
        // Act
        var value = PocoPathHelper.GetValue(rootObject, "$.users[99]");

        // Assert
        value.ShouldBeNull();
    }

    [Fact]
    public void SetValue_ShouldSetSimplePropertyValue()
    {
        // Arrange
        var path = "$.simpleProp";
        var newValue = "new value";

        // Act
        var success = PocoPathHelper.SetValue(rootObject, path, newValue);

        // Assert
        success.ShouldBeTrue();
        rootObject.SimpleProp.ShouldBe(newValue);
    }

    [Fact]
    public void SetValue_ShouldSetNestedPropertyValue()
    {
        // Arrange
        var path = "$.user.age";
        var newValue = 35;

        // Act
        var success = PocoPathHelper.SetValue(rootObject, path, newValue);

        // Assert
        success.ShouldBeTrue();
        rootObject.User.Age.ShouldBe(newValue);
    }

    [Fact]
    public void SetValue_ShouldSetArrayElementValue()
    {
        // Arrange
        var path = "$.tags[1]";
        var newValue = "new-tag";

        // Act
        var success = PocoPathHelper.SetValue(rootObject, path, newValue);

        // Assert
        success.ShouldBeTrue();
        rootObject.Tags[1].ShouldBe(newValue);
    }

    [Fact]
    public void SetValue_ShouldConvertValueWhenSetting()
    {
        // Arrange
        var path = "$.user.age";
        var newValue = "40"; // string

        // Act
        var success = PocoPathHelper.SetValue(rootObject, path, newValue);

        // Assert
        success.ShouldBeTrue();
        rootObject.User.Age.ShouldBe(40); // should be converted to int
    }

    [Fact]
    public void SetValue_ShouldReturnFalseForInvalidPath()
    {
        // Arrange
        var path = "$.invalid.path";
        var newValue = "test";

        // Act
        var success = PocoPathHelper.SetValue(rootObject, path, newValue);

        // Assert
        success.ShouldBeFalse();
    }

    [Fact]
    public void SetValue_ShouldReturnFalseForReadOnlyProperty()
    {
        // Arrange
        var obj = new TestRootWithReadOnly("initial");
        var path = "$.readOnlyProp";
        var newValue = "new";

        // Act
        var success = PocoPathHelper.SetValue(obj, path, newValue);

        // Assert
        success.ShouldBeFalse();
        obj.ReadOnlyProp.ShouldBe("initial");
    }

    [Fact]
    public void GetCollectionElementType_ShouldReturnCorrectTypeForGenericList()
    {
        // Arrange
        var property = typeof(TestRoot).GetProperty(nameof(TestRoot.Users));

        // Act
        var elementType = PocoPathHelper.GetCollectionElementType(property!);

        // Assert
        elementType.ShouldBe(typeof(TestUser));
    }

    [Fact]
    public void GetCollectionElementType_ShouldReturnCorrectTypeForArray()
    {
        // Arrange
        var property = typeof(TestRoot).GetProperty(nameof(TestRoot.Scores));

        // Act
        var elementType = PocoPathHelper.GetCollectionElementType(property!);

        // Assert
        elementType.ShouldBe(typeof(int));
    }

    [Fact]
    public void GetCollectionElementType_ShouldReturnObjectTypeForNonGenericList()
    {
        // Arrange
        var property = typeof(TestRoot).GetProperty(nameof(TestRoot.UntypedList));

        // Act
        var elementType = PocoPathHelper.GetCollectionElementType(property!);

        // Assert
        elementType.ShouldBe(typeof(object));
    }

    [Fact]
    public void GetDictionaryKeyType_ShouldReturnCorrectType()
    {
        // Arrange
        var property = typeof(TestRoot).GetProperty(nameof(TestRoot.Settings));

        // Act
        var keyType = PocoPathHelper.GetDictionaryKeyType(property!);

        // Assert
        keyType.ShouldBe(typeof(string));
    }

    [Fact]
    public void GetDictionaryValueType_ShouldReturnCorrectType()
    {
        // Arrange
        var property = typeof(TestRoot).GetProperty(nameof(TestRoot.Settings));

        // Act
        var valueType = PocoPathHelper.GetDictionaryValueType(property!);

        // Assert
        valueType.ShouldBe(typeof(string));
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

    private sealed class TestRootWithReadOnly
    {
        public string? ReadOnlyProp { get; }

        public TestRootWithReadOnly(string? readOnlyProp)
        {
            ReadOnlyProp = readOnlyProp;
        }
    }

    private sealed class TestRoot
    {
        public string? SimpleProp { get; set; }
        public TestUser? User { get; set; }
        public List<TestUser>? Users { get; set; }
        public List<string>? Tags { get; set; }
        public string? SpecialNameProp { get; set; }
        public int[]? Scores { get; set; }
        public IDictionary<string, string>? Settings { get; set; }
        public ArrayList? UntypedList { get; set; }
    }
}