# Purpose
To create a robust service that can compare two JSON documents, identify the differences, and generate a serializable "patch". This patch will be a list of CRDT-based operations that can be transmitted and applied to another replica to achieve eventual consistency.

# Requirements
- The service must accept two `System.Text.Json.Nodes.JsonNode` objects as input: an "original" and a "modified" version.
- The comparison logic must be recursive to handle nested JSON objects and arrays.
- It must generate an "Upsert" operation for any new or changed values. This operation will be based on the Last-Writer-Wins (LWW) principle, requiring a value and a timestamp (or version vector).
- It must generate a "Remove" operation for any values that exist in the original but not in the modified version. This will create a tombstone.
- The output must be a well-defined, serializable patch object containing a list of operations, where each operation includes a JSON Path to the target field, the operation type (Upsert/Remove), and the necessary CRDT data (e.g., value and timestamp).
- The service should efficiently handle complex JSON structures without significant performance degradation.

## Requirements context
This implementation depends on the foundational CRDT structures being defined.
- `$/features/i-want-to-create-a-crdt-structure-for-all-json-to-be-able-to-replicate-across-services-specs/01-core-crdt-data-structures.md`

# Testing Methodology
Unit testing will be the core methodology.
- **Simple Diffs:** Test with basic JSON objects, covering additions, updates, and deletions of top-level key-value pairs.
- **Nested Diffs:** Test with deeply nested JSON objects to ensure the recursive comparison and JSON Path generation are correct.
- **Array Diffs:** Test with arrays of primitive values and arrays of objects. This should cover adding, removing, and updating elements within arrays.
- **No Difference:** Test with two identical JSON objects, ensuring an empty patch is generated.
- **Complex Scenarios:** Test a mix of all the above in a single, complex JSON document.

# Proposed Solutions
<!---
Here you will need to put a number of solutions that would fit for this problem.
Add the solutions that you rejected as well.
--->
Three potential solutions were considered to address the JSON diffing and patch generation requirement:

1.  **Manual Recursive Comparison (Recommended):**
    *   **Description:** This approach involves writing a custom, recursive service that traverses two `JsonNode` trees simultaneously to identify differences. It would build a list of CRDT-specific `Upsert` and `Remove` operations directly.
    *   **Pros:**
        *   Provides complete control over the diffing logic, ensuring it aligns perfectly with the LWW-based CRDT requirements.
        *   Avoids external dependencies, keeping the implementation self-contained.
        *   Allows for fine-tuned performance optimizations specific to the `System.Text.Json` library.
    *   **Cons:**
        *   Requires more development effort upfront.
        *   The logic for handling arrays and nested structures can be complex and requires careful implementation to avoid bugs.

2.  **Leverage an Existing JSON Diff Library:**
    *   **Description:** This solution proposes using a third-party library like `JsonDiffPatch.Net` to generate a standard JSON Patch (RFC 6902). This standard patch would then be translated into our custom CRDT patch format.
    *   **Pros:**
        *   Reduces development time by leveraging a well-tested library for the complex diffing logic.
    *   **Cons:**
        *   Introduces an external dependency that needs to be managed.
        *   The standard JSON Patch format does not inherently support CRDT concepts like LWW timestamps. The translation step adds complexity and potential for impedance mismatch, as the library's output might not map cleanly to the desired `Upsert`/`Remove` operations.

3.  **Flatten-and-Compare:**
    *   **Description:** This approach involves flattening both JSON documents into key-value pairs, where the key is the JSON Path and the value is the `JsonNode`. The two resulting dictionaries are then compared to find differences.
    *   **Pros:**
        *   Conceptually simpler than direct recursive tree traversal.
    *   **Cons:**
        *   Highly inefficient for memory and performance, as it requires creating complete flattened copies of both documents.
        *   Fails to correctly handle array manipulations. For instance, deleting an element from the beginning of an array would be misinterpreted as an update to all subsequent elements, generating an incorrect and inefficient patch.

**Recommendation:** The **Manual Recursive Comparison** approach is recommended. Despite its initial implementation complexity, it is the only solution that guarantees the generated patch will strictly adhere to the required CRDT semantics without compromises. The full control it offers is essential for building a robust and correct replication system.

# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->
1.  **Define Core Patching Models:**
    *   Create `Modern.CRDT/Models/CrdtOperation.cs`: A record to represent a single operation, containing a `string JsonPath`, `OperationType Type` (an enum), `JsonNode? Value`, and a `long Timestamp`.
    *   Create `Modern.CRDT/Models/OperationType.cs`: An enum with values `Upsert` and `Remove`.
    *   Create `Modern.CRDT/Models/CrdtPatch.cs`: A record to encapsulate a list of `CrdtOperation`s.

2.  **Create the JSON Patcher Service:**
    *   Create `Modern.CRDT/Services/IJsonCrdtPatcher.cs`: An interface defining the contract for the patching service, with a primary method `CrdtPatch Diff(JsonNode? original, JsonNode? modified)`.
    *   Create `Modern.CRDT/Services/JsonCrdtPatcher.cs`: A sealed class implementing `IJsonCrdtPatcher`. This class will house the recursive comparison logic.

3.  **Implement the Recursive Diff Logic:**
    *   The `JsonCrdtPatcher.Diff` method will orchestrate the comparison by calling a private recursive helper method.
    *   The helper method, `CompareNodes(string path, JsonNode? original, JsonNode? modified, List<CrdtOperation> operations)`, will perform the core comparison logic.
    *   It will handle all cases: different node types, null nodes, `JsonObject`, `JsonArray`, and `JsonValue`.
    *   For `JsonObject`, it will compare properties, generating `Remove` operations for missing keys and recursing for existing ones.
    *   For `JsonArray`, it will perform an index-based comparison, generating `Upsert` or `Remove` operations for mismatched or extra elements.
    *   For `JsonValue`, it will compare the values and generate an `Upsert` operation if they differ.
    *   Timestamps for operations will be generated using `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()`.

4.  **Develop Unit Tests:**
    *   Create `Modern.CRDT.UnitTests/Services/JsonCrdtPatcherTests.cs`.
    *   Write tests to cover all scenarios outlined in the "Testing Methodology" section, using xUnit, Moq, and Shouldly for assertions. This includes tests for simple changes, nested objects, array modifications, identical objects, and complex, mixed-change scenarios.

# Proposed Files Needed
<!---
Here you need to list the files you need to load in order to get the correct context for your solution to build and test.
Put in this list only the exising files that need to be modified/loaded. Not the new ones that need to be created.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
With each file in one line.
Remember to ask to load any unit tests if they are related to any files you will want to change.
--->
- `$/Modern.CRDT/Modern.CRDT.csproj` (To understand the main project's structure, dependencies, and to add new files.)
- `$/Modern.CRDT.UnitTests/Modern.CRDT.UnitTests.csproj` (To understand the test project's structure, dependencies, and to add new test files.)
- `$/Modern.CRDT/Models/LWW_Register.cs` (To review an existing LWW implementation for consistency in concepts like timestamps.)
- `$/Modern.CRDT.UnitTests/Models/LWW_RegisterTests.cs` (To understand existing testing conventions and patterns.)

# Changes Done
<!---
Here you add detailed information about all the changes actually done.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
Add all the things that you did in a different way than expected.
--->
- `$/Modern.CRDT/Models/CrdtOperation.cs`: Created a new record to define a single atomic operation within a patch.
- `$/Modern.CRDT/Models/CrdtPatch.cs`: Created a new record to act as a container for a list of `CrdtOperation`s.
- `$/Modern.CRDT/Models/OperationType.cs`: Created a new enum to define the supported patch operations (`Upsert`, `Remove`).
- `$/Modern.CRDT/Services/IJsonCrdtPatcher.cs`: Created a new interface for the JSON diffing service.
- `$/Modern.CRDT/Services/JsonCrdtPatcher.cs`: Implemented the recursive diffing logic as proposed. It traverses two `JsonNode` structures and generates `CrdtOperation`s for any differences found.
- `$/Modern.CRDT.UnitTests/Services/JsonCrdtPatcherTests.cs`: Created a comprehensive suite of unit tests to validate the `JsonCrdtPatcher` implementation, covering all scenarios outlined in the testing methodology.
- `$/FilesDescription.md`: Updated with descriptions for all newly created files.

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
The array comparison logic is a simple, index-based diff. If an element is inserted or removed from the beginning or middle of an array, the service will generate a cascade of `Upsert` operations for all subsequent elements. This is inefficient and can produce larger-than-necessary patches. A more sophisticated diffing algorithm, such as Longest Common Subsequence (LCS) or Myers diff, could be implemented to generate more precise `Insert` and `Delete` operations for arrays. However, this would add significant complexity and was not part of the initial requirement for a simple "index-based comparison".

## Last notes and implementation details
<!---
Here you add comments about the implementation that didn't fit on the previous section.
--->
The implementation follows the "Manual Recursive Comparison" strategy as planned.
- **JSON Path Format:** A dot-notation JSON Path format (e.g., `$.user.details.name`, `$.tags[0]`) was chosen for its widespread familiarity and readability. The recursive logic constructs these paths during traversal.
- **Timestamp Generation:** As required, timestamps for each operation are generated using `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()` to support the LWW (Last-Writer-Wins) model.
- **Immutability:** The service operates on `JsonNode` inputs and generates a new `CrdtPatch` object. Input nodes are not mutated. Values for `Upsert` operations are deep-cloned to ensure the patch is a self-contained snapshot.
- **Type Mismatches:** If a field's type changes (e.g., from a string to an object), the logic correctly generates a single `Upsert` operation to replace the old value with the new one.

# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->