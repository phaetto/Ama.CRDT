namespace Ama.CRDT.Project.Analyzers;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Immutable;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PropertyInfoUsageAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "CRDTPROJ0001";

    private static readonly LocalizableString Title = "Avoid reflection via PropertyInfo";
    private static readonly LocalizableString MessageFormat = "Do not use PropertyInfo.{0}. Use PocoPathHelper.GetAccessor(PropertyInfo).Getter/Setter instead.";
    private static readonly LocalizableString Description = "Avoid using PropertyInfo.GetValue and PropertyInfo.SetValue to prevent reflection usage.  Use PocoPathHelper.GetAccessor(PropertyInfo).Getter/Setter instead.";
    private const string Category = "Performance";

    private static readonly DiagnosticDescriptor Rule = new(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var targetMethod = invocation.TargetMethod;

        if (targetMethod.Name is not ("GetValue" or "SetValue"))
        {
            return;
        }

        var containingType = targetMethod.ContainingType;
        if (IsPropertyInfo(containingType))
        {
            var diagnostic = Diagnostic.Create(
                Rule,
                invocation.Syntax.GetLocation(),
                targetMethod.Name);
            
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsPropertyInfo(ITypeSymbol? type)
    {
        var current = type;
        while (current is not null)
        {
            if (current.ToDisplayString() == "System.Reflection.PropertyInfo")
            {
                return true;
            }
            
            current = current.BaseType;
        }

        return false;
    }
}