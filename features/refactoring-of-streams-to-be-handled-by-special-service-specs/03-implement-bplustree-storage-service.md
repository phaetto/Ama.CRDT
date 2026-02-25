<!---Human--->
# Purpose
<!---
Add the purpose of this user story.
--->
The purpose of this spec is to implement the newly defined `IPartitionStorageService` using the existing stream mechanisms and the `BPlusTreePartitioningStrategy`. This encapsulates all the messy stream pointer manipulation into a single, cohesive service.

<!---Human--->
# Requirements
<!---
Add the requirements, technical or not.
--->
1. Create `BPlusTreePartitionStorageService` (or similarly named service) implementing `IPartitionStorageService`.
2. Move stream pointer logic, file reading/writing logic, and indexing coordination from `PartitionManager` into this new service.
3. This service should depend on `IPartitionStreamProvider` (to get the underlying streams), `IPartitioningStrategy` (for B+ tree mechanics), and the `IPartitionSerializationService` (from Spec 1).
4. Ensure the service efficiently handles offset management and byte-range calculations previously done in the manager.

<!---Human--->
## Requirements context
<!---
Add files that we will load for the UI to add context for the solution design.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
--->
- `$/features/refactoring-of-streams-to-be-handled-by-special-service-specs/01-create-partition-serialization-service.md` (Provides serialization definitions)
- `$/features/refactoring-of-streams-to-be-handled-by-special-service-specs/02-define-partition-storage-interface.md` (Provides the target interface to implement)
- `$/Ama.CRDT/Services/Partitioning/Strategies/BPlusTreePartitioningStrategy.cs` (Provides index management to wrap)
- `$/Ama.CRDT/Services/Partitioning/PartitionManager.cs` (Contains the stream logic that needs to be extracted)
- `$/Ama.CRDT/Services/Partitioning/IPartitionStreamProvider.cs` (The underlying stream provisioner)

<!---Human--->
# Testing Methodology
<!---
Add the testing methodology (manual, unit, integration, end-to-end tests?)
--->
- Unit tests: Write exhaustive tests for `BPlusTreePartitionStorageService` to ensure offsets, streams, and indexing work together correctly using memory streams. Use `Moq` for serialization and stream providers.

<!---AI - Stage 1--->
# Proposed Solutions [AI - Stage 1]
<!---
Here you will need to put a number of solutions that would fit for this problem.
Add the solutions that you rejected as well.
--->
1. **BPlusTree-Specific Storage Service (Recommended)**: Create a `BPlusTreePartitionStorageService` class that explicitly implements `IPartitionStorageService`. This service will take dependencies on `IPartitioningStrategy` (specifically leveraging its offset mechanics), `IPartitionStreamProvider`, and `IPartitionSerializationService`. It extracts all the low-level stream byte manipulation (seeking, calculating partition byte lengths, appending) directly out of `PartitionManager`. This cleanly separates the physical stream management from the logical partition routing handled by the manager.

2. **Generic Stream Storage Service**: Create a completely generic `StreamPartitionStorageService` that handles stream reading/writing using pure offset inputs from any `IPartitioningStrategy`. While highly flexible, this might require a wider interface for `IPartitioningStrategy` to expose B+Tree specific header synchronizations (`BTreeHeader`) and tree updates, leading to a leaky abstraction. Therefore, it is slightly rejected in favor of the first approach, which accepts coupling to the tree index nature if needed.

3. **Decorator Pattern for Streams**: Wrap the streams provided by `IPartitionStreamProvider` with custom tracking decorators that automatically update the `IPartitioningStrategy` offsets when bytes are written. Rejected as over-engineered; it scatters indexing logic inside stream wrappers and makes resource disposal complex compared to a dedicated cohesive service.

<!---AI - Stage 1--->
# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->
- Create a new file `BPlusTreePartitionStorageService.cs` under `$/Ama.CRDT/Services/Partitioning/`.
- Implement `IPartitionStorageService` methods in the new class (e.g., loading, saving, and updating partitions).
- Extract the file stream pointer logic, length calculation, and seeking operations from `PartitionManager.cs` into `BPlusTreePartitionStorageService.cs`.
- Refactor the extracted methods to use the newly created `IPartitionSerializationService` for writing and reading partition objects to/from the raw streams.
- Refactor `PartitionManager.cs` to take an injected `IPartitionStorageService` instead of directly manipulating streams, removing its dependencies on byte arrays and stream offsets.
- Update `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs` to register `BPlusTreePartitionStorageService` against `IPartitionStorageService`.
- Create a test class `BPlusTreePartitionStorageServiceTests.cs` in `$/Ama.CRDT.UnitTests/Services/Partitioning/` testing stream offsets using `MemoryStream` and `Moq`.
- Update existing `PartitionManagerTests.cs` to mock `IPartitionStorageService` rather than testing stream manipulations directly.

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
- `$/features/refactoring-of-streams-to-be-handled-by-special-service-specs/01-create-partition-serialization-service.md` (To understand the partition serialization interface contract)
- `$/features/refactoring-of-streams-to-be-handled-by-special-service-specs/02-define-partition-storage-interface.md` (To understand the storage service interface contract to implement)
- `$/Ama.CRDT/Services/Partitioning/IPartitionStorageService.cs` (The interface to implement, assumed created in previous spec)
- `$/Ama.CRDT/Services/Partitioning/IPartitionSerializationService.cs` (Dependency for byte conversion, assumed created in previous spec)
- `$/Ama.CRDT/Services/Partitioning/PartitionManager.cs` (To extract stream pointer logic and refactor)
- `$/Ama.CRDT/Services/Partitioning/Strategies/BPlusTreePartitioningStrategy.cs` (To integrate with index updates and offsets)
- `$/Ama.CRDT/Services/Partitioning/IPartitionStreamProvider.cs` (Underlying dependency for streams)
- `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs` (To register the new implementation in DI)
- `$/Ama.CRDT.UnitTests/Services/Partitioning/PartitionManagerTests.cs` (Needs to be updated to mock the new service)
- `$/Ama.CRDT.UnitTests/Services/Partitioning/PartitionStorageServiceContractTests.cs` (To ensure the contract test suite covers our implementation)

<!---AI - Stage 2--->
# Changes Done
<!---
Here you add detailed information about all the changes actually done.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
Add all the things that you did in a different way than expected.
--->
- `$/Ama.CRDT/Services/Partitioning/IPartitionStorageService.cs` (Expanded the interface mapped from previous spec to encompass all internal `IPartitioningStrategy` invocations, ensuring that `PartitionManager` is 100% blind to strategy and stream mechanics).
- `$/Ama.CRDT/Services/Partitioning/BPlusTreePartitionStorageService.cs` (Implemented the expanded storage interface, mapping index method delegations to `IPartitioningStrategy` and taking over data stream offsets calculations).
- `$/Ama.CRDT/Services/Partitioning/PartitionManager.cs` (Refactored to rely exclusively on `IPartitionStorageService`. Fully purged properties matching `IPartitionStreamProvider`, `IPartitioningStrategy`, and `IPartitionSerializationService` from this class).
- `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs` (Registered `BPlusTreePartitionStorageService`).
- `$/Ama.CRDT.UnitTests/Services/Partitioning/BPlusTreePartitionStorageServiceTests.cs` (Added offset tests relying heavily on mocks for serializers and stream providers).
- `$/Ama.CRDT.UnitTests/Services/Partitioning/PartitionManagerTests.cs` (Kept standard integration context as it dynamically resolves the correctly mapped DI storage implementation via `AddCrdt()`).

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
Because `IPartitionStorageService` encapsulates exactly the required interface surface area from `IPartitioningStrategy`, the storage interface essentially mirrors the strategy interface methods 1:1, combining them with stream mechanics. If future partitioning strategies behave dramatically different from a tree index, the storage interface might require more extensive refactoring. However, this is tightly isolated away from core document logic.

<!---AI - Stage 2--->
## Last notes and implementation details
<!---
Here you add comments about the implementation that didn't fit on the previous section.
--->
This stage successfully fulfills the initial goal of abstracting streams completely away from `PartitionManager`. The resulting `PartitionManager` is significantly cleaner and focuses solely on document patching, partition metadata logic, and mapping paths to split points.

# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->