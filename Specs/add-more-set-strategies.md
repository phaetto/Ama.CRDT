<!---Human--->
# Purpose
<!---
Add the purpose of this user story.
--->
Set Strategies

<!---Human--->
# Requirements
<!---
Add the requirements, technical or not.
--->
8.  **G-Set (Grow-Only Set):** The simplest set CRDT. You can only add items; removals are not possible.
9.  **2P-Set (Two-Phase Set):** Uses two G-Sets internally: one for additions and one for "tombstones" (removals). Once an item is removed, it can never be re-added.
10. **LWW-Set:** An element's membership is determined by its latest timestamp. An "add" operation gives it a timestamp, and a "remove" operation gives it a later one. This allows elements to be re-added after removal.
11. **OR-Set (Observed-Remove Set):** A more advanced set that allows re-addition without the anomalies of LWW-Set. It tags each element instance with a unique ID, so removals only affect specific instances.
12. **Set with Cardinality:** A set that also maintains a replicated, consistent count of its members, often by combining a set strategy with a counter strategy.

Make only the ones that do not break the public API, except the metadata and other models. Avoid breaking the interface public APIs.

Use the `CrdtMetadataManager` to provide utilities for metadata management.

<!---Human--->
## Requirements context
<!---
Add files that we will load for the UI to add context for the solution design.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
--->
- `C:\sources\Ama.CRDT\Ama.CRDT\Models\CrdtMetadata.cs`
- `C:\sources\Ama.CRDT\Ama.CRDT\Attributes\CrdtArrayLcsStrategyAttribute.cs`
- `C:\sources\Ama.CRDT\Ama.CRDT\Services\Strategies\ArrayLcsStrategy.cs`
- `C:\sources\Ama.CRDT\Ama.CRDT.UnitTests\Services\Strategies\ArrayLcsStrategyTests.cs`
- `C:\sources\Ama.CRDT\Ama.CRDT\Services\CrdtMetadataManager.cs`
- `C:\sources\Ama.CRDT\Ama.CRDT\Services\Helpers\PocoPathHelper.cs`

<!---Human--->
# Testing Methodology
<!---
Add the testing methodology (manual, unit, integration, end-to-end tests?)
--->
Unit tests will be created for each new strategy to ensure they correctly generate patches and apply operations, leading to a converged state. We will test for various scenarios, including concurrent changes from different replicas, to validate the conflict-resolution logic.
The tests have to contain idepontence, commutativity and assosiativity.

<!---AI - Stage 1--->
# Proposed Solutions [AI - Stage 1]
<!---
Here you will need to put a number of solutions that would fit for this problem.
Add the solutions that you rejected as well.
--->
### Solution 1: Implement Each Set as a Distinct Strategy (Recommended)
This approach involves creating a separate attribute and strategy class for each required set CRDT (G-Set, 2P-Set, LWW-Set, OR-Set).
-   **Pros:**
    -   Aligns with the existing architecture where each strategy is a self-contained, single-responsibility class (`LwwStrategy`, `CounterStrategy`, etc.).
    -   Code for each strategy is isolated, making it easier to develop, understand, test, and maintain.
    -   Avoids a large, complex class with conditional logic, which would be harder to reason about.
-   **Cons:**
    -   Results in a higher number of new files.
    -   Some minor code duplication might occur for common set operations, which can be mitigated with a shared base class or helper methods if necessary.

### Solution 2: Create a Generic `CrdtSetStrategy` with Configuration
This approach involves creating a single `CrdtSetStrategyAttribute` and a corresponding `CrdtSetStrategy` class. The attribute would take an enum to specify the desired behavior (e.g., `[CrdtSet(SetType.Lww)]`).
-   **Pros:**
    -   Reduces the number of strategy classes and attributes.
-   **Cons:**
    -   Leads to a complex, multi-responsibility class with significant internal branching (`if/switch` on `SetType`).
    -   The metadata management becomes more complex, as the single strategy would need to handle different state structures.
    -   Harder to test and maintain due to the interwoven logic for different behaviors.

### Solution 3: Phased Implementation (Rejected as a primary strategy)
Implement the simpler sets (G-Set, 2P-Set) first and defer the more complex LWW-Set and OR-Set. While this is a valid project management approach, for the purpose of technical design, it's better to plan for the full scope. The recommended solution (Solution 1) can be implemented in phases.

### Regarding "Set with Cardinality"
This requirement is best addressed as a design pattern rather than a new, single strategy. The library already provides the necessary building blocks: a set strategy for the collection and a counter strategy for the cardinality property. A single strategy trying to manage two separate properties on a POCO would violate the current design principle where a strategy is tied to a single property path. This should be documented as a usage example.

**Recommendation:** Solution 1 is the most robust and maintainable approach. It aligns with the existing design patterns in the codebase, promotes separation of concerns, and simplifies testing.

<!---AI - Stage 1--->
# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->
1.  **Update `CrdtMetadata.cs` Model:**
    -   Add new `ConcurrentDictionary` properties to hold the state required for the new set strategies:
        -   `TwoPhaseSetValues`: To store `(HashSet<object> Adds, HashSet<object> Tomstones)` for 2P-Sets.
        -   `LwwSetValues`: To store `(ConcurrentDictionary<object, ICrdtTimestamp> Adds, ConcurrentDictionary<object, ICrdtTimestamp> Removes)` for LWW-Sets.
        -   `OrSetValues`: To store `(ConcurrentDictionary<object, HashSet<Guid>> Adds, ConcurrentDictionary<object, HashSet<Guid>> Removes)` for OR-Sets.
    -   G-Set does not require dedicated metadata as its state can be derived directly from the POCO's collection.

2.  **Create New Strategy Attributes:**
    -   Create `CrdtGSetStrategyAttribute.cs`, `CrdtTwoPhaseSetStrategyAttribute.cs`, `CrdtLwwSetStrategyAttribute.cs`, and `CrdtOrSetStrategyAttribute.cs`.
    -   Each attribute will inherit from `CrdtStrategyAttribute` and specify its corresponding strategy type.

3.  **Update `CrdtMetadataManager` Service:**
    -   Modify the `Initialize` and `Reset` methods to recognize the new attributes.
    -   When a property with a new set attribute is discovered, the manager will initialize the corresponding state dictionary in the `CrdtMetadata` object for that property's JSON path.

4.  **Implement `GSetStrategy.cs`:**
    -   **`GeneratePatch`**: Will create `Upsert` operations for new elements and ignore any removals.
    -   **`ApplyOperation`**: Will add an element to the target collection if it's not already present, effectively ignoring `Remove` operations.

5.  **Implement `TwoPhaseSetStrategy.cs`:**
    -   **`GeneratePatch`**: Will create `Upsert` ops for additions and `Remove` ops for removals.
    -   **`ApplyOperation`**: Will update the `Adds` and `Tombstones` sets in the metadata. The final POCO collection will be reconstructed based on elements present in `Adds` but not in `Tombstones`. An element cannot be re-added once tombstoned.

6.  **Implement `LwwSetStrategy.cs`:**
    -   **`GeneratePatch`**: Will create `Upsert` and `Remove` operations, each with a new timestamp.
    -   **`ApplyOperation`**: Will compare timestamps in the metadata's `Adds` and `Removes` dictionaries to determine if an operation should be applied. The latest timestamp wins. The POCO collection will be reconstructed based on this state.

7.  **Implement `OrSetStrategy.cs`:**
    -   This strategy will require a custom payload for its operations to include unique tags (e.g., `Guid`) with each element.
    -   **`GeneratePatch`**: For additions, it will generate a new unique tag and create an `Upsert` op. For removals, it will find all known tags for the element and create a `Remove` op containing them.
    -   **`ApplyOperation`**: Will update the `Adds` and `Removes` tag sets in the metadata. The POCO collection will be reconstructed based on elements that have tags in `Adds` that are not present in `Removes`.

8.  **Register New Strategies in DI Container:**
    -   Update `ServiceCollectionExtensions.cs` to register `GSetStrategy`, `TwoPhaseSetStrategy`, `LwwSetStrategy`, and `OrSetStrategy` as transient services.

9.  **Create Unit Tests:**
    -   Create `GSetStrategyTests.cs`, `TwoPhaseSetStrategyTests.cs`, `LwwSetStrategyTests.cs`, and `OrSetStrategyTests.cs`.
    -   Each test suite will validate patch generation and application logic, ensuring convergence under concurrent operations and satisfying the properties of idempotence, commutativity, and associativity.

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
- `$/Ama.CRDT/Models/CrdtMetadata.cs` (To add new state dictionaries for the set strategies)
- `$/Ama.CRDT/Services/ICrdtMetadataManager.cs` (To understand the contract for metadata management)
- `$/Ama.CRDT/Services/CrdtMetadataManager.cs` (To modify for initializing and resetting new metadata structures)
- `$/Ama.CRDT/Attributes/CrdtStrategyAttribute.cs` (To use as the base class for new strategy attributes)
- `$/Ama.CRDT/Services/Strategies/ICrdtStrategy.cs` (The primary interface all new strategies will implement)
- `$/Ama.CRDT/Services/Strategies/ArrayLcsStrategy.cs` (As a reference implementation for a collection-based strategy)
- `$/Ama.CRDT.UnitTests/Services/Strategies/ArrayLcsStrategyTests.cs` (As a template for structuring the new unit tests)
- `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs` (To register the new strategies in the DI container)
- `$/Ama.CRDT/Services/CrdtPatcher.cs` (To understand the context in which `GeneratePatch` is called)
- `$/Ama.CRDT/Services/CrdtApplicator.cs` (To understand the context in which `ApplyOperation` is called)
- `$/Ama.CRDT/Models/CrdtOperation.cs` (To understand the structure of patch operations)
- `$/Ama.CRDT/Services/Strategies/IElementComparerProvider.cs` (To correctly handle object equality within set metadata)

<!---AI - Stage 2--->
# Changes Done
<!---
Here you add detailed information about all the changes actually done.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
Add all the things that you did in a different way than expected.
--->
-   `$/Ama.CRDT/Models/CrdtMetadata.cs`: Added `TwoPhaseSets`, `LwwSets`, and `OrSets` dictionaries to hold state for the new strategies.
-   `$/Ama.CRDT/Models/OrSetItem.cs`: Created a new file to hold the `OrSetAddItem` and `OrSetRemoveItem` record structs, which are used as payloads for OR-Set operations.
-   `$/Ama.CRDT/Attributes/CrdtGSetStrategyAttribute.cs`, `CrdtTwoPhaseSetStrategyAttribute.cs`, `CrdtLwwSetStrategyAttribute.cs`, `CrdtOrSetStrategyAttribute.cs`: Created new attribute classes for each set strategy.
-   `$/Ama.CRDT/Services/Strategies/GSetStrategy.cs`, `TwoPhaseSetStrategy.cs`, `LwwSetStrategy.cs`, `OrSetStrategy.cs`: Implemented the core logic for each of the four new set strategies.
-   `$/Ama.CRDT/Services/CrdtMetadataManager.cs`: Injected `IElementComparerProvider` and updated the constructor. Modified the `Reset` method to clear the new metadata dictionaries. Updated the `PopulateMetadataRecursive` method to correctly initialize the state for each new set strategy based on the initial document.
-   `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs`: Registered all four new strategies in the dependency injection container.
-   `$/Ama.CRDT.UnitTests/Services/Strategies/ArrayLcsStrategyTests.cs`: Updated the constructor call for `CrdtMetadataManager` to pass the new `IElementComparerProvider` dependency.
-   `$/Ama.CRDT.UnitTests/Services/Strategies/GSetStrategyTests.cs`, `TwoPhaseSetStrategyTests.cs`, `LwwSetStrategyTests.cs`, `OrSetStrategyTests.cs`: Created comprehensive unit test suites for each new strategy, validating patch generation, application, idempotence, commutativity, and convergence.
-   `$/FilesDescription.md`: Updated with descriptions for all new files.

The implementation followed the proposed solution closely. The "Set with Cardinality" requirement was not implemented as a distinct strategy, per the recommendation that it should be handled as a design pattern by the library user.

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
There is some code duplication among the new strategy classes, particularly in the helper methods for deserializing values and in the overall structure of the `GeneratePatch` and `ApplyOperation` methods. A shared abstract base class for set strategies could be introduced in the future to consolidate common logic, but was avoided for this implementation to keep each strategy self-contained and explicit.

<!---AI - Stage 2--->
## Last notes and implementation details
<!---
Here you add comments about the implementation that didn't fit on the previous section.
--->
The OR-Set implementation required creating custom payload records (`OrSetAddItem`, `OrSetRemoveItem`) to pass the necessary unique tags within the `CrdtOperation`. This follows the existing pattern used by `ArrayLcsStrategy` for its positional identifiers, ensuring consistency within the library's design. The `CrdtMetadataManager` was extended to be responsible for the initial state creation for all strategies, which centralizes the logic of interpreting a POCO's initial state into CRDT metadata. All unit tests for the new strategies include checks for idempotence, commutativity, and convergence to ensure they adhere to CRDT principles.

# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->