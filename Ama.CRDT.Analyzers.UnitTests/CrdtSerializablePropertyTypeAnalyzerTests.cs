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

        // Create the dummy attributes and CrdtContext definition so the test code compiles and maps successfully
        var dummyCode = @"
namespace Ama.CRDT.Attributes
{
    using System;
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class CrdtSerializableAttribute : Attribute
    {
        public CrdtSerializableAttribute(Type type) {}
    }
}
namespace Ama.CRDT.Models.Aot
{
    public abstract class CrdtContext {}
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

[CrdtSerializable(typeof(MyPoco))]
[CrdtSerializable(typeof(IList<string>))]
[CrdtSerializable(typeof(List<string>))]
public partial class MyContext : CrdtContext {}
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

[CrdtSerializable(typeof(MyPoco))]
public partial class {|#0:MyContext|} : CrdtContext {}
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

[CrdtSerializable(typeof(MyPoco))]
[CrdtSerializable(typeof(IList<string>))]
public partial class {|#0:MyContext|} : CrdtContext {}
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

[CrdtSerializable(typeof(MyPoco))]
public partial class {|#0:MyContext|} : CrdtContext {}
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

[CrdtSerializable(typeof(MyPoco))]
public partial class MyContext : CrdtContext {}

[CrdtSerializable(typeof(IList<string>))]
public partial class OtherContext : CrdtContext {}
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

[CrdtSerializable(typeof(MyPoco))]
public partial class MyContext : CrdtContext {}

[CrdtSerializable(typeof(IList<string>))]
[CrdtSerializable(typeof(List<string>))]
public partial class OtherContext : CrdtContext {}
";
        var test = CreateTest();
        test.TestCode = source;
        await test.RunAsync();
    }
}