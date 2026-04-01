namespace Ama.CRDT.Analyzers;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CrdtIntentUsageAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "CRDT0002";

    private static readonly LocalizableString Title = "Unsupported intent for CRDT strategy";
    private static readonly LocalizableString MessageFormat = "The CRDT strategy '{0}' mapped to property '{1}' does not support the intent '{2}'";
    private static readonly LocalizableString Description = "Explicit CRDT intents must be supported by the strategy assigned to the target property.";
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
        var targetMethod = invocation.TargetMethod;

        var containingTypeStr = targetMethod.ContainingType.ToDisplayString();
        var targetName = targetMethod.Name;

        if ((targetName != "GenerateOperation" && targetName != "GenerateOperationAsync") ||
            (containingTypeStr != "Ama.CRDT.Services.ICrdtPatcher" && containingTypeStr != "Ama.CRDT.Services.IAsyncCrdtPatcher"))
        {
            return;
        }

        if (invocation.Arguments.Length < 3)
        {
            return;
        }

        var propertySymbol = ExtractPropertySymbol(invocation.Arguments[1].Value);
        var intentType = ExtractIntentType(invocation.Arguments[2].Value);

        if (propertySymbol is null || intentType is null)
        {
            return;
        }

        var strategyTypeSymbols = GetStrategyTypeSymbols(propertySymbol, context.Compilation).ToList();
        if (strategyTypeSymbols.Count == 0)
        {
            return;
        }

        var supportedIntentAttributeType = context.Compilation.GetTypeByMetadataName("Ama.CRDT.Attributes.CrdtSupportedIntentAttribute");
        if (supportedIntentAttributeType is null)
        {
            return;
        }

        var supportedIntentTypes = new List<ITypeSymbol>();
        
        foreach (var strategyType in strategyTypeSymbols)
        {
            var supportedIntentAttributes = strategyType.GetAttributes()
                .Where(ad => ad.AttributeClass?.Equals(supportedIntentAttributeType, SymbolEqualityComparer.Default) ?? false);

            var types = supportedIntentAttributes
                .Select(ad => ad.ConstructorArguments.FirstOrDefault().Value as ITypeSymbol)
                .Where(t => t is not null);
                
            supportedIntentTypes.AddRange(types!);
        }

        if (supportedIntentTypes.Count == 0)
        {
            var baseStrategy = GetBaseStrategy(strategyTypeSymbols);
            ReportDiagnostic(context, invocation, baseStrategy, intentType, propertySymbol);
            return;
        }

        var isSupported = supportedIntentTypes.Any(supportedType =>
            context.Compilation.HasImplicitConversion(intentType, supportedType!));

        if (!isSupported)
        {
            var baseStrategy = GetBaseStrategy(strategyTypeSymbols);
            ReportDiagnostic(context, invocation, baseStrategy, intentType, propertySymbol);
        }
    }

    private static INamedTypeSymbol GetBaseStrategy(List<INamedTypeSymbol> strategyTypeSymbols)
    {
        return strategyTypeSymbols.FirstOrDefault(s => s.ContainingNamespace.Name != "Decorators") 
               ?? strategyTypeSymbols[0];
    }

    private static IEnumerable<INamedTypeSymbol> GetStrategyTypeSymbols(IPropertySymbol propertySymbol, Compilation compilation)
    {
        var strategyAttributes = propertySymbol.GetAttributes()
            .Where(ad => ad.AttributeClass != null && ad.AttributeClass.Name.StartsWith("Crdt"));

        foreach (var attr in strategyAttributes)
        {
            var attributeName = attr.AttributeClass!.Name;
            var coreName = attributeName;
            
            if (coreName.StartsWith("Crdt")) coreName = coreName.Substring(4);
            if (coreName.EndsWith("Attribute")) coreName = coreName.Substring(0, coreName.Length - 9);
            if (coreName.EndsWith("Strategy")) coreName = coreName.Substring(0, coreName.Length - 8);
            
            var strategyFullName = $"Ama.CRDT.Services.Strategies.{coreName}Strategy";
            var type = compilation.GetTypeByMetadataName(strategyFullName);
            
            if (type == null)
            {
                strategyFullName = $"Ama.CRDT.Services.Strategies.Decorators.{coreName}Strategy";
                type = compilation.GetTypeByMetadataName(strategyFullName);
            }

            if (type != null)
            {
                yield return type;
            }
        }
    }

    private static IPropertySymbol? ExtractPropertySymbol(IOperation operation)
    {
        var current = operation;
        
        while (current is IConversionOperation conversion)
        {
            current = conversion.Operand;
        }

        if (current is IDelegateCreationOperation delegateCreation)
        {
            current = delegateCreation.Target;
        }

        if (current is IAnonymousFunctionOperation anonymousFunction)
        {
            var body = anonymousFunction.Body;
            if (body.Operations.Length == 1 && body.Operations[0] is IReturnOperation returnOp)
            {
                var returnedValue = returnOp.ReturnedValue;
                
                while (returnedValue is IConversionOperation innerConv)
                {
                    returnedValue = innerConv.Operand;
                }

                if (returnedValue is IPropertyReferenceOperation propertyRef)
                {
                    return propertyRef.Property;
                }
            }
        }

        return null;
    }

    private static ITypeSymbol? ExtractIntentType(IOperation operation)
    {
        var current = operation;
        
        while (current is IConversionOperation conversion)
        {
            current = conversion.Operand;
        }

        return current.Type;
    }

    private static void ReportDiagnostic(OperationAnalysisContext context, IInvocationOperation invocation, INamedTypeSymbol strategy, ITypeSymbol intent, IPropertySymbol property)
    {
        var diagnostic = Diagnostic.Create(
            Rule,
            invocation.Syntax.GetLocation(),
            strategy.Name,
            property.Name,
            intent.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
            
        context.ReportDiagnostic(diagnostic);
    }
}