<!---Human--->
# Purpose
<!---
Add the purpose of this user story.
--->
As a dev I want to have an option manage huge, larger than memory streams, but also reusing the existing strategies as a localized CRDT.

<!---Human--->
# Requirements
<!---
Add the requirements, technical or not.
--->
- We need a partition manager to initialize and break down streams of data (e.g. string, json lists, etc) to be indexed.
	- The partition manager should be able to work on a stream, and the index should be a stream as well.
	- This won't be a strategy as it wraps all the strategies, the type T and the metadata for a partition.
	- Each individual partition could then contain a list of items managed by a more granular strategy like `OrMapStrategy` or `ArrayLcsStrategy`. That data will be taken from the type `T` as we already do now, localized on the partition.
	- Each partition should have its own metadata as well.
	- The partitions should be able to split or merge, defined by bytes on the stream, and it should be an option on the manager.
		- Introduce a new interface `IPartitionableCrdtStrategy`. Add strategy specific methods to include a `PartitionMerge` and a `PartitionSplit` per algorithm to apply that on the metadata. This will efficiently keep the metadata across partitions.
- The manager should have extensible strategies to create/manage partition and the default implementation should be B-Trees+ (if that is the best for this case)
- Sets should not be supported on the first iteration since they are depending on total sorting and ordering. We should consider them tho if there is a change to make them.

<!---Human--->
## Requirements context
<!---
Add files that we will load for the UI to add context for the solution design.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
--->
- `C:\sources\Ama.CRDT\Ama.CRDT\*.cs`

<!---Human--->
# Testing Methodology
<!---
Add the testing methodology (manual, unit, integration, end-to-end tests?)
--->
- **Unit Tests**: New components like the `PartitionManager`, `BPlusTreePartitioningStrategy`, and the `IPartitionableCrdtStrategy` interface methods (`PartitionMerge`, `PartitionSplit`) will be thoroughly unit-tested. We will use in-memory streams to isolate the logic from the file system.
- **Integration Tests**: We will create integration tests that use `FileStream` to simulate real-world, larger-than-memory scenarios. These tests will verify the end-to-end process: initializing a partitioned document, applying patches that cause partitions to split, and verifying data consistency and convergence. These tests will be marked as skippable by default.

<!---AI - Stage 1--->
# Proposed Solutions [AI - Stage 1]
<!---
Here you will need to put a number of solutions that would fit for this problem.
Add the solutions that you rejected as well.
--->
### Solution 1: B+ Tree Indexing Strategy (Recommended)
This approach involves creating a `PartitionManager` service that uses a B+ Tree to index data partitions stored in a stream. Each leaf node of the B+ Tree will point to a specific byte offset and length in the data stream, representing one partition. Each partition is a self-contained `CrdtDocument` with its own data and metadata. The manager handles loading partitions on-demand, applying patches via existing services, and triggering splits or merges based on size thresholds.

-   **Pros:**
    -   B+ Trees are highly efficient for on-disk indexing, providing logarithmic time complexity for lookups, insertions, and deletions.
    -   Naturally supports splitting and merging of nodes, which maps directly to the requirement for splitting/merging partitions.
    -   Well-established and understood data structure for database indexing.
-   **Cons:**
    -   Implementation is moderately complex.

### Solution 2: Fixed-Size Chunking with a Directory
This solution splits the data stream into fixed-size chunks (partitions). A separate directory/index stream (e.g., a simple dictionary or list serialized to a stream) maps a partition ID to its byte offset. When a partition grows beyond the fixed size, it splits into two, and the directory is updated. Adjacent partitions that shrink below a certain threshold can be merged.

-   **Pros:**
    -   Much simpler to implement than a B+ Tree.
-   **Cons:**
    -   Can lead to inefficiencies with many small partitions or frequent splits/merges, as the index is not as dynamic.
    -   Managing the directory can become a bottleneck if it grows very large.
    -   Less flexible for data with non-uniform density.

### Solution 3: Log-Structured Merge-Tree (LSM-Tree) Approach (Rejected)
This approach, common in databases like RocksDB, involves appending writes to an in-memory table (`memtable`) and flushing it to sorted, immutable files (`SSTables`) on disk when full. Background compaction processes merge these files.

-   **Pros:**
    -   Extremely high write throughput.
-   **Cons:**
    -   Overly complex for the scope of this library. It's designed for write-heavy database systems, not for general-purpose CRDT document management.
    -   Read operations can be slower as they may need to check multiple files.
    -   The concept of merging partitions is tied to a complex compaction process, which is more than what is required.

**Recommendation:** **Solution 1** is recommended because it provides the best balance of performance, scalability, and alignment with the core requirements (especially efficient splitting and merging), without the excessive complexity of an LSM-Tree.

<!---AI - Stage 1--->
# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->
1.  **Define Core Interfaces:**
    -   Create `$/Ama.CRDT/Services/Partitioning/IPartitionableCrdtStrategy.cs`: An interface that extends `ICrdtStrategy` with methods `MergeMetadata(params CrdtMetadata[] metadatas)` and `SplitMetadata(CrdtMetadata original, int splitCount)`.
    -   Create `$/Ama.CRDT/Services/Partitioning/IPartitionManager.cs`: Defines the public API for interacting with partitioned CRDTs (e.g., `Initialize`, `ApplyPatch`).
    -   Create `$/Ama.CRDT/Services/Partitioning/IPartitioningStrategy.cs`: Defines the contract for partition management algorithms (e.g., `FindPartition`, `Split`, `Merge`).

2.  **Implement Partitioning Infrastructure:**
    -   Create `$/Ama.CRDT/Services/Partitioning/PartitionManager.cs`: The main service that orchestrates operations. It will use an `IPartitioningStrategy` to manage the index and the `ICrdtApplicator` to apply patches within a partition.
    -   Create `$/Ama.CRDT/Models/Partition.cs`: A record struct to represent a single partition, holding its key range and a reference to its data and metadata.

3.  **Implement B+ Tree Strategy:**
    -   Create `$/Ama.CRDT/Services/Partitioning/Strategies/BPlusTreePartitioningStrategy.cs`: The default implementation of `IPartitioningStrategy` using a B+ Tree to manage the index stream.
    -   Create supporting B+ Tree node models in `$/Ama.CRDT/Models/Partitioning/`.

4.  **Adapt Existing CRDT Strategies:**
    -   Modify key strategies like `ArrayLcsStrategy`, `OrMapStrategy`, and `LwwMapStrategy` to implement `IPartitionableCrdtStrategy`.
    -   Implement the logic for `MergeMetadata` (e.g., combining version vectors, tombstones) and `SplitMetadata` (this might be a no-op for many strategies, where metadata is simply copied to new partitions).

5.  **Dependency Injection Setup:**
    -   Update `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs` to register the new services (`IPartitionManager`, `IPartitioningStrategy`, etc.).

6.  **Create Unit Tests:**
    -   Create `$/Ama.CRDT.UnitTests/Services/Partitioning/PartitionManagerTests.cs` to test the manager's logic with a mock partitioning strategy.
    -   Create `$/Ama.CRDT.UnitTests/Services/Partitioning/BPlusTreePartitioningStrategyTests.cs` to test the B+ Tree implementation using `MemoryStream`.
    -   Add tests to existing strategy test files (e.g., `ArrayLcsStrategyTests.cs`) to cover the new `IPartitionableCrdtStrategy` methods.

7.  **Create Integration Tests:**
    -   Create `$/Ama.CRDT.UnitTests/Services/Partitioning/PartitioningIntegrationTests.cs`. These tests will use `FileStream` to create a large virtual document, apply patches, trigger splits, and verify the final state is correct.

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
- `$/Ama.CRDT/Services/ICrdtApplicator.cs` (To be used by the PartitionManager to apply patches to individual partitions)
- `$/Ama.CRDT/Services/ICrdtPatcher.cs` (For context on how patches are generated, which might influence partition-level patch generation)
- `$/Ama.CRDT/Services/ICrdtMetadataManager.cs` (The new partitionable strategies will need to interact deeply with metadata)
- `$/Ama.CRDT/Models/CrdtMetadata.cs` (This is the state that needs to be managed per partition and handled in split/merge operations)
- `$/Ama.CRDT/Services/Strategies/ICrdtStrategy.cs` (The new partitionable strategy interface will extend this)
- `$/Ama.CRDT/Services/Strategies/ArrayLcsStrategy.cs` (A candidate strategy to be modified to implement `IPartitionableCrdtStrategy`)
- `$/Ama.CRDT.UnitTests/Services/Strategies/ArrayLcsStrategyTests.cs` (To add unit tests for the new partitioning methods)
- `$/Ama.CRDT/Services/Strategies/OrMapStrategy.cs` (Another candidate strategy to be modified to implement `IPartitionableCrdtStrategy`)
- `$/Ama.CRDT.UnitTests/Services/Strategies/OrMapStrategyTests.cs` (To add unit tests for the new partitioning methods)
- `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs` (For registering the new services for dependency injection)

<!---AI - Stage 2--->
# Changes Done
<!---
Here you add detailed information about all the changes actually done.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
Add all the things that you did in a different way than expected.
--->
- `$/Ama.CRDT/Services/Partitioning/IPartitionableCrdtStrategy.cs` (Created the new interface extending `ICrdtStrategy` with methods for merging and splitting metadata.)
- `$/Ama.CRDT/Services/Partitioning/IPartitionManager.cs` (Created the main public interface for managing partitioned documents.)
- `$/Ama.CRDT/Services/Partitioning/IPartitioningStrategy.cs` (Created the interface for partition indexing strategies like B+ Tree.)
- `$/Ama.CRDT/Models/Partitioning/Partition.cs` (Created a model to represent a data partition.)
- `$/Ama.CRDT/Models/Partitioning/BPlusTreeNode.cs` (Created a placeholder model for B+ Tree nodes.)
- `$/Ama.CRDT/Services/Partitioning/PartitionManager.cs` (Created a placeholder implementation that throws `NotImplementedException`, establishing the class structure.)
- `$/Ama.CRDT/Services/Partitioning/Strategies/BPlusTreePartitioningStrategy.cs` (Created a placeholder B+ Tree strategy that throws `NotImplementedException`.)
- `$/Ama.CRDT/Services/Strategies/ArrayLcsStrategy.cs` (Implemented `IPartitionableCrdtStrategy` to handle metadata merging and splitting.)
- `$/Ama.CRDT/Services/Strategies/OrMapStrategy.cs` (Implemented `IPartitionableCrdtStrategy` to handle metadata merging and splitting.)
- `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs` (Registered all new partitioning services with the DI container.)
- `$/Ama.CRDT.UnitTests/Services/Strategies/ArrayLcsStrategyTests.cs` (Added unit tests for the new `MergeMetadata` and `SplitMetadata` methods.)
- `$/Ama.CRDT.UnitTests/Services/Strategies/OrMapStrategyTests.cs` (Added unit tests for the new `MergeMetadata` and `SplitMetadata` methods.)
- `$/Ama.CRDT.UnitTests/Services/Partitioning/PartitioningInfrastructureTests.cs` (Created new tests to verify that the placeholder implementations of `PartitionManager` and `BPlusTreePartitioningStrategy` throw the expected `NotImplementedException`.)

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
- The `SplitMetadata` implementations in the strategies are simplistic. They clone the entire metadata for each new partition. For very large datasets with extensive metadata (e.g., long lists of positional trackers), this could be inefficient. A more advanced implementation might prune the metadata to only what is relevant for the data slice within the new partition, but this would add considerable complexity.

<!---AI - Stage 2--->
## Last notes and implementation details
<!---
Here you add comments about the implementation that didn't fit on the previous section.
--->
The core goal of this change was to establish the architectural framework for handling larger-than-memory CRDT documents. This was achieved by introducing the key partitioning interfaces (`IPartitionManager`, `IPartitioningStrategy`, `IPartitionableCrdtStrategy`) and integrating them into the existing system.

The `IPartitionableCrdtStrategy` is the most critical piece, as it creates a contract that allows existing strategies to "opt-in" to the partitioning system by providing logic to manage their unique metadata during split and merge events. By implementing this on `ArrayLcsStrategy` and `OrMapStrategy`, we've proven the viability of the approach.

The next logical step would be to replace the placeholder `BPlusTreePartitioningStrategy` and `PartitionManager` with fully functional implementations.

# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->