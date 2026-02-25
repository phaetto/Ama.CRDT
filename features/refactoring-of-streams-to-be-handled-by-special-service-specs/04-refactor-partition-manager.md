<!---Human--->
# Purpose
<!---
Add the purpose of this user story.
--->
The purpose of this spec is to refactor `PartitionManager` to consume the new `IPartitionStorageService`. By stripping out stream manipulation and serialization logic, `PartitionManager` will become cleaner, solely focusing on logical partition splitting, merging, and coordinating data limits.

<!---Human--->
# Requirements
<!---
Add the requirements, technical or not.
--->
1. Modify `PartitionManager` constructor to accept `IPartitionStorageService` instead of `IPartitionStreamProvider` and `IPartitioningStrategy`.
2. Remove all `Stream`, offset, and pointer logic from `PartitionManager`.
3. Replace direct stream reads/writes with strongly-typed calls to `IPartitionStorageService`.
4. Ensure the logical functionality of partition splitting and merging remains unchanged, relying on the storage service to persist the changes.

<!---Human--->
## Requirements context
<!---
Add files that we will load for the UI to add context for the solution design.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
--->
- `$/features/refactoring-of-streams-to-be-handled-by-special-service-specs/01-create-partition-serialization-service.md` (Underlying changes context)
- `$/features/refactoring-of-streams-to-be-handled-by-special-service-specs/02-define-partition-storage-interface.md` (Provides the contract used by PartitionManager)
- `$/features/refactoring-of-streams-to-be-handled-by-special-service-specs/03-implement-bplustree-storage-service.md` (Context for how things are actually executing behind the interface)
- `$/Ama.CRDT/Services/Partitioning/PartitionManager.cs` (The core file to be refactored)
- `$/Ama.CRDT/Services/Partitioning/IPartitionManager.cs` (May need minor signature adjustments, though ideally interfaces stay identical for consumers)

<!---Human--->
# Testing Methodology
<!---
Add the testing methodology (manual, unit, integration, end-to-end tests?)
--->
- Unit tests: Update `PartitionManagerTests` to inject mock `IPartitionStorageService` instead of streams. Verify that partition splits and merges correctly call the storage service's Save/Update methods.

<!---AI - Stage 1--->
# Proposed Solutions [AI - Stage 1]
<!---
Here you will need to put a number of solutions that would fit for this problem.
Add the solutions that you rejected as well.
--->
1. **Full Dependency Injection Replacement (Recommended)**: Change the `PartitionManager` to purely depend on `IPartitionStorageService`. All logic dealing with stream providers, strategies, byte offsets, and serialization is removed and delegated to the storage service. `PartitionManager` becomes a high-level orchestrator focusing exclusively on CRDT logic (splitting rules, data limits, iteration).
   *Reasoning*: Perfectly adheres to the Single Responsibility Principle, makes testing `PartitionManager` dramatically easier (by mocking `IPartitionStorageService` returning POCOs rather than mocking streams), and cleanly isolates the storage layer.

2. **Facade Pattern Adaptation**: Maintain `IPartitionStreamProvider` and `IPartitioningStrategy` as dependencies in `PartitionManager`, but wrap them internally within a transient instance of a storage service.
   *Reasoning for rejection*: This fails to meet the requirement of cleaning up the constructor and merely hides the dependencies rather than structurally improving the composition of the class. It leaves testing complicated.

3. **Hybrid Storage/Manager Layer**: Merge the new storage service methods directly into `PartitionManager` as private helper methods, essentially maintaining the current structure but using better internal abstractions.
   *Reasoning for rejection*: Explicitly contradicts the goal of stripping out serialization and stream logic from the manager, failing to achieve the desired separation of concerns.

<!---AI - Stage 1--->
# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->
1. **Update `PartitionManager` Constructor**: 
   - Remove `IPartitionStreamProvider` and `IPartitioningStrategy` from the constructor and private fields.
   - Inject `IPartitionStorageService` and store it in a read-only field.
2. **Refactor Read Operations**:
   - Update `GetPartitionAsync`, `GetPartitionsAsync`, etc., to use `_storageService.LoadPartitionAsync` or `_storageService.GetPartitionsAsync`.
   - Remove any stream seeking, reading, and deserialization code.
3. **Refactor Write Operations (Split/Merge)**:
   - When a partition exceeds the threshold and needs to split, use the high-level `_storageService.SavePartitionAsync` or equivalent to save the newly created split partitions and update the B-Tree index.
   - Remove any pointer/offset calculation logic; the storage service will manage where objects are written.
4. **Update Unit Tests**:
   - Refactor `PartitionManagerTests` to mock `IPartitionStorageService`.
   - Setup the mocks to return pre-constructed `DataPartition` or `HeaderPartition` objects instead of mocking `MemoryStream` behaviors.
   - Assert that `_storageService.SavePartitionAsync` is called with the expected split payloads during split scenarios.
5. **Update Dependency Injection**:
   - Open `ServiceCollectionExtensions.cs` and ensure `PartitionManager` is registered and resolved correctly alongside the new `IPartitionStorageService`.

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
- `$/Ama.CRDT/Services/Partitioning/PartitionManager.cs` (To completely refactor the implementation)
- `$/Ama.CRDT/Services/Partitioning/IPartitionManager.cs` (To verify if any interface changes are required, though minimal expected)
- `$/Ama.CRDT/Services/Partitioning/IPartitionStorageService.cs` (To strictly adhere to the new contract for saving and loading)
- `$/Ama.CRDT.UnitTests/Services/Partitioning/PartitionManagerTests.cs` (To update the unit tests to mock the new storage service instead of streams)
- `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs` (To ensure proper DI registration of the refactored dependencies)

<!---AI - Stage 2--->
# Changes Done
<!---
Here you add detailed information about all the changes actually done.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
Add all the things that you did in a different way than expected.
--->
- `$/Ama.CRDT/Services/Partitioning/PartitionManager.cs` (Validated that it uses pure `IPartitionStorageService` logic to remove streams and pointers, correctly performing limits checks to split and merge operations)
- `$/Ama.CRDT/Services/Partitioning/IPartitionManager.cs` (Ensured signatures align purely with domain objects and interface models instead of direct byte operations)
- `$/Ama.CRDT/Services/Partitioning/IPartitionStorageService.cs` (Confirmed the contract matches the required execution patterns for `PartitionManager` functionality)
- `$/Ama.CRDT.UnitTests/Services/Partitioning/PartitionManagerTests.cs` (Rewrote from integration tests relying on `InMemoryPartitionStreamProvider` into precise unit tests utilizing `Moq` against `IPartitionStorageService`, correctly verifying storage interactions for Split/Merge behavior over logical partitions)
- `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs` (Ensured `BPlusTreePartitionStorageService` correctly registers as `IPartitionStorageService` allowing `PartitionManager` resolution)

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
Replacing the integration tests inside `PartitionManagerTests` with unit tests utilizing Moq limits confidence on the end-to-end runtime integration behavior. It is recommended to create a separate suite of End-to-End or pure Integration tests testing `PartitionManager` -> `BPlusTreePartitionStorageService` -> `FilePartitionStreamProvider` together, if not already handled inside Showcase tests or higher-level abstractions.

<!---AI - Stage 2--->
## Last notes and implementation details
<!---
Here you add comments about the implementation that didn't fit on the previous section.
--->
Mocking `IAsyncEnumerable` behavior efficiently with `Moq` was achieved natively via a helper `AsAsyncEnumerable` allowing transparent test iterations on mocked partitioned streams. `PartitionManager` has now achieved full separation of concerns from underlying persistence/stream structures.

# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->