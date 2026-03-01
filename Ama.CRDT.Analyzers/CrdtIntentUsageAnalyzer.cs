namespace Ama.CRDT.Analyzers;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
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

        if (targetMethod.Name != "GenerateOperation" ||
            targetMethod.ContainingType.ToDisplayString() != "Ama.CRDT.Services.ICrdtPatcher")
        {
            return;
        }

        if (invocation.Arguments.Length < 3)
        {
            return;
        }

        var expressionArg = invocation.Arguments[1];
        var intentArg = invocation.Arguments[2];

        var propertySymbol = ExtractPropertySymbol(expressionArg.Value);
        if (propertySymbol is null)
        {
            return;
        }

        var intentType = ExtractIntentType(intentArg.Value);
        if (intentType is null)
        {
            return;
        }

        var strategyTypeSymbol = GetStrategyTypeSymbol(propertySymbol, context.Compilation);
        if (strategyTypeSymbol is null)
        {
            return;
        }

        var supportedIntentAttributeType = context.Compilation.GetTypeByMetadataName("Ama.CRDT.Attributes.CrdtSupportedIntentAttribute");
        if (supportedIntentAttributeType is null)
        {
            return;
        }

        var supportedIntentAttributes = strategyTypeSymbol.GetAttributes()
            .Where(ad => ad.AttributeClass?.Equals(supportedIntentAttributeType, SymbolEqualityComparer.Default) ?? false);

        var supportedIntentTypes = supportedIntentAttributes
            .Select(ad => ad.ConstructorArguments.FirstOrDefault().Value as ITypeSymbol)
            .Where(t => t is not null)
            .ToList();

        if (supportedIntentTypes.Count == 0)
        {
            ReportDiagnostic(context, invocation, strategyTypeSymbol, intentType, propertySymbol);
            return;
        }

        var isSupported = supportedIntentTypes.Any(supportedType =>
            context.Compilation.HasImplicitConversion(intentType, supportedType!));

        if (!isSupported)
        {
            ReportDiagnostic(context, invocation, strategyTypeSymbol, intentType, propertySymbol);
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

    private static INamedTypeSymbol? GetStrategyTypeSymbol(IPropertySymbol propertySymbol, Compilation compilation)
    {
        var crdtStrategyAttributeType = compilation.GetTypeByMetadataName("Ama.CRDT.Attributes.CrdtStrategyAttribute");
        if (crdtStrategyAttributeType is null)
        {
            return null;
        }

        var strategyAttributeData = propertySymbol.GetAttributes()
            .FirstOrDefault(ad => ad.AttributeClass?.BaseType?.Equals(crdtStrategyAttributeType, SymbolEqualityComparer.Default) ?? false);

        if (strategyAttributeData?.AttributeClass is null)
        {
            return null;
        }

        var attributeName = strategyAttributeData.AttributeClass.Name;
        const string prefix = "Crdt";
        const string suffix = "StrategyAttribute";
        
        if (!attributeName.StartsWith(prefix) || !attributeName.EndsWith(suffix))
        {
            return null;
        }

        var coreName = attributeName.Substring(prefix.Length, attributeName.Length - prefix.Length - suffix.Length);
        var strategyFullName = $"Ama.CRDT.Services.Strategies.{coreName}Strategy";
        
        return compilation.GetTypeByMetadataName(strategyFullName);
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