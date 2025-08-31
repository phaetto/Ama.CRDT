<!---Human--->
# Purpose
<!---
Add the purpose of this user story.
--->
To refactor ordered-collection and map-based CRDT strategies (e.g., `ArrayLcsStrategy`, `LwwMapStrategy`) to be fully composable. This will enable the system to detect and generate patches for changes made to the properties of complex objects *within* a collection, rather than only detecting the addition or removal of the entire object.

This change provides three main benefits:
1.  **Efficiency:** It generates smaller, more precise patches. Instead of sending a whole object when only one field changes, only the modified property is sent over the wire, reducing network bandwidth and processing overhead.
2.  **Granularity:** It allows for finer-grained conflict resolution. If two users concurrently change different properties of the same object in a list or map, a non-composable strategy would see this as a conflict on the whole object. A composable strategy resolves this as two independent, non-conflicting changes to the object's properties.
3.  **Improved User Experience:** In collaborative applications, this prevents a user's minor edit (e.g., fixing a typo) from overwriting another user's concurrent edit on a different part of the same item, leading to more intuitive and less lossy merges.

<!---Human--->
# Requirements
<!---
Add the requirements, technical or not.
--->
1.  **Introduce `[CrdtCompose]` Attribute:**
    *   A new attribute, `CrdtComposeAttribute`, must be created.
    *   When an `IEnumerable` or `IDictionary` property is decorated with this attribute, its associated strategy (e.g., `ArrayLcsStrategy`, `LwwMapStrategy`) MUST perform recursive diffing on its elements/values to find nested changes.
    *   If the attribute is absent, the strategy MUST default to the existing, more performant behavior of treating any element modification as a "remove" of the old state and an "add" of the new state (for lists) or a single `Upsert` of the key-value pair (for maps). This makes the feature opt-in.
2.  **Refactor Ordered Collection and Map Strategies:**
    *   The `GeneratePatch` method in strategies like `ArrayLcsStrategy`, `SortedSetStrategy`, `LwwMapStrategy`, and `OrMapStrategy` must be updated.
    *   **For Collections:** When an element is identified as a `Match` in the LCS diff, the strategy must delegate back to the `ICrdtPatcher` by calling `patcher.DifferentiateObject` for the matched pair of elements. The path must use the stable original index (e.g., `$.myArray[originalIndex]`).
    *   **For Maps:** When a key exists in both the `from` and `to` dictionaries, the strategy must delegate back to `patcher.DifferentiateObject` for the corresponding value pair. The path must use the map key (e.g., `$.myMap['someKey']`).
3.  **Stable Element Identification:**
    *   **For Collections:** Compositional diffing requires an `IElementComparer` that checks for equality based on a stable, unique key (e.g., an `Id` property), not on deep value equality. The documentation must emphasize this requirement.
    *   **For Maps:** The map key itself serves as the stable identifier. The feature will rely on the key type's standard `Equals` and `GetHashCode` implementations.
4.  **No Changes to Core Services:** The core `CrdtPatcher` and `CrdtApplicator` services are architected to support this recursive model and should not require modification. The changes are confined to the strategy layer.
5.  **Out of Scope: Unordered Sets:** Unordered set strategies (`LwwSetStrategy`, `OrSetStrategy`, `GSetStrategy`, etc.) are not part of this refactoring. Their element-wise remove/add behavior, which relies on a deep-equality check, is considered correct for sets that lack a stable order or identifier.

<!---Human--->
## Requirements context
<!---
Add files that we will load for the UI to add context for the solution design.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
--->
-   `$/Ama.CRDT/Services/Strategies/ArrayLcsStrategy.cs` (Primary collection strategy to be refactored.)
-   `$/Ama.CRDT/Services/Strategies/SortedSetStrategy.cs` (Secondary ordered collection strategy to be refactored.)
-   `$/Ama.CRDT/Services/Strategies/LwwMapStrategy.cs` (Primary map strategy to be refactored.)
-   `$/Ama.CRDT/Services/Strategies/OrMapStrategy.cs` (Secondary map strategy to be refactored.)
-   `$/Ama.CRDT/Services/CrdtPatcher.cs` (The core service that will be called recursively.)
-   `$/Ama.CRDT/Services/ICrdtPatcher.cs` (The interface defining the `DifferentiateObject` method.)
-   `$/Ama.CRDT/Services/CrdtApplicator.cs` (To confirm its logic correctly handles nested patch application.)
-   `$/Ama.CRDT/Services/Strategies/ICrdtStrategy.cs` (The core strategy interface.)
-   `$/Ama.CRDT.UnitTests/Services/Strategies/ArrayLcsStrategyTests.cs` (Test file to be updated.)
-   `$/Ama.CRDT.UnitTests/Services/Strategies/SortedSetStrategyTests.cs` (Test file to be updated.)
-   `$/Ama.CRDT.UnitTests/Services/Strategies/LwwMapStrategyTests.cs` (Test file to be updated.)
-   `$/Ama.CRDT.UnitTests/Services/Strategies/OrMapStrategyTests.cs` (Test file to be updated.)

<!---Human--->
# Testing Methodology
<!---
Add the testing methodology (manual, unit, integration, end-to-end tests?)
--->
Unit tests for all affected collection and map strategies will be heavily expanded.

1.  **Compositional Tests for Collections (Opt-In):**
    *   **GIVEN:** An array with `[CrdtCompose]` and a key-based `IElementComparer`.
    *   **WHEN:** A single property of one element is modified.
    *   **THEN:** Assert a single, granular `CrdtOperation` is generated for the nested property (e.g., path `$.myList[1].name`).
2.  **Compositional Tests for Maps (Opt-In):**
    *   **GIVEN:** A dictionary with `[CrdtCompose]`.
    *   **WHEN:** A single property of a value object is modified.
    *   **THEN:** Assert a single, granular `CrdtOperation` is generated for the nested property (e.g., `$.myMap['someKey'].name`).
3.  **Non-Compositional / Opt-Out Tests:**
    *   **GIVEN:** A collection or map property *without* `[CrdtCompose]`.
    *   **WHEN:** An element/value is modified internally.
    *   **THEN:** Assert the patch contains operations for the entire element (remove/add for lists) or key-value pair (upsert for maps), preserving the existing behavior.
4.  **Complex Mixed-Operation Tests:**
    *   **GIVEN:** A `[CrdtCompose]`-enabled collection or map.
    *   **WHEN:** A series of changes occur simultaneously: an item is added, another is removed, and a third, existing item has a property updated.
    *   **THEN:** Assert the patch contains distinct and correct operations for each change, including a granular update for the modified property.
5.  **Regression Testing:** All existing tests for the affected strategies must be maintained and continue to pass.

<!---AI - Stage 1--->
# Proposed Solutions [AI - Stage 1]
<!---
Here you will need to put a number of solutions that would fit for this problem.
Add the solutions that you rejected as well.
--->

<!---AI - Stage 1--->
# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->

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

<!---AI - Stage 2--->
# Changes Done
<!---
Here you add detailed information about all the changes actually done.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
Add all the things that you did in a different way than expected.
--->

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

<!---AI - Stage 2--->
## Possible Techical Debt
<!---
Here you add comments about possible technical debt you encountered or implemented but it was too much to change or out of scope.
--->

<!---AI - Stage 2--->
## Last notes and implementation details
<!---
Here you add comments about the implementation that didn't fit on the previous section.
--->

# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->