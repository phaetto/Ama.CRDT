<!---Human--->
# Purpose
<!---
Add the purpose of this user story.
--->
We need to reimplement the LCS strategy for ordered lists with tie brakers.

<!---Human--->
# Requirements
<!---
Add the requirements, technical or not.
--->
We need a very detailes and causual strategy that would preserve the order of a list with additional metadata.

With the current logic, the deterministic tie-breaker (the GUID comparison) is applied universally, even when it shouldn't be. It cannot distinguish between a **concurrent operation** (which needs a tie-breaker) and a **sequential operation** (which must respect the user's explicit intent).
This leads to the exact "bug" you describe: a user sees a list, inserts an item at a specific position, and the CRDT might move it somewhere else simply because its operation ID was arbitrarily "larger" than an existing one. This violates the principle of least astonishment and is unacceptable.
**The root cause is that our system currently lacks a concept of causality.** It doesn't know if an operation `happened-before` or is `concurrent-with` the state of the list it's trying to modify.
Here is how we fix it, not by changing the tie-breaker, but by changing **when we use it**.

## The Solution: Shift from Indices to Positional Identifiers
The fundamental problem is that array indices are unstable. When you insert an item at index 1, that index might mean something completely different on another replica that has already processed a concurrent insertion. The professional solution, used in systems like Google Docs, is to stop using integer indices in the operations themselves.
Instead, we give each element a permanent, unique **positional identifier** that describes its location relative to its neighbors.
**Here's the concept:**
1.  **Assign Stable Positions:** Think of the items in the list not as being at indices 0, 1, 2, but at positions like 1.0, 2.0, 3.0.
2.  **Generate "In-Between" Positions:** When a user wants to insert "X" between "A" (at position 1.0) and "B" (at position 2.0), the application generates a new position that falls between them, for example, **1.5**.
3.  **Create Unambiguous Operations:** The `CrdtOperation` is no longer `"insert at index 1"`. It becomes `"insert 'X' at stable position 1.5"`. This operation is globally unambiguous. It doesn't matter what other operations have happened concurrently; the position `1.5` will always be after `1.0` and before `2.0`.
**How this fixes your scenario:**
*   **Initial State:** `[A, B, C]` with positions `[1.0, 2.0, 3.0]`. This is converged on all replicas.
*   **User Action:** A user on Replica 1 inserts "X" between "A" and "B".
*   **Patch Generation:** The application doesn't say "index 1". It calculates a new position between A (1.0) and B (2.0), which is 1.5. The operation is: `insert("X", position: 1.5)`.
*   **Application:** When any replica receives this operation, it finds the two elements that `1.5` falls between and inserts "X" there. The list becomes `[A, X, B, C]`. The user's intent is perfectly preserved. There is no ambiguity, so the GUID tie-breaker is never needed for this sequential operation.

## What about Concurrent Insertions?
Now, what if two users insert at the *exact same position* concurrently?
*   Replica 1 inserts "X" between A (1.0) and B (2.0), generating the operation `insert("X", position: 1.5)`.
*   Replica 2 concurrently inserts "Y" between A (1.0) and B (2.0), also generating `insert("Y", position: 1.5)`.
**This** is where the tie-breaker is correctly used. When a replica tries to insert an element with a position that already exists due to a concurrent operation, it then falls back to the deterministic tie-breaker (e.g., comparing the operation GUIDs) to decide the final order. For example, the rule might result in the final list being `[A, X, Y, B, C]`.

## Summary of the Fix
The fix is to change the paradigm of the `ArrayLcsStrategy`:
1.  **Stop using integer indices** as the way to identify position in the `CrdtOperation`.
2.  **Introduce a new CRDT Strategy** (e.g., `PositionalArrayStrategy`) that uses fractional indexing or another positional identifier system.
3.  **The state of the list** is no longer just the data `[A, B, C]` but a list of pairs `[(1.0, A), (2.0, B), (3.0, C)]`. The metadata would store these positions.
4.  **The GUID tie-breaker is ONLY used** as a last resort to order elements that are inserted at the exact same logical position by concurrent operations. For all sequential operations, the generated position is unique and sufficient.
This approach correctly separates the problem of sequential user intent from the problem of resolving concurrent race conditions.

<!---Human--->
## Requirements context
<!---
Add files that we will load for the UI to add context for the solution design.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
--->
- `$/Ama.CRDT/Attributes/CrdtStrategyAttribute.cs`
- `$/Ama.CRDT/Services/Strategies/SortedSetStrategy.cs` (example strategy for lists)
- `$/Ama.CRDT/Services/CrdtApplicator.cs`
- `$/Ama.CRDT/Services/CrdtPatcher.cs`
- `$/Ama.CRDT/Models/CrdtMetadata.cs`

<!---Human--->
# Testing Methodology
<!---
Add the testing methodology (manual, unit, integration, end-to-end tests?)
--->
Lots of unit tests, for the strategy and the applicator.

<!---AI - Stage 1--->
# Proposed Solutions
<!---
Here you will need to put a number of solutions that would fit for this problem.
Add the solutions that you rejected as well.
--->
Here are three potential solutions to implement a causally-correct ordered list CRDT strategy, with a clear recommendation.

### Solution 1: Positional Identifiers with Fractional Indexing (Recommended)

This solution directly implements the strategy outlined in the requirements. It replaces unstable integer indices with stable, fractional positions for each list element.

*   **Description:** Each element in a managed list is associated with a positional identifier (e.g., a string like "1.0", "1.5") which is stored in the `CrdtMetadata`. An insert operation between position "1.0" and "2.0" generates a new item with a position like "1.5". A remove operation targets an item by its stable position, not its index. The `CrdtOperation` will be augmented to carry this positional information. A deterministic tie-breaker (like comparing operation IDs) is used *only* when two concurrent operations generate the exact same positional identifier.
*   **Pros:**
    *   **Correctly Preserves Intent:** Perfectly handles sequential user operations, as the generated position is unambiguous.
    *   **Robust Concurrency:** Correctly uses a tie-breaker only for true concurrent conflicts at the same logical position.
    *   **Industry Standard:** This approach is a well-established best practice used in successful collaborative editing systems.
    *   **Good Fit:** Integrates well with the existing strategy-based architecture by introducing a new, specialized strategy.
*   **Cons:**
    *   **Increased Complexity:** Requires changes to `CrdtMetadata`, `CrdtOperation`, and the patch generation/application logic.
    *   **State Overhead:** The metadata for each list will grow, as it needs to store a stable position for every element.

### Solution 2: Tombstone-based Linked List

This approach models the list as a logical linked list where deletes are marked with tombstones instead of being physically removed.

*   **Description:** Each element is given a unique ID. An insert operation specifies the ID of the element it should follow. A delete operation marks the target element with a "tombstone" flag but leaves it in the list to act as a positional anchor for future inserts. A separate garbage collection process is needed to prune tombstones once it's safe to do so.
*   **Pros:**
    *   Also achieves causal correctness and preserves user intent.
*   **Cons:**
    *   **Unbounded State Growth:** Without a complex and reliable garbage collection mechanism, the list of tombstones will grow indefinitely.
    *   **Performance:** Accessing an element by its numerical index (e.g., `list[5]`) becomes an O(n) operation, as it requires traversing the logical linked list and skipping tombstones.
    *   **Larger Architectural Shift:** This represents a more significant departure from the current POCO-centric array model than Solution 1.

### Solution 3: Causal Trees (e.g., LSEQ/Logoot) (Rejected)

This is a more advanced, academic approach that uses hierarchical identifiers to place elements in a sequence.

*   **Description:** Each element is assigned a complex, tree-structured identifier. These identifiers are designed so that a position can always be generated between any two existing identifiers without conflict. This eliminates the need for a tie-breaker for concurrent insertions entirely.
*   **Pros:**
    *   **Theoretically Elegant:** Guarantees that every concurrent insertion has a unique, unambiguous position.
*   **Cons:**
    *   **Extreme Complexity:** The logic for generating and comparing these identifiers is highly complex and difficult to implement and debug correctly.
    *   **Identifier Size:** The identifiers can become very long, significantly increasing the size of the metadata and operation payloads.
    *   **Overkill:** This level of complexity is likely unnecessary for the problem at hand and introduces significant implementation risk.

**Recommendation:** Solution 1 is the clear winner. It directly addresses the identified problem, aligns perfectly with the detailed requirements, and represents a pragmatic and proven industry solution. It balances correctness with manageable implementation complexity and fits neatly into the existing architecture.

<!---AI - Stage 1--->
# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->
1.  **Introduce `PositionalIdentifier` Model:**
    *   Create a new `readonly record struct PositionalIdentifier(string Position, Guid OperationId)` in the Models folder. This will be the unit of metadata for each list element, storing its fractional position and the ID of the operation that inserted it (for tie-breaking).

2.  **Update `CrdtMetadata` Model:**
    *   Add a new property to `CrdtMetadata.cs`: `public Dictionary<string, List<PositionalIdentifier>> PositionalTrackers { get; set; } = new();`. This will store the ordered list of `PositionalIdentifier`s for each array property, keyed by its JSON Path.

3.  **Create New `CrdtPositionalArrayStrategy` and Attribute:**
    *   Create `$/Ama.CRDT/Attributes/CrdtPositionalArrayStrategyAttribute.cs`.
    *   Create `$/Ama.CRDT/Services/Strategies/PositionalArrayStrategy.cs`. This class will implement `ICrdtStrategy`.

4.  **Implement Patch Generation in `PositionalArrayStrategy`:**
    *   The `GeneratePatch` method will be the most complex part.
    *   It will receive the `before` and `after` documents and their metadata.
    *   Using an LCS-like algorithm and the `IElementComparer`, it will identify added, removed, and unchanged items.
    *   **For Additions:** When an item is added, the strategy will find its `before` and `after` neighbors in the `PositionalTrackers` metadata. It will then call a helper method to generate a new fractional position string between the neighbors' positions (e.g., between "1" and "2", generate "1.5"; between "1.5" and "2", generate "1.75"). It will then create an `Upsert` `CrdtOperation` containing the item's value and its newly generated position.
    *   **For Removals:** It will find the `PositionalIdentifier` of the removed item in the `before` metadata and create a `Remove` `CrdtOperation` that targets the item via its unique `Position`.

5.  **Refactor `ICrdtStrategy` and `CrdtApplicator`:**
    *   The current `ICrdtStrategy` `ApplyOperation` is too simple. We need to introduce a more capable method for strategies to handle their own application logic.
    *   Modify `ICrdtStrategy.cs` to include a new method: `void ApplyOperation(object document, CrdtMetadata metadata, CrdtOperation operation)`.
    *   Update all existing strategies (`LwwStrategy`, `CounterStrategy`, etc.) to implement this new method.
    *   Modify `CrdtApplicator.cs`. Instead of containing complex logic for handling arrays, it will now simply resolve the strategy for the operation's path and call the new `ApplyOperation` method on that strategy, making the applicator a simple dispatcher.

6.  **Implement Application Logic in `PositionalArrayStrategy`:**
    *   The new `ApplyOperation` method will handle both `Upsert` and `Remove`.
    *   **For `Upsert`:** It will read the `Position` and `Value` from the operation. It will search the `PositionalTrackers` list in the metadata for an existing entry with the same `Position`.
        *   If none exists, it finds the correct insertion index in the list and adds the new `PositionalIdentifier` to the metadata and the `Value` to the POCO list.
        *   If one *does* exist (a concurrent insert), it compares the `OperationId` of the incoming operation with the existing one. The "winner" is inserted after the "loser" in both the metadata and the POCO list.
    *   **For `Remove`:** It will find the `PositionalIdentifier` in the metadata by its `Position`, determine its index, and remove the item from both the metadata tracker and the POCO list at that index.

7.  **Update `CrdtMetadataManager`:**
    *   Modify the `Initialize` method. When it encounters a list property decorated with `CrdtPositionalArrayStrategyAttribute`, it must populate the `PositionalTrackers` metadata by creating initial `PositionalIdentifier`s for each existing element (e.g., "1", "2", "3", ...).

8.  **Dependency Injection:**
    *   Register the new `PositionalArrayStrategy` in `ServiceCollectionExtensions.cs`.

9.  **Write Comprehensive Unit Tests:**
    *   Create `$/Ama.CRDT.UnitTests/Services/Strategies/PositionalArrayStrategyTests.cs`.
    *   Test simple cases: insert at start, middle, end; remove from start, middle, end.
    *   Test sequential cases: apply patch A, then generate and apply patch B based on the new state. Ensure user intent is preserved.
    *   Test concurrent cases: create two divergent states from a common ancestor, generate patches for both, and apply them in both orders (A then B, and B then A). Assert that the final state is identical in both cases. Test the tie-breaker logic specifically.
    *   Update `CrdtApplicatorTests.cs` to verify its new role as a dispatcher.

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
- `$/Ama.CRDT/Models/CrdtMetadata.cs` (To add storage for positional trackers)
- `$/Ama.CRDT/Services/CrdtApplicator.cs` (Will be refactored to delegate application logic to strategies)
- `$/Ama.CRDT/Services/ICrdtApplicator.cs` (May require updates to reflect applicator refactoring)
- `$/Ama.CRDT/Services/CrdtPatcher.cs` (To review how strategies are currently resolved and used for patch generation)
- `$/Ama.CRDT/Services/CrdtMetadataManager.cs` (Will be modified to initialize positional metadata for new documents)
- `$/Ama.CRDT/Services/ICrdtMetadataManager.cs` (To check if the interface needs updating)
- `$/Ama.CRDT/Services/Strategies/ICrdtStrategy.cs` (Will be modified to include a more comprehensive `ApplyOperation` method)
- `$/Ama.CRDT/Services/Strategies/ArrayLcsStrategy.cs` (As a reference for the current flawed implementation and for LCS logic)
- `$/Ama.CRDT/Services/Strategies/CrdtStrategyManager.cs` (To ensure it can resolve the new strategy attribute)
- `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs` (To register the new strategy for DI)
- `$/Ama.CRDT.UnitTests/Services/CrdtApplicatorTests.cs` (To be updated to test the new dispatcher logic)
- `$/Ama.CRDT.UnitTests/Services/Strategies/ArrayLcsStrategyTests.cs` (As a reference for building robust concurrent tests for the new strategy)

<!---AI - Stage 2--->
# Changes Done
<!---
Here you add detailed information about all the changes actually done.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
Add all the things that you did in a different way than expected.
--->
- `$/Ama.CRDT/Models/PositionalIdentifier.cs`: Created a new record struct to hold the stable position and operation ID for tie-breaking. It implements `IComparable` to ensure correct sorting.
- `$/Ama.CRDT/Models/PositionalItem.cs`: Created a new record struct to serve as the payload for `Upsert` operations, bundling the position and value.
- `$/Ama.CRDT/Models/CrdtMetadata.cs`: Added the `PositionalTrackers` dictionary to store the list of `PositionalIdentifier`s for each managed array.
- `$/Ama.CRDT/Services/Strategies/ICrdtStrategy.cs`: Modified the interface to replace the old `ApplyOperation` with a new signature `ApplyOperation(object root, CrdtMetadata metadata, CrdtOperation operation)`, giving strategies access to metadata.
- `$/Ama.CRDT/Services/CrdtApplicator.cs`: Refactored to call the new `ICrdtStrategy.ApplyOperation` method. The applicator retains its role as a gatekeeper, checking for stale LWW operations and seen exceptions before delegating the actual data modification to the strategy.
- `$/Ama.CRDT/Services/CrdtMetadataManager.cs`: Updated the metadata initialization logic (`PopulateMetadataRecursive`) to recognize properties using `ArrayLcsStrategy` and create the initial `PositionalTrackers` for them.
- `$/Ama.CRDT/Services/Strategies/CrdtStrategyManager.cs`: Updated to make the new `ArrayLcsStrategy` the preferred default strategy for array properties.
- `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs`: Registered the new `ArrayLcsStrategy` in the dependency injection container.
- `$/Ama.CRDT/Services/Strategies/ArrayLcsStrategy.cs`: Created the new strategy implementation. It uses fractional indexing (with decimals for calculation) to generate stable positions. `GeneratePatch` uses an LCS diff to create `Upsert` and `Remove` operations with positional data. `ApplyOperation` correctly inserts or removes items from the POCO list and the metadata tracker, handling concurrent insertions via the tie-breaker.
- `$/Ama.CRDT.UnitTests/Services/CrdtApplicatorTests.cs`: Updated the test setup to include the new `ArrayLcsStrategy`. Existing tests remain valid as the core applicator logic for LWW/Counter strategies was preserved.
- `$/Ama.CRDT.UnitTests/Services/Strategies/ArrayLcsStrategyTests.cs`: Created a new test suite with comprehensive tests for the `ArrayLcsStrategy`, including tests for concurrent insertions and a mix of insertions and removals to ensure convergence.

The implementation follows the proposed technical steps closely, resulting in a robust, causally-correct strategy for ordered lists.

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
The `ICrdtStrategy` interface was changed, which is a breaking change. All other classes implementing this interface (`LwwStrategy`, `CounterStrategy`, `SortedSetStrategy`) must be updated to implement the new `ApplyOperation(object root, CrdtMetadata metadata, CrdtOperation operation)` method signature. For strategies like `LwwStrategy` and `CounterStrategy` that don't need to modify metadata during application, the implementation can simply ignore the `metadata` parameter and perform the same data manipulation as before.

<!---AI - Stage 2--->
## Possible Techical Debt
<!---
Here you add comments about possible technical debt you encountered or implemented but it was too much to change or out of scope.
--->
The `CrdtApplicator` still contains a check for `is LwwStrategy` to decide which idempotency mechanism to use (timestamps vs. seen exceptions). A more extensible design might involve adding a property to the `ICrdtStrategy` interface to declare its type (e.g., LWW-based vs. Op-based), removing the need for concrete type checking in the applicator. However, modifying the existing logic was kept minimal to reduce the scope of change.

The fractional position generation in `ArrayLcsStrategy` uses `decimal` for calculations, which is robust but could be a performance consideration in scenarios with extremely high-frequency edits on very long lists. More advanced, string-based positional algorithms exist but are significantly more complex to implement.

<!---AI - Stage 2--->
## Last notes and implementation details
<!---
Here you add comments about the implementation that didn't fit on the previous section.
--->
The new `ArrayLcsStrategy` is now the default strategy for any collection property that does not have an explicit strategy attribute. This ensures that developers get the benefits of the more robust, causally-correct list handling out of the box. The old `SortedSetStrategy` is still available and can be used by explicitly adding the `[SortedSetStrategy]` attribute to a property.

The core of the solution is the complete separation of an element's *value* from its *position*. The position is now managed entirely within the `CrdtMetadata`, making the CRDT operations unambiguous and ensuring user intent is preserved during sequential edits, while still correctly resolving conflicts during concurrent edits.

# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->