namespace Ama.CRDT.Analyzers;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CrdtStrategyTypeAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "CRDT0001";

    private static readonly LocalizableString Title = "Unsupported property type for CRDT strategy";
    private static readonly LocalizableString MessageFormat = "The CRDT strategy '{0}' does not support the property type '{1}'";
    private static readonly LocalizableString Description = "CRDT strategies must be applied to properties of a compatible type.";
    private const string Category = "Usage";

    private static readonly DiagnosticDescriptor Rule = new(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeProperty, SymbolKind.Property);
    }

    private static void AnalyzeProperty(SymbolAnalysisContext context)
    {
        var propertySymbol = (IPropertySymbol)context.Symbol;
        var propertyTypeSymbol = propertySymbol.Type;

        var crdtStrategyAttributeType = context.Compilation.GetTypeByMetadataName("Ama.CRDT.Attributes.CrdtStrategyAttribute");
        if (crdtStrategyAttributeType is null)
        {
            return;
        }

        var strategyAttributeData = propertySymbol.GetAttributes()
            .FirstOrDefault(ad => ad.AttributeClass?.BaseType?.Equals(crdtStrategyAttributeType, SymbolEqualityComparer.Default) ?? false);

        if (strategyAttributeData?.AttributeClass is null)
        {
            return;
        }

        var attributeClassSymbol = strategyAttributeData.AttributeClass;
        var strategyName = GetStrategyNameFromAttribute(attributeClassSymbol);
        if (strategyName is null)
        {
            return;
        }

        var strategyFullName = $"Ama.CRDT.Services.Strategies.{strategyName}";
        var strategyTypeSymbol = context.Compilation.GetTypeByMetadataName(strategyFullName);

        if (strategyTypeSymbol is null)
        {
            // This can happen for custom strategies outside the main library.
            // We cannot validate them with this mechanism, so we skip.
            return;
        }

        var crdtSupportedTypeAttributeType = context.Compilation.GetTypeByMetadataName("Ama.CRDT.Attributes.CrdtSupportedTypeAttribute");
        if (crdtSupportedTypeAttributeType is null)
        {
            return;
        }

        var supportedTypeAttributes = strategyTypeSymbol.GetAttributes()
            .Where(ad => ad.AttributeClass?.Equals(crdtSupportedTypeAttributeType, SymbolEqualityComparer.Default) ?? false);

        var supportedTypes = supportedTypeAttributes
            .Select(ad => ad.ConstructorArguments.FirstOrDefault().Value as ITypeSymbol)
            .Where(t => t is not null)
            .ToList();

        if (supportedTypes.Count == 0)
        {
            // If a strategy has no CrdtSupportedTypeAttribute, we cannot validate it.
            return;
        }

        var isSupported = supportedTypes.Any(supportedType => IsTypeCompatible(propertyTypeSymbol, supportedType!, context.Compilation));

        if (!isSupported)
        {
            var diagnostic = Diagnostic.Create(Rule, propertySymbol.Locations[0], strategyName, propertyTypeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static string? GetStrategyNameFromAttribute(INamedTypeSymbol attributeClassSymbol)
    {
        const string prefix = "Crdt";
        const string suffix = "StrategyAttribute";
        var attributeName = attributeClassSymbol.Name;

        if (!attributeName.StartsWith(prefix) || !attributeName.EndsWith(suffix))
        {
            return null;
        }
        
        var coreName = attributeName.Substring(prefix.Length);
        return coreName.Substring(0, coreName.Length - "Attribute".Length);
    }

    private static bool IsTypeCompatible(ITypeSymbol propertyType, ITypeSymbol supportedType, Compilation compilation)
    {
        if (supportedType.SpecialType == SpecialType.System_Object)
        {
            return true;
        }

        return compilation.HasImplicitConversion(propertyType, supportedType);
    }
}