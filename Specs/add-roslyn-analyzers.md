<!---Human--->
# Purpose
<!---
Add the purpose of this user story.
--->
As a dev I want to include analyzers that will help users mpa the strategy attributes with types that thy support.

<!---Human--->
# Requirements
<!---
Add the requirements, technical or not.
--->
Right now, the user can mark any property with any strategy. If the strategy is not supporting the type of the property at the best it silently does nothing and at the worst throws an exception.

I want to have Roslyn analyzers do that in the solution. An analyzer should check the attribute usage and the type of the property marked and should error in case that the combination is not supported at compile time.

We need a project for those analyzers and tha analyzer artifacts muct be packed with the project `Ama.CRDT`.

The logic to find out if it is supported should follow the logic:
- Find the properties with a `CrdtStrategyAttribute`
- Jump to the strategy type from the type defined there.
- Check the type for `CrdtSupportedType` (a new custom attribute that decorates a strategy, can be multiple)
- Error if it is not derived from a supported type defined in one of the attributes' types.

<!---Human--->
## Requirements context
<!---
Add files that we will load for the UI to add context for the solution design.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
--->
- `$/Ama.CRDT/Attributes/CrdtStrategyAttribute.cs`

<!---Human--->
# Testing Methodology
<!---
Add the testing methodology (manual, unit, integration, end-to-end tests?)
--->
The primary testing method will be through unit tests within the dedicated analyzer test project. These tests will use the `Microsoft.CodeAnalysis.Testing` framework to supply source code snippets and verify that the analyzer produces the expected diagnostics (or no diagnostics) for various valid and invalid attribute usages.

- **Valid Cases:** Test scenarios where strategy attributes are correctly applied to properties of supported types.
- **Invalid Cases:** Test scenarios where strategy attributes are applied to properties of unsupported types.
- **Edge Cases:** Test properties with no CRDT attributes, properties with multiple non-CRDT attributes, and complex types (e.g., generics, derived classes, interfaces).

Manual testing will involve referencing the `Ama.CRDT` project in a sample console application to confirm that Visual Studio displays the compile-time errors as expected in the editor.

<!---AI - Stage 1--->
# Proposed Solutions [AI - Stage 1]
<!---
Here you will need to put a number of solutions that would fit for this problem.
Add the solutions that you rejected as well.
--->
### Solution 1: Standard Roslyn Analyzer Project (Recommended)
This approach involves creating a new, dedicated project for the Roslyn analyzers using the standard Visual Studio template.
-   **Description:** A new `Ama.CRDT.Analyzers` project will be created. This project will contain the diagnostic analyzer logic. The main `Ama.CRDT` project will then reference the analyzer project. This is configured in the `.csproj` file to package the analyzer's DLLs into the `analyzers` folder of the `Ama.CRDT` NuGet package, making them available to any project that consumes the package without adding a runtime dependency.
-   **Pros:**
    -   It is the standard, officially supported method for creating and distributing Roslyn analyzers.
    -   Project templates automatically set up the analyzer, code fix provider, and unit test projects, simplifying development and testing.
    -   MSBuild integration for packaging is handled automatically and correctly.
-   **Cons:**
    -   Adds a few new projects to the solution (`.Analyzers`, `.Analyzers.CodeFixes`, `.Analyzers.UnitTests`).

### Solution 2: Source Generator with Diagnostics
This approach uses a Source Generator instead of a classic Diagnostic Analyzer to perform the analysis.
-   **Description:** An `IIncrementalGenerator` would be implemented to traverse the syntax tree, find the relevant attributes, and report diagnostics if the type compatibility rules are violated.
-   **Pros:**
    -   Can be useful if code generation based on the attributes were also required in the future.
-   **Cons:**
    -   This is not the primary purpose of a Source Generator. A `DiagnosticAnalyzer` is the purpose-built tool for static analysis and reporting diagnostics, making it a simpler and more direct solution.
    -   Adds unnecessary complexity for a problem that is purely about analysis, not code generation.
-   **Reason for Rejection:** Over-engineering the solution. The problem requires analysis only, for which a Diagnostic Analyzer is the ideal tool.

### Solution 3: Manual Class Library and MSBuild Configuration
This approach involves creating the analyzer logic in a standard class library and then manually configuring the `Ama.CRDT.csproj` to package it as an analyzer.
-   **Description:** The analyzer code would live in a regular class library. The `.csproj` file of the `Ama.CRDT` project would be hand-edited to include the compiled DLLs from this library into the `analyzers/dotnet/cs` folder of the final NuGet package.
-   **Pros:**
    -   Fewer projects are added to the solution compared to the standard template.
-   **Cons:**
    -   The process is manual, brittle, and error-prone.
    -   Lacks the benefits of the standard analyzer project template, such as simplified debugging and testing infrastructure.
-   **Reason for Rejection:** The standard template (Solution 1) provides a robust, reliable, and easier-to-maintain solution that handles all the complex MSBuild wiring automatically.

**Recommendation:** Solution 1 is strongly recommended as it aligns with best practices for Roslyn analyzer development and distribution, ensuring maintainability and correctness.

<!---AI - Stage 1--->
# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->
1.  **Project Creation:**
    -   Create a new Roslyn Analyzer project named `Ama.CRDT.Analyzers`. This will also generate `Ama.CRDT.Analyzers.CodeFixes` and `Ama.CRDT.Analyzers.UnitTests`.
    -   Add a reference from the `Ama.CRDT` project to `Ama.CRDT.Analyzers` and ensure the `.csproj` entry includes `<OutputItemType>Analyzer</OutputItemType>` and `<ReferenceOutputAssembly>false</ReferenceOutputAssembly>`.
    -   Add a project reference from `Ama.CRDT.Analyzers` to `Ama.CRDT` to get access to the attribute types.

2.  **Create `CrdtSupportedTypeAttribute`:**
    -   In the `Ama.CRDT` project, create a new file: `$/Ama.CRDT/Attributes/CrdtSupportedTypeAttribute.cs`.
    -   This attribute will take a `System.Type` as a constructor argument.
    -   It will be decorated with `[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]`.

3.  **Decorate Existing Strategies:**
    -   Modify each CRDT strategy class in `$/Ama.CRDT/Services/Strategies/` to include one or more `CrdtSupportedTypeAttribute`s defining the property types it can be applied to.
    -   For example, `CounterStrategy` will get `[CrdtSupportedType(typeof(int))]`, `[CrdtSupportedType(typeof(long))]`, etc. The `ArrayLcsStrategy` will get `[CrdtSupportedType(typeof(System.Collections.IEnumerable))]`.

4.  **Implement the Diagnostic Analyzer:**
    -   In `Ama.CRDT.Analyzers`, create a new `DiagnosticAnalyzer` named `CrdtStrategyTypeAnalyzer`.
    -   Define a `DiagnosticDescriptor` with an ID (e.g., "AMA0001"), title, message format (`The strategy '{0}' does not support the property type '{1}'.`), category ("Usage"), and `DiagnosticSeverity.Error`.
    -   In the `Initialize` method, register a symbol action for `SymbolKind.Property` (`context.RegisterSymbolAction(AnalyzeProperty, SymbolKind.Property)`).

5.  **Implement Analyzer Logic in `AnalyzeProperty`:**
    -   The method receives a `SymbolAnalysisContext`.
    -   Get the `IPropertySymbol` from the context.
    -   Iterate through the attributes on the property symbol.
    -   For each attribute, check if its class inherits from `CrdtStrategyAttribute`.
    -   If it does, get the strategy's `INamedTypeSymbol` from the `CrdtStrategyAttribute.StrategyType` property.
    -   Get the property's type symbol (`IPropertySymbol.Type`).
    -   Retrieve all `CrdtSupportedTypeAttribute`s from the strategy's type symbol.
    -   Extract the `ITypeSymbol` for the supported type from each `CrdtSupportedTypeAttribute`.
    -   Check if the property's type has an implicit conversion to any of the supported types using `context.Compilation.HasImplicitConversion()`. A custom check for assignability (walking up the type hierarchy) might be more robust.
    -   If no supported type is compatible with the property's type, report a diagnostic using `context.ReportDiagnostic()`.

6.  **Implement Unit Tests:**
    -   In the `Ama.CRDT.Analyzers.UnitTests` project, create a test file for `CrdtStrategyTypeAnalyzer`.
    -   Write tests to cover:
        -   A valid use case for a simple type (e.g., `[CrdtCounterStrategy]` on an `int`).
        -   An invalid use case (e.g., `[CrdtCounterStrategy]` on a `string`).
        -   A valid use case for a collection type (e.g., `[CrdtArrayLcsStrategy]` on a `List<string>`).
        -   An invalid use case for a collection type (e.g., `[CrdtArrayLcsStrategy]` on an `int`).
        -   A property with no CRDT attributes (should not trigger a diagnostic).

<!---AI - Stage 1--->
# Proposed Files Needed
<!---
Here you need to list the files you need to load in order to get the correct context for your solution to build and test.
Put in this list only the exising files that need to be modified/loaded. Not the new ones that need to be created.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
With each file in one line.
Remember to ask to load any unit tests if they are related to any files you will want to change.
--->
- `$/Ama.CRDT/Ama.CRDT.csproj` (To add the project reference to the new analyzer project)
- `$/Ama.CRDT/Attributes/CrdtStrategyAttribute.cs` (To get the base attribute definition)
- `$/Ama.CRDT/Services/Strategies/ArrayLcsStrategy.cs` (To be decorated with the new attribute)
- `$/Ama.CRDT/Services/Strategies/AverageRegisterStrategy.cs` (To be decorated with the new attribute)
- `$/Ama.CRDT/Services/Strategies/BoundedCounterStrategy.cs` (To be decorated with the new attribute)
- `$/Ama.CRDT/Services/Strategies/CounterStrategy.cs` (To be decorated with the new attribute)
- `$/Ama.CRDT/Services/Strategies/ExclusiveLockStrategy.cs` (To be decorated with the new attribute)
- `$/Ama.CRDT/Services/Strategies/FixedSizeArrayStrategy.cs` (To be decorated with the new attribute)
- `$/Ama.CRDT/Services/Strategies/GCounterStrategy.cs` (To be decorated with the new attribute)
- `$/Ama.CRDT/Services/Strategies/GSetStrategy.cs` (To be decorated with the new attribute)
- `$/Ama.CRDT/Services/Strategies/LseqStrategy.cs` (To be decorated with the new attribute)
- `$/Ama.CRDT/Services/Strategies/LwwMapStrategy.cs` (To be decorated with the new attribute)
- `$/Ama.CRDT/Services/Strategies/LwwSetStrategy.cs` (To be decorated with the new attribute)
- `$/Ama.CRDT/Services/Strategies/LwwStrategy.cs` (To be decorated with the new attribute)
- `$/Ama.CRDT/Services/Strategies/MaxWinsStrategy.cs` (To be decorated with the new attribute)
- `$/Ama.CRDT/Services/Strategies/MinWinsStrategy.cs` (To be decorated with the new attribute)
- `$/Ama.CRDT/Services/Strategies/OrMapStrategy.cs` (To be decorated with the new attribute)
- `$/Ama.CRDT/Services/Strategies/OrSetStrategy.cs` (To be decorated with the new attribute)
- `$/Ama.CRDT/Services/Strategies/PriorityQueueStrategy.cs` (To be decorated with the new attribute)
- `$/Ama.CRDT/Services/Strategies/SortedSetStrategy.cs` (To be decorated with the new attribute)
- `$/Ama.CRDT/Services/Strategies/StateMachineStrategy.cs` (To be decorated with the new attribute)
- `$/Ama.CRDT/Services/Strategies/TwoPhaseSetStrategy.cs` (To be decorated with the new attribute)
- `$/Ama.CRDT/Services/Strategies/VoteCounterStrategy.cs` (To be decorated with the new attribute)

<!---AI - Stage 2--->
# Changes Done
<!---
Here you add detailed information about all the changes actually done.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
Add all the things that you did in a different way than expected.
--->
- `$/Ama.CRDT/Ama.CRDT.csproj`: Added a `ProjectReference` to the new `Ama.CRDT.Analyzers` project. This is configured with `OutputItemType="Analyzer"` and `ReferenceOutputAssembly="false"` to ensure the analyzer is correctly packaged with the NuGet package without creating a runtime dependency.
- `$/Ama.CRDT/Attributes/CrdtSupportedTypeAttribute.cs`: Created a new attribute to decorate strategy classes. This attribute takes a `Type` argument to declare a supported property type for the strategy.
- All strategy files in `$/Ama.CRDT/Services/Strategies/`: Decorated each strategy class with one or more `[CrdtSupportedType]` attributes to define the types of properties they are compatible with (e.g., `IEnumerable`, `IDictionary`, `IComparable`, `int`, `object`).
- `$/Ama.CRDT.Analyzers/Ama.CRDT.Analyzers.csproj`: Created the project file for the Roslyn analyzer. It targets `netstandard2.0` for maximum compatibility and references the main `Ama.CRDT` project.
- `$/Ama.CRDT.Analyzers/CrdtStrategyTypeAnalyzer.cs`: Implemented the core analyzer logic. It inspects properties with CRDT strategy attributes, determines the strategy's supported types via the new `CrdtSupportedTypeAttribute`, and reports a compile-time error if the property's type is incompatible.
- `$/Ama.CRDT.Analyzers.UnitTests/Ama.CRDT.Analyzers.UnitTests.csproj`: Created the project file for the analyzer's unit tests.
- `$/Ama.CRDT.Analyzers.UnitTests/CrdtStrategyTypeAnalyzerTests.cs`: Implemented unit tests using the `Microsoft.CodeAnalysis.CSharp.Testing.XUnit` framework to verify the analyzer's correctness for both valid and invalid scenarios.

<!---AI - Stage 2--->
# Manual Changes Needed
<!---
Here you add detailed information about all the manual changes that might be needed to be done from a human.
Example types of changes are:
	- Configuration settings
	- Environment variables
	- Deployments/Scripts/Setups external to this app
	- Dependencies to external projects that would need changes (like nuget packages for example)
	- Settings in other systems (for example, enable some flag or permissions in Github)
If there are none, then just write "No manual changes needed to be applied."
--->
No manual changes are needed. The new analyzer and test projects will be automatically integrated into the solution. The analyzer itself will be packaged into the `Ama.CRDT` NuGet package upon publishing.

<!---AI - Stage 2--->
## Possible Techical Debt
<!---
Here you add comments about possible technical debt you encountered or implemented but it was too much to change or out of scope.
--->
The method for extracting the `StrategyType` from a `CrdtStrategyAttribute` in the analyzer relies on parsing the C# syntax of the attribute's constructor. This is slightly brittle; if the way strategy attributes are defined changes significantly (e.g., moving away from the `base(typeof(T))` pattern), the analyzer will break. A more resilient approach would be to pass the strategy type via a generic parameter on the base attribute (e.g., `CrdtStrategyAttribute<TStrategy>`), but this would be a breaking change to the public API, which was not requested. The current implementation is a good, non-invasive solution.

<!---AI - Stage 2--->
## Last notes and implementation details
<!---
Here you add comments about the implementation that didn't fit on the previous section.
--->
The implementation follows the recommended "Standard Roslyn Analyzer Project" solution. A new analyzer project (`Ama.CRDT.Analyzers`) and a corresponding unit test project (`Ama.CRDT.Analyzers.UnitTests`) have been created.

The core of the solution is the new `CrdtSupportedTypeAttribute`, which acts as a declarative contract on each strategy class. The `CrdtStrategyTypeAnalyzer` reads these contracts and compares them against the types of properties where CRDT attributes are applied.

Type compatibility is checked using `compilation.HasImplicitConversion`, which robustly handles inheritance, interface implementations, and numeric conversions. A special case was added to treat `[CrdtSupportedType(typeof(object))]` as a wildcard, meaning the strategy supports any property type. This is used for flexible strategies like `LwwStrategy`. The analyzer is packaged directly into the `Ama.CRDT` NuGet, providing immediate feedback to consumers of the library without requiring a separate installation.

# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->