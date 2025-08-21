# Purpose
<!---
Add the purpose of this user story.
--->
The current implementation of Last-Writer-Wins (LWW) modifies the JSON document by wrapping every value in a `{ "v": ..., "t": ... }` object. This makes the stored JSON verbose and difficult for non-CRDT-aware systems to parse. This story aims to refactor the LWW implementation to store metadata, such as timestamps, in a separate, parallel document. This will keep the primary data document clean and unmodified in structure, while still allowing for LWW conflict resolution during patch generation and application. The core patching and applying services will be updated to handle this new dual-document approach.

# Requirements
	- The current implementation of LWW transforms the underlying JSON document by wrapping every value in a `{ "v": ..., "t": ... }` object. While this correctly enforces LWW semantics, it makes the stored JSON more verbose and potentially harder for other, non-CRDT-aware systems to read. We need to store metadata (like timestamps) separately from the data.
	- The `JsonCrdtPatcher` service is not aware of this LWW `{v, t}` structure. It compares the wrapped objects directly. This works, but a more integrated solution would have the patcher understand the LWW convention and potentially generate more granular patches (e.g., only updating the 'v' or 't' fields). We need this too.
	- We should have parallel documents that we track this, so we leave the original documents untouched.

## Requirements context
- `$/features/i-want-to-create-a-crdt-structure-for-all-json-to-be-able-to-replicate-across-services-specs/01-core-crdt-data-structures.md`
- `$/features/i-want-to-create-a-crdt-structure-for-all-json-to-be-able-to-replicate-across-services-specs/02-json-diff-and-patch-generation.md`
- `$/features/i-want-to-create-a-crdt-structure-for-all-json-to-be-able-to-replicate-across-services-specs/03-json-patch-application.md`

# Testing Methodology
Update the existing unit tests.

# Proposed Solutions
<!---
Here you will need to put a number of solutions that would fit for this problem.
Add the solutions that you rejected as well.
--->
1.  **Recommended: Parallel Metadata JSON Document.**
    *   **Description:** For a given data document (`data.json`), we maintain a corresponding metadata document (`meta.json`). This `meta.json` mirrors the structure of the data document, but its leaf nodes contain the LWW timestamp for the corresponding value in `data.json`. The `JsonCrdtPatcher` and `JsonCrdtApplicator` services will be updated to operate on a pair of (data, metadata) documents.
    *   **Pros:**
        *   Achieves the primary goal of keeping the data document clean and free of CRDT-specific artifacts.
        *   The mirrored structure is intuitive and easy to reason about.
        *   Enforces a clear separation of concerns between data and its replication metadata.
    *   **Cons:**
        *   Increases complexity by requiring the management and synchronization of two documents.
        *   Could increase storage overhead, although this is an explicit trade-off for data cleanliness.
    *   **Reasoning for Recommendation:** This approach directly fulfills all the requirements outlined, especially the critical need to leave the original document structure untouched. It provides the cleanest separation and is the most robust long-term solution.

2.  **Rejected: Flattened Path-to-Timestamp Map.**
    *   **Description:** Instead of a parallel JSON tree, the metadata is stored as a flat dictionary where each key is a JSON Path string (`$.path.to.value`) and the value is the timestamp.
    *   **Pros:**
        *   Potentially more storage-efficient for documents with sparse updates.
        *   Direct lookup of a timestamp by path is very fast.
    *   **Cons:**
        *   Generating and parsing JSON Paths for every node during diffing can be computationally intensive.
        *   Does not handle array element identification well without more complex pathing logic.
        *   Loses the structural context that a mirrored tree provides, making some operations harder to implement.

3.  **Rejected: Single Document with a Reserved `_metadata` Key.**
    *   **Description:** Combine both data and metadata into a single JSON document, with the metadata stored under a reserved top-level key like `_metadata`. The structure under this key would mirror the data portion of the document.
    *   **Pros:**
        *   Keeps data and metadata in a single, atomic, and easily transportable unit.
    *   **Cons:**
        *   Directly violates the requirement to leave the original document untouched and clean for other systems. It pollutes the document root with a proprietary key.

# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->
1.  **Introduce a `CrdtDocument` Model:**
    *   Create a new record `CrdtDocument(JsonNode Data, JsonNode Metadata)` in the `Modern.CRDT/Models` folder. This will encapsulate the data and its corresponding metadata tree, simplifying method signatures.

2.  **Update Service Interfaces:**
    *   Modify `IJsonCrdtPatcher.GeneratePatch` to accept two `CrdtDocument` objects (`from` and `to`) instead of two `JsonNode` objects.
    *   Modify `IJsonCrdtApplicator.ApplyPatch` to accept a base `CrdtDocument` and return the updated `CrdtDocument`.

3.  **Refactor `JsonCrdtPatcher` Service:**
    *   Implement the updated `GeneratePatch` method signature.
    *   The core recursive comparison logic will now navigate both the `Data` and `Metadata` trees in parallel for the `from` and `to` documents.
    *   When a difference is found in the data, the patcher will consult the `Metadata` trees to retrieve the timestamps for the respective values.
    *   A `CrdtOperation` will only be generated for a change in the `to` document if its corresponding timestamp is greater than the timestamp in the `from` document.

4.  **Refactor `JsonCrdtApplicator` Service:**
    *   Implement the updated `ApplyPatch` method signature. It will take a `CrdtDocument` and a `CrdtPatch` as input.
    *   For each `CrdtOperation` in the patch, the applicator will:
        *   Navigate to the target path in the input `CrdtDocument`.
        *   Retrieve the existing timestamp from the `Metadata` node at that path.
        *   Apply the operation (update value in `Data` and timestamp in `Metadata`) only if the timestamp in the `CrdtOperation` is greater than the existing timestamp (LWW rule).
        *   Ensure that the logic can correctly create new paths in both the `Data` and `Metadata` trees if they do not exist.

5.  **Update Unit Tests:**
    *   Modify `JsonCrdtPatcherTests.cs` to test the new parallel document logic. Tests will now set up `from` and `to` states, each with a data and a metadata JSON, and verify that the generated patch correctly reflects LWW rules.
    *   Modify `JsonCrdtApplicatorTests.cs` to test the updated application logic. Tests will provide a base `CrdtDocument`, apply a patch, and assert that the resulting data and metadata are correct based on LWW conflict resolution.

# Proposed Files Needed
<!---
Here you need to list the files you need to load in order to get the correct context for your solution to build and test.
Put in this list only the exising files that need to be modified/loaded. Not the new ones that need to be created.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
With each file in one line.
Remember to ask to load any unit tests if they are related to any files you will want to change.
--->
	- `$/Modern.CRDT/Services/IJsonCrdtPatcher.cs` (To update the service interface for patch generation.)
	- `$/Modern.CRDT/Services/JsonCrdtPatcher.cs` (To implement the new LWW logic using parallel metadata documents.)
	- `$/Modern.CRDT.UnitTests/Services/JsonCrdtPatcherTests.cs` (To update unit tests to validate the new patcher implementation.)
	- `$/Modern.CRDT/Services/IJsonCrdtApplicator.cs` (To update the service interface for patch application.)
	- `$/Modern.CRDT/Services/JsonCrdtApplicator.cs` (To implement the new LWW logic for applying patches using metadata.)
	- `$/Modern.CRDT.UnitTests/Services/JsonCrdtApplicatorTests.cs` (To update unit tests to validate the new applicator implementation.)
	- `$/Modern.CRDT/Models/CrdtOperation.cs` (To have context on the operation model which contains the timestamp.)

# Changes Done
<!---
Here you add detailed information about all the changes actually done.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
Add all the things that you did in a different way than expected.
--->
- Created `$/Modern.CRDT/Models/CrdtDocument.cs` to encapsulate the data and metadata JSON documents.
- Updated `$/Modern.CRDT/Services/IJsonCrdtPatcher.cs` and its implementation `JsonCrdtPatcher.cs`. The `GeneratePatch` method now accepts two `CrdtDocument` instances. The logic was rewritten to compare data nodes while using the corresponding metadata nodes to enforce Last-Writer-Wins (LWW) rules, generating an operation only if the 'to' timestamp is greater than the 'from' timestamp.
- Updated `$/Modern.CRDT/Services/IJsonCrdtApplicator.cs` and its implementation `JsonCrdtApplicator.cs`. The `ApplyPatch` method now operates on a `CrdtDocument`. The logic was rewritten to apply operations to both the data and metadata trees, checking the existing timestamp in the metadata against the operation's timestamp to resolve conflicts according to LWW rules.
- Fully refactored `$/Modern.CRDT.UnitTests/Services/JsonCrdtPatcherTests.cs` to test the new dual-document comparison logic and LWW rules.
- Fully refactored `$/Modern.CRDT.UnitTests/Services/JsonCrdtApplicatorTests.cs` to test the updated patch application logic on `CrdtDocument` instances, including LWW conflict resolution.
- Renamed `Diff` to `GeneratePatch` in `IJsonCrdtPatcher` and `Apply` to `ApplyPatch` in `IJsonCrdtApplicator` for better clarity in the service interfaces.

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
- The current patch generation for removals relies on the metadata for the removed key still being present in the "to" document's metadata (effectively as a tombstone with a newer timestamp). While the implementation supports this, consumers of the service must ensure they create metadata tombstones for deletions.
- Array diffing is purely position-based. It does not detect item moves or content-based matches (like an LCS diff algorithm would), which could lead to more extensive patches (remove from old position, add to new position) than strictly necessary for arrays of objects.

## Last notes and implementation details
<!---
Here you add comments about the implementation that didn't fit on the previous section.
--->
- The core of this change is the introduction of the `CrdtDocument` which separates data from replication metadata. This keeps the primary data document clean and compatible with systems that are not CRDT-aware.
- The `JsonCrdtPatcher` now enforces LWW *during patch generation*. An operation for a modified value is only created if the timestamp for that value in the target document is newer than in the source document.
- The `JsonCrdtApplicator` enforces LWW *during patch application*. An operation from a patch is only applied if its timestamp is newer than the timestamp of the existing data at that path. This ensures idempotency and correct conflict resolution when applying patches from multiple sources.
- The unit tests were significantly updated to reflect this new paradigm, using separate `data` and `meta` JSON trees to set up test scenarios and validate outcomes.

# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->