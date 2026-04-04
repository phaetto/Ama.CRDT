namespace Ama.CRDT.Analyzers;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CrdtSerializablePropertyTypeAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "CRDT0003";

    private static readonly LocalizableString Title = "Missing CrdtAotTypeAttribute registration for property type";
    private static readonly LocalizableString MessageFormat = "The property '{0}' on registered class '{1}' uses type '{2}' which must be registered with [CrdtAotTypeAttribute] on '{3}'";
    private static readonly LocalizableString Description = "All complex types used in properties of a registered CRDT model must also be explicitly registered in a CrdtAotContext to support AOT serialization.";
    private const string Category = "Usage";

    private static readonly DiagnosticDescriptor Rule = new(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var crdtContextType = compilationContext.Compilation.GetTypeByMetadataName("Ama.CRDT.Models.Aot.CrdtAotContext") 
                               ?? compilationContext.Compilation.GetTypeByMetadataName("Ama.CRDT.Models.Aot.CrdtContext");
            var serializableAttributeSymbol = compilationContext.Compilation.GetTypeByMetadataName("Ama.CRDT.Attributes.CrdtAotTypeAttribute")
                               ?? compilationContext.Compilation.GetTypeByMetadataName("Ama.CRDT.Attributes.CrdtSerializableAttribute");

            if (crdtContextType == null || serializableAttributeSymbol == null)
            {
                return;
            }

            // Pre-calculate all globally registered types mapping to the context that registered them
            var globalRegisteredTypes = new Dictionary<ITypeSymbol, INamedTypeSymbol>(SymbolEqualityComparer.Default);
            var visitor = new ContextSymbolVisitor(crdtContextType, serializableAttributeSymbol, globalRegisteredTypes);
            
            visitor.Visit(compilationContext.Compilation.Assembly.GlobalNamespace);

            foreach (var reference in compilationContext.Compilation.References)
            {
                if (compilationContext.Compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol asmSymbol)
                {
                    var name = asmSymbol.Name;
                    if (name.StartsWith("System") || 
                        name.StartsWith("Microsoft") || 
                        name == "mscorlib" || 
                        name == "netstandard" || 
                        name.StartsWith("xunit") || 
                        name.StartsWith("Terminal.Gui"))
                    {
                        continue;
                    }
                    
                    visitor.Visit(asmSymbol.GlobalNamespace);
                }
            }

            // ACTION 1: Check DECLARED types using SymbolAction (this also covers inherited properties correctly)
            compilationContext.RegisterSymbolAction(symbolContext =>
            {
                var namedType = (INamedTypeSymbol)symbolContext.Symbol;

                if (!InheritsFrom(namedType, crdtContextType))
                {
                    return;
                }

                var localRegisteredTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
                var contextAttributes = namedType.GetAttributes()
                    .Where(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, serializableAttributeSymbol));

                foreach (var attr in contextAttributes)
                {
                    if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is ITypeSymbol typeSymbol)
                    {
                        localRegisteredTypes.Add(typeSymbol.WithNullableAnnotation(NullableAnnotation.None));
                    }
                }

                foreach (var registeredType in localRegisteredTypes)
                {
                    var nsName = registeredType.ContainingNamespace?.ToDisplayString();
                    if (nsName != null && (nsName.StartsWith("System") || nsName.StartsWith("Microsoft")))
                    {
                        continue;
                    }

                    foreach (var prop in GetAllPublicProperties(registeredType))
                    {
                        var propType = prop.Type.WithNullableAnnotation(NullableAnnotation.None);

                        if (IsSimpleType(propType)) continue;

                        if (!globalRegisteredTypes.ContainsKey(propType))
                        {
                            ReportDiagnostic(symbolContext.ReportDiagnostic, namedType, prop.Name, registeredType.Name, propType);
                        }
                    }
                }
            }, SymbolKind.NamedType);

            // ACTION 2: Check INITIALIZER types using SyntaxNodeAction (Naturally provides the shared SemanticModel!)
            compilationContext.RegisterSyntaxNodeAction(syntaxContext =>
            {
                var propSyntax = (PropertyDeclarationSyntax)syntaxContext.Node;
                
                if (propSyntax.Initializer == null) return;

                var propertySymbol = syntaxContext.SemanticModel.GetDeclaredSymbol(propSyntax) as IPropertySymbol;
                if (propertySymbol == null) return;

                var containingType = propertySymbol.ContainingType;
                
                // Only inspect properties of types that are registered in our CRDT contexts
                if (globalRegisteredTypes.TryGetValue(containingType, out var contextType))
                {
                    ITypeSymbol? initType = null;
                    
                    if (propSyntax.Initializer.Value is ObjectCreationExpressionSyntax objCreation)
                    {
                        initType = syntaxContext.SemanticModel.GetTypeInfo(objCreation, syntaxContext.CancellationToken).Type;
                    }
                    else if (propSyntax.Initializer.Value is CollectionExpressionSyntax collectionExpr)
                    {
                        var typeInfo = syntaxContext.SemanticModel.GetTypeInfo(collectionExpr, syntaxContext.CancellationToken);
                        initType = typeInfo.ConvertedType ?? typeInfo.Type;
                    }
                    else if (propSyntax.Initializer.Value is ImplicitObjectCreationExpressionSyntax implicitCreation)
                    {
                        initType = syntaxContext.SemanticModel.GetTypeInfo(implicitCreation, syntaxContext.CancellationToken).Type;
                    }

                    if (initType != null)
                    {
                        initType = initType.WithNullableAnnotation(NullableAnnotation.None);
                        
                        if (!IsSimpleType(initType) && !globalRegisteredTypes.ContainsKey(initType))
                        {
                            var propType = propertySymbol.Type.WithNullableAnnotation(NullableAnnotation.None);
                            // Prevent duplicate reporting if declared and initialized types are exactly the same
                            if (!SymbolEqualityComparer.Default.Equals(initType, propType) || globalRegisteredTypes.ContainsKey(propType))
                            {
                                ReportDiagnostic(syntaxContext.ReportDiagnostic, contextType, propertySymbol.Name, containingType.Name, initType);
                            }
                        }
                    }
                }
            }, SyntaxKind.PropertyDeclaration);
        });
    }

    private static void ReportDiagnostic(Action<Diagnostic> reportAction, INamedTypeSymbol contextType, string propertyName, string registeredTypeName, ITypeSymbol missingType)
    {
        var location = contextType.Locations.FirstOrDefault();

        var diagnostic = Diagnostic.Create(
            Rule,
            location,
            propertyName,
            registeredTypeName,
            missingType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            contextType.Name);
            
        reportAction(diagnostic);
    }

    private static IEnumerable<IPropertySymbol> GetAllPublicProperties(ITypeSymbol typeSymbol)
    {
        var current = typeSymbol;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            foreach (var member in current.GetMembers().OfType<IPropertySymbol>())
            {
                if (member.DeclaredAccessibility == Accessibility.Public && !member.IsStatic)
                {
                    yield return member;
                }
            }
            current = current.BaseType;
        }
    }

    private static bool InheritsFrom(INamedTypeSymbol symbol, INamedTypeSymbol baseType)
    {
        var current = symbol.BaseType;
        while (current != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, baseType))
            {
                return true;
            }
            current = current.BaseType;
        }
        return false;
    }

    private static bool IsSimpleType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol namedType && namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            type = namedType.TypeArguments[0];
        }

        if (type.TypeKind == TypeKind.Enum) 
        {
            return true;
        }

        switch (type.SpecialType)
        {
            case SpecialType.System_Boolean:
            case SpecialType.System_Char:
            case SpecialType.System_SByte:
            case SpecialType.System_Byte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
            case SpecialType.System_Decimal:
            case SpecialType.System_Single:
            case SpecialType.System_Double:
            case SpecialType.System_String:
            case SpecialType.System_DateTime:
                return true;
        }

        var name = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (name == "global::System.Guid" ||
            name == "global::System.DateTimeOffset" ||
            name == "global::System.TimeSpan")
        {
            return true;
        }

        return false;
    }

    private sealed class ContextSymbolVisitor : SymbolVisitor
    {
        private readonly INamedTypeSymbol _crdtContextType;
        private readonly INamedTypeSymbol _serializableAttr;
        private readonly Dictionary<ITypeSymbol, INamedTypeSymbol> _registeredTypes;

        public ContextSymbolVisitor(INamedTypeSymbol crdtContextType, INamedTypeSymbol serializableAttr, Dictionary<ITypeSymbol, INamedTypeSymbol> registeredTypes)
        {
            _crdtContextType = crdtContextType;
            _serializableAttr = serializableAttr;
            _registeredTypes = registeredTypes;
        }

        public override void VisitNamespace(INamespaceSymbol symbol)
        {
            if (symbol.IsGlobalNamespace == false)
            {
                var name = symbol.Name;
                if (symbol.ContainingNamespace?.IsGlobalNamespace == true)
                {
                    // Skip massive framework namespaces to save time
                    if (name == "System" || name == "Microsoft" || name == "Windows")
                    {
                        return;
                    }
                }
            }

            foreach (var member in symbol.GetMembers())
            {
                member.Accept(this);
            }
        }

        public override void VisitNamedType(INamedTypeSymbol symbol)
        {
            if (InheritsFrom(symbol, _crdtContextType))
            {
                foreach (var attr in symbol.GetAttributes())
                {
                    if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, _serializableAttr))
                    {
                        if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is ITypeSymbol typeSymbol)
                        {
                            var cleanType = typeSymbol.WithNullableAnnotation(NullableAnnotation.None);
                            if (!_registeredTypes.ContainsKey(cleanType))
                            {
                                _registeredTypes.Add(cleanType, symbol);
                            }
                        }
                    }
                }
            }

            foreach (var member in symbol.GetTypeMembers())
            {
                member.Accept(this);
            }
        }
    }
}