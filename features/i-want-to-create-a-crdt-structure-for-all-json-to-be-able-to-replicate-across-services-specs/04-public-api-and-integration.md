# Purpose
To design and implement a high-level, user-friendly public API that encapsulates the complexity of the underlying diff, patch, and merge logic. This API will be the main entry point for developers using the CRDT library, providing a simple way to manage and synchronize JSON-based state.

# Requirements
- Create a primary service, for example, `IJsonCrdtService`, to orchestrate the CRDT operations.
- Provide a method to generate a patch from two JSON documents: `CrdtPatch CreatePatch(JsonNode original, JsonNode modified)`.
- Provide a method to apply a patch to a JSON document: `JsonNode Merge(JsonNode original, CrdtPatch patch)`.
- Provide a convenience method that combines both steps: `JsonNode Merge(JsonNode original, JsonNode modified)`.
- The API should be injectable via DI.
- The API design should be intuitive and hide the implementation details of patch generation and application.
- Include helper methods or overloads to seamlessly work with POCOs, handling the serialization to `JsonNode` and deserialization back to the POCO type internally.
- Remember that the metadata (LWW timestamps) are decoupled from the document itself. Maybe we should have the opportunity to deserialize that as well.

## Requirements context
This implementation integrates all previously developed components.
- `$/features/i-want-to-create-a-crdt-structure-for-all-json-to-be-able-to-replicate-across-services-specs/01-core-crdt-data-structures.md`
- `$/features/i-want-to-create-a-crdt-structure-for-all-json-to-be-able-to-replicate-across-services-specs/02-json-diff-and-patch-generation.md`
- `$/features/i-want-to-create-a-crdt-structure-for-all-json-to-be-able-to-replicate-across-services-specs/03-json-patch-application.md`
- `C:\sources\Modern.CRDT\features\i-want-to-create-a-crdt-structure-for-all-json-to-be-able-to-replicate-across-services-specs\put-the-lww-structures-in-metadata.md`
- `C:\sources\Modern.CRDT\Modern.CRDT\Models\CrdtDocument.cs`
- `C:\sources\Modern.CRDT\Modern.CRDT\Services\JsonCrdtPatcher.cs`

# Testing Methodology
Testing will focus on integration and end-to-end scenarios to ensure all components work together correctly.
- **End-to-End Merge:** Start with a base object. Create two divergent modifications of it.
  1. Generate a patch from `replica A` -> `base`.
  2. Apply this patch to `replica B`.
  3. Generate a patch from `replica B` -> `base`.
  4. Apply this patch to `replica A`.
  5. Assert that both replicas have converged to the exact same final state.
- **POCO Integration:** Test the helper methods that work with plain C# objects. Ensure they are correctly serialized, merged, and deserialized, with the final object reflecting the merged state.
- **API Usability:** Write tests that mimic how a developer would use the library, ensuring the public methods are easy to use and behave as expected.

# Proposed Solutions
<!---
Here you will need to put a number of solutions that would fit for this problem.
Add the solutions that you rejected as well.
--->
1.  **Recommended: Facade Service with `CrdtDocument` Wrapper.**
    *   **Description:** This approach introduces a `CrdtDocument` record to encapsulate both the `Data` (as `JsonNode` or a generic `T`) and the `Metadata` (as `JsonObject`). A new `JsonCrdtService` will act as a facade, orchestrating the underlying `IJsonCrdtPatcher` and `IJsonCrdtApplicator` services. Its public methods will accept and return `CrdtDocument` objects, providing a clean and explicit API for handling state that includes both data and its associated LWW metadata.
    *   **Reasoning:** This is the cleanest solution. It directly addresses the requirement of managing decoupled data and metadata by creating a logical container for them. This makes the API intuitive and type-safe. It aligns perfectly with DI and SOLID principles by separating the high-level orchestration (`JsonCrdtService`) from the low-level diffing and patching logic. The flow of data is explicit and predictable, which reduces the chance of errors.

2.  **Rejected: Metadata as Separate Method Parameters.**
    *   **Description:** Instead of a wrapper object, the service methods would take the data (`JsonNode`) and metadata (`JsonObject`) as separate parameters and would return a tuple like `(JsonNode mergedData, JsonObject mergedMetadata)`.
    *   **Reasoning for Rejection:** This leads to cluttered and lengthy method signatures (e.g., `CreatePatch(JsonNode, JsonObject, JsonNode, JsonObject)`). It's less object-oriented and increases the risk of developers accidentally passing the wrong metadata with a given data object. Encapsulation in a `CrdtDocument` is superior for readability and safety.

3.  **Rejected: Static Helper Class.**
    *   **Description:** Implement the orchestration logic in a static helper class with static methods, like `CrdtManager.Merge(...)`.
    *   **Reasoning for Rejection:** This approach is fundamentally incompatible with the requirement for Dependency Injection. It makes the code difficult to test in isolation, as dependencies like the patcher and applicator would have to be newed up internally or accessed via a service locator anti-pattern. An injectable service is the standard for modern, testable .NET applications.

# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->
1.  **Create `CrdtDocument` Models:**
    *   Create `$/Modern.CRDT/Models/CrdtDocument.cs`: A `readonly record struct` to hold `JsonNode Data` and `JsonObject Metadata`. This will be the standard DTO for JSON-based operations.
    *   Create `$/Modern.CRDT/Models/CrdtDocumentOfT.cs`: A generic `readonly record struct CrdtDocument<T>` to hold `T Data` and `JsonObject Metadata`. This will support the POCO helper methods.

2.  **Create `IJsonCrdtService` Interface:**
    *   Create `$/Modern.CRDT/Services/IJsonCrdtService.cs`.
    *   Define the public API contract using the new `CrdtDocument` models.
    *   Methods will include:
        *   `CrdtPatch CreatePatch(CrdtDocument original, CrdtDocument modified);`
        *   `CrdtDocument Merge(CrdtDocument original, CrdtPatch patch);`
        *   `CrdtDocument Merge(CrdtDocument original, CrdtDocument modified);`
        *   `CrdtPatch CreatePatch<T>(CrdtDocument<T> original, CrdtDocument<T> modified) where T : class;`
        *   `CrdtDocument<T> Merge<T>(CrdtDocument<T> original, CrdtPatch patch) where T : class;`
        *   `CrdtDocument<T> Merge<T>(CrdtDocument<T> original, CrdtDocument<T> modified) where T : class;`

3.  **Implement `JsonCrdtService` Class:**
    *   Create `$/Modern.CRDT/Services/JsonCrdtService.cs`.
    *   Inject `IJsonCrdtPatcher` and `IJsonCrdtApplicator` via the constructor.
    *   Implement the `CrdtDocument`-based methods by delegating to the injected services. The service will be responsible for unpacking the `CrdtDocument` before calling the patcher/applicator and wrapping the results back into a new `CrdtDocument`.
    *   Implement the generic `<T>` overloads. These methods will handle the `System.Text.Json.JsonSerializer` logic to convert POCOs to/from `JsonNode` before calling the non-generic counterparts.

4.  **Create DI Extension Method:**
    *   Create `$/Modern.CRDT/Extensions/ServiceCollectionExtensions.cs`.
    *   Add a public static method `AddJsonCrdt(this IServiceCollection services)`.
    *   This extension will register `IJsonCrdtPatcher`, `IJsonCrdtApplicator`, and `IJsonCrdtService` with their concrete implementations in the DI container, simplifying setup for consumers of the library.

5.  **Create Unit and Integration Tests:**
    *   Create `$/Modern.CRDT.UnitTests/Services/JsonCrdtServiceTests.cs`.
    *   Write tests to validate the orchestration logic, using Moq to verify calls to dependencies.
    *   Write end-to-end integration tests as described in the "Testing Methodology", ensuring two divergent replicas converge to an identical state (`Data` and `Metadata`).
    *   Write specific tests for the POCO overloads to ensure serialization/deserialization works as expected and that the final `CrdtDocument<T>` contains the correctly merged data and updated metadata.

# Proposed Files Needed
<!---
Here you need to list the files you need to load in order to get the correct context for your solution to build and test.
Put in this list only the exising files that need to be modified/loaded. Not the new ones that need to be created.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
With each file in one line.
Remember to ask to load any unit tests if they are related to any files you will want to change.
--->
- `$/Modern.CRDT/Services/IJsonCrdtPatcher.cs` (To be injected into the new `JsonCrdtService`)
- `$/Modern.CRDT/Services/JsonCrdtPatcher.cs` (Implementation needed for DI and testing)
- `$/Modern.CRDT/Services/IJsonCrdtApplicator.cs` (To be injected into the new `JsonCrdtService`)
- `$/Modern.CRDT/Services/JsonCrdtApplicator.cs` (Implementation needed for DI and testing)
- `$/Modern.CRDT/Models/CrdtPatch.cs` (Required for public API method signatures)
- `$/Modern.CRDT/Models/CrdtOperation.cs` (Required dependency for `CrdtPatch`)
- `$/Modern.CRDT/Modern.CRDT.csproj` (To add the new files to the project)
- `$/Modern.CRDT.UnitTests/Modern.CRDT.UnitTests.csproj` (To add the new test file to the test project)

# Changes Done
<!---
Here you add detailed information about all the changes actually done.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
Add all the things that you did in a different way than expected.
--->
- `$/Modern.CRDT/Models/CrdtDocument.cs` (Created): Added a record to encapsulate a document's data and its associated LWW metadata.
- `$/Modern.CRDT/Models/CrdtDocumentOfT.cs` (Created): Added a generic version of `CrdtDocument` to support POCOs.
- `$/Modern.CRDT/Services/IJsonCrdtService.cs` (Created): Defined the public API for the facade service.
- `$/Modern.CRDT/Services/JsonCrdtService.cs` (Created): Implemented the facade service to orchestrate patching and merging, including POCO serialization support.
- `$/Modern.CRDT/Extensions/ServiceCollectionExtensions.cs` (Created): Added a `AddJsonCrdt` extension method for easy DI setup.
- `$/Modern.CRDT/Modern.CRDT.csproj` (Modified): Added `Microsoft.Extensions.DependencyInjection` package reference.
- `$/Modern.CRDT.UnitTests/Services/JsonCrdtServiceTests.cs` (Created): Added end-to-end integration tests to verify convergence for both POCOs and raw `JsonNode`s.
- `$/Modern.CRDT.UnitTests/Modern.CRDT.UnitTests.csproj` (Modified): Added `Microsoft.Extensions.DependencyInjection` package reference to support test setup.
- `$/FilesDescription.md` (Modified): Added entries for all new files.

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

## Possible Techical Debt
<!---
Here you add comments about possible technical debt you encountered or implemented but it was too much to change or out of scope.
--->


## Last notes and implementation details
<!---
Here you add comments about the implementation that didn't fit on the previous section.
--->
The implementation follows the facade pattern, encapsulating the `IJsonCrdtPatcher` and `IJsonCrdtApplicator` services. A key design consideration was the management of LWW metadata (timestamps). The underlying services (`JsonCrdtPatcher` and `JsonCrdtApplicator`) rely on the `CrdtDocument` having pre-populated metadata. The new `JsonCrdtService` does not automatically generate or update these timestamps. This decision was made to keep the high-level API simple and delegate timestamp generation strategy to the consumer. This gives the developer flexibility to use various timestamp sources (e.g., client-side milliseconds, server-authoritative hybrid logical clocks). For testing purposes, metadata is manually constructed to ensure predictable outcomes. The POCO helper methods simply handle serialization and deserialization, they do not create or modify metadata.

# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->