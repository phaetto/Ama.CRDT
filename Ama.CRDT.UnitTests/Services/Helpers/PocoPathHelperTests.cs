namespace Ama.CRDT.UnitTests.Services.Helpers;

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Services.Helpers;
using Shouldly;
using Xunit;

public sealed class PocoPathHelperTests
{
    private readonly IEnumerable<CrdtContext> _aotContexts;
    private readonly TestRoot rootObject;

    public PocoPathHelperTests()
    {
        // Supply the Source Generator context specific to these test models
        _aotContexts = [new HelpersTestCrdtContext()];
        
        rootObject = new TestRoot
        {
            Id = Guid.Parse("12345678-1234-1234-1234-123456789012"),
            Status = TestStatus.Active,
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
            UniqueTags = new HashSet<string> { "unique1" },
            Scores = [100, 200],
            Settings = new Dictionary<string, string> { { "theme", "dark" } },
            UsersMap = new Dictionary<string, TestUser> 
            { 
                { "u1", new TestUser { Name = "Charlie", Age = 40 } } 
            }
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
        var result = PocoPathHelper.ParsePath(path!);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void ResolvePath_ShouldResolveSimpleProperty()
    {
        // Arrange
        var path = "$.simpleProp";

        // Act
        var (parent, property, finalSegment) = PocoPathHelper.ResolvePath(rootObject, path, _aotContexts);

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
        var (parent, property, finalSegment) = PocoPathHelper.ResolvePath(rootObject, path, _aotContexts);

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
        var (parent, property, finalSegment) = PocoPathHelper.ResolvePath(rootObject, path, _aotContexts);

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
        var (parent, property, finalSegment) = PocoPathHelper.ResolvePath(rootObject, path, _aotContexts);

        // Assert
        parent.ShouldBe(rootObject.Users![0]);
        property.ShouldNotBeNull();
        property.Name.ShouldBe(nameof(TestUser.Age));
        finalSegment.ShouldBe("age");
    }

    [Fact]
    public void ResolvePath_ShouldResolveDictionaryItself()
    {
        // Arrange
        var path = "$.settings";

        // Act
        var (parent, property, finalSegment) = PocoPathHelper.ResolvePath(rootObject, path, _aotContexts);

        // Assert
        parent.ShouldBe(rootObject);
        property.ShouldNotBeNull();
        property.Name.ShouldBe(nameof(TestRoot.Settings));
        finalSegment.ShouldBe("settings");
    }

    [Fact]
    public void ResolvePath_ShouldResolveDictionaryElement()
    {
        // Arrange
        var path = "$.settings['theme']";

        // Act
        var (parent, property, finalSegment) = PocoPathHelper.ResolvePath(rootObject, path, _aotContexts);

        // Assert
        parent.ShouldBe(rootObject);
        property.ShouldNotBeNull();
        property.Name.ShouldBe(nameof(TestRoot.Settings));
        finalSegment.ShouldBe(new PocoPathHelper.DictionaryKeyPathSegment("theme"));
    }

    [Fact]
    public void ResolvePath_ShouldResolvePropertyOfDictionaryElement()
    {
        // Arrange
        var path = "$.usersMap['u1'].name";

        // Act
        var (parent, property, finalSegment) = PocoPathHelper.ResolvePath(rootObject, path, _aotContexts);

        // Assert
        parent.ShouldBe(rootObject.UsersMap!["u1"]);
        property.ShouldNotBeNull();
        property.Name.ShouldBe(nameof(TestUser.Name));
        finalSegment.ShouldBe("name");
    }

    [Fact]
    public void ResolvePath_ShouldHandlePascalCasePropertyName()
    {
        // Arrange
        var path = "$.SimpleProp";

        // Act
        var (parent, property, finalSegment) = PocoPathHelper.ResolvePath(rootObject, path, _aotContexts);

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
        var (parent, property, finalSegment) = PocoPathHelper.ResolvePath(rootObject, path, _aotContexts);

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
        var (parent, property, finalSegment) = PocoPathHelper.ResolvePath(rootObject, path, _aotContexts);

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
        var (parent, property, finalSegment) = PocoPathHelper.ResolvePath(rootObject, path, _aotContexts);

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
        var (parent, property, finalSegment) = PocoPathHelper.ResolvePath(rootObject, path, _aotContexts);

        // Assert
        parent.ShouldBeNull();
        property.ShouldBeNull();
        finalSegment.ShouldBeNull();
    }

    [Fact]
    public void ConvertValue_ShouldReturnNull_WhenValueIsNull()
    {
        // Act
        var result = PocoPathHelper.ConvertValue(null, typeof(int), _aotContexts);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ConvertValue_ShouldReturnSameValue_WhenTypeIsCorrect()
    {
        // Arrange
        var value = "test";

        // Act
        var result = PocoPathHelper.ConvertValue(value, typeof(string), _aotContexts);

        // Assert
        result.ShouldBe(value);
    }

    [Fact]
    public void ConvertValue_ShouldChangeTypeForPrimitives()
    {
        // Arrange
        long value = 123L;

        // Act
        var result = PocoPathHelper.ConvertValue(value, typeof(int), _aotContexts);

        // Assert
        result.ShouldBe(123);
    }

    [Fact]
    public void ConvertValue_ShouldHandleNullableTypes()
    {
        // Arrange
        int value = 42;

        // Act
        var result = PocoPathHelper.ConvertValue(value, typeof(int?), _aotContexts);

        // Assert
        result.ShouldBe(42);
    }

    [Fact]
    public void ConvertValue_ShouldReturnOriginalValue_OnFailedConversion()
    {
        // Arrange
        var value = "not-a-number";

        // Act
        var result = PocoPathHelper.ConvertValue(value, typeof(int), _aotContexts);

        // Assert
        result.ShouldBe(value);
    }

    [Fact]
    public void ConvertValue_ShouldParseEnum()
    {
        // Arrange
        var value = "Pending";

        // Act
        var result = PocoPathHelper.ConvertValue(value, typeof(TestStatus), _aotContexts);

        // Assert
        result.ShouldBe(TestStatus.Pending);
    }

    [Fact]
    public void ConvertValue_ShouldParseGuid()
    {
        // Arrange
        var guidString = "12345678-1234-1234-1234-123456789012";

        // Act
        var result = PocoPathHelper.ConvertValue(guidString, typeof(Guid), _aotContexts);

        // Assert
        result.ShouldBe(Guid.Parse(guidString));
    }

    [Fact]
    public void ConvertValue_ShouldCreateObject_FromDictionary()
    {
        // Arrange
        var dict = new Dictionary<string, object>
        {
            { "Name", "CreatedUser" },
            { "Age", 99 }
        };

        // Act
        var result = PocoPathHelper.ConvertValue(dict, typeof(TestUser), _aotContexts);

        // Assert
        var user = result.ShouldBeOfType<TestUser>();
        user.Name.ShouldBe("CreatedUser");
        user.Age.ShouldBe(99);
    }

    [Fact]
    public void ConvertValue_ShouldCreateKeyValuePair_FromDictionary()
    {
        // Arrange
        var dict = new Dictionary<string, object>
        {
            { "Key", "myKey" },
            { "Value", 100 }
        };

        // Act
        var result = PocoPathHelper.ConvertValue(dict, typeof(KeyValuePair<string, int>), _aotContexts);

        // Assert
        var kvp = result.ShouldBeOfType<KeyValuePair<string, int>>();
        kvp.Key.ShouldBe("myKey");
        kvp.Value.ShouldBe(100);
    }

    [Fact]
    public void GetValue_ShouldReturnSimplePropertyValue()
    {
        // Act
        var value = PocoPathHelper.GetValue(rootObject, "$.simpleProp", _aotContexts);

        // Assert
        value.ShouldBe("value");
    }

    [Fact]
    public void GetValue_ShouldReturnNestedPropertyValue()
    {
        // Act
        var value = PocoPathHelper.GetValue(rootObject, "$.user.name", _aotContexts);

        // Assert
        value.ShouldBe("John");
    }

    [Fact]
    public void GetValue_ShouldReturnArrayElementObject()
    {
        // Act
        var value = PocoPathHelper.GetValue(rootObject, "$.users[1]", _aotContexts);

        // Assert
        value.ShouldBe(rootObject.Users![1]);
        ((TestUser)value!).Name.ShouldBe("Bob");
    }

    [Fact]
    public void GetValue_ShouldReturnArrayElementPrimitive()
    {
        // Act
        var value = PocoPathHelper.GetValue(rootObject, "$.tags[0]", _aotContexts);

        // Assert
        value.ShouldBe("tag1");
    }

    [Fact]
    public void GetValue_ShouldReturnDictionaryItself()
    {
        // Act
        var value = PocoPathHelper.GetValue(rootObject, "$.settings", _aotContexts);

        // Assert
        value.ShouldBe(rootObject.Settings);
        var dict = value.ShouldBeAssignableTo<IDictionary<string, string>>();
        dict["theme"].ShouldBe("dark");
    }

    [Fact]
    public void GetValue_ShouldReturnDictionaryElementValue()
    {
        // Act
        var value = PocoPathHelper.GetValue(rootObject, "$.settings['theme']", _aotContexts);

        // Assert
        value.ShouldBe("dark");
    }

    [Fact]
    public void GetValue_ShouldReturnPropertyOfDictionaryElement()
    {
        // Act
        var value = PocoPathHelper.GetValue(rootObject, "$.usersMap['u1'].name", _aotContexts);

        // Assert
        value.ShouldBe("Charlie");
    }

    [Fact]
    public void GetValue_ShouldReturnNullForInvalidProperty()
    {
        // Act
        var value = PocoPathHelper.GetValue(rootObject, "$.user.invalidProp", _aotContexts);

        // Assert
        value.ShouldBeNull();
    }

    [Fact]
    public void GetValue_ShouldReturnNullForIndexOutOfBounds()
    {
        // Act
        var value = PocoPathHelper.GetValue(rootObject, "$.users[99]", _aotContexts);

        // Assert
        value.ShouldBeNull();
    }

    [Fact]
    public void GetValue_ShouldReturnNullForInvalidDictionaryKey()
    {
        // Act
        var value = PocoPathHelper.GetValue(rootObject, "$.settings['missing-key']", _aotContexts);

        // Assert
        value.ShouldBeNull();
    }

    [Fact]
    public void GetValue_Generic_ShouldReturnSimpleProperty()
    {
        // Act
        var value = PocoPathHelper.GetValue<string>(rootObject, "$.simpleProp", _aotContexts);

        // Assert
        value.ShouldBe("value");
    }

    [Fact]
    public void GetValue_Generic_ShouldReturnArrayElement()
    {
        // Act
        var value = PocoPathHelper.GetValue<string>(rootObject, "$.tags[0]", _aotContexts);

        // Assert
        value.ShouldBe("tag1");
    }

    [Fact]
    public void GetValue_Generic_ShouldReturnDictionaryItself()
    {
        // Act
        var value = PocoPathHelper.GetValue<IDictionary<string, string>>(rootObject, "$.settings", _aotContexts);

        // Assert
        value.ShouldBe(rootObject.Settings);
        value!["theme"].ShouldBe("dark");
    }

    [Fact]
    public void GetValue_Generic_ShouldReturnDictionaryElement()
    {
        // Act
        var value = PocoPathHelper.GetValue<string>(rootObject, "$.settings['theme']", _aotContexts);

        // Assert
        value.ShouldBe("dark");
    }

    [Fact]
    public void GetValue_Generic_ShouldReturnPropertyOfDictionaryElement()
    {
        // Act
        var value = PocoPathHelper.GetValue<int>(rootObject, "$.usersMap['u1'].age", _aotContexts);

        // Assert
        value.ShouldBe(40);
    }

    [Fact]
    public void GetValue_Generic_ShouldReturnEnumProperty()
    {
        // Act
        var value = PocoPathHelper.GetValue<TestStatus>(rootObject, "$.status", _aotContexts);

        // Assert
        value.ShouldBe(TestStatus.Active);
    }

    [Fact]
    public void GetValue_Generic_ShouldReturnDefault_OnInvalidPath()
    {
        // Act
        var value = PocoPathHelper.GetValue<int>(rootObject, "$.invalidProp", _aotContexts);

        // Assert
        value.ShouldBe(0);
    }

    [Fact]
    public void SetValue_ShouldSetSimplePropertyValue()
    {
        // Arrange
        var path = "$.simpleProp";
        var newValue = "new value";

        // Act
        var success = PocoPathHelper.SetValue(rootObject, path, newValue, _aotContexts);

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
        var success = PocoPathHelper.SetValue(rootObject, path, newValue, _aotContexts);

        // Assert
        success.ShouldBeTrue();
        rootObject.User!.Age.ShouldBe(newValue);
    }

    [Fact]
    public void SetValue_ShouldSetArrayElementValue()
    {
        // Arrange
        var path = "$.tags[1]";
        var newValue = "new-tag";

        // Act
        var success = PocoPathHelper.SetValue(rootObject, path, newValue, _aotContexts);

        // Assert
        success.ShouldBeTrue();
        rootObject.Tags![1].ShouldBe(newValue);
    }

    [Fact]
    public void SetValue_ShouldSetDictionaryItself()
    {
        // Arrange
        var path = "$.settings";
        var newDict = new Dictionary<string, string> { { "theme", "light" }, { "mode", "auto" } };

        // Act
        var success = PocoPathHelper.SetValue(rootObject, path, newDict, _aotContexts);

        // Assert
        success.ShouldBeTrue();
        rootObject.Settings.ShouldBe(newDict);
        rootObject.Settings!["mode"].ShouldBe("auto");
    }

    [Fact]
    public void SetValue_ShouldSetDictionaryElementValue()
    {
        // Arrange
        var path = "$.settings['theme']";
        var newValue = "light";

        // Act
        var success = PocoPathHelper.SetValue(rootObject, path, newValue, _aotContexts);

        // Assert
        success.ShouldBeTrue();
        rootObject.Settings!["theme"].ShouldBe(newValue);
    }

    [Fact]
    public void SetValue_ShouldCreateDictionaryElement_IfMissing()
    {
        // Arrange
        var path = "$.settings['newKey']";
        var newValue = "newVal";

        // Act
        var success = PocoPathHelper.SetValue(rootObject, path, newValue, _aotContexts);

        // Assert
        success.ShouldBeTrue();
        rootObject.Settings!["newKey"].ShouldBe(newValue);
    }

    [Fact]
    public void SetValue_ShouldSetNestedPropertyOfDictionaryElement()
    {
        // Arrange
        var path = "$.usersMap['u1'].age";
        var newValue = 55;

        // Act
        var success = PocoPathHelper.SetValue(rootObject, path, newValue, _aotContexts);

        // Assert
        success.ShouldBeTrue();
        rootObject.UsersMap!["u1"].Age.ShouldBe(newValue);
    }

    [Fact]
    public void SetValue_ShouldConvertValueWhenSetting()
    {
        // Arrange
        var path = "$.user.age";
        var newValue = "40"; // string

        // Act
        var success = PocoPathHelper.SetValue(rootObject, path, newValue, _aotContexts);

        // Assert
        success.ShouldBeTrue();
        rootObject.User!.Age.ShouldBe(40); // should be converted to int
    }

    [Fact]
    public void SetValue_ShouldReturnFalseForInvalidPath()
    {
        // Arrange
        var path = "$.invalid.path";
        var newValue = "test";

        // Act
        var success = PocoPathHelper.SetValue(rootObject, path, newValue, _aotContexts);

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
        var success = PocoPathHelper.SetValue(obj, path, newValue, _aotContexts);

        // Assert
        success.ShouldBeFalse();
        obj.ReadOnlyProp.ShouldBe("initial");
    }

    [Fact]
    public void SetValue_Generic_ShouldSetSimpleProperty()
    {
        // Arrange
        var path = "$.simpleProp";
        var newValue = "new generic value";

        // Act
        var success = PocoPathHelper.SetValue(rootObject, path, newValue, _aotContexts);

        // Assert
        success.ShouldBeTrue();
        rootObject.SimpleProp.ShouldBe(newValue);
    }

    [Fact]
    public void SetValue_Generic_ShouldSetArrayElement()
    {
        // Arrange
        var path = "$.tags[0]";
        var newValue = "updated-tag";

        // Act
        var success = PocoPathHelper.SetValue(rootObject, path, newValue, _aotContexts);

        // Assert
        success.ShouldBeTrue();
        rootObject.Tags![0].ShouldBe(newValue);
    }

    [Fact]
    public void SetValue_Generic_ShouldSetDictionaryItself()
    {
        // Arrange
        var path = "$.settings";
        var newDict = new Dictionary<string, string> { { "font", "arial" } };

        // Act
        var success = PocoPathHelper.SetValue(rootObject, path, newDict, _aotContexts);

        // Assert
        success.ShouldBeTrue();
        rootObject.Settings.ShouldBe(newDict);
        rootObject.Settings!["font"].ShouldBe("arial");
    }

    [Fact]
    public void SetValue_Generic_ShouldSetDictionaryElement()
    {
        // Arrange
        var path = "$.settings['theme']";
        var newValue = "blue";

        // Act
        var success = PocoPathHelper.SetValue(rootObject, path, newValue, _aotContexts);

        // Assert
        success.ShouldBeTrue();
        rootObject.Settings!["theme"].ShouldBe(newValue);
    }

    [Fact]
    public void ConvertTo_ShouldReturnDefault_WhenNull()
    {
        // Act
        var result = PocoPathHelper.ConvertTo<string>(null, _aotContexts);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ConvertTo_ShouldCastDirectly_WhenTypesMatch()
    {
        // Arrange
        object val = 100;

        // Act
        var result = PocoPathHelper.ConvertTo<int>(val, _aotContexts);

        // Assert
        result.ShouldBe(100);
    }

    [Fact]
    public void ConvertTo_ShouldConvert_WhenTypesDiffer()
    {
        // Arrange
        object val = "100";

        // Act
        var result = PocoPathHelper.ConvertTo<int>(val, _aotContexts);

        // Assert
        result.ShouldBe(100);
    }

    [Fact]
    public void GetTypeInfo_ShouldIdentifyCollectionAndElementType()
    {
        // Act
        var typeInfo = PocoPathHelper.GetTypeInfo(typeof(List<TestUser>), _aotContexts);

        // Assert
        typeInfo.IsCollection.ShouldBeTrue();
        typeInfo.CollectionElementType.ShouldBe(typeof(TestUser));
    }

    [Fact]
    public void GetTypeInfo_ShouldIdentifyDictionaryAndKeyTypes()
    {
        // Act
        var typeInfo = PocoPathHelper.GetTypeInfo(typeof(Dictionary<string, TestUser>), _aotContexts);

        // Assert
        typeInfo.IsDictionary.ShouldBeTrue();
        typeInfo.DictionaryKeyType.ShouldBe(typeof(string));
        typeInfo.DictionaryValueType.ShouldBe(typeof(TestUser));
    }

    [Fact]
    public void InstantiateCollection_ShouldCreateList_ForIEnumerable()
    {
        // Arrange
        var propType = typeof(IEnumerable<string>);

        // Act
        var collection = PocoPathHelper.InstantiateCollection(propType, _aotContexts);

        // Assert
        collection.ShouldBeOfType<List<string>>();
    }

    [Fact]
    public void InstantiateCollection_ShouldCreateHashSet_ForISet()
    {
        // Arrange
        var propType = typeof(ISet<string>);

        // Act
        var collection = PocoPathHelper.InstantiateCollection(propType, _aotContexts);

        // Assert
        collection.ShouldBeOfType<HashSet<string>>();
    }

    [Fact]
    public void InstantiateCollection_ShouldCreateConcreteList_ForConcreteList()
    {
        // Arrange
        var propType = typeof(List<int>);

        // Act
        var collection = PocoPathHelper.InstantiateCollection(propType, _aotContexts);

        // Assert
        collection.ShouldBeOfType<List<int>>();
    }

    [Fact]
    public void AddToCollection_ShouldAddItemToList()
    {
        // Arrange
        var list = new List<string> { "a" };

        // Act
        PocoPathHelper.AddToCollection(list, "b", _aotContexts);

        // Assert
        list.ShouldContain("a");
        list.ShouldContain("b");
        list.Count.ShouldBe(2);
    }

    [Fact]
    public void AddToCollection_ShouldConvertAndAddItemToList()
    {
        // Arrange
        var list = new List<int> { 1 };

        // Act
        PocoPathHelper.AddToCollection(list, "2", _aotContexts);

        // Assert
        list.ShouldContain(1);
        list.ShouldContain(2);
        list.Count.ShouldBe(2);
    }

    [Fact]
    public void RemoveFromCollection_ShouldRemoveItem()
    {
        // Arrange
        var list = new List<string> { "a", "b", "c" };

        // Act
        PocoPathHelper.RemoveFromCollection(list, "b", _aotContexts);

        // Assert
        list.ShouldNotContain("b");
        list.Count.ShouldBe(2);
    }

    [Fact]
    public void ClearCollection_ShouldEmptyTheCollection()
    {
        // Arrange
        var list = new List<string> { "a", "b", "c" };

        // Act
        PocoPathHelper.ClearCollection(list, _aotContexts);

        // Assert
        list.ShouldBeEmpty();
    }

    [Fact]
    public void ParseExpression_ShouldReturnCorrectPath_ForSimpleProperty()
    {
        // Arrange
        Expression<Func<TestRoot, string>> expression = x => x.SimpleProp!;

        // Act
        var result = PocoPathHelper.ParseExpression(expression, _aotContexts);

        // Assert
        result.JsonPath.ShouldBe("$.simpleProp");
        result.Property.Name.ShouldBe(nameof(TestRoot.SimpleProp));
    }

    [Fact]
    public void ParseExpression_ShouldReturnCorrectPath_ForNestedProperty()
    {
        // Arrange
        Expression<Func<TestRoot, int>> expression = x => x.User!.Age;

        // Act
        var result = PocoPathHelper.ParseExpression(expression, _aotContexts);

        // Assert
        result.JsonPath.ShouldBe("$.user.age");
        result.Property.Name.ShouldBe(nameof(TestUser.Age));
    }

    [Fact]
    public void ParseExpression_ShouldReturnCorrectPath_ForPropertyThroughArrayIndex()
    {
        // Arrange
        Expression<Func<TestRoot, int>> expression = x => x.Users![0].Age;

        // Act
        var result = PocoPathHelper.ParseExpression(expression, _aotContexts);

        // Assert
        result.JsonPath.ShouldBe("$.users[0].age");
        result.Property.Name.ShouldBe(nameof(TestUser.Age));
    }

    [Fact]
    public void ParseExpression_ShouldThrow_WhenExpressionEndsInArrayIndex()
    {
        // Arrange
        Expression<Func<TestRoot, int>> expression = x => x.Scores![0];

        // Act & Assert
        var ex = Should.Throw<ArgumentException>(() => PocoPathHelper.ParseExpression(expression, _aotContexts));
        ex.Message.ShouldContain("Expression must end in a property access");
    }

    [Fact]
    public void ParseExpression_ShouldThrow_WhenExpressionEndsInStringIndex()
    {
        // Arrange
        Expression<Func<TestRoot, string>> expression = x => x.Settings!["theme"];

        // Act & Assert
        var ex = Should.Throw<ArgumentException>(() => PocoPathHelper.ParseExpression(expression, _aotContexts));
        ex.Message.ShouldContain("Expression must end in a property access");
    }

    [Fact]
    public void ParseExpression_ShouldThrow_WhenExpressionIsInvalid()
    {
        // Arrange
        Expression<Func<TestRoot, string>> expression = x => x.ToString()!;

        // Act & Assert
        Should.Throw<ArgumentException>(() => PocoPathHelper.ParseExpression(expression, _aotContexts));
    }

    [Fact]
    public void IsCollection_ShouldReturnTrueForList()
    {
        // Act
        var result = PocoPathHelper.IsCollection(typeof(List<string>));

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsCollection_ShouldReturnFalseForString()
    {
        // Act
        var result = PocoPathHelper.IsCollection(typeof(string));

        // Assert
        result.ShouldBeFalse();
    }
}