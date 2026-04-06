namespace Ama.CRDT.Analyzers.UnitTests;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using System.Threading.Tasks;
using Xunit;

public sealed class CrdtDecoratorBehaviorAnalyzerTests
{
    private static CSharpAnalyzerTest<CrdtDecoratorBehaviorAnalyzer, DefaultVerifier> CreateTest(string source)
    {
        var mockCode = @"
namespace Microsoft.Extensions.DependencyInjection
{
    public interface IServiceCollection {}
}

namespace Ama.CRDT.Models
{
    public enum DecoratorBehavior { Before = 0, After = 1, Complex = 2 }
}

namespace Ama.CRDT.Attributes
{
    [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class AllowedDecoratorBehaviorAttribute : System.Attribute
    {
        public AllowedDecoratorBehaviorAttribute(Ama.CRDT.Models.DecoratorBehavior behavior) {}
        public AllowedDecoratorBehaviorAttribute(params Ama.CRDT.Models.DecoratorBehavior[] behaviors) {}
    }
}

namespace Ama.CRDT.Extensions
{
    public static class ServiceCollectionExtensions 
    {
        public static Microsoft.Extensions.DependencyInjection.IServiceCollection AddCrdtApplicatorDecorator<TDecorator>(this Microsoft.Extensions.DependencyInjection.IServiceCollection services, Ama.CRDT.Models.DecoratorBehavior? behavior = null) where TDecorator : class => services;
        public static Microsoft.Extensions.DependencyInjection.IServiceCollection AddCrdtPatcherDecorator<TDecorator>(this Microsoft.Extensions.DependencyInjection.IServiceCollection services, Ama.CRDT.Models.DecoratorBehavior? behavior = null) where TDecorator : class => services;
    }
}
";
        var test = new CSharpAnalyzerTest<CrdtDecoratorBehaviorAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            TestCode = source
        };

        test.TestState.Sources.Add(mockCode);

        return test;
    }

    [Fact]
    public async Task WhenValidBehaviorProvided_ShouldNotReportDiagnostic()
    {
        var source = @"
using Ama.CRDT.Models;
using Ama.CRDT.Attributes;
using Ama.CRDT.Extensions;
using Microsoft.Extensions.DependencyInjection;

[AllowedDecoratorBehavior(DecoratorBehavior.After)]
public class MyDecorator {}

public class TestClass
{
    public void TestMethod(IServiceCollection services)
    {
        services.AddCrdtApplicatorDecorator<MyDecorator>(DecoratorBehavior.After);
    }
}
";
        var test = CreateTest(source);
        await test.RunAsync();
    }

    [Fact]
    public async Task WhenInvalidBehaviorProvided_ShouldReportDiagnostic()
    {
        var sourceWithMarkup = @"
using Ama.CRDT.Models;
using Ama.CRDT.Attributes;
using Ama.CRDT.Extensions;
using Microsoft.Extensions.DependencyInjection;

[AllowedDecoratorBehavior(DecoratorBehavior.After)]
public class MyDecorator {}

public class TestClass
{
    public void TestMethod(IServiceCollection services)
    {
        services.AddCrdtApplicatorDecorator<MyDecorator>({|#0:DecoratorBehavior.Before|});
    }
}
";

        var test = CreateTest(sourceWithMarkup);
        var expectedDiag = new DiagnosticResult("CRDT0004", DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments("MyDecorator", "Before", "After");
        
        test.ExpectedDiagnostics.Add(expectedDiag);
        await test.RunAsync();
    }

    [Fact]
    public async Task WhenNoBehaviorProvided_ShouldNotReportDiagnostic()
    {
        var source = @"
using Ama.CRDT.Models;
using Ama.CRDT.Attributes;
using Ama.CRDT.Extensions;
using Microsoft.Extensions.DependencyInjection;

[AllowedDecoratorBehavior(DecoratorBehavior.After)]
public class MyDecorator {}

public class TestClass
{
    public void TestMethod(IServiceCollection services)
    {
        services.AddCrdtApplicatorDecorator<MyDecorator>();
    }
}
";
        var test = CreateTest(source);
        await test.RunAsync();
    }

    [Fact]
    public async Task WhenValidBehaviorArrayProvided_ShouldNotReportDiagnostic()
    {
        var source = @"
using Ama.CRDT.Models;
using Ama.CRDT.Attributes;
using Ama.CRDT.Extensions;
using Microsoft.Extensions.DependencyInjection;

[AllowedDecoratorBehavior(DecoratorBehavior.Before, DecoratorBehavior.After)]
public class MyDecorator {}

public class TestClass
{
    public void TestMethod(IServiceCollection services)
    {
        services.AddCrdtApplicatorDecorator<MyDecorator>(DecoratorBehavior.Before);
        services.AddCrdtApplicatorDecorator<MyDecorator>(DecoratorBehavior.After);
    }
}
";
        var test = CreateTest(source);
        await test.RunAsync();
    }

    [Fact]
    public async Task WhenInvalidBehaviorArrayProvided_ShouldReportDiagnostic()
    {
        var sourceWithMarkup = @"
using Ama.CRDT.Models;
using Ama.CRDT.Attributes;
using Ama.CRDT.Extensions;
using Microsoft.Extensions.DependencyInjection;

[AllowedDecoratorBehavior(DecoratorBehavior.Before, DecoratorBehavior.After)]
public class MyDecorator {}

public class TestClass
{
    public void TestMethod(IServiceCollection services)
    {
        services.AddCrdtApplicatorDecorator<MyDecorator>({|#0:DecoratorBehavior.Complex|});
    }
}
";

        var test = CreateTest(sourceWithMarkup);
        var expectedDiag = new DiagnosticResult("CRDT0004", DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments("MyDecorator", "Complex", "Before, After");
        
        test.ExpectedDiagnostics.Add(expectedDiag);
        await test.RunAsync();
    }

    [Fact]
    public async Task WhenMultipleAttributesProvided_ShouldNotReportDiagnostic()
    {
        var source = @"
using Ama.CRDT.Models;
using Ama.CRDT.Attributes;
using Ama.CRDT.Extensions;
using Microsoft.Extensions.DependencyInjection;

[AllowedDecoratorBehavior(DecoratorBehavior.Before)]
[AllowedDecoratorBehavior(DecoratorBehavior.After)]
public class MyDecorator {}

public class TestClass
{
    public void TestMethod(IServiceCollection services)
    {
        services.AddCrdtApplicatorDecorator<MyDecorator>(DecoratorBehavior.Before);
        services.AddCrdtApplicatorDecorator<MyDecorator>(DecoratorBehavior.After);
    }
}
";
        var test = CreateTest(source);
        await test.RunAsync();
    }

    [Fact]
    public async Task WhenMultipleAttributesWithDuplicatesProvided_ShouldReportDistinctBehaviorsInDiagnostic()
    {
        var sourceWithMarkup = @"
using Ama.CRDT.Models;
using Ama.CRDT.Attributes;
using Ama.CRDT.Extensions;
using Microsoft.Extensions.DependencyInjection;

[AllowedDecoratorBehavior(DecoratorBehavior.Before)]
[AllowedDecoratorBehavior(DecoratorBehavior.Before, DecoratorBehavior.After)]
public class MyDecorator {}

public class TestClass
{
    public void TestMethod(IServiceCollection services)
    {
        // Complex is not allowed, this will trigger the diagnostic.
        services.AddCrdtApplicatorDecorator<MyDecorator>({|#0:DecoratorBehavior.Complex|});
    }
}
";

        var test = CreateTest(sourceWithMarkup);
        var expectedDiag = new DiagnosticResult("CRDT0004", DiagnosticSeverity.Error)
            .WithLocation(0)
            // It should be deterministically ordered "Before, After" due to OrderBy and HashSet removal of duplicates
            .WithArguments("MyDecorator", "Complex", "Before, After");
        
        test.ExpectedDiagnostics.Add(expectedDiag);
        await test.RunAsync();
    }
}