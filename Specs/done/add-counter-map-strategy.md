<!---Human--->
# Purpose
<!---
Add the purpose of this user story.
--->
Implement counter strategies for Maps and Sets.

<!---Human--->
# Requirements
<!---
Add the requirements, technical or not.
--->
**Counter for maps and ordered sets:** Deterministic strategies that resemble the Counter, MinWins, MaxWins for elements on a map or an ordered set.

Make only the ones that do not break the public API, except the metadata and other models. Avoid breaking the interface public APIs.

Use the `CrdtMetadataManager` to provide utilities for metadata management.

<!---Human--->
## Requirements context
<!---
Add files that we will load for the UI to add context for the solution design.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
--->
- `$/Ama.CRDT/Models/CrdtMetadata.cs`
- `$/Ama.CRDT/Attributes/CrdtArrayLcsStrategyAttribute.cs`
- `$/Ama.CRDT/Services/Strategies/ArrayLcsStrategy.cs`
- `$/Ama.CRDT.UnitTests/Services/Strategies/ArrayLcsStrategyTests.cs`
- `$/Ama.CRDT/Services/CrdtMetadataManager.cs`
- `$/Ama.CRDT/Services/Helpers/PocoPathHelper.cs`
- `$/Ama.CRDT/Services/Strategies/CounterStrategy.cs`
- `$/Ama.CRDT/Services/Strategies/ApplyOperationContext.cs`
- `$/Ama.CRDT/Services/Strategies/GeneratePatchContext.cs`

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
### Solution 1: Specialized Metadata Fields (Recommended)
- **Description**: This approach involves adding new, specific dictionaries to the `CrdtMetadata` class to store the per-element state for each new map strategy. For example, a `Dictionary<string, Dictionary<string, (long P, long N)>>` would be added for the `CounterMapStrategy`. The outer dictionary is keyed by the property's JSON path, and the inner dictionary is keyed by the map's key, holding the PN-Counter state.
- **Pros**:
    - Aligns with the existing design of the library, where state for strategies like LWW, OR-Set, and LSEQ is explicitly stored in `CrdtMetadata`.
    - Type-safe and efficient, avoiding runtime casting and serialization complexities.
    - Simple to implement by following established patterns within the codebase.
- **Cons**:
    - The `CrdtMetadata` class will grow with each new strategy-specific state. This is a manageable trade-off.
- **Reason for Recommendation**: This solution is the most consistent with the library's architecture. It provides the best balance of type safety, performance, and simplicity, making it the lowest-risk and most maintainable option.

### Solution 2: Generic Metadata Storage
- **Description**: Add a single generic dictionary, such as `Dictionary<string, object> AdditionalMetadata`, to `CrdtMetadata`. Each strategy would then be responsible for storing and retrieving its own state from this dictionary, casting the `object` to its required type.
- **Pros**:
    - Highly extensible, as new strategies wouldn't require modifying the `CrdtMetadata` class.
- **Cons**:
    - Not type-safe, relying on runtime casting which can introduce errors.
    - Adds complexity to serialization and deserialization of the metadata.
    - Less clear to debug, as the structure of the metadata is not explicit.

### Solution 3: Co-locate Metadata in User's POCO (Rejected)
- **Description**: Require the user to define their maps with a value that wraps both the data and its metadata, for example `IDictionary<string, CounterValue>` where `CounterValue` contains the application value and the CRDT state.
- **Pros**:
    - Metadata is directly tied to the data it describes.
- **Cons**:
    - This is a major anti-pattern for this library. It breaks the fundamental design principle of separating the user's domain model from the CRDT metadata state.
    - It would be a significant breaking change and would pollute the user's POCOs.

<!---AI - Stage 1--->
# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->
1.  **Update `CrdtMetadata.cs`**:
    - Add three new properties to store per-element metadata for maps:
        - `public Dictionary<string, Dictionary<string, (long P, long N)>> CounterMap { get; set; }`
        - `public Dictionary<string, Dictionary<string, ICrdtTimestamp>> MaxWinsMap { get; set; }`
        - `public Dictionary<string, Dictionary<string, ICrdtTimestamp>> MinWinsMap { get; set; }`

2.  **Create New Strategy Attributes**:
    - `$/Ama.CRDT/Attributes/CrdtCounterMapStrategyAttribute.cs`: Defines the attribute to mark a dictionary property to use the `CounterMapStrategy`.
    - `$/Ama.CRDT/Attributes/CrdtMaxWinsMapStrategyAttribute.cs`: Defines the attribute for the `MaxWinsMapStrategy`.
    - `$/Ama.CRDT/Attributes/CrdtMinWinsMapStrategyAttribute.cs`: Defines the attribute for the `MinWinsMapStrategy`.
    - Each attribute will be decorated with `[CrdtSupportedType(typeof(IDictionary))]`.

3.  **Implement New Strategies**:
    - `$/Ama.CRDT/Services/Strategies/CounterMapStrategy.cs`:
        - `GeneratePatch`: Compare old and new dictionaries. Generate `Increment` operations for value changes. New keys will result in an `Upsert` with the initial count. Key removals are not a natural fit for a PN-Counter and will be ignored.
        - `ApplyOperation`: Use the operation's payload to update the PN-Counter state in `metadata.CounterMap`. Recalculate the value in the user's dictionary based on the updated P and N counts.
    - `$/Ama.CRDT/Services/Strategies/MaxWinsMapStrategy.cs`:
        - `GeneratePatch`: Compare dictionaries. Generate `Upsert` operations for new or changed values and `Remove` operations for removed keys.
        - `ApplyOperation`: A LWW-Register per key. Check the timestamp in `metadata.MaxWinsMap`. If the incoming operation is newer, update the metadata and apply the change (upsert/remove) to the user's dictionary.
    - `$/Ama.CRDT/Services/Strategies/MinWinsMapStrategy.cs`:
        - Similar to `MaxWinsMapStrategy`, but resolves conflicts by choosing the lower value (still based on the higher timestamp).

4.  **Update `CrdtMetadataManager.cs`**:
    - Modify the `Initialize` and `Reset` methods. When a property with one of the new map strategy attributes is found, ensure its top-level entry is created in the corresponding metadata dictionary (e.g., `metadata.CounterMap[path] = new();`).

5.  **Register New Services**:
    - In `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs`, add the new strategies (`CounterMapStrategy`, `MaxWinsMapStrategy`, `MinWinsMapStrategy`) to the dependency injection container.

6.  **Create Unit Tests**:
    - `$/Ama.CRDT.UnitTests/Services/Strategies/CounterMapStrategyTests.cs`: Test concurrent increments/decrements, convergence, idempotence, and commutativity.
    - `$/Ama.CRDT.UnitTests/Services/Strategies/MaxWinsMapStrategyTests.cs`: Test concurrent upserts and removes, ensuring LWW conflict resolution works correctly. Verify convergence, idempotence, and commutativity.
    - `$/Ama.CRDT.UnitTests/Services/Strategies/MinWinsMapStrategyTests.cs`: Same test structure as `MaxWinsMapStrategyTests`, but verifying min-wins logic.

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
- `$/Ama.CRDT/Models/CrdtMetadata.cs` (To add new dictionaries for map strategy states.)
- `$/Ama.CRDT/Services/CrdtMetadataManager.cs` (To add initialization logic for the new strategies.)
- `$/Ama.CRDT.UnitTests/Services/CrdtMetadataManagerTests.cs` (To update tests for changes in the metadata manager.)
- `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs` (To register the new strategies for dependency injection.)
- `$/Ama.CRDT/Services/Strategies/CounterStrategy.cs` (As a reference for implementing counter logic.)
- `$/Ama.CRDT/Services/Strategies/LwwMapStrategy.cs` (As a reference for implementing map-based LWW logic.)
- `$/Ama.CRDT.UnitTests/Services/Strategies/LwwMapStrategyTests.cs` (As a template for writing map strategy unit tests.)
- `$/Ama.CRDT/Services/Helpers/PocoPathHelper.cs` (Will be used by strategies to interact with the data model.)

<!---AI - Stage 2--->
# Changes Done
<!---
Here you add detailed information about all the changes actually done.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
Add all the things that you did in a different way than expected.
--->
- `$/Ama.CRDT/Models/CrdtMetadata.cs`: Added the `CounterMaps` property. Removed the previously added `MaxWinsMaps` and `MinWinsMaps` properties, as the refactored strategies are now stateless.
- `$/Ama.CRDT/Attributes/CrdtCounterMapStrategyAttribute.cs`: Created a new attribute for the `CounterMapStrategy`.
- `$/Ama.CRDT/Attributes/CrdtMaxWinsMapStrategyAttribute.cs`: Created a new attribute for the `MaxWinsMapStrategy`.
- `$/Ama.CRDT/Attributes/CrdtMinWinsMapStrategyAttribute.cs`: Created a new attribute for the `MinWinsMapStrategy`.
- `$/Ama.CRDT/Services/Strategies/CounterMapStrategy.cs`: Implemented the strategy where each key in a dictionary is a PN-Counter.
- `$/Ama.CRDT/Services/Strategies/MaxWinsMapStrategy.cs`: Re-implemented the strategy to be purely value-based. It resolves conflicts by choosing the highest value for each key and makes the map keys grow-only (removals are ignored).
- `$/Ama.CRDT/Services/Strategies/MinWinsMapStrategy.cs`: Re-implemented the strategy to be purely value-based. It resolves conflicts by choosing the lowest value for each key and makes the map keys grow-only.
- `$/Ama.CRDT/Services/CrdtMetadataManager.cs`: Updated `Reset`, `Clone`, and `InitializeMapMetadata` to handle only the new `CounterMaps` state.
- `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs`: Registered `CounterMapStrategy`, `MaxWinsMapStrategy`, and `MinWinsMapStrategy` in the DI container.
- `$/Ama.CRDT.UnitTests/Services/CrdtMetadataManagerTests.cs`: Updated the `Reset` unit test to reflect the changes in `CrdtMetadata`.
- `$/Ama.CRDT.UnitTests/Services/Strategies/CounterMapStrategyTests.cs`: Added unit tests for the `CounterMapStrategy`.
- `$/Ama.CRDT.UnitTests/Services/Strategies/MaxWinsMapStrategyTests.cs`: Added new unit tests for the value-based `MaxWinsMapStrategy`.
- `$/Ama.CRDT.UnitTests/Services/Strategies/MinWinsMapStrategyTests.cs`: Added new unit tests for the value-based `MinWinsMapStrategy`.

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
The current implementation of value-based `MaxWinsMapStrategy` and `MinWinsMapStrategy` does not propagate key removals. This is a necessary constraint to ensure convergence without using timestamps. If support for key deletion is required in the future for these specific strategies, they would need to be redesigned, likely using a more complex CRDT algorithm like an OR-Set combined with a value register, which is beyond the current scope.

<!---AI - Stage 2--->
## Last notes and implementation details
<!---
Here you add comments about the implementation that didn't fit on the previous section.
--->
Based on the feedback, `MaxWinsMapStrategy` and `MinWinsMapStrategy` have been successfully refactored to use a pure value-based conflict resolution model. This approach is valid and convergent under the constraint that the set of keys in the map is grow-only; the strategies will not generate operations to remove keys.

- **`CounterMapStrategy`**: Functions as a map of PN-Counters, summing deltas for each key. It requires metadata to track the positive and negative counts.
- **`MaxWinsMapStrategy`**: For each key, the value will always converge to the maximum value seen across all replicas for that key.
- **`MinWinsMapStrategy`**: For each key, the value will always converge to the minimum value seen across all replicas for that key.

These new map strategies provide more versatile options for handling dictionary-like data structures in a distributed environment.

# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->