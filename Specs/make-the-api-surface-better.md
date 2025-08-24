<!---Human--->
# Purpose
<!---
Add the purpose of this user story.
--->
As a dev I want the surface of the API to be reliable, with proper excpetions thrown and necessary CodeAnalysis attributes.

<!---Human--->
# Requirements
<!---
Add the requirements, technical or not.
--->
- Make sure all the surface APIs throw relevant exceptions
- Make sure there is xml docs for models
- Make sure there are xml docs for interfaces (they will be used for DI)
- Make sure the implementation classes import xml docs
- Make sure we use proper CodeAnalysis attributes on parameters and methods.
- Make sure service collection extensions make sense and have xml docs.
- Make sure that the readme file is updated with the latest API calls/names/methods.

<!---Human--->
## Requirements context
<!---
Add files that we will load for the UI to add context for the solution design.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
--->
- `C:\sources\Ama.CRDT\Ama.CRDT\*.cs`
- `C:\sources\Ama.CRDT\README.md`

<!---Human--->
# Testing Methodology
<!---
Add the testing methodology (manual, unit, integration, end-to-end tests?)
--->
- **Unit Testing**: New unit tests will be added to verify that public API methods throw the expected exceptions (e.g., `ArgumentNullException`) when provided with invalid input. Existing tests will be reviewed to ensure they remain valid.
- **Static Analysis**: The build process will be configured to generate an XML documentation file, which will trigger compile-time warnings for any missing documentation on the public API surface, ensuring full coverage.
- **Manual Verification**: The `README.md` file will be manually reviewed to ensure all code examples and API descriptions are accurate and reflect the changes made.

<!---AI - Stage 1--->
# Proposed Solutions [AI - Stage 1]
<!---
Here you will need to put a number of solutions that would fit for this problem.
Add the solutions that you rejected as well.
--->
### Solution 1: Comprehensive API Sweep (Recommended)
This approach involves a systematic review of the entire public API surface of the `Ama.CRDT` library in a single, cohesive effort. We will iterate through every public type (interfaces, classes, models) and member, applying all required improvements simultaneously: adding parameter validation and exception throwing, writing detailed XML documentation, applying relevant `System.Diagnostics.CodeAnalysis` attributes, and using `<inheritdoc />` where appropriate.

-   **Pros**:
    -   Ensures maximum consistency in documentation style, exception handling, and attribute usage across the entire library.
    -   Addresses all requirements comprehensively in one pass, providing a clear and holistic improvement.
    -   Simplifies the review process by consolidating all related changes into a single set of modifications.
-   **Cons**:
    -   Results in a larger, single changeset that requires careful and thorough review.

-   **Reason for Recommendation**: This is the preferred approach because the requirements are deeply interconnected aspects of API design. Tackling them together guarantees a uniform and high-quality public contract. A piecemeal approach would risk introducing inconsistencies in documentation or validation logic.

### Solution 2: Incremental, Component-Based Refactoring
This approach involves breaking down the work by component or namespace. For example, one set of changes would focus solely on the `Models` namespace, another on the `Services/Strategies`, and so on.

-   **Pros**:
    -   Creates smaller, more focused changes that are quicker to review individually.
-   **Cons**:
    -   Can lead to inconsistencies in style and completeness if the work is done over an extended period.
    -   It's less efficient, as it requires revisiting the same components multiple times if, for instance, a model change impacts a service.

### Solution 3: Incremental, Requirement-Based Refactoring (Rejected)
This approach would involve addressing one requirement at a time across the entire codebase. For example, completing all `ArgumentNullException` checks first, then adding all XML documentation in a separate pass.

-   **Pros**:
    -   Focuses on a single type of task at a time.
-   **Cons**:
    -   Highly inefficient, requiring developers to open, edit, and understand the context of the same files multiple times.
    -   Greatly increases the risk of merge conflicts and regressions. This approach is not practical for this set of requirements.

<!---AI - Stage 1--->
# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->
1.  **Project Configuration**:
    -   Modify `$/Ama.CRDT/Ama.CRDT.csproj` to enable XML documentation generation by adding `<GenerateDocumentationFile>true</GenerateDocumentationFile>`. This will help enforce documentation completeness.

2.  **Document Public Models (`$/Ama.CRDT/Models`)**:
    -   Add comprehensive `<summary>` XML comments to all public classes, records, structs, interfaces, and enums and their properties. This includes `CrdtDocument<T>`, `CrdtMetadata`, `CrdtOperation`, `CrdtPatch`, `ICrdtTimestamp`, etc.

3.  **Document and Harden Service Interfaces (`$/Ama.CRDT/Services` and sub-folders)**:
    -   For all public interfaces (e.g., `ICrdtPatcher`, `ICrdtApplicator`, `ICrdtMetadataManager`, `ICrdtStrategy`), add detailed `<summary>`, `<param>`, and `<returns>` XML comments to each method and property. This defines the public contract for consumers.

4.  **Update Service Implementations**:
    -   In all public implementation classes, use `<inheritdoc />` on methods and properties to inherit documentation from the corresponding interfaces.
    -   Implement robust input validation at the beginning of every public method. Use `ArgumentNullException.ThrowIfNull()` for non-nullable reference types.
    -   Apply appropriate `System.Diagnostics.CodeAnalysis` attributes (e.g., `[DisallowNull]`) to parameters to improve the static analysis experience for library users.

5.  **Refine Dependency Injection Setup (`$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs`)**:
    -   Add detailed XML documentation to the `AddCrdt` extension method and its parameters to clarify the setup process and configuration options.
    -   Ensure the method signature and logic are clear and intuitive.

6.  **Add Unit Tests for Validation**:
    -   In the `$/Ama.CRDT.UnitTests` project, create new tests for key services (`CrdtPatcherTests`, `CrdtApplicatorTests`, etc.).
    -   These tests will specifically target the new validation logic, passing `null` or invalid arguments to public methods and asserting that the correct exception type (e.g., `ArgumentNullException`) is thrown.

7.  **Update `README.md`**:
    -   Review the entire `$/README.md` file.
    -   Verify that all code examples related to API usage and DI setup are correct and reflect the current API.
    -   If necessary, add a small section explaining the library's exception-throwing behavior for invalid arguments.

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
- `$/Ama.CRDT/Ama.CRDT.csproj` (To enable XML doc generation)
- `$/Ama.CRDT/Attributes/CrdtArrayLcsStrategyAttribute.cs` (API Surface Review)
- `$/Ama.CRDT/Attributes/CrdtCounterStrategyAttribute.cs` (API Surface Review)
- `$/Ama.CRDT/Attributes/CrdtLwwStrategyAttribute.cs` (API Surface Review)
- `$/Ama.CRDT/Attributes/CrdtSortedSetStrategyAttribute.cs` (API Surface Review)
- `$/Ama.CRDT/Attributes/CrdtStrategyAttribute.cs` (API Surface Review)
- `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs` (API Surface Review)
- `$/Ama.CRDT/Models/CrdtDocumentOfT.cs` (API Surface Review)
- `$/Ama.CRDT/Models/CrdtMetadata.cs` (API Surface Review)
- `$/Ama.CRDT/Models/CrdtOperation.cs` (API Surface Review)
- `$/Ama.CRDT/Models/CrdtOptions.cs` (API Surface Review)
- `$/Ama.CRDT/Models/CrdtPatch.cs` (API Surface Review)
- `$/Ama.CRDT/Models/EpochTimestamp.cs` (API Surface Review)
- `$/Ama.CRDT/Models/ICrdtTimestamp.cs` (API Surface Review)
- `$/Ama.CRDT/Models/OperationType.cs` (API Surface Review)
- `$/Ama.CRDT/Models/PositionalIdentifier.cs` (API Surface Review)
- `$/Ama.CRDT/Models/PositionalItem.cs` (API Surface Review)
- `$/Ama.CRDT/Services/CrdtApplicator.cs` (API Surface Review)
- `$/Ama.CRDT/Services/CrdtMetadataManager.cs` (API Surface Review)
- `$/Ama.CRDT/Services/CrdtPatchBuilder.cs` (API Surface Review)
- `$/Ama.CRDT/Services/CrdtPatcher.cs` (API Surface Review)
- `$/Ama.CRDT/Services/CrdtPatcherFactory.cs` (API Surface Review)
- `$/Ama.CRDT/Services/CrdtService.cs` (API Surface Review)
- `$/Ama.CRDT/Services/EpochTimestampProvider.cs` (API Surface Review)
- `$/Ama.CRDT/Services/ICrdtApplicator.cs` (API Surface Review)
- `$/Ama.CRDT/Services/ICrdtMetadataManager.cs` (API Surface Review)
- `$/Ama.CRDT/Services/ICrdtPatchBuilder.cs` (API Surface Review)
- `$/Ama.CRDT/Services/ICrdtPatcher.cs` (API Surface Review)
- `$/Ama.CRDT/Services/ICrdtPatcherFactory.cs` (API Surface Review)
- `$/Ama.CRDT/Services/ICrdtService.cs` (API Surface Review)
- `$/Ama.CRDT/Services/ICrdtTimestampProvider.cs` (API Surface Review)
- `$/Ama.CRDT/Services/Strategies/ArrayLcsStrategy.cs` (API Surface Review)
- `$/Ama.CRDT/Services/Strategies/CounterStrategy.cs` (API Surface Review)
- `$/Ama.CRDT/Services/Strategies/CrdtStrategyManager.cs` (API Surface Review)
- `$/Ama.CRDT/Services/Strategies/ElementComparerProvider.cs` (API Surface Review)
- `$/Ama.CRDT/Services/Strategies/ICrdtStrategy.cs` (API Surface Review)
- `$/Ama.CRDT/Services/Strategies/ICrdtStrategyManager.cs` (API Surface Review)
- `$/Ama.CRDT/Services/Strategies/IElementComparer.cs` (API Surface Review)
- `$/Ama.CRDT/Services/Strategies/IElementComparerProvider.cs` (API Surface Review)
- `$/Ama.CRDT/Services/Strategies/LwwStrategy.cs` (API Surface Review)
- `$/Ama.CRDT/Services/Strategies/SortedSetStrategy.cs` (API Surface Review)
- `$/Ama.CRDT.UnitTests/Services/CrdtApplicatorTests.cs` (To add exception handling tests)
- `$/Ama.CRDT.UnitTests/Services/CrdtPatcherTests.cs` (To add exception handling tests)
- `$/Ama.CRDT.UnitTests/Services/CrdtMetadataManagerTests.cs` (To add exception handling tests)
- `$/README.md` (To update documentation with API changes)

<!---AI - Stage 2--->
# Changes Done
<!---
Here you add detailed information about all the changes actually done.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
Add all the things that you did in a different way than expected.
--->
- **`$/Ama.CRDT/Ama.CRDT.csproj`**: Enabled XML documentation generation by adding `<GenerateDocumentationFile>true</GenerateDocumentationFile>`.
- **`$/Ama.CRDT/Models/*`**: Added comprehensive XML documentation to all public models, including records, structs, and enums.
- **`$/Ama.CRDT/Attributes/*`**: Reviewed and improved XML documentation on all CRDT strategy attributes.
- **`$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs`**: Added detailed XML documentation for all public extension methods, explaining their purpose and usage in the DI container setup.
- **`$/Ama.CRDT/Services/Interfaces/*`**: Added detailed XML documentation (`<summary>`, `<param>`, `<returns>`, `<exception>`) to all public interfaces, defining a clear contract for all services.
- **`$/Ama.CRDT/Services/Implementations/*`**:
    -   Added `<inheritdoc />` to all public methods in service implementations to inherit documentation from interfaces.
    -   Implemented robust null-checking for all public method parameters using `ArgumentNullException.ThrowIfNull()` and `ArgumentException.ThrowIfNullOrWhiteSpace()`.
    -   Applied `[DisallowNull]` attributes to parameters where appropriate to improve static analysis.
    -   Corrected a bug in `CrdtPatcher` and `CrdtApplicator` where `ArgumentNullException.ThrowIfNull` was being called on struct types (`CrdtDocument` and `CrdtPatch`), which has no effect. The checks were replaced with proper validation on the essential properties within the structs (e.g., `from.Metadata`).
- **`$/Ama.CRDT/Services/Strategies/Implementations/*`**: Added null checks to the `ApplyOperation` method in all strategy implementations, as this is on the hot path for the public-facing `ICrdtApplicator.ApplyPatch` method.
- **`$/Ama.CRDT.UnitTests/*`**:
    -   Added new unit tests to `CrdtApplicatorTests`, `CrdtPatcherTests`, and `CrdtMetadataManagerTests` to verify that `ArgumentNullException` and `ArgumentException` are correctly thrown for invalid inputs on their public methods.
- **`$/README.md`**: Reviewed the `README.md` file. No changes were necessary as the public API signatures remain the same, and the examples are still valid. The added robustness and documentation do not require changes to the README content.

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
No manual changes needed to be applied.

<!---AI - Stage 2--->
## Possible Techical Debt
<!---
Here you add comments about possible technical debt you encountered or implemented but it was too much to change or out of scope.
--->
- The default deep object comparer in `ElementComparerProvider` uses reflection and can be slow for large, complex objects in collections. While it provides a functional default, performance-critical applications should implement and register a type-specific `IElementComparer` to avoid this overhead. This is documented, but it remains a potential performance bottleneck if overlooked.
- Some deserialization logic within strategies (e.g., converting from `JsonElement` or `IDictionary<string, object>`) also relies on reflection (`Activator.CreateInstance`). This could be optimized using source generators or compiled expressions if it proves to be a bottleneck in high-throughput scenarios.

<!---AI - Stage 2--->
## Last notes and implementation details
<!---
Here you add comments about the implementation that didn't fit on the previous section.
--->
The API hardening was a comprehensive sweep across the library. The most significant change was not just adding documentation, but enforcing a strict "fail-fast" policy with argument validation on every public entry point. This makes the library more predictable and robust for consumers.

A key correction was made to how null checks were performed on `CrdtDocument<T>` and `CrdtPatch`, which are structs. The original code was attempting to check the struct variables for null, which is ineffective. The implementation was corrected to check the essential non-nullable reference type properties *within* the structs (like `CrdtMetadata`), which correctly enforces the API contract.

Unit tests were added to lock in this behavior, ensuring that passing null for required parameters results in the expected exceptions. This makes the API not only better documented but also verifiably more reliable.

# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->