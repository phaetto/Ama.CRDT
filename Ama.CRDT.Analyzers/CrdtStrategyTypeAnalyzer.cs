namespace Ama.CRDT.Analyzers;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
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

        var supportedTypes = new List<ITypeSymbol>();

        if (strategyName == "StateMachineStrategy")
        {
            var validatorTypeArg = strategyAttributeData.ConstructorArguments.FirstOrDefault();
            if (validatorTypeArg.Value is ITypeSymbol validatorType)
            {
                var stateMachineInterfaceType = context.Compilation.GetTypeByMetadataName("Ama.CRDT.Extensions.IStateMachine`1");
                
                if (stateMachineInterfaceType is not null)
                {
                    var stateMachineInterfaces = validatorType.AllInterfaces.Where(i =>
                        SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, stateMachineInterfaceType));

                    foreach (var smInterface in stateMachineInterfaces)
                    {
                        supportedTypes.Add(smInterface.TypeArguments[0]);
                    }

                    if (validatorType is INamedTypeSymbol namedValidatorType && namedValidatorType.IsGenericType && SymbolEqualityComparer.Default.Equals(namedValidatorType.OriginalDefinition, stateMachineInterfaceType))
                    {
                        supportedTypes.Add(namedValidatorType.TypeArguments[0]);
                    }
                }
            }
        }
        else
        {
            var crdtSupportedTypeAttributeType = context.Compilation.GetTypeByMetadataName("Ama.CRDT.Attributes.CrdtSupportedTypeAttribute");
            if (crdtSupportedTypeAttributeType is not null)
            {
                var supportedTypeAttributes = strategyTypeSymbol.GetAttributes()
                    .Where(ad => ad.AttributeClass?.Equals(crdtSupportedTypeAttributeType, SymbolEqualityComparer.Default) ?? false);

                var extractedTypes = supportedTypeAttributes
                    .Select(ad => ad.ConstructorArguments.FirstOrDefault().Value as ITypeSymbol)
                    .Where(t => t is not null);
                    
                supportedTypes.AddRange(extractedTypes!);
            }
        }

        if (supportedTypes.Count == 0)
        {
            // If a strategy has no CrdtSupportedTypeAttribute (and is not StateMachine resolving properly), we cannot validate it.
            return;
        }

        var isSupported = supportedTypes.Any(supportedType => IsTypeCompatible(propertyTypeSymbol, supportedType, context.Compilation));

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

        if (compilation.HasImplicitConversion(propertyType, supportedType))
        {
            return true;
        }

        // Check for generic interface equivalents (e.g., IList<T> for IList)
        return IsGenericInterfaceEquivalent(propertyType, supportedType);
    }

    private static bool IsGenericInterfaceEquivalent(ITypeSymbol propertyType, ITypeSymbol supportedType)
    {
        var supportedName = GetFullMetadataName(supportedType);
        var genericEquivalentName = supportedName switch
        {
            "System.Collections.IList" => "System.Collections.Generic.IList`1",
            "System.Collections.IDictionary" => "System.Collections.Generic.IDictionary`2",
            "System.Collections.ICollection" => "System.Collections.Generic.ICollection`1",
            "System.Collections.IEnumerable" => "System.Collections.Generic.IEnumerable`1",
            "System.Collections.Generic.ISet`1" => "System.Collections.Generic.ISet`1",
            _ => null
        };

        if (genericEquivalentName is null)
        {
            return false;
        }

        if (GetFullMetadataName(propertyType.OriginalDefinition) == genericEquivalentName)
        {
            return true;
        }

        foreach (var interfaceSymbol in propertyType.AllInterfaces)
        {
            if (GetFullMetadataName(interfaceSymbol.OriginalDefinition) == genericEquivalentName)
            {
                return true;
            }
        }

        return false;
    }

    private static string GetFullMetadataName(ITypeSymbol symbol)
    {
        if (symbol is null)
        {
            return string.Empty;
        }

        var namespaceName = symbol.ContainingNamespace?.ToDisplayString();
        if (string.IsNullOrEmpty(namespaceName) || symbol.ContainingNamespace!.IsGlobalNamespace)
        {
            return symbol.MetadataName;
        }

        return $"{namespaceName}.{symbol.MetadataName}";
    }
}