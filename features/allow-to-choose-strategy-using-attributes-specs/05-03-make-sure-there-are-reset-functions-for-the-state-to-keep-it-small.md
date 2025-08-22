<!---Human--->
# Purpose
<!---
Add the purpose of this user story.
--->
As a developer using this library for long-running applications or services with high update frequencies, I need mechanisms to manage the growth of the CRDT state metadata. The current implementation, where the set of seen operation IDs and tombstones for deleted fields grow indefinitely, is not sustainable. Unbounded growth leads to significant performance degradation over time, increasing latency and storage costs for serializing, transmitting, and persisting the metadata. This user story aims to introduce state compaction and garbage collection mechanisms to ensure the library remains efficient and scalable for production use cases.

<!---Human--->
# Requirements
<!---
Add the requirements, technical or not.
--->
To address the state growth problem, the library's core components need to be refactored to support more sophisticated state management and compaction strategies.

### 1. Abstract Timestamp Representation
The current use of `long` for timestamps is restrictive. To support advanced compaction strategies like version vectors, we need a more flexible system.
- **R1.1:** Introduce a new interface, `ICrdtTimestamp`, which must be `IComparable`. This will represent a logical point in time.
- **R1.2:** Update all relevant models (`CrdtOperation`, `CrdtMetadata`) and services (`IJsonCrdtApplicator`, `LwwStrategy`) to use `ICrdtTimestamp` instead of `long`.
- **R1.3:** Provide a default, backward-compatible implementation of `ICrdtTimestamp` that wraps a `long` value (e.g., `EpochTimestamp`) to ensure existing functionality is not broken.
- **R1.4:** The `CrdtOperation` model must be updated to include a `ReplicaId` (of type `string`) to identify the source of the operation, which is essential for version vector logic.

### 2. Version Vector for Idempotency
The `HashSet<Guid> SeenOperations` for idempotency checks is the primary cause of unbounded state growth. It should be replaced with a version vector.
- **R2.1:** Modify `CrdtMetadata` to replace `HashSet<Guid> SeenOperations` with a version vector structure. This will consist of two parts:
    - `IDictionary<string, ICrdtTimestamp> VersionVector`: Maps a `ReplicaId` to the latest contiguous timestamp received from that replica.
    - `HashSet<Guid> SeenExceptions`: Stores the IDs of operations that have been received out of order (i.e., their timestamp is newer than what's in the version vector for that replica).
- **R2.2:** Refactor the `ApplyOperationWithStateCheck` method in `JsonCrdtApplicator` to perform idempotency checks using the new version vector logic:
    - An operation is considered "seen" and must be ignored if its timestamp is less than or equal to the timestamp for its `ReplicaId` in the `VersionVector`.
    - It must also be ignored if its ID is present in the `SeenExceptions` set.

### 3. State Compaction and Pruning
Mechanisms must be provided to actively reduce the size of the metadata.
- **R3.1:** Implement a compaction method for the idempotency state. This method should be able to advance the timestamp in the `VersionVector` for a given replica and remove any corresponding operation IDs from the `SeenExceptions` set that are now covered by the new, higher timestamp.
- **R3.2:** Implement a garbage collection mechanism for LWW tombstones. This method will remove entries from the `Lww` metadata dictionary if their `ICrdtTimestamp` is older than a specified "pruning threshold" timestamp. This assumes the system has reached a state where no older updates could possibly resurface the deleted data.
- **R3.3 (Optional):** Introduce a dedicated service, `ICrdtMetadataManager`, to encapsulate the compaction and pruning logic.

<!---Human--->
## Requirements context
<!---
Add files that we will load for the UI to add context for the solution design.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
--->
The following files are central to this change as they define the state's structure and the logic for its application.
- `$/Modern.CRDT/Services/JsonCrdtApplicator.cs`: This service implements the core logic for applying patches. The method `ApplyOperationWithStateCheck` contains the idempotency check that needs to be completely refactored to use the new version vector model instead of the simple `HashSet`.
- `$/Modern.CRDT/Models/CrdtMetadata.cs`: This model defines the structure of the CRDT state. It will be significantly modified to replace the `SeenOperations` set with the `VersionVector` and `SeenExceptions` collections, and to use the new `ICrdtTimestamp` interface.

<!---Human--->
# Testing Methodology
<!---
Add the testing methodology (manual, unit, integration, end-to-end tests?)
--->
A multi-layered testing approach will be used to validate the new state management features.

### Unit Tests
- **`ICrdtTimestamp`:** Create unit tests for the default `long`-based implementation and any other implementations to ensure the comparison logic (`<`, `>`, `==`) is correct.
- **`JsonCrdtApplicator`:** The state-checking logic will be thoroughly tested. Scenarios will include:
    - Rejecting operations with timestamps older than or equal to the replica's timestamp in the version vector.
    - Rejecting operations whose IDs are already in the `SeenExceptions` set.
    - Accepting a valid, in-order operation and verifying that the `VersionVector` is advanced correctly.
    - Accepting a valid, out-of-order (future) operation and verifying it is added to `SeenExceptions` without advancing the `VersionVector`.
- **Metadata Compaction Logic:**
    - Write tests to confirm that pruning tombstones correctly removes LWW entries older than a specified threshold timestamp while leaving newer ones intact.
    - Write tests to verify that compacting the `SeenExceptions` set correctly removes IDs that are now covered by an advanced `VersionVector` timestamp.

### Integration Tests
- Simulate a multi-replica environment where operations are generated and applied, including duplicates, reordered messages, and in-order messages. The final state of the merged document and its associated metadata must be asserted for consistency and correctness.
- Design a test to simulate a long-running process with a high volume of updates. Periodically trigger the state compaction and pruning logic and assert that the size of the `CrdtMetadata` object remains bounded and does not grow indefinitely.

### Manual Testing
- During development, manually inspect the serialized JSON of the `CrdtMetadata` object before and after applying patches and running compaction logic. This will serve as a valuable sanity check to ensure the data structures are evolving as expected.

<!---AI - Stage 1--->
# Proposed Solutions [AI - Stage 1]
<!---
Here you will need to put a number of solutions that would fit for this problem.
Add the solutions that you rejected as well.
--->
### Solution 1: Comprehensive State Management Overhaul (Recommended)
This solution directly implements all specified requirements, providing a robust and flexible framework for managing CRDT state.
- **Timestamp Abstraction:** Introduce `ICrdtTimestamp` and a default `EpochTimestamp` (wrapping `long`). This decouples the logic from a specific time representation, allowing for future extensions like Hybrid Logical Clocks (HLCs).
- **Version Vector Idempotency:** Replace the `HashSet<Guid> SeenOperations` with `IDictionary<string, ICrdtTimestamp> VersionVector` and `HashSet<Guid> SeenExceptions`. This is the most efficient way to track seen operations per replica and provides a solid foundation for compaction.
- **Dedicated Management Service:** Create a new `ICrdtMetadataManager` service to encapsulate the logic for pruning LWW tombstones and compacting the `SeenExceptions` set. This adheres to the Single Responsibility Principle, keeping the `JsonCrdtApplicator` focused on applying patches.
- **Reasoning:** This is the recommended approach because it fully solves the unbounded state growth problem for both idempotency tracking and LWW tombstones. The abstraction of `ICrdtTimestamp` and the creation of a dedicated manager service result in a cleaner, more maintainable, and extensible design.

### Solution 2: Minimalist Tombstone Pruning
This solution focuses only on the LWW tombstone growth, which is a less critical part of the problem.
- **Keep `long` Timestamps:** Avoid introducing the `ICrdtTimestamp` interface to minimize changes.
- **Pruning Method:** Add a public method directly to the `CrdtMetadata` model, like `PruneLwwTombstones(long threshold)`, that iterates through the LWW dictionary and removes entries older than the threshold.
- **Idempotency Unchanged:** The `SeenOperations` `HashSet` remains, and its unbounded growth is not addressed.
- **Reasoning for Rejection:** While simple to implement, this solution is incomplete. It ignores the primary source of state growth (`SeenOperations`), which can become much larger than the LWW tombstones. It provides only a partial fix and kicks the main problem down the road.

### Solution 3: Simplified Version Vector without Timestamp Abstraction
This approach implements the version vector but without the `ICrdtTimestamp` abstraction.
- **Use `long` for Version Vector:** The `VersionVector` in `CrdtMetadata` would be `IDictionary<string, long>`, representing a simple sequence number per replica.
- **Update `CrdtOperation`:** Add `string ReplicaId` and `long SequenceNumber` to the operation model.
- **No `ICrdtTimestamp`:** All timestamp logic continues to use the primitive `long` type.
- **Reasoning for Rejection:** This solution solves the idempotency growth issue but misses the opportunity to make the system more flexible. By hardcoding `long`, it makes it difficult to adopt more advanced timestamping systems in the future that might be better suited for distributed environments (e.g., HLCs that combine physical time with logical counters). The recommended solution's `ICrdtTimestamp` provides this flexibility for very little additional implementation cost.

<!---AI - Stage 1--->
# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->
1.  **Create Timestamp Abstraction:**
    -   Create `$/Modern.CRDT/Models/ICrdtTimestamp.cs` with an interface `ICrdtTimestamp : IComparable<ICrdtTimestamp>`.
    -   Create `$/Modern.CRDT/Models/EpochTimestamp.cs` with a `readonly record struct` implementing `ICrdtTimestamp` and wrapping a `long` value.
2.  **Update Core Data Models:**
    -   Modify `$/Modern.CRDT/Models/CrdtOperation.cs`:
        -   Add a `public string ReplicaId { get; init; }`.
        -   Change the `Timestamp` property from `long` to `ICrdtTimestamp`.
    -   Modify `$/Modern.CRDT/Models/CrdtMetadata.cs`:
        -   Remove `public HashSet<Guid> SeenOperations { get; set; }`.
        -   Change `public IDictionary<string, long> Lww { get; set; }` to `public IDictionary<string, ICrdtTimestamp> Lww { get; set; }`.
        -   Add `public IDictionary<string, ICrdtTimestamp> VersionVector { get; set; }`.
        -   Add `public HashSet<CrdtOperation> SeenExceptions { get; set; }`. Storing the full operation in exceptions is better for later processing.
3.  **Create Metadata Management Service:**
    -   Create `$/Modern.CRDT/Services/ICrdtMetadataManager.cs`:
        -   Define a method `void PruneLwwTombstones(CrdtMetadata metadata, ICrdtTimestamp threshold)` to remove old LWW entries.
        -   Define a method `void UpdateVersionVector(CrdtMetadata metadata, CrdtOperation operation)` to handle the core logic of advancing the version vector and compacting `SeenExceptions`.
    -   Create `$/Modern.CRDT/Services/CrdtMetadataManager.cs` to implement the interface.
4.  **Refactor `JsonCrdtApplicator`:**
    -   Inject `ICrdtMetadataManager` into `$/Modern.CRDT/Services/JsonCrdtApplicator.cs`.
    -   Completely rewrite the `ApplyOperationWithStateCheck` method. It will now determine if an operation is a duplicate, out-of-order, or valid by querying the `VersionVector` and `SeenExceptions` from the metadata.
    -   If an operation is valid, after it's applied by the strategy, call `metadataManager.UpdateVersionVector(metadata, operation)` to update the state.
5.  **Update Strategies:**
    -   Modify `$/Modern.CRDT/Services/Strategies/LwwStrategy.cs`: Update the `GeneratePatch` method to handle `ICrdtTimestamp` comparisons.
6.  **Update Dependency Injection:**
    -   In `$/Modern.CRDT/Extensions/ServiceCollectionExtensions.cs`, register the new `ICrdtMetadataManager` service.
7.  **Implement Unit Tests:**
    -   Create `$/Modern.CRDT.UnitTests/Models/EpochTimestampTests.cs`.
    -   Create `$/Modern.CRDT.UnitTests/Services/CrdtMetadataManagerTests.cs` to test pruning and version vector update logic.
    -   Heavily revise `$/Modern.CRDT.UnitTests/Services/JsonCrdtApplicatorTests.cs` to test the new idempotency logic with various scenarios (in-order, out-of-order, duplicate ops).
    -   Update `$/Modern.CRDT.UnitTests/Services/Strategies/LwwStrategyTests.cs` to use `ICrdtTimestamp`.

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
- `$/Modern.CRDT/Models/CrdtMetadata.cs` (Core data structure for state, needs significant changes)
- `$/Modern.CRDT/Models/CrdtOperation.cs` (Will be modified to support replica IDs and abstract timestamps)
- `$/Modern.CRDT/Services/JsonCrdtApplicator.cs` (The idempotency check logic within this service needs a complete overhaul)
- `$/Modern.CRDT/Services/IJsonCrdtApplicator.cs` (Interface may need updates to reflect new types)
- `$/Modern.CRDT/Services/Strategies/LwwStrategy.cs` (Uses timestamps for comparison and needs to be updated to the new interface)
- `$/Modern.CRDT/Extensions/ServiceCollectionExtensions.cs` (To register the new metadata manager service)
- `$/Modern.CRDT.UnitTests/Services/JsonCrdtApplicatorTests.cs` (Tests for the applicator must be rewritten to validate the version vector logic)
- `$/Modern.CRDT.UnitTests/Services/Strategies/LwwStrategyTests.cs` (Tests need to be updated to use the new timestamp model)

<!---AI - Stage 2--->
# Changes Done
<!---
Here you add detailed information about all the changes actually done.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
Add all the things that you did in a different way than expected.
--->
- `$/Modern.CRDT/Models/ICrdtTimestamp.cs`: (New) Created the interface to abstract time representation.
- `$/Modern.CRDT/Models/EpochTimestamp.cs`: (New) Created a default `long`-based implementation of `ICrdtTimestamp`.
- `$/Modern.CRDT/Models/CrdtOperation.cs`: (Modified) Updated the record to include a `Guid Id` and `string ReplicaId`, and changed `long Timestamp` to `ICrdtTimestamp Timestamp`.
- `$/Modern.CRDT/Models/CrdtMetadata.cs`: (Modified) Completely overhauled the class to use `IDictionary<string, ICrdtTimestamp> Lww`, `IDictionary<string, ICrdtTimestamp> VersionVector`, and `ISet<CrdtOperation> SeenExceptions`.
- `$/Modern.CRDT/Services/ICrdtMetadataManager.cs`: (New) Defined the service contract for pruning and compacting metadata.
- `$/Modern.CRDT/Services/CrdtMetadataManager.cs`: (New) Implemented the metadata management logic.
- `$/Modern.CRDT/Services/JsonCrdtApplicator.cs`: (Modified) Rewrote the `ApplyOperationWithStateCheck` method to use the new version vector and exceptions set for robust idempotency checks.
- `$/Modern.CRDT/Services/Strategies/LwwStrategy.cs`: (Modified) Updated both `GeneratePatch` and `ApplyOperation` to work with the new `CrdtOperation` and `ICrdtTimestamp` models.
- `$/Modern.CRDT/Extensions/ServiceCollectionExtensions.cs`: (Modified) Registered the new `ICrdtMetadataManager` service for dependency injection.
- `$/Modern.CRDT.UnitTests/Models/EpochTimestampTests.cs`: (New) Added unit tests for the new timestamp model.
- `$/Modern.CRDT.UnitTests/Services/CrdtMetadataManagerTests.cs`: (New) Added unit tests for the metadata manager to verify pruning and compaction logic.
- `$/Modern.CRDT.UnitTests/Services/JsonCrdtApplicatorTests.cs`: (Modified) Completely rewrote the tests to validate the new version-vector-based idempotency logic.
- `$/Modern.CRDT.UnitTests/Services/Strategies/LwwStrategyTests.cs`: (Modified) Updated tests to use the new `CrdtOperation` model with `ICrdtTimestamp`.

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
The `IJsonCrdtPatcher` interface and its implementation do not have a concept of a `ReplicaId`. Consequently, when the `LwwStrategy`'s `GeneratePatch` method creates a `CrdtOperation`, it uses a placeholder `ReplicaId`. The service that invokes the patcher is responsible for iterating over the generated operations and assigning the correct `ReplicaId`. This could be improved in the future by passing context, such as a replica ID, into the patch generation process.

<!---AI - Stage 2--->
## Last notes and implementation details
<!---
Here you add comments about the implementation that didn't fit on the previous section.
--->
- The `JsonCrdtApplicator` is now responsible for identifying new vs. old operations and applying them. If an operation is new and successfully applied, it is added to the `SeenExceptions` set. It does *not* advance the `VersionVector` itself.
- The new `ICrdtMetadataManager` service should be used by the application layer to perform maintenance on the `CrdtMetadata` object. The `AdvanceVersionVector` method is the key to managing state growth; it should be called when the application knows that a set of operations up to a certain timestamp from a replica has been successfully processed, allowing `SeenExceptions` to be garbage collected.
- To facilitate the compaction logic in `CrdtMetadataManager`, the `SeenExceptions` collection in `CrdtMetadata` stores the full `CrdtOperation` object, not just its `Guid`. This provides the necessary `ReplicaId` and `Timestamp` for filtering during compaction and was deemed a necessary deviation from one interpretation of the requirements for a functional implementation.

# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->