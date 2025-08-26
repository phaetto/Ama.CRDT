namespace Ama.CRDT.Analyzers.UnitTests;

using Ama.CRDT.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using System.Threading.Tasks;
using Xunit;

public sealed class CrdtStrategyTypeAnalyzerTests
{
    private static CSharpAnalyzerTest<CrdtStrategyTypeAnalyzer, DefaultVerifier> CreateTest()
    {
        var test = new CSharpAnalyzerTest<CrdtStrategyTypeAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90
        };

        test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(CrdtStrategyAttribute).Assembly.Location));

        return test;
    }

    [Fact]
    public async Task WhenCounterStrategyAppliedToValidType_ShouldNotReportDiagnostic()
    {
        var source = @"
using Ama.CRDT.Attributes;

public class MyPoco
{
    [CrdtCounterStrategy]
    public int MyCounter { get; set; }
}
";
        var test = CreateTest();
        test.TestCode = source;
        await test.RunAsync();
    }

    [Fact]
    public async Task WhenCounterStrategyAppliedToInvalidType_ShouldReportDiagnostic()
    {
        var source = @"
using Ama.CRDT.Attributes;

public class MyPoco
{
    [CrdtCounterStrategy]
    public string MyCounter { get; set; }
}
";
        var expected = new DiagnosticResult("CRDT0001", DiagnosticSeverity.Error)
            .WithLocation(7, 19)
            .WithArguments("CounterStrategy", "string");

        var test = CreateTest();
        test.TestCode = source;
        test.ExpectedDiagnostics.Add(expected);
        await test.RunAsync();
    }

    [Fact]
    public async Task WhenArrayStrategyAppliedToValidType_ShouldNotReportDiagnostic()
    {
        var source = @"
using Ama.CRDT.Attributes;
using System.Collections.Generic;

public class MyPoco
{
    [CrdtArrayLcsStrategy]
    public List<string> MyList { get; set; }
}
";
        var test = CreateTest();
        test.TestCode = source;
        await test.RunAsync();
    }

    [Fact]
    public async Task WhenArrayStrategyAppliedToInvalidType_ShouldReportDiagnostic()
    {
        var source = @"
using Ama.CRDT.Attributes;

public class MyPoco
{
    [CrdtArrayLcsStrategy]
    public int MyList { get; set; }
}
";
        var expected = new DiagnosticResult("CRDT0001", DiagnosticSeverity.Error)
            .WithLocation(7, 16)
            .WithArguments("ArrayLcsStrategy", "int");

        var test = CreateTest();
        test.TestCode = source;
        test.ExpectedDiagnostics.Add(expected);
        await test.RunAsync();
    }

    [Fact]
    public async Task WhenInterfaceBasedStrategyAppliedToValidType_ShouldNotReportDiagnostic()
    {
        var source = @"
using Ama.CRDT.Attributes;
using System;

public class MyPoco
{
    [CrdtMaxWinsStrategy]
    public DateTime MyValue { get; set; } // DateTime implements IComparable
}
";
        var test = CreateTest();
        test.TestCode = source;
        await test.RunAsync();
    }

    [Fact]
    public async Task WhenNoCrdtAttribute_ShouldNotReportDiagnostic()
    {
        var source = @"
public class MyPoco
{
    public int MyValue { get; set; }
}
";
        var test = CreateTest();
        test.TestCode = source;
        await test.RunAsync();
    }
}