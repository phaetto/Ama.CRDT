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
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };

        test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(CrdtStrategyAttribute).Assembly.Location));
        test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(ICrdtPatcher).Assembly.Location));
        // IAsyncCrdtPatcher is in the same assembly as ICrdtPatcher, so the reference is already added.

        return test;
    }

    [Fact]
    public async Task WhenValidIntentUsed_ShouldNotReportDiagnostic()
    {
        var source = @"
using Ama.CRDT.Attributes.Strategies;
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
using Ama.CRDT.Attributes.Strategies;
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
using Ama.CRDT.Attributes.Strategies;
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
using Ama.CRDT.Attributes.Strategies;
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

    [Fact]
    public async Task WhenValidIntentUsedOnDeepPath_ShouldNotReportDiagnostic()
    {
        var source = @"
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Services;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using System.Collections.Generic;

public class MyPoco
{
    public Level1 L1 { get; set; }
}

public class Level1
{
    [CrdtRgaStrategy]
    public List<string> MyList { get; set; }
}

public class TestClass
{
    public void DoWork(ICrdtPatcher patcher, CrdtDocument<MyPoco> doc)
    {
        patcher.GenerateOperation(doc, x => x.L1.MyList, new InsertIntent(0, ""val""));
    }
}
";
        var test = CreateTest();
        test.TestCode = source;
        await test.RunAsync();
    }

    [Fact]
    public async Task WhenInvalidIntentUsedOnDeepPath_ShouldReportDiagnostic()
    {
        var source = @"
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Services;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;

public class MyPoco
{
    public Level1 L1 { get; set; }
}

public class Level1
{
    [CrdtCounterStrategy]
    public int MyCounter { get; set; }
}

public class TestClass
{
    public void DoWork(ICrdtPatcher patcher, CrdtDocument<MyPoco> doc)
    {
        patcher.GenerateOperation(doc, x => x.L1.MyCounter, new InsertIntent(0, 5));
    }
}
";
        var expected = new DiagnosticResult("CRDT0002", DiagnosticSeverity.Error)
            .WithLocation(22, 9)
            .WithArguments("CounterStrategy", "MyCounter", "InsertIntent");

        var test = CreateTest();
        test.TestCode = source;
        test.ExpectedDiagnostics.Add(expected);
        await test.RunAsync();
    }

    [Fact]
    public async Task WhenValidIntentUsedOnListIndexerPath_ShouldNotReportDiagnostic()
    {
        var source = @"
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Services;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using System.Collections.Generic;

public class MyPoco
{
    public List<Tag> Tags { get; set; }
}

public class Tag
{
    [CrdtCounterStrategy]
    public int Value { get; set; }
}

public class TestClass
{
    public void DoWork(ICrdtPatcher patcher, CrdtDocument<MyPoco> doc)
    {
        patcher.GenerateOperation(doc, x => x.Tags[0].Value, new IncrementIntent(5));
    }
}
";
        var test = CreateTest();
        test.TestCode = source;
        await test.RunAsync();
    }

    [Fact]
    public async Task WhenInvalidIntentUsedOnListIndexerPath_ShouldReportDiagnostic()
    {
        var source = @"
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Services;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using System.Collections.Generic;

public class MyPoco
{
    public List<Tag> Tags { get; set; }
}

public class Tag
{
    [CrdtLwwStrategy]
    public int Value { get; set; }
}

public class TestClass
{
    public void DoWork(ICrdtPatcher patcher, CrdtDocument<MyPoco> doc)
    {
        patcher.GenerateOperation(doc, x => x.Tags[0].Value, new IncrementIntent(5));
    }
}
";
        var expected = new DiagnosticResult("CRDT0002", DiagnosticSeverity.Error)
            .WithLocation(23, 9)
            .WithArguments("LwwStrategy", "Value", "IncrementIntent");

        var test = CreateTest();
        test.TestCode = source;
        test.ExpectedDiagnostics.Add(expected);
        await test.RunAsync();
    }

    [Fact]
    public async Task WhenValidIntentUsedOnDictionaryIndexerPath_ShouldNotReportDiagnostic()
    {
        var source = @"
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Services;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using System.Collections.Generic;

public class MyPoco
{
    public Dictionary<string, Metric> Metrics { get; set; }
}

public class Metric
{
    [CrdtCounterStrategy]
    public int Count { get; set; }
}

public class TestClass
{
    public void DoWork(ICrdtPatcher patcher, CrdtDocument<MyPoco> doc)
    {
        patcher.GenerateOperation(doc, x => x.Metrics[""cpu""].Count, new IncrementIntent(5));
    }
}
";
        var test = CreateTest();
        test.TestCode = source;
        await test.RunAsync();
    }

    [Fact]
    public async Task WhenInvalidIntentUsedOnDictionaryIndexerPath_ShouldReportDiagnostic()
    {
        var source = @"
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Services;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using System.Collections.Generic;

public class MyPoco
{
    public Dictionary<string, Metric> Metrics { get; set; }
}

public class Metric
{
    [CrdtLwwStrategy]
    public int Count { get; set; }
}

public class TestClass
{
    public void DoWork(ICrdtPatcher patcher, CrdtDocument<MyPoco> doc)
    {
        patcher.GenerateOperation(doc, x => x.Metrics[""cpu""].Count, new IncrementIntent(5));
    }
}
";
        var expected = new DiagnosticResult("CRDT0002", DiagnosticSeverity.Error)
            .WithLocation(23, 9)
            .WithArguments("LwwStrategy", "Count", "IncrementIntent");

        var test = CreateTest();
        test.TestCode = source;
        test.ExpectedDiagnostics.Add(expected);
        await test.RunAsync();
    }

    [Fact]
    public async Task WhenValidDecoratorIntentUsed_ShouldNotReportDiagnostic()
    {
        var source = @"
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Attributes.Decorators;
using Ama.CRDT.Services;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents.Decorators;
using System.Collections.Generic;

public class MyPoco
{
    [CrdtEpochBound]
    [CrdtLwwStrategy]
    public string MyString { get; set; }
}

public class TestClass
{
    public void DoWork(ICrdtPatcher patcher, CrdtDocument<MyPoco> doc)
    {
        patcher.GenerateOperation(doc, x => x.MyString, new EpochClearIntent());
    }
}
";
        var test = CreateTest();
        test.TestCode = source;
        await test.RunAsync();
    }

    [Fact]
    public async Task WhenValidBaseIntentUsedWithDecorator_ShouldNotReportDiagnostic()
    {
        var source = @"
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Attributes.Decorators;
using Ama.CRDT.Services;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using System.Collections.Generic;

public class MyPoco
{
    [CrdtEpochBound]
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
    public async Task WhenInvalidIntentUsedWithDecorator_ShouldReportDiagnostic()
    {
        var source = @"
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Attributes.Decorators;
using Ama.CRDT.Services;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using System.Collections.Generic;

public class MyPoco
{
    [CrdtEpochBound]
    [CrdtLwwStrategy]
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
        var expected = new DiagnosticResult("CRDT0002", DiagnosticSeverity.Error)
            .WithLocation(20, 9)
            .WithArguments("LwwStrategy", "MyList", "InsertIntent");

        var test = CreateTest();
        test.TestCode = source;
        test.ExpectedDiagnostics.Add(expected);
        await test.RunAsync();
    }

    [Fact]
    public async Task WhenValidIntentUsedWithAsyncPatcher_ShouldNotReportDiagnostic()
    {
        var source = @"
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Services;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using System.Collections.Generic;
using System.Threading.Tasks;

public class MyPoco
{
    [CrdtRgaStrategy]
    public List<string> MyList { get; set; }
}

public class TestClass
{
    public async Task DoWork(IAsyncCrdtPatcher patcher, CrdtDocument<MyPoco> doc)
    {
        await patcher.GenerateOperationAsync(doc, x => x.MyList, new InsertIntent(0, ""val""));
    }
}
";
        var test = CreateTest();
        test.TestCode = source;
        await test.RunAsync();
    }

    [Fact]
    public async Task WhenInvalidIntentUsedWithAsyncPatcher_ShouldReportDiagnostic()
    {
        var source = @"
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Services;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using System.Threading.Tasks;

public class MyPoco
{
    [CrdtCounterStrategy]
    public int MyCounter { get; set; }
}

public class TestClass
{
    public async Task DoWork(IAsyncCrdtPatcher patcher, CrdtDocument<MyPoco> doc)
    {
        await patcher.GenerateOperationAsync(doc, x => x.MyCounter, new InsertIntent(0, ""val""));
    }
}
";
        var expected = new DiagnosticResult("CRDT0002", DiagnosticSeverity.Error)
            .WithLocation(18, 15)
            .WithArguments("CounterStrategy", "MyCounter", "InsertIntent");

        var test = CreateTest();
        test.TestCode = source;
        test.ExpectedDiagnostics.Add(expected);
        await test.RunAsync();
    }
}