namespace Ama.CRDT.Project.Analyzers;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Immutable;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SystemConvertUsageAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "CRDTPROJ0002";

    private static readonly LocalizableString Title = "Avoid using System.Convert";
    private static readonly LocalizableString MessageFormat = "Do not use System.Convert.{0}. Prefer explicit parsing or casting instead.";
    private static readonly LocalizableString Description = "Avoid using System.Convert utilities. Use PocoPathHelper utilities with Expression casting.";
    private const string Category = "Design";

    private static readonly DiagnosticDescriptor Rule = new(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            return;
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        if (context.Operation is not IInvocationOperation invocation)
        {
            return;
        }

        var targetMethod = invocation.TargetMethod;

        if (targetMethod.ContainingType?.ToDisplayString() == "System.Convert")
        {
            var diagnostic = Diagnostic.Create(
                Rule,
                invocation.Syntax.GetLocation(),
                targetMethod.Name);
            
            context.ReportDiagnostic(diagnostic);
        }
    }
}