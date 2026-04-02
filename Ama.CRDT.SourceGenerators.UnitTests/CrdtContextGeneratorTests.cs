namespace Ama.CRDT.SourceGenerators.UnitTests;

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using Xunit;

public sealed class CrdtContextGeneratorTests
{
    [Fact]
    public void Generator_ShouldCreateAotContext_WhenAttributesArePresent()
    {
        // Arrange
        var source = @"
using System;
using System.Collections.Generic;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;

namespace TestNamespace
{
    public class User
    {
        public string Name { get; set; }
        public int Age { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
    }

    [CrdtSerializable(typeof(User))]
    public partial class MyAotContext : CrdtContext
    {
    }
}";

        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();

        // Add reference to Ama.CRDT
        references.Add(MetadataReference.CreateFromFile(typeof(Attributes.CrdtSerializableAttribute).Assembly.Location));

        var compilation = CSharpCompilation.Create(
            "TestCompilation",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new CrdtContextGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        // Act
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        // Assert
        diagnostics.ShouldBeEmpty();
        var runResult = driver.GetRunResult();
        runResult.GeneratedTrees.Length.ShouldBe(1);

        var generatedSyntax = runResult.GeneratedTrees[0].GetText().ToString();

        // Verify key generated elements
        generatedSyntax.ShouldContain("partial class MyAotContext");
        generatedSyntax.ShouldContain("typeof(global::TestNamespace.User)");
        generatedSyntax.ShouldContain("createInstance: () => new global::TestNamespace.User()");
        generatedSyntax.ShouldContain("[\"Name\"] = new global::Ama.CRDT.Models.Aot.CrdtPropertyInfo(");
        generatedSyntax.ShouldContain("getter: obj => ((global::TestNamespace.User)obj).Name");
        generatedSyntax.ShouldContain("setter: (obj, val) => ((global::TestNamespace.User)obj).Name = val == null ? default! : (string)val");
    }

    [Fact]
    public void Generator_ShouldNotGenerateSetter_ForInitOnlyProperties()
    {
        // Arrange
        var source = @"
using System;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;

namespace TestNamespace
{
    public class ResultRecord
    {
        public object Document { get; init; }
    }

    [CrdtSerializable(typeof(ResultRecord))]
    public partial class MyAotContext : CrdtContext
    {
    }
}";

        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();

        // Add reference to Ama.CRDT
        references.Add(MetadataReference.CreateFromFile(typeof(Attributes.CrdtSerializableAttribute).Assembly.Location));

        var compilation = CSharpCompilation.Create(
            "TestCompilation",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new CrdtContextGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        // Act
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        // Assert
        diagnostics.ShouldBeEmpty();
        var runResult = driver.GetRunResult();
        runResult.GeneratedTrees.Length.ShouldBe(1);

        var generatedSyntax = runResult.GeneratedTrees[0].GetText().ToString();

        // Verify that the init-only property generates a false canWrite flag and a null setter
        generatedSyntax.ShouldContain("[\"Document\"] = new global::Ama.CRDT.Models.Aot.CrdtPropertyInfo(");
        generatedSyntax.ShouldContain("canWrite: false");
        generatedSyntax.ShouldContain("setter: null");
        
        // Assert we did not mistakenly emit an assignment lambda for Document
        generatedSyntax.ShouldNotContain("setter: (obj, val) => ((global::TestNamespace.ResultRecord)obj).Document =");
    }

    [Fact]
    public void Generator_ShouldNotGenerateCreateInstance_WhenClassHasRequiredMembers()
    {
        // Arrange
        var source = @"
using System;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;

namespace TestNamespace
{
    public class TreeNode
    {
        public required string Id { get; set; }
    }

    [CrdtSerializable(typeof(TreeNode))]
    public partial class MyAotContext : CrdtContext
    {
    }
}";

        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();

        references.Add(MetadataReference.CreateFromFile(typeof(Attributes.CrdtSerializableAttribute).Assembly.Location));

        var compilation = CSharpCompilation.Create(
            "TestCompilation",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new CrdtContextGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        // Act
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        // Assert
        diagnostics.ShouldBeEmpty();
        var runResult = driver.GetRunResult();
        runResult.GeneratedTrees.Length.ShouldBe(1);

        var generatedSyntax = runResult.GeneratedTrees[0].GetText().ToString();

        // Verify that a class with a required member has a null createInstance method to avoid compile errors
        generatedSyntax.ShouldContain("createInstance: null");
        generatedSyntax.ShouldNotContain("createInstance: () => new global::TestNamespace.TreeNode()");
    }
}