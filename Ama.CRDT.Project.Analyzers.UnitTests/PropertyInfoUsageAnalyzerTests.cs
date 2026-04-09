namespace Ama.CRDT.Project.Analyzers.UnitTests;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using System.Threading.Tasks;
using Xunit;

public sealed class PropertyInfoUsageAnalyzerTests
{
    private static CSharpAnalyzerTest<PropertyInfoUsageAnalyzer, DefaultVerifier> CreateTest()
    {
        var test = new CSharpAnalyzerTest<PropertyInfoUsageAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };

        return test;
    }

    [Fact]
    public async Task WhenGetValueIsUsedOnPropertyInfo_ShouldReportDiagnostic()
    {
        var source = @"
using System.Reflection;

public class TestClass
{
    public void DoWork(PropertyInfo prop, object instance)
    {
        prop.GetValue(instance);
    }
}
";
        var expected = new DiagnosticResult("CRDTPROJ0001", DiagnosticSeverity.Error)
            .WithLocation(8, 9)
            .WithArguments("GetValue");

        var test = CreateTest();
        test.TestCode = source;
        test.ExpectedDiagnostics.Add(expected);
        await test.RunAsync();
    }

    [Fact]
    public async Task WhenSetValueIsUsedOnPropertyInfo_ShouldReportDiagnostic()
    {
        var source = @"
using System.Reflection;

public class TestClass
{
    public void DoWork(PropertyInfo prop, object instance)
    {
        prop.SetValue(instance, ""test"");
    }
}
";
        var expected = new DiagnosticResult("CRDTPROJ0001", DiagnosticSeverity.Error)
            .WithLocation(8, 9)
            .WithArguments("SetValue");

        var test = CreateTest();
        test.TestCode = source;
        test.ExpectedDiagnostics.Add(expected);
        await test.RunAsync();
    }

    [Fact]
    public async Task WhenPropertyInfoNameIsAccessed_ShouldNotReportDiagnostic()
    {
        var source = @"
using System.Reflection;

public class TestClass
{
    public void DoWork(PropertyInfo prop)
    {
        _ = prop.Name;
        _ = prop.PropertyType;
        _ = prop.DeclaringType;
    }
}
";
        // .Name, .PropertyType, and .DeclaringType are metadata access and completely AOT-safe
        var test = CreateTest();
        test.TestCode = source;
        await test.RunAsync();
    }

    [Fact]
    public async Task WhenGetPropertyIsUsedOnType_ShouldReportDiagnostic()
    {
        var source = @"
using System;

public class TestClass
{
    public void DoWork()
    {
        typeof(TestClass).GetProperty(""DoWork"");
    }
}
";
        var expected = new DiagnosticResult("CRDTPROJ0001", DiagnosticSeverity.Error)
            .WithLocation(8, 9)
            .WithArguments("GetProperty");

        var test = CreateTest();
        test.TestCode = source;
        test.ExpectedDiagnostics.Add(expected);
        await test.RunAsync();
    }

    [Fact]
    public async Task WhenActivatorCreateInstanceWithNoGeneric_ShouldReportDiagnostic()
    {
        var source = @"
using System;

public class TestClass
{
    public void DoWork()
    {
        Activator.CreateInstance(typeof(TestClass));
    }
}
";
        var expected = new DiagnosticResult("CRDTPROJ0001", DiagnosticSeverity.Error)
            .WithLocation(8, 9)
            .WithArguments("CreateInstance");

        var test = CreateTest();
        test.TestCode = source;
        test.ExpectedDiagnostics.Add(expected);
        await test.RunAsync();
    }

    [Fact]
    public async Task WhenActivatorCreateInstanceWithGeneric_ShouldNotReportDiagnostic()
    {
        var source = @"
using System;

public class TestClass
{
    public void DoWork()
    {
        Activator.CreateInstance<TestClass>();
    }
}
";
        var test = CreateTest();
        test.TestCode = source;
        await test.RunAsync();
    }

    [Fact]
    public async Task WhenGetValueIsUsedOnDictionary_ShouldNotReportDiagnostic()
    {
        var source = @"
using System.Collections.Generic;

public class TestClass
{
    public void DoWork(Dictionary<string, string> dict)
    {
        dict.GetValueOrDefault(""key"");
    }
}
";
        var test = CreateTest();
        test.TestCode = source;
        await test.RunAsync();
    }

    [Fact]
    public async Task WhenSetValueIsNotReflection_ShouldNotReportDiagnostic()
    {
        var source = @"
public class MockProperty
{
    public void SetValue(object instance, object value) { }
}

public class TestClass
{
    public void DoWork(MockProperty prop, object instance)
    {
        prop.SetValue(instance, ""test"");
    }
}
";
        var test = CreateTest();
        test.TestCode = source;
        await test.RunAsync();
    }
}