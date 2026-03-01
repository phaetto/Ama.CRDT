namespace Ama.CRDT.Analyzers.UnitTests;

using Ama.CRDT.Attributes;
using Ama.CRDT.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using System.Threading.Tasks;
using Xunit;

public sealed class CrdtIntentUsageAnalyzerTests
{
    private static CSharpAnalyzerTest<CrdtIntentUsageAnalyzer, DefaultVerifier> CreateTest()
    {
        var test = new CSharpAnalyzerTest<CrdtIntentUsageAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90
        };

        test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(CrdtStrategyAttribute).Assembly.Location));
        test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(ICrdtPatcher).Assembly.Location));

        return test;
    }

    [Fact]
    public async Task WhenValidIntentUsed_ShouldNotReportDiagnostic()
    {
        var source = @"
using Ama.CRDT.Attributes;
using Ama.CRDT.Services;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using System.Collections.Generic;
using System.Linq.Expressions;

public class MyPoco
{
    [CrdtRgaStrategy]
    public List<string> MyList { get; set; }
}

public class TestClass
{
    public void DoWork(ICrdtPatcher patcher, CrdtDocument<MyPoco> doc)
    {
        patcher.GenerateOperation(doc, x => x.MyList, new InsertIntent(0, ""val""));
    }
}
";
        var test = CreateTest();
        test.TestCode = source;
        await test.RunAsync();
    }

    [Fact]
    public async Task WhenInvalidIntentUsedOnSupportedStrategy_ShouldReportDiagnostic()
    {
        var source = @"
using Ama.CRDT.Attributes;
using Ama.CRDT.Services;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using System.Collections.Generic;
using System.Linq.Expressions;

public class CustomIntent : IOperationIntent { }

public class MyPoco
{
    [CrdtRgaStrategy]
    public List<string> MyList { get; set; }
}

public class TestClass
{
    public void DoWork(ICrdtPatcher patcher, CrdtDocument<MyPoco> doc)
    {
        patcher.GenerateOperation(doc, x => x.MyList, new CustomIntent());
    }
}
";
        var expected = new DiagnosticResult("CRDT0002", DiagnosticSeverity.Error)
            .WithLocation(21, 9)
            .WithArguments("RgaStrategy", "MyList", "CustomIntent");

        var test = CreateTest();
        test.TestCode = source;
        test.ExpectedDiagnostics.Add(expected);
        await test.RunAsync();
    }

    [Fact]
    public async Task WhenStrategyDoesNotSupportIntents_ShouldReportDiagnostic()
    {
        var source = @"
using Ama.CRDT.Attributes;
using Ama.CRDT.Services;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using System.Linq.Expressions;

public class MyPoco
{
    [CrdtCounterStrategy]
    public int MyCounter { get; set; }
}

public class TestClass
{
    public void DoWork(ICrdtPatcher patcher, CrdtDocument<MyPoco> doc)
    {
        patcher.GenerateOperation(doc, x => x.MyCounter, new InsertIntent(0, ""val""));
    }
}
";
        var expected = new DiagnosticResult("CRDT0002", DiagnosticSeverity.Error)
            .WithLocation(18, 9)
            .WithArguments("CounterStrategy", "MyCounter", "InsertIntent");

        var test = CreateTest();
        test.TestCode = source;
        test.ExpectedDiagnostics.Add(expected);
        await test.RunAsync();
    }

    [Fact]
    public async Task WhenIrrelevantMethodCalled_ShouldNotReportDiagnostic()
    {
        var source = @"
using System;

public class TestClass
{
    public void DoWork()
    {
        Console.WriteLine(""Hello"");
    }
}
";
        var test = CreateTest();
        test.TestCode = source;
        await test.RunAsync();
    }

    [Fact]
    public async Task WhenGeneratePatchCalled_ShouldNotReportDiagnostic()
    {
        var source = @"
using Ama.CRDT.Attributes;
using Ama.CRDT.Services;
using Ama.CRDT.Models;

public class MyPoco
{
    [CrdtCounterStrategy]
    public int MyCounter { get; set; }
}

public class TestClass
{
    public void DoWork(ICrdtPatcher patcher, CrdtDocument<MyPoco> doc, MyPoco changed)
    {
        patcher.GeneratePatch(doc, changed);
    }
}
";
        var test = CreateTest();
        test.TestCode = source;
        await test.RunAsync();
    }
}