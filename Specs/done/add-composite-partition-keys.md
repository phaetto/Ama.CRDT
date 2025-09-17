<!---Human--->
# Purpose
The purpose of this user story is to re-architect the CRDT partitioning mechanism to be robust, scalable, and safe for complex, real-world data models.

The current `PartitionManager` implementation is brittle and suffers from two critical design flaws:
1.  **Inability to handle non-partitioned fields:** When a partitionable collection (like a large dictionary) is split, the `PartitionManager` implicitly duplicates all other non-partitioned fields in the data model. Subsequent updates to these duplicated fields lead to data inconsistency and corruption, as only one copy is updated.
2.  **Lack of multi-document support:** The manager uses a single, global keyspace for its B+ Tree index. It has no concept of logical document ownership (e.g., Tenant ID, Document ID). This makes it impossible to manage more than one logical document, as key collisions between different documents are inevitable, leading to catastrophic data corruption where data from one document is written into another.

This story will introduce a **Composite Partition Key** model. This model will allow developers to define a `LogicalKey` on their data model (e.g., `TenantId`), which ensures data isolation. The CRDT strategy for a large collection will provide a `RangeKey` (e.g., a dictionary key or a stable list identifier), which allows a single logical group to be split across multiple physical partitions based on size. This change will make the partitioning feature safe, flexible enough to support all CRDT strategies (including list-based ones), and aligned with standard patterns for scalable distributed data systems.

<!---Human--->
# Requirements
### Technical Requirements
1.  **Introduce `[PartitionKey]` Attribute:** A new attribute, `[PartitionKey(string propertyName)]`, must be created. This attribute will be used on the root class of a CRDT document to designate a single property as the `LogicalKey` for partitioning.
2.  **Create `CompositePartitionKey`:** A new `readonly record struct` named `CompositePartitionKey` must be implemented.
    - It will contain two `object` properties: `LogicalKey` and `RangeKey`.
    - It must implement `IComparable` and `IComparable<CompositePartitionKey>`. The comparison logic must sort first by `LogicalKey` and then by `RangeKey`.
3.  **Refactor `IPartitioningStrategy`:** The `BPlusTreePartitioningStrategy` and its interface must be updated to store and query using the new `CompositePartitionKey` instead of a simple `object` key.
4.  **Redefine `IPartitionableCrdtStrategy` Role:** The role of this interface will be narrowed. It is no longer responsible for the entire partition key. Its methods will now function as follows:
    - `GetKeyFromOperation(CrdtOperation op)`: Must return the `RangeKey` portion of the composite key (e.g., the dictionary key or `PositionalIdentifier`).
    - `Split(...)`: Must split the partitionable collection and its metadata *within a single logical document*, returning the `RangeKey` that acts as the split point.
    - `Merge(...)`: Must merge two adjacent physical partitions that belong to the same logical document.
5.  **Update `CrdtPatch` Model:** The `CrdtPatch` record must be updated to include a new property: `public object? LogicalKey { get; init; }`.
6.  **Refactor `PartitionManager<T>`:** The `PartitionManager` requires a significant refactor to become the orchestrator of the composite key model.
    - It must identify the `LogicalKey` property via the `[PartitionKey]` attribute at initialization.
    - `ApplyPatchAsync(CrdtPatch patch)` must now require `patch.LogicalKey` to be populated. It will use this key as the `LogicalKey` for all composite key operations.
    - It must implement a "Header" partition model to safely manage non-partitioned fields. Data for a logical document will be stored in two places:
        - A single "header partition" containing the document object with its partitionable collection empty. This stores all non-partitioned fields.
        - One or more "data partitions" containing only the items of the large, partitionable collection.
    - When a patch is applied, the manager must route operations to the correct storage location (header vs. data partitions) based on the operation's JSON path.
    - Splitting and merging operations must only ever affect the "data partitions". The "header partition" is never split.
7.  **Update Existing Strategies:** `OrMapStrategy` must be updated to conform to the new, narrowed role of `IPartitionableCrdtStrategy`.
8.  **Implement on a List Strategy:** To validate the new architecture, `ArrayLcsStrategy` must be updated to implement `IPartitionableCrdtStrategy`, using its `PositionalIdentifier` as the `RangeKey`.

### Non-Technical Requirements
1.  **Data Isolation:** The system must provide an absolute guarantee that data belonging to one `LogicalKey` cannot be read from or written to a partition belonging to a different `LogicalKey`.
2.  **Scalability:** A single logical document (e.g., a "whale tenant" with millions of items in a collection) must be able to scale horizontally across hundreds or thousands of physical partitions without performance degradation.
3.  **Developer Experience:** The API for enabling partitioning on a data model should be simple and declarative, fulfilled by adding the `[PartitionKey]` attribute.

<!---Human--->
## Requirements context
The following files provide the necessary context for the current state of the partitioning system and the key components that will need to be refactored or extended.
	- `$/Ama.CRDT/Services/Partitioning/PartitionManager.cs` (This is the central class for the partitioning logic and will require the most significant refactoring to support composite keys and the header/data partition model.)
	- `$/Ama.CRDT/Services/Partitioning/IPartitioningStrategy.cs` (The interface for the underlying index, which will need to be adapted for composite keys.)
	- `$/Ama.CRDT/Services/Partitioning/Strategies/BPlusTreePartitioningStrategy.cs` (The B+ Tree implementation that must be modified to handle the storage, retrieval, and comparison of `CompositePartitionKey`.)
	- `$/Ama.CRDT/Services/Partitioning/IPartitionableCrdtStrategy.cs` (This interface defines the contract that will be narrowed in scope. Understanding its current role is key to refactoring it.)
	- `$/Ama.CRDT/Services/Strategies/OrMapStrategy.cs` (The only existing implementation of `IPartitionableCrdtStrategy`, which will need to be updated to the new model.)
	- `$/Ama.CRDT/Services/Strategies/ArrayLcsStrategy.cs` (A primary candidate for becoming partitionable. This file shows how a list-based strategy currently works and will be the target for implementing the new partitionable contract.)
	- `$/Ama.CRDT/Models/CrdtPatch.cs` (The data transfer object for patches, which needs to be modified to carry the `LogicalKey`.)
	- `$/Ama.CRDT.UnitTests/Services/Partitioning/PartitionManagerTests.cs` (These tests define the current behavior and will need to be completely overhauled to validate the new composite key logic, including data isolation and header field consistency.)

<!---Human--->
# Testing Methodology
The testing methodology will be comprehensive, focusing on ensuring correctness, data integrity, and robustness of the new partitioning model.

**1. Unit Tests:**
-   **`CompositePartitionKeyTests` (New):** A new test class will be created to rigorously test the `IComparable` implementation of the `CompositePartitionKey` struct, covering comparisons with equal logical keys, different logical keys, null range keys, and different types for keys.
-   **`BPlusTreePartitioningStrategyTests` (Modified):** Existing tests will be updated to use `CompositePartitionKey`. New tests will be added to verify correct node splitting and searching with composite keys, ensuring the "sort-by-logical-then-by-range" behavior is correct.
-   **`PartitionManagerTests` (Heavily Modified):** This will be the main focus of unit testing. Tests will be rewritten to cover the following scenarios:
    -   **Initialization:** Verifying that initializing a document creates the correct header and initial data partition.
    -   **Data Isolation:** A critical test that creates two logical documents (e.g., "tenant-A", "tenant-B") and applies patches to each, asserting that the data for one is never mixed with the other.
    -   **Header Field Consistency:** A test that forces a data partition to split and then applies an update to a non-partitioned (header) field, asserting that the change is correctly applied only once and is reflected regardless of which data partition is accessed later.
    -   **Partition Splitting:** A test that adds enough data to a single logical document to trigger a split, verifying that the B+ Tree index is updated correctly with the new composite key ranges.
    -   **Partition Merging:** A test that removes data from a logical document with multiple physical partitions, triggering a merge and verifying the index is correctly updated.
    -   **Patch Application:** Tests to ensure patches containing operations for both header fields and the partitioned collection are correctly routed and applied.

**2. Integration Tests:**
-   An end-to-end integration test will be created using `FileStream` to simulate durable storage for the data and index streams.
-   This test will orchestrate a complex lifecycle for multiple logical documents:
    1.  Initialize Document A and Document B.
    2.  Add a small amount of data to Document B.
    3.  Add a large amount of data to Document A, enough to force several partition splits.
    4.  Update a header field on Document A.
    5.  Remove a large amount of data from Document A, enough to force at least one partition merge.
    6.  Throughout the process, read back data from both documents to continuously verify consistency of both header fields and the partitioned collection.

**3. Manual Testing:**
-   No specific manual tests are required if the automated test coverage is sufficient. The `Ama.CRDT.ShowCase` project can be optionally updated to demonstrate the feature with a more complex, partitioned model.

<!---AI - Stage 1--->
# Proposed Solutions [AI - Stage 1]
<!---
Here you will need to put a number of solutions that would fit for this problem.
Add the solutions that you rejected as well.
--->
Here are three potential solutions to address the partitioning system's flaws:

**Solution 1: Header/Data Partition Model (Recommended)**
*   **Description:** This approach directly implements the solution outlined in the requirements. For each unique `LogicalKey`, the `PartitionManager` will maintain one special "header" partition and one or more "data" partitions. The header partition stores the main document object with the partitionable collection emptied, effectively holding all non-partitioned fields. Data partitions exclusively store subsets of the partitionable collection's items. The `PartitionManager` inspects the JSON path of each incoming `CrdtOperation` to intelligently route it to either the header partition (for root-level fields) or the appropriate data partition (for collection items). This is orchestrated using a `CompositePartitionKey` (`LogicalKey`, `RangeKey`), where the header partition can be identified by a `null` `RangeKey`.
*   **Pros:**
    *   **Eliminates Data Corruption:** Completely solves the data duplication and consistency issues by storing non-partitioned fields in a single, authoritative location.
    *   **True Data Isolation:** The `LogicalKey` in the composite key provides a hard boundary in the B+ Tree index, guaranteeing that one document's data cannot bleed into another's.
    *   **Highly Scalable and Efficient:** Splitting and merging only affect data partitions, which is efficient. Reads and writes are targeted precisely to the required partition.
*   **Cons:**
    *   **Highest Complexity:** This is the most complex solution, requiring a major rewrite of `PartitionManager`'s core logic to handle operation routing and the dual-partition model.
*   **Recommendation:** This is the best and most robust solution. It correctly solves all identified architectural flaws and provides a solid foundation for a truly scalable and reliable partitioning feature. The initial complexity is justified by the long-term stability and correctness it provides.

**Solution 2: Full Document Duplication with Logical Key Scoping (Rejected)**
*   **Description:** This is a less complex alternative where every physical partition still contains a full copy of the document object, including non-partitioned fields. The `CompositePartitionKey` would be used to isolate different logical documents. However, to handle updates to non-partitioned fields, the `PartitionManager` would need to find *all* physical partitions belonging to a given `LogicalKey` and apply the same update to each one of them.
*   **Pros:**
    *   Simpler `PartitionManager` logic, as there is no need for path-based operation routing.
*   **Cons:**
    *   **Massive Write Amplification:** An update to a single header field triggers N writes, where N is the number of partitions. This is extremely inefficient.
    *   **High Risk of Inconsistency:** If any one of the N writes fails, the partitions for that logical document become inconsistent. This reintroduces the very problem we are trying to solve, albeit scoped to a single logical document.
    *   **Poor Performance:** The write amplification would lead to significant performance degradation, especially for documents that have been split into many partitions.
*   **Reason for Rejection:** This solution trades architectural correctness for a minor reduction in initial implementation complexity. The performance and data consistency risks are too high, making it unsuitable for a production-grade system.

**Solution 3: Metadata-Only Partitioning (Rejected)**
*   **Description:** In this model, the B+ Tree index would still use the `CompositePartitionKey`. However, instead of splitting data across multiple streams, all data for a single logical document would reside in a single, large stream. The B+ Tree would only store metadata pointers (offsets and lengths) to the location of data "chunks" within that single stream.
*   **Pros:**
    *   Reduces the number of file handles required, as there would be fewer physical streams to manage.
*   **Cons:**
    *   **Defeats Scalability:** Fails to solve the "larger-than-memory" problem, as a single logical document is still constrained by the size limits of a single stream.
    *   **Inefficient Splits/Merges:** Splitting or merging would require complex and slow operations to shift large amounts of data within the single stream, rather than simply creating or deleting small, separate partition files.
*   **Reason for Rejection:** This approach misunderstands the core goal of physical partitioning for scalability. It offers no real benefits over the current system and introduces significant complexity in managing the data within a single stream, making it an impractical solution.

<!---AI - Stage 1--->
# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->
1.  **Create Core Models and Attributes:**
    *   **File:** `$/Ama.CRDT/Attributes/PartitionKeyAttribute.cs` (New)
        *   **Action:** Create a new `sealed class PartitionKeyAttribute : Attribute` that accepts a `string propertyName` in its constructor. This will be used to decorate the root class of a partitionable document.
    *   **File:** `$/Ama.CRDT/Models/Partitioning/CompositePartitionKey.cs` (New)
        *   **Action:** Implement a `public readonly record struct CompositePartitionKey : IComparable<CompositePartitionKey>, IComparable`. It will contain `public object LogicalKey { get; }` and `public object? RangeKey { get; }`. The `CompareTo` logic will be implemented to sort by `LogicalKey` first, then by `RangeKey`. Handle null `RangeKey` as the lowest value.
    *   **File:** `$/Ama.CRDT/Models/CrdtPatch.cs` (Modify)
        *   **Action:** Add a new property: `public object? LogicalKey { get; init; }`.

2.  **Update Core Partitioning Interfaces:**
    *   **File:** `$/Ama.CRDT/Services/Partitioning/IPartitioningStrategy.cs` (Modify)
        *   **Action:** Change method signatures to work with `CompositePartitionKey`. For example, `FindPartitionAsync(object key)` becomes `FindPartitionAsync(CompositePartitionKey key)`.
    *   **File:** `$/Ama.CRDT/Services/Partitioning/IPartitionableCrdtStrategy.cs` (Modify)
        *   **Action:** Refine the interface methods. `GetKeyFromOperation(CrdtOperation op)` will now return the `RangeKey` (`object`). `Split` will now return a `SplitResult` containing the `RangeKey` that serves as the new partition's starting key.

3.  **Refactor `BPlusTreePartitioningStrategy`:**
    *   **File:** `$/Ama.CRDT/Services/Partitioning/Strategies/BPlusTreePartitioningStrategy.cs` (Modify)
        *   **Action:** Update the entire class to store, compare, and search for `CompositePartitionKey` instances instead of `object`. This will impact node-splitting logic, key comparisons, and search algorithms.

4.  **Major Refactor of `PartitionManager<T>`:**
    *   **File:** `$/Ama.CRDT/Services/Partitioning/PartitionManager.cs` (Modify)
        *   **Action (Initialization):** On initialization, use reflection to find the property on `T` decorated with `[PartitionKey]` and cache its `PropertyInfo`. Also, identify the property representing the partitionable collection.
        *   **Action (Header Partitions):** Introduce logic to distinguish between a "header" partition (where `RangeKey` is `null`) and "data" partitions. When initializing a new logical document, create its header partition by cloning the document and clearing the partitionable collection.
        *   **Action (Patch Application):** Overhaul `ApplyPatchAsync`. It must now read `patch.LogicalKey`. For each operation in the patch, check its JSON path.
            *   If the path targets the partitionable collection, extract the `RangeKey` using `IPartitionableCrdtStrategy` and find the correct data partition using the full `CompositePartitionKey`.
            *   If the path targets any other field, find the header partition using a `CompositePartitionKey` with a `null` `RangeKey`.
        *   **Action (Splitting):** Modify the splitting logic so that it only ever splits data partitions. The header partition is never split. The new key for the B+ Tree will be a `CompositePartitionKey` with the same `LogicalKey` and the new `RangeKey` from the `SplitResult`.

5.  **Update and Implement `IPartitionableCrdtStrategy`:**
    *   **File:** `$/Ama.CRDT/Services/Strategies/OrMapStrategy.cs` (Modify)
        *   **Action:** Update its implementation of `IPartitionableCrdtStrategy` to conform to the new contract. `GetKeyFromOperation` will extract the dictionary key from the operation's value. `Split` will divide the dictionary and its metadata, returning a dictionary key as the `RangeKey` split point.
    *   **File:** `$/Ama.CRDT/Services/Strategies/ArrayLcsStrategy.cs` (Modify)
        *   **Action:** Implement `IPartitionableCrdtStrategy`. `GetKeyFromOperation` will extract the `PositionalIdentifier` from the operation's value. `Split` will find a midpoint `PositionalIdentifier` in the positional map to use as the `RangeKey` for the new partition.

6.  **Update and Expand Unit Tests:**
    *   **File:** `$/Ama.CRDT.UnitTests/Models/Partitioning/CompositePartitionKeyTests.cs` (New)
        *   **Action:** Create comprehensive tests for the `CompareTo` method of `CompositePartitionKey`, covering various types, nulls, and edge cases.
    *   **File:** `$/Ama.CRDT.UnitTests/Services/Partitioning/BPlusTreePartitioningStrategyTests.cs` (Modify)
        *   **Action:** Rewrite existing tests to use `CompositePartitionKey` and verify the correct sorting and searching behavior.
    *   **File:** `$/Ama.CRDT.UnitTests/Services/Partitioning/PartitionManagerTests.cs` (Modify)
        *   **Action:** Overhaul the test suite to validate the new architecture. Add specific, isolated tests for: data isolation between two logical keys, header field updates after a data partition split, correct operation routing, and successful splitting/merging of data partitions for a single logical key.

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
	- `$/Ama.CRDT/Services/Partitioning/PartitionManager.cs` (The central component that needs a major refactoring to orchestrate the composite key and header/data partition model.)
	- `$/Ama.CRDT/Services/Partitioning/IPartitioningStrategy.cs` (The interface for the B+ Tree, which must be updated to use `CompositePartitionKey`.)
	- `$/Ama.CRDT/Services/Partitioning/Strategies/BPlusTreePartitioningStrategy.cs` (The implementation of the B+ Tree index, which must be modified to handle `CompositePartitionKey` logic.)
	- `$/Ama.CRDT/Services/Partitioning/IPartitionableCrdtStrategy.cs` (The contract for partitionable CRDTs, whose role is being redefined.)
	- `$/Ama.CRDT/Services/Strategies/OrMapStrategy.cs` (The existing implementation of `IPartitionableCrdtStrategy` that must be updated.)
	- `$/Ama.CRDT/Services/Strategies/ArrayLcsStrategy.cs` (The list-based strategy that will be made partitionable to validate the new architecture.)
	- `$/Ama.CRDT/Models/CrdtPatch.cs` (The DTO for patches, which needs a new `LogicalKey` property.)
	- `$/Ama.CRDT.UnitTests/Services/Partitioning/PartitionManagerTests.cs` (The primary test file that needs to be completely rewritten to validate data isolation, header consistency, and correct split/merge logic.)
	- `$/Ama.CRDT.UnitTests/Services/Partitioning/BPlusTreePartitioningStrategyTests.cs` (The tests for the B+ Tree, which need to be updated to use `CompositePartitionKey`.)

<!---AI - Stage 2--->
# Changes Done
<!---
Here you add detailed information about all the changes actually done.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
Add all the things that you did in a different way than expected.
--->
- `$/Ama.CRDT/Attributes/PartitionKeyAttribute.cs` (New): Created the attribute to designate a logical key property on a data model.
- `$/Ama.CRDT/Models/Partitioning/CompositePartitionKey.cs` (New): Implemented the `readonly record struct` to encapsulate the `LogicalKey` and `RangeKey`, along with the required `IComparable` logic.
- `$/Ama.CRDT/Models/CrdtPatch.cs` (Modified): Added the `LogicalKey` property to carry the logical document identifier with each patch.
- `$/Ama.CRDT/Services/Partitioning/IPartitioningStrategy.cs` (Modified): Updated the `FindPartitionAsync` method signature to accept the new `CompositePartitionKey` and added XML comments to other methods clarifying the new key type requirement.
- `$/Ama.CRDT/Services/Partitioning/Strategies/BPlusTreePartitioningStrategy.cs` (Modified): Refactored the B+ Tree implementation to natively handle `CompositePartitionKey`, updating all comparison, insertion, and search logic accordingly.
- `$/Ama.CRDT/Services/Partitioning/IPartitionableCrdtStrategy.cs` (Modified): Redefined the interface to focus on range keys. `GetKeyFromOperation` was updated to accept the path of the partitionable property to help distinguish between header and data operations.
- `$/Ama.CRDT/Services/Strategies/OrMapStrategy.cs` (Modified): Updated the strategy to correctly implement the new, narrowed `IPartitionableCrdtStrategy` interface, handling range key extraction and splitting/merging of its specific collection and metadata.
- `$/Ama.CRDT/Services/Strategies/ArrayLcsStrategy.cs` (Modified): Implemented the `IPartitionableCrdtStrategy` interface, making list-based strategies compatible with the new partitioning system for the first time.
- `$/Ama.CRDT/Services/Partitioning/PartitionManager.cs` (Modified): Performed a major overhaul to orchestrate the entire composite key and header/data partition model. It now handles logical key discovery, operation routing, and manages the lifecycle of header and data partitions, ensuring data isolation and consistency.
- `$/Ama.CRDT.UnitTests/Models/Partitioning/CompositePartitionKeyTests.cs` (New): Added comprehensive unit tests for the new `CompositePartitionKey` to ensure its comparison logic is correct.
- `$/Ama.CRDT.UnitTests/Services/Partitioning/BPlusTreePartitioningStrategyTests.cs` (Modified): Updated unit tests to use `CompositePartitionKey`, verifying the correctness of the B+ Tree's new key handling.
- `$/Ama.CRDT.UnitTests/Services/Partitioning/PartitionManagerTests.cs` (Modified): Completely rewrote the tests to validate the new architecture, including specific scenarios for data isolation, header/data partition routing, and split/merge correctness.

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
- The metadata splitting and merging logic within the `IPartitionableCrdtStrategy` implementations (`OrMapStrategy`, `ArrayLcsStrategy`) is specific to the metadata types they use (e.g., `OrMaps`, `PositionalTrackers`). While functional, a more generic mechanism for splitting/merging arbitrary metadata attached to a partition could be beneficial in the future to simplify adding new partitionable strategies.
- The `PartitionManager` currently assumes that a document's initial state (passed to `InitializeAsync`) fits in memory. For bootstrapping extremely large, pre-existing documents, a streaming initialization approach might be necessary.

<!---AI - Stage 2--->
## Last notes and implementation details
<!---
Here you add comments about the implementation that didn't fit on the previous section.
--->
- The core of this implementation is the strict separation of concerns introduced by the header/data partition model. The `PartitionManager` is now the sole orchestrator, using the `CompositePartitionKey` to query the `IPartitioningStrategy` (the B+ Tree) and using the `IPartitionableCrdtStrategy` (`OrMapStrategy`, etc.) as a "plugin" to understand the internal structure of a specific collection.
- A key design decision was to add the `partitionablePropertyPath` to the `GetKeyFromOperation` method on `IPartitionableCrdtStrategy`. This allows the strategy to definitively know if an operation is targeting the collection it manages versus another property on the document. Returning `null` is the signal that the operation belongs to the header partition.
- The unit tests for `PartitionManager` were rewritten from scratch to reflect the new user stories: data isolation between tenants, correct routing of header vs. data operations, and ensuring header partitions are never split. These tests are critical for guaranteeing the new architecture's correctness.
- The B+ Tree was refactored to use `Comparer<object>.Default.Compare` on its keys. Since `CompositePartitionKey` implements `IComparable`, this works seamlessly and allows the B+ Tree to correctly sort and search the composite keys without needing to know their internal structure.

# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->