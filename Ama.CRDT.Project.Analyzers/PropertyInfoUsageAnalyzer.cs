namespace Ama.CRDT.Project.Analyzers;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Immutable;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PropertyInfoUsageAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "CRDTPROJ0001";

    private static readonly LocalizableString Title = "Avoid reflection to ensure AOT compatibility";
    private static readonly LocalizableString MessageFormat = "Do not use '{0}'. Reflection breaks AOT compilation. Use AOT-compatible alternatives.";
    private static readonly LocalizableString Description = "Avoid using reflection types (PropertyInfo, MethodInfo, etc.) and reflection methods (Type.GetProperty, etc.) as they are not Native AOT compatible.";
    private const string Category = "Performance";

    private static readonly DiagnosticDescriptor Rule = new(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeOperation, 
            OperationKind.Invocation, 
            OperationKind.PropertyReference, 
            OperationKind.FieldReference);
    }

    private static void AnalyzeOperation(OperationAnalysisContext context)
    {
        ISymbol? symbol = context.Operation switch
        {
            IInvocationOperation invocation => invocation.TargetMethod,
            IPropertyReferenceOperation propertyRef => propertyRef.Property,
            IFieldReferenceOperation fieldRef => fieldRef.Field,
            _ => null
        };

        if (symbol?.ContainingType is null)
        {
            return;
        }

        var containingType = symbol.ContainingType;

        if (IsBannedReflectionType(containingType))
        {
            ReportDiagnostic(context, symbol.Name);
            return;
        }

        if (IsTypeOrTypeInfo(containingType))
        {
            if (symbol.Name is "GetProperty" or "GetProperties"
                            or "GetMethod" or "GetMethods"
                            or "GetField" or "GetFields"
                            or "GetEvent" or "GetEvents"
                            or "GetConstructor" or "GetConstructors"
                            or "GetMember" or "GetMembers"
                            or "InvokeMember"
                            or "MakeGenericType")
            {
                ReportDiagnostic(context, symbol.Name);
                return;
            }
        }

        if (containingType.ToDisplayString() == "System.Activator" && symbol.Name == "CreateInstance")
        {
            if (context.Operation is IInvocationOperation invocation && invocation.TargetMethod.TypeArguments.Length == 0)
            {
                ReportDiagnostic(context, symbol.Name);
                return;
            }
        }
    }

    private static void ReportDiagnostic(OperationAnalysisContext context, string memberName)
    {
        var diagnostic = Diagnostic.Create(
            Rule,
            context.Operation.Syntax.GetLocation(),
            memberName);
        
        context.ReportDiagnostic(diagnostic);
    }

    private static bool IsBannedReflectionType(ITypeSymbol type)
    {
        var current = type;
        while (current is not null)
        {
            var name = current.ToDisplayString();
            if (name is "System.Type" or "System.Reflection.TypeInfo")
            {
                // Handled specifically for exact banned methods. We don't blanket ban all Type usage.
                return false;
            }

            if (name is "System.Reflection.PropertyInfo"
                     or "System.Reflection.MethodInfo"
                     or "System.Reflection.FieldInfo"
                     or "System.Reflection.EventInfo"
                     or "System.Reflection.ConstructorInfo"
                     or "System.Reflection.ParameterInfo"
                     or "System.Reflection.MemberInfo")
            {
                return true;
            }
            
            current = current.BaseType;
        }

        return false;
    }

    private static bool IsTypeOrTypeInfo(ITypeSymbol type)
    {
        var current = type;
        while (current is not null)
        {
            var name = current.ToDisplayString();
            if (name is "System.Type" or "System.Reflection.TypeInfo")
            {
                return true;
            }
            
            current = current.BaseType;
        }

        return false;
    }
}