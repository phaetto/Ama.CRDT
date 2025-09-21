<!---Human--->
# Purpose
<!---
Add the purpose of this user story.
--->
I want the partition manager to be able to manage multiple properties from the type that is requested.

<!---Human--->
# Requirements
<!---
Add the requirements, technical or not.
--->
- The `GetPartitionContentAsync` should be extended to use multiple range-keys that will end up creating a whole object for all properties.
- `GetPartitionAsync`, `GetAllDataPartitionsAsync` should propably be focusing on one dimension only (one property). Each property should have its own index and data streams.
- `IPartitionStreamProvider` should create different index and data streams per property for isolation.
- `BPlusTreePartitioningStrategy` should change the LRU cache to be per property stream so there are no collisions

<!---Human--->
## Requirements context
<!---
Add files that we will load for the UI to add context for the solution design.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
--->
- `C:\sources\Ama.CRDT\Ama.CRDT\Services\Partitioning\*.cs`
- `C:\sources\Ama.CRDT\Ama.CRDT.ShowCase.LargerThanMemory\Services\FileSystemPartitionStreamProvider.cs`
- `C:\sources\Ama.CRDT\Ama.CRDT.ShowCase.LargerThanMemory\Models\BlogPost.cs`
- `C:\sources\Ama.CRDT\Ama.CRDT\Models\Partitioning\CompositePartitionKey.cs`
- `C:\sources\Ama.CRDT\Ama.CRDT.UnitTests\Services\Partitioning\PartitionManagerTests.cs`
- `C:\sources\Ama.CRDT\Ama.CRDT.UnitTests\Services\Partitioning\BPlusTreePartitioningStrategyTests.cs`
- `C:\sources\Ama.CRDT\Ama.CRDT.UnitTests\Services\Partitioning\Serialization\IndexDefaultSerializationHelperTests.cs`

<!---Human--->
# Testing Methodology
<!---
Add the testing methodology (manual, unit, integration, end-to-end tests?)
--->
- **Unit Tests:** New and existing unit tests for `PartitionManager` and `BPlusTreePartitioningStrategy` will be created and updated to cover multi-property scenarios. Mocks will be used for dependencies like `IPartitionStreamProvider` to verify correct interactions.
- **Integration Tests:** The `LargerThanMemory` showcase application can be adapted to serve as an integration test, verifying that an object with multiple partitioned properties can be correctly stored and retrieved from the filesystem.

<!---AI - Stage 1--->
# Proposed Solutions [AI - Stage 1]
<!---
Here you will need to put a number of solutions that would fit for this problem.
Add the solutions that you rejected as well.
--->
### Solution 1: Property-Centric Streams and Orchestration (Recommended)
This approach treats each partitionable property as an independent, partitioned collection with its own dedicated index and data streams. The `PartitionManager` acts as an orchestrator, directing operations to the correct streams based on the property path.

-   **`IPartitionStreamProvider`:** Modified to accept a `propertyPath` to generate unique streams for each property (e.g., `doc1_Comments.index`, `doc1_Tags.index`).
-   **`PartitionManager`:** Becomes responsible for identifying partitionable properties on a type, routing operations from a `CrdtPatch` to the appropriate property's streams, and reconstructing the full object by fetching data from multiple properties' partitions.
-   **`BPlusTreePartitioningStrategy`:** Its internal node cache key would be modified to include the property context (e.g., a composite key of `logicalKey`, `propertyPath`, and `offset`) to prevent collisions between different property indexes.

-   **Reasoning for Recommendation:** This solution offers the best separation of concerns and scalability. It provides strong physical isolation between properties, minimizing complexity within the B+ Tree implementation itself. While the `PartitionManager` gains orchestration logic, this is preferable to complicating the low-level data structure. This design directly aligns with all stated requirements.

### Solution 2: Interleaved B+ Tree Index (Rejected)
This solution proposes using a single index file for all partitionable properties. The distinction between properties would be handled by using a complex, composite key within the B+ Tree, such as `(PropertyName, RangeKey)`.

-   **Reasoning for Rejection:** This approach dramatically increases the complexity of the B+ Tree implementation. Key comparison, balancing, and splitting logic would become significantly more difficult to implement correctly and efficiently. It would likely lead to performance degradation due to the larger, more complex keys and could introduce subtle bugs that are hard to trace. It also violates the requirement for separate index/data streams.

### Solution 3: Metadata-Driven Property Mapping (Rejected)
This solution suggests using a single index and data file but adding a separate metadata layer (e.g., in a header partition or a separate file) that maps property names to a list of partition identifiers. The `PartitionManager` would consult this map to find the correct partitions for a given property.

-   **Reasoning for Rejection:** This introduces an extra layer of indirection and a new piece of state that must be managed transactionally. The metadata map becomes a single point of contention and a potential source of data corruption if updates during partition splits are not handled perfectly. It also fails to meet the requirement of having isolated data streams for each property.

<!---AI - Stage 1--->
# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->
1.  **Update Stream Provider Interfaces and Implementations:**
    -   Modify `IPartitionStreamProvider.GetIndexStream` and `GetDataStream` to accept a `string propertyPath` parameter.
    -   In `FileSystemPartitionStreamProvider`, update the file naming convention to include the `propertyPath` to ensure file uniqueness (e.g., `_replicaId/logicalKey_propertyPath.index`).

2.  **Refactor the Partitioning Strategy:**
    -   In `BPlusTreePartitioningStrategy`, change the internal `_nodeCache` from `LRUCache<long, BPlusTreeNode<IComparable>>` to `LRUCache<Tuple<string, long>, BPlusTreeNode<IComparable>>`.
    -   The cache key will be a tuple of `(streamIdentifier, offset)`, where `streamIdentifier` is a unique string derived from the logical key and property path. This ensures that cached nodes from different property indexes do not conflict.
    -   Update all methods that interact with the cache to use this new composite key structure.

3.  **Enhance the Partition Manager:**
    -   Modify `PartitionManager` to identify all partitionable properties of a given type via reflection, caching the results for performance.
    -   In `ApplyPatchAsync`, for each `CrdtOperation`, determine the target property. If the property is partitionable, use the `IPartitionStreamProvider` to get the specific streams for that property and delegate the operation to the `IPartitioningStrategy`.
    -   If an operation targets the root object or a non-partitioned property, a new mechanism to store this "root data" will be introduced. A dedicated stream (e.g., `logicalKey_root.data`) will be used to store the main object shell without its partitioned collections.
    -   Modify `GetPartitionAsync` and `GetAllDataPartitionsAsync` to accept a `string propertyPath` to focus on a single partitioned property.
    -   Implement the new method `GetPartitionContentAsync(IComparable logicalKey)`. This method will first deserialize the root object from its dedicated stream. Then, for each partitionable property on the object, it will call the new `GetAllDataPartitionsAsync(logicalKey, propertyPath)` and merge the resulting data back into the root object, reconstructing the complete data model.

4.  **Update Data Models for Testing:**
    -   In `BlogPost.cs` within the showcase project, add a second partitionable collection (e.g., `public List<string> Tags { get; set; }`) with a corresponding CRDT strategy attribute to facilitate testing.

5.  **Expand Unit Test Coverage:**
    -   In `PartitionManagerTests.cs`, add new tests to verify:
        -   `ApplyPatchAsync` correctly routes operations to different streams based on property paths.
        -   `GetPartitionContentAsync` successfully reconstructs a complete object from a root object and multiple partitioned properties.
        -   Mock `IPartitionStreamProvider` to ensure it's called with the correct `propertyPath` for each operation.
    -   In `BPlusTreePartitioningStrategyTests.cs`, add tests that simulate interleaved calls for different properties to the same strategy instance, ensuring the cache behaves correctly without collisions.

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
- `$/Ama.CRDT/Services/Partitioning/IPartitionManager.cs` (To modify method signatures and add the new `GetPartitionContentAsync` method for object reconstruction.)
- `$/Ama.CRDT/Services/Partitioning/PartitionManager.cs` (To implement the core orchestration logic for handling multiple properties.)
- `$/Ama.CRDT/Services/Partitioning/IPartitioningStrategy.cs` (To review and ensure its interface supports the new stateless, stream-passing approach.)
- `$/Ama.CRDT/Services/Partitioning/Strategies/BPlusTreePartitioningStrategy.cs` (To update the internal caching mechanism to be property-aware.)
- `$/Ama.CRDT/Services/Partitioning/IPartitionStreamProvider.cs` (To modify the stream retrieval methods to accept a property path.)
- `$/Ama.CRDT.ShowCase.LargerThanMemory/Services/FileSystemPartitionStreamProvider.cs` (To implement the updated interface and modify the file naming logic.)
- `$/Ama.CRDT.ShowCase.LargerThanMemory/Models/BlogPost.cs` (To add another partitionable property to serve as a real-world test case.)
- `$/Ama.CRDT.UnitTests/Services/Partitioning/PartitionManagerTests.cs` (To add unit tests for the new multi-property orchestration and object reconstruction logic.)
- `$/Ama.CRDT.UnitTests/Services/Partitioning/BPlusTreePartitioningStrategyTests.cs` (To add unit tests verifying the isolation of the node cache across different properties.)
- `C:\sources\Ama.CRDT\Ama.CRDT\Models\CrdtOperation.cs`
- `C:\sources\Ama.CRDT\Ama.CRDT\Services\ICrdtMetadataManager.cs`
- `C:\sources\Ama.CRDT\Ama.CRDT.ShowCase.LargerThanMemory\*.cs`

<!---AI - Stage 2--->
# Changes Done
<!---
Here you add detailed information about all the changes actually done.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
Add all the things that you did in a different way than expected.
--->
-   **`$/Ama.CRDT/Services/Partitioning/IPartitionStreamProvider.cs`**: Modified `GetIndexStreamAsync` and `GetDataStreamAsync` to accept a `propertyPath` parameter, enabling stream isolation for each partitionable property. `GetDataStreamAsync` was also updated to take an `IComparable logicalKey` for consistency.
-   **`$/Ama.CRDT.ShowCase.LargerThanMemory/Services/FileSystemPartitionStreamProvider.cs`**: Implemented the updated `IPartitionStreamProvider` interface. File naming logic was changed to incorporate a sanitized version of the `propertyPath` for both index (`index_{propertyPath}.bin`) and data (`data/{logicalKey}_{propertyPath}.dat`) files, ensuring physical separation.
-   **`$/Ama.CRDT/Services/Partitioning/IPartitioningStrategy.cs`**: Updated all method signatures to accept a `propertyPath` parameter, allowing the strategy to operate on the correct set of streams for a given property.
-   **`$/Ama.CRDT/Services/Partitioning/Strategies/BPlusTreePartitioningStrategy.cs`**:
    -   Modified all public methods to accept and pass the `propertyPath` to the `IPartitionStreamProvider`.
    -   Re-implemented the internal node cache to use a composite key of `(string propertyPath, long offset)` instead of just `long offset`. This prevents cache collisions when the strategy instance processes operations for different properties (and thus different index files) in the same scope. A `ValueTuple<string, long>` is used as the key.
    -   Updated helper methods like `ReadNodeAsync`, `AddToCache`, and `TryGetFromCache` to work with the new composite key.
-   **`$/Ama.CRDT/Services/Partitioning/IPartitionManager.cs`**:
    -   Introduced a new method `GetFullObjectAsync(IComparable logicalKey)` to reconstruct the complete object by fetching and merging data from the root object's partition and all partitioned properties.
    -   Modified existing methods (`GetPartitionAsync`, `GetPartitionContentAsync`, `GetAllDataPartitionsAsync`, `GetDataPartitionCountAsync`, `GetDataPartitionByIndexAsync`) to accept a `propertyPath` to specify which partitioned property to query.
    -   `GetPartitionContentAsync` for a data partition now correctly reassembles the object with its root data from the header partition.
-   **`$/Ama.CRDT/Services/Partitioning/PartitionManager.cs`**:
    -   Refactored the constructor to discover all partitionable properties on type `T` using reflection and a static cache, instead of just one.
    -   `InitializeAsync` was updated to create a single header partition for root data (in a stream designated by the `$` path) and then create an initial data partition for *each* discovered partitionable property in its own set of streams.
    -   `ApplyPatchAsync` was overhauled to group `CrdtOperation`s by their target property path. Operations on non-partitionable properties are routed to the header partition. Operations on partitionable properties are routed to the appropriate partition within that property's dedicated index/data streams.
    -   Implemented `GetFullObjectAsync`, which loads the header object, then iterates through each partitionable property, loads all its data partitions, and merges the collection data back into the main object.
    -   All other methods were updated to use the `propertyPath` to select the correct streams and strategy context. A special property path `$` is used for the header/root object.
-   **`$/Ama.CRDT.ShowCase.LargerThanMemory/Models/BlogPost.cs`**: Added a new partitionable property `public IDictionary<string, string> Tags { get; set; }` decorated with `[CrdtOrMapStrategy]` to serve as a test case for the multi-property functionality.
-   **`$/Ama.CRDT.UnitTests/Services/Partitioning/BPlusTreePartitioningStrategyTests.cs`**: Added a new test `Cache_ShouldBeIsolatedAcrossProperties` to specifically verify that the updated node cache correctly isolates nodes from different property streams, preventing collisions. Existing tests were updated to pass the `propertyPath`.
-   **`$/Ama.CRDT.UnitTests/Services/Partitioning/PartitionManagerTests.cs`**:
    -   Created a new test model `MultiPartitionedModel` with two partitionable properties (`Items` and `Tags`).
    -   Added new tests: `InitializeAsync_ForMultiPropertyType_ShouldCreatePartitionsForEachProperty`, `ApplyPatchAsync_ForMultiPropertyType_ShouldRouteToCorrectPropertyStreams`, and `GetFullObjectAsync_ShouldReconstructObjectFromAllPartitions`.
    -   Existing tests were refactored to use the new `MultiPartitionedModel` and to test against a single property (`Items`), passing the property path where required.

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
-   The `PartitionManager<T>` implementation uses reflection to find partitionable properties at startup. While this is cached statically, it still adds a small overhead on the first instantiation for a given type `T`. For performance-critical scenarios, a source generator could be considered in the future to pre-compute this information.
-   Error handling for malformed JSON paths in a `CrdtOperation` is basic. The current logic for routing an operation to a property assumes simple paths (e.g., `$.items[...]`). A more robust path parser in `PartitionManager` could provide better diagnostics if an operation's path is complex or doesn't match any known property.

<!---AI - Stage 2--->
## Last notes and implementation details
<!---
Here you add comments about the implementation that didn't fit on the previous section.
--->
-   The core design decision was to make `IPartitioningStrategy` stateless with respect to the property it's operating on, requiring the `propertyPath` to be passed in on every call. This keeps the strategy implementation clean and allows a single scoped instance to manage indexes for multiple properties simultaneously.
-   The `BPlusTreePartitioningStrategy`'s node cache was a critical component to update. Using a `ValueTuple<string, long>` as the key ensures that nodes from different index files (identified by `propertyPath`) are cached independently, even if their byte offsets within their respective files are identical. This prevents subtle and difficult-to-debug cache collision bugs.
-   `PartitionManager.GetFullObjectAsync` is the new primary method for retrieving a complete, partitioned object. The other `Get...` methods like `GetPartitionContentAsync` are now lower-level, property-specific queries.
-   When retrieving a *data* partition's content via `GetPartitionContentAsync`, the manager now also loads the corresponding header partition and merges the root data. This provides a complete "view" of the object for that specific partition's data, which is useful for the `GetFullObjectAsync` implementation and for debugging.
-   The logic in `PartitionManager` to determine the target property from a `CrdtOperation.JsonPath` is based on parsing the first segment of the path. Operations targeting non-partitionable properties are correctly routed to the header partition by default.

# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->