namespace Ama.CRDT.Analyzers.UnitTests;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using System.Threading.Tasks;
using Xunit;

public sealed class CrdtSerializablePropertyTypeAnalyzerTests
{
    private static CSharpAnalyzerTest<CrdtSerializablePropertyTypeAnalyzer, DefaultVerifier> CreateTest()
    {
        var test = new CSharpAnalyzerTest<CrdtSerializablePropertyTypeAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };

        // Create the dummy attributes and CrdtAotContext definition so the test code compiles and maps successfully
        var dummyCode = @"
namespace Ama.CRDT.Attributes
{
    using System;
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class CrdtAotTypeAttribute : Attribute
    {
        public CrdtAotTypeAttribute(Type type) {}
    }
}
namespace Ama.CRDT.Models.Aot
{
    public abstract class CrdtAotContext {}
}
";
        test.TestState.Sources.Add(dummyCode);
        return test;
    }

    [Fact]
    public async Task WhenAllComplexPropertiesAreRegistered_ShouldNotReportDiagnostic()
    {
        var source = @"
using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;
using System.Collections.Generic;

public class MyPoco
{
    public string Title { get; set; }
    public IList<string> Tags { get; set; } = new List<string>();
}

[CrdtAotTypeAttribute(typeof(MyPoco))]
[CrdtAotTypeAttribute(typeof(IList<string>))]
[CrdtAotTypeAttribute(typeof(List<string>))]
public partial class MyContext : CrdtAotContext {}
";
        var test = CreateTest();
        test.TestCode = source;
        await test.RunAsync();
    }

    [Fact]
    public async Task WhenDeclaredTypeIsNotRegistered_ShouldReportDiagnostic()
    {
        var source = @"
using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;
using System.Collections.Generic;

public class MyPoco
{
    public string Title { get; set; }
    public IList<string> Tags { get; set; }
}

[CrdtAotTypeAttribute(typeof(MyPoco))]
public partial class {|#0:MyContext|} : CrdtAotContext {}
";
        var expected = new DiagnosticResult("CRDT0003", DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments("Tags", "MyPoco", "IList<string>", "MyContext");

        var test = CreateTest();
        test.TestCode = source;
        test.ExpectedDiagnostics.Add(expected);
        await test.RunAsync();
    }

    [Fact]
    public async Task WhenInitializedTypeIsNotRegistered_ShouldReportDiagnostic()
    {
        var source = @"
using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;
using System.Collections.Generic;

public class MyPoco
{
    public IList<string> Tags { get; set; } = new List<string>();
}

[CrdtAotTypeAttribute(typeof(MyPoco))]
[CrdtAotTypeAttribute(typeof(IList<string>))]
public partial class {|#0:MyContext|} : CrdtAotContext {}
";
        var expected = new DiagnosticResult("CRDT0003", DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments("Tags", "MyPoco", "List<string>", "MyContext");

        var test = CreateTest();
        test.TestCode = source;
        test.ExpectedDiagnostics.Add(expected);
        await test.RunAsync();
    }

    [Fact]
    public async Task WhenImplicitNewInitializedTypeIsNotRegistered_ShouldReportDiagnostic()
    {
        var source = @"
using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;
using System.Collections.Generic;

public class MyPoco
{
    public Dictionary<string, List<string>> Votes { get; set; } = new();
}

[CrdtAotTypeAttribute(typeof(MyPoco))]
public partial class {|#0:MyContext|} : CrdtAotContext {}
";
        var expected = new DiagnosticResult("CRDT0003", DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments("Votes", "MyPoco", "Dictionary<string, List<string>>", "MyContext");

        var test = CreateTest();
        test.TestCode = source;
        test.ExpectedDiagnostics.Add(expected);
        await test.RunAsync();
    }

    [Fact]
    public async Task WhenTypeIsRegisteredInAnotherContext_ShouldNotReportDiagnostic()
    {
        var source = @"
using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;
using System.Collections.Generic;

public class MyPoco
{
    public string Title { get; set; }
    public IList<string> Tags { get; set; }
}

[CrdtAotTypeAttribute(typeof(MyPoco))]
public partial class MyContext : CrdtAotContext {}

[CrdtAotTypeAttribute(typeof(IList<string>))]
public partial class OtherContext : CrdtAotContext {}
";
        var test = CreateTest();
        test.TestCode = source;
        await test.RunAsync();
    }

    [Fact]
    public async Task WhenInitializedTypeIsRegisteredInAnotherContext_ShouldNotReportDiagnostic()
    {
        var source = @"
using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;
using System.Collections.Generic;

public class MyPoco
{
    public IList<string> Tags { get; set; } = new List<string>();
}

[CrdtAotTypeAttribute(typeof(MyPoco))]
public partial class MyContext : CrdtAotContext {}

[CrdtAotTypeAttribute(typeof(IList<string>))]
[CrdtAotTypeAttribute(typeof(List<string>))]
public partial class OtherContext : CrdtAotContext {}
";
        var test = CreateTest();
        test.TestCode = source;
        await test.RunAsync();
    }
}