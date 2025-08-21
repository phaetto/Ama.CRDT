# Purpose
To develop a service that can take a JSON document and a CRDT-based patch (generated from the diff service) and apply the changes. This service is responsible for correctly interpreting the operations in the patch and modifying the JSON document to converge its state with another replica.

# Requirements
- The service must accept a base `System.Text.Json.Nodes.JsonNode` and a patch object.
- It must process each operation within the patch sequentially.
- For "Upsert" operations, it must navigate to the specified JSON Path. If the path does not exist, it should be created. The value at the target path should only be updated if the timestamp in the patch operation is newer than the timestamp of the existing value (LWW rule).
- For "Remove" operations, it must navigate to the specified JSON Path and remove the node.
- The service must handle conflicts gracefully using the LWW mechanism inherent in the patch operations.
- The service must return the new, modified `JsonNode` after applying all operations.
- The process must be idempotent: applying the same patch multiple times should result in the same final state as applying it once.

## Requirements context
This implementation depends on the core CRDT structures and the patch generation logic.
- `$/features/i-want-to-create-a-crdt-structure-for-all-json-to-be-able-to-replicate-across-services-specs/01-core-crdt-data-structures.md`
- `$/features/i-want-to-create-a-crdt-structure-for-all-json-to-be-able-to-replicate-across-services-specs/02-json-diff-and-patch-generation.md`

# Testing Methodology
The testing will be done through unit tests focusing on the patch application logic.
- **Apply Patches:** Create a base JSON and a patch. Apply the patch and assert that the resulting JSON is correct. Cover additions, updates, and removals.
- **Conflict Resolution:** Create a scenario where the base JSON has a "newer" version of a field than the incoming patch. Apply the patch and assert that the field's value does not change (LWW rule).
- **Path Creation:** Test applying a patch that targets a deeply nested path that does not exist in the base JSON. Assert that the service correctly creates the intermediate objects/arrays.
- **Idempotency:** Apply the same patch twice to the same base JSON and assert the result is identical to applying it once.
- **Empty Patch:** Apply an empty patch to a JSON document and ensure the document remains unchanged.

# Proposed Solutions
<!---
Here you will need to put a number of solutions that would fit for this problem.
Add the solutions that you rejected as well.
--->
1.  **Iterative Path Traversal:** This approach involves manually parsing the JSON Path string and iterating through the segments to traverse the `JsonNode` tree. For each segment, the code would check for the existence of the next node and create it if missing (`JsonObject` for properties, `JsonArray` for indices).
    *   **Pros:** No external dependencies, gives fine-grained control over the traversal and modification logic.
    *   **Cons:** The logic for creating nested paths and handling array resizing can become complex and error-prone. The code can be difficult to read and maintain.

2.  **Use an External JSON Path Library:** Leverage a third-party library (e.g., `JsonPath.Net`) to handle the selection of nodes based on the JSON Path. The service would use the library to find the target node and then apply the modification.
    *   **Pros:** Simplifies the node selection logic.
    *   **Cons:** Most JSON Path libraries are designed for querying, not modification or creation. The core challenge of creating non-existent paths would remain, requiring a hybrid approach. It also introduces an external dependency.

3.  **Custom Recursive Approach (Recommended):** This approach involves creating a private recursive function that processes the JSON Path one segment at a time. The function would take the current node and the remaining path segments. It would navigate or create the next node in the path and then call itself with that node and the rest of the path. The base case is when the path is fully traversed, at which point the "Upsert" or "Remove" operation is applied.
    *   **Pros:** Leads to cleaner, more maintainable, and logically sound code, especially for handling the creation of deeply nested structures. It remains self-contained within the project without external dependencies. The recursive nature maps well to the hierarchical structure of JSON.
    *   **Cons:** For extremely deep JSON structures (thousands of levels), recursion could potentially lead to stack overflow issues, but this is highly unlikely for typical use cases.

**Recommendation:** The **Custom Recursive Approach** is recommended. It provides the best balance of control, code clarity, and robustness for meeting the requirements, particularly the critical need to create paths on the fly.

# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->
1.  **Create `IJsonCrdtApplicator` Interface:**
    *   Define a new file `$/Modern.CRDT/Services/IJsonCrdtApplicator.cs`.
    *   This interface will declare a single method: `JsonNode Apply(JsonNode baseNode, CrdtPatch patch)`.

2.  **Implement `JsonCrdtApplicator` Service:**
    *   Create the implementation file `$/Modern.CRDT/Services/JsonCrdtApplicator.cs`.
    *   The public `Apply` method will handle initial checks (null inputs), clone the base `JsonNode` to ensure immutability of the input, and iterate through the operations in the `CrdtPatch`.
    *   For each operation, it will call a private recursive helper method to apply the change.

3.  **Develop the Recursive Patch Logic:**
    *   Inside `JsonCrdtApplicator`, implement a private recursive function to handle the traversal and application of a single `CrdtOperation`.
    *   This function will parse the JSON Path from the operation into a queue of segments.
    *   It will recursively traverse the `JsonNode` graph, creating `JsonObject` or `JsonArray` nodes as needed if parts of the path do not exist.
    *   At the final path segment, it will perform the "Upsert" or "Remove" operation.
    *   The "Upsert" logic will include the Last-Writer-Wins (LWW) check: it will only apply the update if the operation's timestamp is greater than the timestamp of the existing data. This requires a convention for storing timestamps in the JSON itself (e.g., wrapping values in an object like `{ "v": ..., "t": ... }`).

4.  **Create Unit Tests:**
    *   Create a new test file `$/Modern.CRDT.UnitTests/Services/JsonCrdtApplicatorTests.cs`.
    *   Implement tests covering all scenarios outlined in the "Testing Methodology" section:
        *   Simple upsert (add and update).
        *   LWW conflict resolution (older timestamp ignored).
        *   Node removal.
        *   Creation of deeply nested paths.
        *   Operations on arrays.
        *   Idempotency of patch application.
        *   Applying an empty patch.

# Proposed Files Needed
<!---
Here you need to list the files you need to load in order to get the correct context for your solution to build and test.
Put in this list only the exising files that need to be modified/loaded. Not the new ones that need to be created.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
With each file in one line.
Remember to ask to load any unit tests if they are related to any files you will want to change.
--->
- `$/Modern.CRDT/Models/CrdtOperation.cs` (To understand the structure of an individual patch operation)
- `$/Modern.CRDT/Models/CrdtPatch.cs` (To understand the overall patch structure which is a primary input to the new service)
- `$/Modern.CRDT/Models/OperationType.cs` (To use the `Upsert` and `Remove` enum values)
- `$/Modern.CRDT/Services/JsonCrdtPatcher.cs` (To understand how patches are generated, which informs how they should be applied)
- `$/Modern.CRDT.UnitTests/Services/JsonCrdtPatcherTests.cs` (To see concrete examples of generated patches that the new service must be able to consume)

# Changes Done
<!---
Here you add detailed information about all the changes actually done.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
Add all the things that you did in a different way than expected.
--->
- `$/Modern.CRDT/Services/IJsonCrdtApplicator.cs` (Created new file): Defined the interface for the patch application service.
- `$/Modern.CRDT/Services/JsonCrdtApplicator.cs` (Created new file): Implemented the service using a recursive approach to traverse JSON paths. The implementation includes:
    - Path creation for `Upsert` operations on non-existent paths.
    - Last-Writer-Wins (LWW) conflict resolution logic. This was implemented by wrapping all values in an object `{ "v": <value>, "t": <timestamp> }`. An operation only succeeds if its timestamp is greater than the existing timestamp.
    - Handling of `Remove` operations for both object properties and array elements. To ensure safety with array index shifting, `Remove` operations are processed after all `Upsert` operations and are sorted by path in descending order.
- `$/Modern.CRDT.UnitTests/Services/JsonCrdtApplicatorTests.cs` (Created new file): Added a comprehensive suite of xUnit tests to validate the `JsonCrdtApplicator`. The tests cover all requirements, including path creation, LWW conflict resolution, array manipulation, idempotency, and edge cases like empty patches or null documents.

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
- The current implementation of LWW transforms the underlying JSON document by wrapping every value in a `{ "v": ..., "t": ... }` object. While this correctly enforces LWW semantics, it makes the stored JSON more verbose and potentially harder for other, non-CRDT-aware systems to read. A more advanced solution might store metadata (like timestamps) separately from the data, but that would significantly increase complexity.
- The `JsonCrdtPatcher` service is not aware of this LWW `{v, t}` structure. It compares the wrapped objects directly. This works, but a more integrated solution would have the patcher understand the LWW convention and potentially generate more granular patches (e.g., only updating the 'v' or 't' fields). This is out of scope for the current task.

## Last notes and implementation details
<!---
Here you add comments about the implementation that didn't fit on the previous section.
--->
- The core logic is implemented in the `JsonCrdtApplicator` class. It processes `Upsert` and `Remove` operations separately to handle array modifications safely.
- `Upsert` operations are applied first, creating any necessary nested objects or arrays along the specified JSON Path.
- `Remove` operations are applied last. They are sorted by JSON Path in descending order (e.g., `$.tags[2]` before `$.tags[1]`) to prevent index shifting from invalidating subsequent operations within the same patch.
- The LWW conflict resolution is handled by the `GetTimestamp` and `CreateLwwNode` private methods, which read from and write to the `{ "v": ..., "t": ... }` structure. If a target node in the base document is not in this format, its timestamp is considered to be `0`.

# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->