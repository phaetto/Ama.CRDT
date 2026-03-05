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
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };

        test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(CrdtStrategyAttribute).Assembly.Location));

        return test;
    }

    [Fact]
    public async Task WhenCounterStrategyAppliedToValidType_ShouldNotReportDiagnostic()
    {
        var source = @"
using Ama.CRDT.Attributes.Strategies;

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
using Ama.CRDT.Attributes.Strategies;

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
using Ama.CRDT.Attributes.Strategies;
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
    public async Task WhenArrayStrategyAppliedToGenericIListInterface_ShouldNotReportDiagnostic()
    {
        var source = @"
using Ama.CRDT.Attributes.Strategies;
using System.Collections.Generic;

public class MyPoco
{
    [CrdtArrayLcsStrategy]
    public IList<string> MyList { get; set; }
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
using Ama.CRDT.Attributes.Strategies;

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
    public async Task WhenMapStrategyAppliedToGenericIDictionaryInterface_ShouldNotReportDiagnostic()
    {
        var source = @"
using Ama.CRDT.Attributes.Strategies;
using System.Collections.Generic;

public class MyPoco
{
    [CrdtLwwMapStrategy]
    public IDictionary<string, int> MyMap { get; set; }
}
";
        var test = CreateTest();
        test.TestCode = source;
        await test.RunAsync();
    }

    [Fact]
    public async Task WhenInterfaceBasedStrategyAppliedToValidType_ShouldNotReportDiagnostic()
    {
        var source = @"
using Ama.CRDT.Attributes.Strategies;
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

    [Fact]
    public async Task WhenStateMachineStrategyAppliedToValidType_ShouldNotReportDiagnostic()
    {
        var source = @"
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Extensions;

public class ValidStateValidator : IStateMachine<int>
{
    public bool IsValidTransition(int from, int to) => true;
}

public class MyPoco
{
    [CrdtStateMachineStrategy(typeof(ValidStateValidator))]
    public int State { get; set; }
}
";
        var test = CreateTest();
        test.TestCode = source;
        await test.RunAsync();
    }

    [Fact]
    public async Task WhenStateMachineStrategyAppliedToInvalidType_ShouldReportDiagnostic()
    {
        var source = @"
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Extensions;

public class InvalidStateValidator : IStateMachine<int>
{
    public bool IsValidTransition(int from, int to) => true;
}

public class MyPoco
{
    [CrdtStateMachineStrategy(typeof(InvalidStateValidator))]
    public string State { get; set; }
}
";
        var expected = new DiagnosticResult("CRDT0001", DiagnosticSeverity.Error)
            .WithLocation(13, 19)
            .WithArguments("StateMachineStrategy", "string");

        var test = CreateTest();
        test.TestCode = source;
        test.ExpectedDiagnostics.Add(expected);
        await test.RunAsync();
    }

    [Fact]
    public async Task WhenValidTypeUsedWithMultipleDecorators_ShouldNotReportDiagnostic()
    {
        var source = @"
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Attributes.Decorators;

public class MyPoco
{
    [CrdtEpochBound]
    [CrdtApprovalQuorum(2)]
    [CrdtCounterStrategy]
    public int MyCounter { get; set; }
}
";
        var test = CreateTest();
        test.TestCode = source;
        await test.RunAsync();
    }

    [Fact]
    public async Task WhenInvalidTypeUsedWithMultipleDecorators_ShouldReportDiagnostic()
    {
        var source = @"
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Attributes.Decorators;

public class MyPoco
{
    [CrdtEpochBound]
    [CrdtApprovalQuorum(2)]
    [CrdtCounterStrategy]
    public string MyCounter { get; set; }
}
";
        var expected = new DiagnosticResult("CRDT0001", DiagnosticSeverity.Error)
            .WithLocation(10, 19)
            .WithArguments("CounterStrategy", "string");

        var test = CreateTest();
        test.TestCode = source;
        test.ExpectedDiagnostics.Add(expected);
        await test.RunAsync();
    }
}