namespace Ama.CRDT.UnitTests.Services.Helpers;

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Ama.CRDT.Services.Helpers;
using Shouldly;
using Xunit;

public sealed class ExpressionToJsonPathConverterTests
{
    private readonly TestRoot testInstance = new()
    {
        TopLevelProperty = "root-prop",
        NestedObject = new TestNested { Name = "nested-name", Value = 123 },
        SimpleList = ["one", "two", "three"],
        ComplexList =
        [
            new TestNested { Name = "item0", Value = 0 },
            new TestNested { Name = "item1", Value = 1 }
        ],
        SimpleArray = [10, 20, 30],
        ComplexArray =
        [
            new TestNested { Name = "arr-item0", Value = 100 },
            new TestNested { Name = "arr-item1", Value = 200 }
        ]
    };

    [Theory]
    [MemberData(nameof(PathConversionAndResolutionCases))]
    public void Convert_ShouldProduceValidAndResolvableJsonPath(Expression<Func<TestRoot, object>> expression, string expectedPath, Type expectedParentType, string expectedPropertyName, object expectedFinalSegment)
    {
        // Act
        var jsonPath = ExpressionToJsonPathConverter.Convert(expression);
        var (parent, property, finalSegment) = PocoPathHelper.ResolvePath(testInstance, jsonPath);

        // Assert
        jsonPath.ShouldBe(expectedPath);
        
        parent.ShouldNotBeNull();
        parent.ShouldBeOfType(expectedParentType);
        
        property.ShouldNotBeNull();
        property.Name.ShouldBe(expectedPropertyName);

        finalSegment.ShouldNotBeNull();
        finalSegment.ShouldBe(expectedFinalSegment);
    }

    [Fact]
    public void Convert_WithVariableIndexer_ShouldProduceValidAndResolvableJsonPath()
    {
        // Arrange
        var index = 1;
        Expression<Func<TestRoot, object>> expression = root => root.ComplexList[index].Name;
        const string expectedPath = "$.complexList[1].name";

        // Act
        var jsonPath = ExpressionToJsonPathConverter.Convert(expression);
        var (parent, property, finalSegment) = PocoPathHelper.ResolvePath(testInstance, jsonPath);

        // Assert
        jsonPath.ShouldBe(expectedPath);

        parent.ShouldNotBeNull();
        parent.ShouldBeOfType<TestNested>();
        ((TestNested)parent).Name.ShouldBe("item1");

        property.ShouldNotBeNull();
        property.Name.ShouldBe(nameof(TestNested.Name));

        finalSegment.ShouldNotBeNull();
        finalSegment.ShouldBe("name");
    }

    public static IEnumerable<object[]> PathConversionAndResolutionCases()
    {
        // Simple top-level property
        yield return
        [
            (Expression<Func<TestRoot, object>>)(root => root.TopLevelProperty),
            "$.topLevelProperty",
            typeof(TestRoot),
            nameof(TestRoot.TopLevelProperty),
            "topLevelProperty"
        ];
        
        // Nested property
        yield return
        [
            (Expression<Func<TestRoot, object>>)(root => root.NestedObject.Name),
            "$.nestedObject.name",
            typeof(TestNested),
            nameof(TestNested.Name),
            "name"
        ];
        
        // Simple list indexer
        yield return
        [
            (Expression<Func<TestRoot, object>>)(root => root.SimpleList[2]),
            "$.simpleList[2]",
            typeof(TestRoot),
            nameof(TestRoot.SimpleList),
            2
        ];

        // Simple array indexer
        yield return
        [
            (Expression<Func<TestRoot, object>>)(root => root.SimpleArray[0]),
            "$.simpleArray[0]",
            typeof(TestRoot),
            nameof(TestRoot.SimpleArray),
            0
        ];

        // Complex list property access
        yield return
        [
            (Expression<Func<TestRoot, object>>)(root => root.ComplexList[0].Value),
            "$.complexList[0].value",
            typeof(TestNested),
            nameof(TestNested.Value),
            "value"
        ];

        // Complex array property access
        yield return
        [
            (Expression<Func<TestRoot, object>>)(root => root.ComplexArray[1].Name),
            "$.complexArray[1].name",
            typeof(TestNested),
            nameof(TestNested.Name),
            "name"
        ];
    }
}