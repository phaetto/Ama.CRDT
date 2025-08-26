<!---Human--->
# Purpose
<!---
Add the purpose of this user story.
--->
To refactor collection-based CRDT strategies, primarily `ArrayLcsStrategy`, to be fully composable. This will enable the system to detect and generate patches for changes made to the properties of complex objects *within* a collection, rather than only detecting the addition or removal of the entire object.

<!---Human--->
# Requirements
<!---
Add the requirements, technical or not.
--->
1.  **Recursive Diffing in Collections:** Collection strategies must be responsible for managing the collection's structure (e.g., membership, order), but must delegate the diffing of individual elements back to the main `ICrdtPatcher` service.
2.  **Handling In-Place Updates:** When an element is identified as existing in both the original and modified versions of a collection (e.g., a matched item in an LCS diff), the strategy must trigger a recursive diff on that element to generate patches for any internal property changes.
3.  **Refactor `ArrayLcsStrategy`:** The `GeneratePatch` method of `ArrayLcsStrategy` must be modified. After identifying the Longest Common Subsequence (LCS), it must iterate through the matched elements and call `patcher.DifferentiateObject` for each pair to find nested changes.
4.  **Pathing for Nested Elements:** The path provided for the recursive diff must be constructed using the element's stable index from the original collection (e.g., `$.myList[4].propertyName`).
5.  **Scope Limitation for Unordered Sets:** Unordered set strategies (like `LwwSetStrategy`, `OrSetStrategy`, etc.) will not be refactored for composition in this phase. Their current behavior of treating an update as a "remove" of the old state and an "add" of the new state is considered correct, as there is no stable, index-based pathing for their elements. This behavior relies on the `IElementComparer` performing a deep equality check.
6.  **No Core Service Changes:** The core `CrdtPatcher` and `CrdtApplicator` services are already architected to support this recursive model and should not require modification.

<!---Human--->
## Requirements context
<!---
Add files that we will load for the UI to add context for the solution design.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
--->
-   `$/Ama.CRDT/Services/Strategies/ArrayLcsStrategy.cs` (The primary strategy to be refactored)
-   `$/Ama.CRDT/Services/Strategies/SortedSetStrategy.cs` (A reference for the correct compositional pattern)
-   `$/Ama.CRDT/Services/CrdtPatcher.cs` (The core service that will be called recursively)
-   `$/Ama.CRDT/Services/ICrdtPatcher.cs` (The interface for the patcher)
-   `$/Ama.CRDT/Services/CrdtApplicator.cs` (To confirm its logic correctly handles nested patch application)
-   `$/Ama.CRDT/Services/Strategies/ICrdtStrategy.cs` (The core strategy interface)
-   `$/Ama.CRDT.UnitTests/Services/Strategies/ArrayLcsStrategyTests.cs` (The test file that will need significant updates to verify the new behavior)

<!---Human--->
# Testing Methodology
<!---
Add the testing methodology (manual, unit, integration, end-to-end tests?)
--->
Unit tests for `ArrayLcsStrategy` will be heavily expanded.

1.  **New Compositional Tests:** A new suite of tests will be created to specifically validate compositional behavior.
    *   A key scenario to test: An array of complex objects is diffed where the array structure is unchanged, but a single property of one element inside the array is modified. The test must assert that a single, nested `CrdtOperation` (e.g., `Upsert` or `Increment`) is generated for that specific property, not an `Upsert`/`Remove` operation on the array itself.
2.  **Regression Testing:** Existing tests for `ArrayLcsStrategy` (covering additions, removals, and reordering) will be maintained to ensure no regressions are introduced.
3.  **Complex Scenarios:** Tests will include scenarios with multiple nested changes across several elements in the same list to ensure all changes are captured correctly.

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