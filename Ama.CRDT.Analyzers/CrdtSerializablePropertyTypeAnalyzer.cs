namespace Ama.CRDT.Analyzers;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CrdtSerializablePropertyTypeAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "CRDT0003";

    private static readonly LocalizableString Title = "Missing CrdtSerializable registration for property type";
    private static readonly LocalizableString MessageFormat = "The property '{0}' on registered class '{1}' uses type '{2}' which must be registered with [CrdtSerializable] on '{3}'";
    private static readonly LocalizableString Description = "All complex types used in properties of a registered CRDT model must also be explicitly registered in a CrdtContext to support AOT serialization.";
    private const string Category = "Usage";

    private static readonly DiagnosticDescriptor Rule = new(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var crdtContextType = compilationContext.Compilation.GetTypeByMetadataName("Ama.CRDT.Models.Aot.CrdtContext");
            var serializableAttributeSymbol = compilationContext.Compilation.GetTypeByMetadataName("Ama.CRDT.Attributes.CrdtSerializableAttribute");

            if (crdtContextType == null || serializableAttributeSymbol == null)
            {
                return;
            }

            // Pre-calculate all globally registered types across the entire compilation
            // to support cross-context and cross-project type resolution.
            var globalRegisteredTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
            var visitor = new ContextSymbolVisitor(crdtContextType, serializableAttributeSymbol, globalRegisteredTypes);
            
            // Visit ONLY the current assembly to prevent massive memory usage, timeouts, 
            // and AD0001 exceptions caused by traversing the entire GlobalNamespace (which includes mscorlib).
            visitor.Visit(compilationContext.Compilation.Assembly.GlobalNamespace);

            // Visit referenced assemblies that might contain base contexts, explicitly skipping huge BCL/System ones
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

            compilationContext.RegisterSymbolAction(symbolContext =>
            {
                AnalyzeNamedType(symbolContext, crdtContextType, serializableAttributeSymbol, globalRegisteredTypes);
            }, SymbolKind.NamedType);
        });
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context, INamedTypeSymbol crdtContextType, INamedTypeSymbol serializableAttributeSymbol, HashSet<ITypeSymbol> globalRegisteredTypes)
    {
        var namedType = (INamedTypeSymbol)context.Symbol;

        // Ensure we are inspecting a class that inherits from CrdtContext
        if (!InheritsFrom(namedType, crdtContextType))
        {
            return;
        }

        var localRegisteredTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        
        var contextAttributes = namedType.GetAttributes()
            .Where(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, serializableAttributeSymbol))
            .ToList();

        foreach (var attr in contextAttributes)
        {
            if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is ITypeSymbol typeSymbol)
            {
                localRegisteredTypes.Add(typeSymbol.WithNullableAnnotation(NullableAnnotation.None));
            }
        }

        // Iterate over all user-defined types that have been registered on THIS context
        foreach (var registeredType in localRegisteredTypes)
        {
            var nsName = registeredType.ContainingNamespace?.ToDisplayString();
            if (nsName != null && (nsName.StartsWith("System") || nsName.StartsWith("Microsoft")))
            {
                continue; // Skip inspecting internals of framework types like List<T>, Dictionary<K,V>
            }

            foreach (var prop in GetAllPublicProperties(registeredType))
            {
                var propType = prop.Type.WithNullableAnnotation(NullableAnnotation.None);

                if (IsSimpleType(propType))
                {
                    continue;
                }

                // 1. Verify the declared property type is registered ANYWHERE (globally)
                if (!globalRegisteredTypes.Contains(propType))
                {
                    ReportDiagnostic(context, namedType, prop, registeredType, propType);
                }

                // 2. Verify the initialized concrete type is registered ANYWHERE (e.g., initialized with 'new List<string>()')
                foreach (var syntaxRef in prop.DeclaringSyntaxReferences)
                {
                    var syntaxTree = syntaxRef.SyntaxTree;
                    
                    // Crucial safeguard to prevent "SyntaxTree is not part of the compilation" AD0001 Exception
                    if (syntaxTree == null || !context.Compilation.ContainsSyntaxTree(syntaxTree))
                    {
                        continue;
                    }

                    if (syntaxRef.GetSyntax(context.CancellationToken) is PropertyDeclarationSyntax propSyntax)
                    {
                        ITypeSymbol? initType = null;
                        
                        if (propSyntax.Initializer?.Value is ObjectCreationExpressionSyntax objCreation)
                        {
                            var semanticModel = context.Compilation.GetSemanticModel(syntaxTree);
                            initType = semanticModel.GetTypeInfo(objCreation, context.CancellationToken).Type;
                        }
                        else if (propSyntax.Initializer?.Value is CollectionExpressionSyntax collectionExpr)
                        {
                            var semanticModel = context.Compilation.GetSemanticModel(syntaxTree);
                            var typeInfo = semanticModel.GetTypeInfo(collectionExpr, context.CancellationToken);
                            initType = typeInfo.ConvertedType ?? typeInfo.Type;
                        }
                        else if (propSyntax.Initializer?.Value is ImplicitObjectCreationExpressionSyntax implicitCreation)
                        {
                            var semanticModel = context.Compilation.GetSemanticModel(syntaxTree);
                            initType = semanticModel.GetTypeInfo(implicitCreation, context.CancellationToken).Type;
                        }

                        if (initType != null)
                        {
                            initType = initType.WithNullableAnnotation(NullableAnnotation.None);
                            
                            if (!IsSimpleType(initType) && !globalRegisteredTypes.Contains(initType))
                            {
                                // Prevent duplicate reporting if declared and initialized types are exactly the same
                                if (!SymbolEqualityComparer.Default.Equals(initType, propType) || globalRegisteredTypes.Contains(propType))
                                {
                                    ReportDiagnostic(context, namedType, prop, registeredType, initType);
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private static void ReportDiagnostic(SymbolAnalysisContext context, INamedTypeSymbol contextType, IPropertySymbol prop, ITypeSymbol registeredType, ITypeSymbol missingType)
    {
        // Try to attach the diagnostic specifically to the context class definition
        var location = contextType.Locations.FirstOrDefault();

        var diagnostic = Diagnostic.Create(
            Rule,
            location,
            prop.Name,
            registeredType.Name,
            missingType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            contextType.Name);
            
        context.ReportDiagnostic(diagnostic);
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
        private readonly HashSet<ITypeSymbol> _registeredTypes;

        public ContextSymbolVisitor(INamedTypeSymbol crdtContextType, INamedTypeSymbol serializableAttr, HashSet<ITypeSymbol> registeredTypes)
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
                            _registeredTypes.Add(typeSymbol.WithNullableAnnotation(NullableAnnotation.None));
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