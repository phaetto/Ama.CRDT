namespace Ama.CRDT.Project.Analyzers.UnitTests;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using System.Threading.Tasks;
using Xunit;

public sealed class SystemConvertUsageAnalyzerTests
{
    [Fact]
    public async Task WhenConvertToInt32IsUsed_ShouldReportDiagnostic()
    {
        var source = @"
using System;

public class TestClass
{
    public void DoWork()
    {
        var value = Convert.ToInt32(""123"");
    }
}
";
        var expected = new DiagnosticResult("CRDTPROJ0002", DiagnosticSeverity.Error)
            .WithLocation(8, 21)
            .WithArguments("ToInt32");

        var test = CreateTest();
        test.TestCode = source;
        test.ExpectedDiagnostics.Add(expected);
        await test.RunAsync();
    }

    [Fact]
    public async Task WhenIntParseIsUsed_ShouldNotReportDiagnostic()
    {
        var source = @"
public class TestClass
{
    public void DoWork()
    {
        var value = int.Parse(""123"");
    }
}
";
        var test = CreateTest();
        test.TestCode = source;
        await test.RunAsync();
    }

    [Fact]
    public async Task WhenCustomConvertIsUsed_ShouldNotReportDiagnostic()
    {
        var source = @"
public static class Convert 
{
    public static int ToInt32(string value) => 0;
}

public class TestClass
{
    public void DoWork()
    {
        var value = Convert.ToInt32(""123"");
    }
}
";
        var test = CreateTest();
        test.TestCode = source;
        await test.RunAsync();
    }

    private static CSharpAnalyzerTest<SystemConvertUsageAnalyzer, DefaultVerifier> CreateTest()
    {
        var test = new CSharpAnalyzerTest<SystemConvertUsageAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };

        return test;
    }
}