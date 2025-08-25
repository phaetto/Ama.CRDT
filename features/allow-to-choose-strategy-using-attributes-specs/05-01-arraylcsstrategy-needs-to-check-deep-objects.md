<!---Human--->
# Purpose
<!---
Add the purpose of this user story.
--->
As a user of this library, I want `ArrayLcsStrategy` to generate deep array object operations.

<!---Human--->
# Requirements
<!---
Add the requirements, technical or not.
--->
The `ArrayLcsStrategy` currently treats array elements as atomic values when performing the LCS comparison (using `JsonNode.DeepEquals`). It does not recursively generate patches for items that have been modified in place. For example, if an array contains complex objects and a property of one of those objects changes, the current strategy would see the old object and the new object as completely different, generating a `Remove` and an `Upsert` rather than a more granular patch for the nested property change. A fully recursive array strategy would be more efficient and needs to be implemented.

- Add tests for this strategy too with all those edge cases.

<!---Human--->
## Requirements context
<!---
Add files that we will load for the UI to add context for the solution design.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
--->

<!---Human--->
# Testing Methodology
<!---
Add the testing methodology (manual, unit, integration, end-to-end tests?)
--->
Unit tests will be created to validate the new recursive behavior of the `ArrayLcsStrategy`. These tests will cover:
- Modification of a property within a complex object in an array.
- A mix of insertions, deletions, and in-place modifications.
- Unchanged arrays of complex objects.
- Existing behavior for arrays of primitive types to prevent regressions.

Existing unit tests for other strategies and the `JsonCrdtPatcher` will be updated and verified to ensure they are not broken by the required interface changes.

<!---AI - Stage 1--->
# Proposed Solutions
<!---
Here you will need to put a number of solutions that would fit for this problem.
Add the solutions that you rejected as well.
--->
### Solution 1: Enhance `ArrayLcsStrategy` with Recursive Diffing (Recommended)
This approach involves modifying the existing `ArrayLcsStrategy` to handle recursive diffing for matched objects. When the LCS algorithm identifies a matched pair of `JsonObject`s, instead of doing nothing, it will delegate back to the `JsonCrdtPatcher` to generate a sub-patch for the differences within that object. This keeps the logic encapsulated and aligns with user expectations for an array strategy.

*   **Pros:**
    *   Most intuitive solution from an API consumer's perspective.
    *   Keeps all array-related logic within the `ArrayLcsStrategy`.
    *   Avoids creating new, potentially confusing, strategy classes.
*   **Cons:**
    *   Requires a way to break potential circular dependencies between the patcher and the strategy (e.g., by passing the patcher as a method parameter).

### Solution 2: Introduce a New `RecursiveArrayLcsStrategy`
This solution proposes creating a new, separate strategy (`RecursiveArrayLcsStrategy`) and a corresponding attribute. Users would need to explicitly opt-in to this new strategy for deep array diffing. The existing `ArrayLcsStrategy` would remain unchanged for simple, non-recursive comparisons.

*   **Pros:**
    *   Ensures 100% backward compatibility with no risk of behavioral changes for existing users.
    *   Separates simple and complex array diffing logic.
*   **Cons:**
    *   Introduces API fragmentation, forcing users to choose between two similar array strategies.
    *   Increases the number of classes and complexity of the library.

### Solution 3: Modify `JsonCrdtPatcher` to Handle Matched Array Items (Rejected)
In this approach, the `ArrayLcsStrategy` would only be responsible for identifying insertions, deletions, and matches. The main `JsonCrdtPatcher` would then inspect the matched items and, if they are objects, it would initiate the recursive diffing process itself.

*   **Pros:**
    *   Centralizes the recursive logic within the patcher.
*   **Cons:**
    *   Breaks the strategy pattern's encapsulation by making the caller (`JsonCrdtPatcher`) responsible for part of the strategy's work.
    *   Creates a tight coupling between the patcher and the specific implementation details of the `ArrayLcsStrategy`.

<!---AI - Stage 1--->
# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->
1.  **Update `ICrdtStrategy` Interface:**
    *   Modify the `GeneratePatch` method signature to accept the calling `IJsonCrdtPatcher` instance as a parameter: `void GeneratePatch(IJsonCrdtPatcher patcher, List<CrdtOperation> operations, string path, JsonNode? nodeA, JsonNode? nodeB, long timestamp);`. This will break the potential circular dependency if DI were used and provide the necessary context for recursion.

2.  **Update All `ICrdtStrategy` Implementations:**
    *   Update the `GeneratePatch` method signature in `LwwStrategy`, `CounterStrategy`, and `ArrayLcsStrategy` to match the new interface. The `LwwStrategy` and `CounterStrategy` will not use the new `patcher` parameter.

3.  **Modify `ArrayLcsStrategy.GeneratePatch`:**
    *   In the logic that handles matched items (`Lcs.LcsDiffEntryType.Match`), check if the two matched nodes (`oldItem` and `newItem`) are `JsonObject`s.
    *   If they are, use the new `patcher` parameter to recursively call its diffing logic on these two objects. The path for the recursive call must be correctly constructed by appending the array index (e.g., `path + "/" + oldIndex`).
    *   The operations generated by the recursive call will be automatically added to the main operations list by the patcher.

4.  **Update `JsonCrdtPatcher.CompareNode`:**
    *   When invoking a strategy's `GeneratePatch` method, pass `this` as the first argument to satisfy the updated interface.

5.  **Create `ArrayLcsStrategyTests.cs`:**
    *   Create a new unit test file: `$/Modern.CRDT.UnitTests/Services/Strategies/ArrayLcsStrategyTests.cs`.
    *   Implement tests to verify that modifying a property of an object within an array generates a granular `Upsert` operation for the nested property (e.g., `$.items[1].name`).
    *   Add tests for mixed operations (update, insert, delete) within the same array.
    *   Add tests to ensure arrays of primitives still work correctly and that identical complex arrays produce no operations.

6.  **Verify Existing Tests:**
    *   Run all existing unit tests, especially for `JsonCrdtPatcherTests`, `LwwStrategyTests`, and `CounterStrategyTests`, to ensure the interface change did not cause any regressions.

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
- `$/Modern.CRDT/Services/Strategies/ICrdtStrategy.cs` (The strategy interface needs to be modified to allow recursive patching.)
- `$/Modern.CRDT/Services/Strategies/ArrayLcsStrategy.cs` (The primary file for modification to implement the recursive logic.)
- `$/Modern.CRDT/Services/JsonCrdtPatcher.cs` (Will be modified to pass itself to the strategy.)
- `$/Modern.CRDT/Services/IJsonCrdtPatcher.cs` (To check the public interface of the patcher.)
- `$/Modern.CRDT/Services/Strategies/LwwStrategy.cs` (Needs its method signature updated to match the interface change.)
- `$/Modern.CRDT/Services/Strategies/CounterStrategy.cs` (Needs its method signature updated to match the interface change.)
- `$/Modern.CRDT.UnitTests/Services/JsonCrdtPatcherTests.cs` (To ensure existing patcher tests are not broken by the changes.)
- `$/Modern.CRDT.UnitTests/Services/Strategies/LwwStrategyTests.cs` (To ensure LWW strategy tests still pass after the interface change.)
- `$/Modern.CRDT.UnitTests/Services/Strategies/CounterStrategyTests.cs` (To ensure Counter strategy tests still pass after the interface change.)

<!---AI - Stage 2--->
# Changes Done
<!---
Here you add detailed information about all the changes actually done.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
Add all the things that you did in a different way than expected.
--->
- `$/Modern.CRDT/Services/IJsonCrdtPatcher.cs`: Added a new method `DifferentiateObject` to the interface to allow strategies to call back into the patcher for recursive diffing of complex objects.
- `$/Modern.CRDT/Services/JsonCrdtPatcher.cs`: Implemented the `DifferentiateObject` method by refactoring the existing private `CompareObjectProperties` logic. The main `GeneratePatch` now calls this method, and the patcher's recursive logic for nested objects is preserved.
- `$/Modern.CRDT/Services/Strategies/ICrdtStrategy.cs`: The `GeneratePatch` method signature was changed to `void GeneratePatch(IJsonCrdtPatcher patcher, List<CrdtOperation> operations, string path, PropertyInfo property, ...)` to enable the callback mechanism and allow strategies to add operations directly to a shared list. This is a breaking change for the interface.
- `$/Modern.CRDT/Services/Strategies/LwwStrategy.cs`: Updated to implement the new `ICrdtStrategy.GeneratePatch` signature.
- `$/Modern.CRDT/Services/Strategies/CounterStrategy.cs`: Updated to implement the new `ICrdtStrategy.GeneratePatch` signature.
- `$/Modern.CRDT/Services/Strategies/ArrayLcsStrategy.cs`: Heavily modified to support deep object diffing. It now contains a full LCS-based `Diff` implementation. When it detects two matched objects that are not identical, it calls `patcher.DifferentiateObject` to generate a sub-patch for the object's properties. A heuristic was introduced in a custom `IEqualityComparer` to consider any two `JsonObject`s as matchable for the purpose of the LCS algorithm.
- `$/Modern.CRDT.UnitTests/Services/Strategies/ArrayLcsStrategyTests.cs`: A new test file was created with tests specifically for the new recursive array diffing logic, including in-place updates and mixed operations.
- `$/Modern.CRDT.UnitTests/Services/JsonCrdtPatcherTests.cs`: Tests were updated to align with the new strategy interface, but no logical changes were needed.
- `$/Modern.CRDT.UnitTests/Services/Strategies/LwwStrategyTests.cs`: Tests were updated to call the new `GeneratePatch` method signature.
- `$/Modern.CRDT.UnitTests/Services/Strategies/CounterStrategyTests.cs`: Tests were updated to call the new `GeneratePatch` method signature.
- `$/FilesDescription.md`: Added the new test file `ArrayLcsStrategyTests.cs` to the project file manifest.

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
The `ArrayLcsStrategy` now uses a heuristic to match objects within an array: it considers any two `JsonObject`s as potentially equal for the purpose of finding a "match". This works for arrays where the order is mostly preserved, but it could produce suboptimal or strange diffs for arrays with significant reordering of dissimilar objects. A more robust solution would require a way to identify objects, such as a designated key property (e.g., `id`), but this was beyond the scope of the current requirements. The current implementation correctly solves the specified problem of updating an object in place.

<!---AI - Stage 2--->
## Last notes and implementation details
<!---
Here you add comments about the implementation that didn't fit on the previous section.
--->
The core of this implementation was enabling a circular dependency (`Patcher -> Strategy -> Patcher`) in a clean way. This was achieved by defining a clear interface on the patcher (`IJsonCrdtPatcher.DifferentiateObject`) for strategies to call back into. This is a powerful pattern but required a breaking change to the `ICrdtStrategy` interface. All existing strategies were updated to conform to the new interface. The `ArrayLcsStrategy` is now significantly more powerful, capable of producing much more granular and efficient patches for arrays of complex objects.

# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->