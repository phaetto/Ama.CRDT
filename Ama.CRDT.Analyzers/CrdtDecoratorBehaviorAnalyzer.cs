namespace Ama.CRDT.Analyzers;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CrdtDecoratorBehaviorAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "CRDT0004";

    private static readonly LocalizableString Title = "Unsupported decorator behavior";
    private static readonly LocalizableString MessageFormat = "The decorator '{0}' does not support the behavior '{1}'. Allowed behaviors are: {2}";
    private static readonly LocalizableString Description = "Decorators must be registered with a behavior they support.";
    private const string Category = "Usage";

    private static readonly DiagnosticDescriptor Rule = new(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;

        if (method.Name != "AddCrdtApplicatorDecorator" && method.Name != "AddCrdtPatcherDecorator")
        {
            return;
        }

        var containingType = method.ContainingType;
        if (containingType?.Name != "ServiceCollectionExtensions")
        {
            return;
        }

        var typeArg = method.TypeArguments.FirstOrDefault();
        if (typeArg == null)
        {
            return;
        }

        var behaviorArg = invocation.Arguments.FirstOrDefault(a => a.Parameter?.Name == "behavior");
        if (behaviorArg == null || behaviorArg.IsImplicit) 
        {
            return;
        }

        var argumentValue = behaviorArg.Value;
        
        // Unwrap nullable conversions if present
        while (argumentValue is IConversionOperation conversion)
        {
            argumentValue = conversion.Operand;
        }

        var providedValueOpt = argumentValue.ConstantValue;
        if (!providedValueOpt.HasValue || providedValueOpt.Value == null)
        {
            return;
        }

        int providedBehaviorInt;
        try
        {
            providedBehaviorInt = Convert.ToInt32(providedValueOpt.Value);
        }
        catch
        {
            return;
        }

        var allowedAttributes = typeArg.GetAttributes()
            .Where(a => a.AttributeClass?.Name == "AllowedDecoratorBehaviorAttribute" &&
                        a.AttributeClass.ContainingNamespace?.ToDisplayString() == "Ama.CRDT.Attributes")
            .ToList();

        if (allowedAttributes.Count == 0)
        {
            return;
        }

        // Using a HashSet to gracefully handle duplicates if multiple attributes define overlapping behaviors.
        var allowedInts = new HashSet<int>();
        foreach (var attr in allowedAttributes)
        {
            if (attr.ConstructorArguments.Length == 0) continue;

            var arg = attr.ConstructorArguments[0];
            
            if (arg.Kind == TypedConstantKind.Array)
            {
                foreach (var element in arg.Values)
                {
                    if (element.Value != null)
                    {
                        allowedInts.Add(Convert.ToInt32(element.Value));
                    }
                }
            }
            else if (arg.Value != null)
            {
                allowedInts.Add(Convert.ToInt32(arg.Value));
            }
        }

        if (allowedInts.Contains(providedBehaviorInt))
        {
            return;
        }

        var behaviorEnumType = context.Compilation.GetTypeByMetadataName("Ama.CRDT.Models.DecoratorBehavior");
        var providedBehaviorName = behaviorEnumType != null ? GetEnumName(behaviorEnumType, providedBehaviorInt) ?? providedBehaviorInt.ToString() : providedBehaviorInt.ToString();
        var allowedBehaviorNames = string.Join(", ", allowedInts.OrderBy(x => x).Select(i => behaviorEnumType != null ? GetEnumName(behaviorEnumType, i) ?? i.ToString() : i.ToString()));

        var diagnostic = Diagnostic.Create(
            Rule, 
            behaviorArg.Syntax!.GetLocation(), 
            typeArg.Name, 
            providedBehaviorName, 
            allowedBehaviorNames);

        context.ReportDiagnostic(diagnostic);
    }

    private static string? GetEnumName(INamedTypeSymbol enumType, int value)
    {
        foreach (var member in enumType.GetMembers().OfType<IFieldSymbol>())
        {
            if (member.HasConstantValue && member.ConstantValue is int v && v == value)
            {
                return member.Name;
            }
        }
        return null;
    }
}