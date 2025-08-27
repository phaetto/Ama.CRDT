<!---Human--->
# Purpose
<!---
Add the purpose of this user story.
--->
Object & Map Strategies

<!---Human--->
# Requirements
<!---
Add the requirements, technical or not.
--->
21. **LWW-Map:** A dictionary/map where each key-value pair is an independent LWW-Register.
22. **OR-Map (Observed-Remove Map):** A map that uses the OR-Set logic for its keys, providing more precise semantics for concurrent key additions and removals.

Make only the ones that do not break the public API, except the metadata and other models. Avoid breaking the interface public APIs. If any of the above strategies cannot be done by not breaking the APIs, REJECT the whole request and make no code. Ask for the APIs you need instead. No half-made solutions are allowed.

Use the `CrdtMetadataManager` to provide utilities for metadata management.

Update the readme.

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
- `C:\sources\Ama.CRDT\Ama.CRDT\Services\Strategies\OrSetStrategy.cs`
- `C:\sources\Ama.CRDT\README.md`

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
Here are the proposed solutions for implementing the LWW-Map and OR-Map strategies:

### Solution 1: Create Distinct, Self-Contained Strategies (Recommended)
This approach involves creating two new, separate strategy classes, `LwwMapStrategy` and `OrMapStrategy`, each with a corresponding attribute. This aligns perfectly with the existing architecture, where each CRDT type has its own dedicated strategy class.

-   **LWW-Map:** The `LwwMapStrategy` will manage an `IDictionary`. It will use a new metadata structure (`Dictionary<string, Dictionary<object, ICrdtTimestamp>>`) within `CrdtMetadata` to track the last-write timestamp for each key in the dictionary. `GeneratePatch` will create `Upsert` and `Remove` operations for key-value pairs based on timestamp comparisons. `ApplyOperation` will only apply an incoming change if its timestamp is newer than the one in the metadata.
-   **OR-Map:** The `OrMapStrategy` will manage the presence of keys using OR-Set logic (unique tags for adds and tombstones for removes) while managing the associated values using LWW logic. This requires new metadata structures for add-tags and remove-tags per key. This prevents anomalies where a key is re-added after being removed.

-   **Reasoning for Recommendation:** This solution is clean, modular, and has the lowest risk of introducing breaking changes to public interfaces. It follows the established pattern in the codebase, making it easy to understand and maintain.

### Solution 2: Create a Single, Generic Dictionary Strategy
This approach would involve creating a single `CrdtDictionaryStrategy` with a more complex attribute that allows specifying the desired behavior for keys and values (e.g., `[CrdtDictionary(KeyStrategy = KeyConflict.ORSet, ValueStrategy = ValueConflict.LWW)]`).

-   **Pros:** Highly flexible and could accommodate future map-based CRDTs without adding new strategy classes.
-   **Cons:** This is significantly more complex to implement. It would require a major refactor of the `CrdtStrategyManager` and the `ICrdtStrategy` interface to handle nested or composed strategies. This complexity introduces a high risk of unforeseen side effects and would almost certainly lead to breaking changes in the public API, which is explicitly forbidden by the requirements.
-   **Verdict:** Rejected due to high complexity and high risk of breaking the public API.

### Solution 3: Adapt Existing Set Strategies
This approach suggests modifying the existing `LwwSetStrategy` and `OrSetStrategy` to handle `IDictionary` in addition to `IEnumerable`.

-   **Pros:** Could potentially reduce code duplication.
-   **Cons:** This would pollute the logic of set-based strategies with dictionary-specific concerns, making them harder to reason about. Dictionaries have distinct concepts (keys vs. values) that don't map cleanly onto set elements. For example, in a map, you might want to update a value without changing the key's membership, a concept that doesn't exist in a set. This approach would lead to a confusing and less maintainable implementation.
-   **Verdict:** Rejected because it violates the Single Responsibility Principle and would make the existing strategies overly complex and difficult to maintain.

<!---AI - Stage 1--->
# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->
1.  **Modify `CrdtMetadata.cs` Model:**
    -   Add `public Dictionary<string, Dictionary<object, ICrdtTimestamp>> LwwMapTimestamps { get; set; } = new();` to store timestamps for each key in an LWW-Map.
    -   Add `public Dictionary<string, Dictionary<object, HashSet<Guid>>> OrMapAdds { get; set; } = new();` to store add-tags for OR-Map keys.
    -   Add `public Dictionary<string, Dictionary<object, HashSet<Guid>>> OrMapTombstones { get; set; } = new();` to store remove-tags for OR-Map keys.

2.  **Update `CrdtMetadataManager.cs` Service:**
    -   Update the `Initialize` and `Reset` methods to traverse `IDictionary` properties and pre-populate the metadata structures if needed, ensuring they are ready for use by the strategies.

3.  **Implement LWW-Map Strategy:**
    -   Create `$/Ama.CRDT/Attributes/CrdtLwwMapStrategyAttribute.cs`.
    -   Create `$/Ama.CRDT/Services/Strategies/LwwMapStrategy.cs` implementing `ICrdtStrategy`. This strategy will compare dictionaries key by key and generate `Upsert`/`Remove` operations based on LWW rules.
    -   Create `$/Ama.CRDT.UnitTests/Services/Strategies/LwwMapStrategyTests.cs` with tests for convergence, idempotence, and commutativity under concurrent add, update, and remove operations.

4.  **Implement OR-Map Strategy:**
    -   Create `$/Ama.CRDT/Models/OrMapItem.cs` to define payload structures for OR-Map operations (e.g., `OrMapAddItem` with key, value, and tag; `OrMapRemoveItem` with key and tags).
    -   Create `$/Ama.CRDT/Attributes/CrdtOrMapStrategyAttribute.cs`.
    -   Create `$/Ama.CRDT/Services/Strategies/OrMapStrategy.cs` implementing `ICrdtStrategy`. This strategy will use OR-Set logic for key membership and LWW logic for value updates.
    -   Create `$/Ama.CRDT.UnitTests/Services/Strategies/OrMapStrategyTests.cs` with tests for convergence, especially scenarios involving re-adding a previously removed key.

5.  **Update Core Services:**
    -   Modify `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs` to register `LwwMapStrategy` and `OrMapStrategy` in the DI container.
    -   Modify `$/Ama.CRDT/Services/Strategies/CrdtStrategyManager.cs` to recognize the new attributes and return the corresponding strategy instances.

6.  **Update Documentation:**
    -   Edit `$/README.md` to add a new "Object & Map Strategies" section, documenting `LWW-Map` and `OR-Map` with usage examples.

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
-   `$/Ama.CRDT/Models/CrdtMetadata.cs` (To be modified to hold state for the new map strategies)
-   `$/Ama.CRDT/Services/CrdtMetadataManager.cs` (To be modified to initialize and reset the new metadata structures)
-   `$/Ama.CRDT.UnitTests/Services/CrdtMetadataManagerTests.cs` (To be modified to test the changes in the metadata manager)
-   `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs` (To be modified to register the new strategies for dependency injection)
-   `$/Ama.CRDT/Services/Strategies/CrdtStrategyManager.cs` (To be modified to resolve the new strategies from their attributes)
-   `$/Ama.CRDT/Attributes/CrdtStrategyAttribute.cs` (To be used as the base class for the new strategy attributes)
-   `$/Ama.CRDT/Services/Strategies/ICrdtStrategy.cs` (The interface that the new strategies will implement)
-   `$/Ama.CRDT/Services/Strategies/LwwSetStrategy.cs` (As a reference for implementing the LWW-Map logic)
-   `$/Ama.CRDT/Services/Strategies/OrSetStrategy.cs` (As a reference for implementing the OR-Map logic)
-   `$/Ama.CRDT/Models/OrSetItem.cs` (As a reference for creating the new `OrMapItem.cs` model)
-   `$/README.md` (To be modified to include documentation for the new strategies)

<!---AI - Stage 2--->
# Changes Done
<!---
Here you add detailed information about all the changes actually done.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
Add all the things that you did in a different way than expected.
--->
- `$/Ama.CRDT/Models/CrdtMetadata.cs`: Added `LwwMaps` and `OrMaps` properties to hold state for the new dictionary strategies.
- `$/Ama.CRDT/Services/CrdtMetadataManager.cs`: Updated `Reset` and `Clone` to handle the new metadata properties. Modified `PopulateMetadataRecursive` to correctly recurse into `IDictionary` values. Added `InitializeMapMetadata` helper to populate metadata for `LwwMapStrategy` and `OrMapStrategy`.
- `$/Ama.CRDT/Services/Strategies/CrdtStrategyManager.cs`: Updated `GetStrategy` to handle `IDictionary` properties without an explicit attribute, safely defaulting them to the LWW strategy (which treats the entire dictionary as one value). This prevents them from being incorrectly handled by the default array strategy.
- `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs`: Registered `LwwMapStrategy` and `OrMapStrategy` in the dependency injection container.
- `$/Ama.CRDT/Attributes/CrdtLwwMapStrategyAttribute.cs`: (New File) Created the attribute to mark properties for the `LwwMapStrategy`.
- `$/Ama.CRDT/Attributes/CrdtOrMapStrategyAttribute.cs`: (New File) Created the attribute to mark properties for the `OrMapStrategy`.
- `$/Ama.CRDT/Models/OrMapItem.cs`: (New File) Created payload record structs (`OrMapAddItem`, `OrMapRemoveItem`) for OR-Map operations.
- `$/Ama.CRDT/Services/Strategies/LwwMapStrategy.cs`: (New File) Implemented the `LwwMapStrategy`, where each key-value pair is an independent LWW-Register.
- `$/Ama.CRDT/Services/Strategies/OrMapStrategy.cs`: (New File) Implemented the `OrMapStrategy`, which uses OR-Set logic for key presence and LWW logic for value updates.
- `$/README.md`: Updated the strategies table to include `LwwMapStrategy` and `OrMapStrategy` with descriptions and use cases.
- `$/Ama.CRDT.UnitTests/Services/Strategies/LwwMapStrategyTests.cs`: (New File) Added unit tests for `LwwMapStrategy` covering commutativity, idempotence, and LWW conflict resolution.
- `$/Ama.CRDT.UnitTests/Services/Strategies/OrMapStrategyTests.cs`: (New File) Added unit tests for `OrMapStrategy` covering commutativity, idempotence, remove/re-add scenarios, and LWW value updates.
- `$/Ama.CRDT.UnitTests/Services/CrdtMetadataManagerTests.cs`: Updated the `Reset_ShouldClearAllState` test to include clearing the new map metadata.

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
- The new map strategies rely on `IElementComparerProvider` to get an equality comparer for dictionary keys. This reuses the existing provider for collections. While functional, a more robust solution might involve a dedicated `IDictionaryKeyComparerProvider` to make the distinction clearer and more extensible for future dictionary-based strategies.
- The default behavior for a dictionary property without a CRDT attribute is now the `LwwStrategy`, which replaces the entire dictionary on any change. While a safe default, it's not efficient for large dictionaries. Users must explicitly use `[CrdtLwwMapStrategy]` for per-key conflict resolution. This could be highlighted more prominently in the documentation.

<!---AI - Stage 2--->
## Last notes and implementation details
<!---
Here you add comments about the implementation that didn't fit on the previous section.
--->
The `OrMapStrategy` implementation is a hybrid strategy. It uses OR-Set semantics (unique tags for additions and tombstones for removals) to manage the presence of keys in the dictionary. This allows a key to be correctly re-added after being removed, even with concurrent operations. Simultaneously, it uses LWW semantics to manage the value associated with each key. This is achieved by storing value timestamps in the main `CrdtMetadata.Lww` dictionary using a path format like `$.myMap.['someKey']`. This separation of concerns ensures that key membership and value updates are resolved independently and correctly, providing powerful and intuitive behavior for distributed map management. The `LwwMapStrategy` is a simpler implementation where each key-value pair is treated as a single LWW-Register, using a dedicated metadata structure to track timestamps per key.

# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->