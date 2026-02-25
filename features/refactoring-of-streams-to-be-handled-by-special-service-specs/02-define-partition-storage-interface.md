<!---Human--->
# Purpose
<!---
Add the purpose of this user story.
--->
The purpose of this spec is to define a high-level `IPartitionStorageService` interface. This interface will allow `PartitionManager` to manage strongly-typed partitions without needing to know anything about streams, files, or pointer management.

<!---Human--->
# Requirements
<!---
Add the requirements, technical or not.
--->
1. Create `IPartitionStorageService` interface.
2. The interface should expose strongly-typed methods such as `SavePartitionAsync`, `LoadPartitionAsync`, `GetPartitionsAsync` (returning `IAsyncEnumerable<IPartition>`), and metadata queries.
3. The interface should entirely hide `Stream` usage.
4. Update the high-level architecture documentation if necessary to reflect this new abstraction point.

<!---Human--->
## Requirements context
<!---
Add files that we will load for the UI to add context for the solution design.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
--->
- `$/features/refactoring-of-streams-to-be-handled-by-special-service-specs/01-create-partition-serialization-service.md` (Context for how serialization will be handled under the hood)
- `$/Ama.CRDT/Services/Partitioning/IPartitionManager.cs` (To understand the required operations that the storage interface needs to support)
- `$/Ama.CRDT/Models/Partitioning/IPartition.cs` (The core domain models the storage interface will handle)

<!---Human--->
# Testing Methodology
<!---
Add the testing methodology (manual, unit, integration, end-to-end tests?)
--->
- Unit tests: Not strictly applicable for just an interface definition, but we will create basic mock tests to ensure the interface contract covers all `PartitionManager` needs.

<!---AI - Stage 1--->
# Proposed Solutions [AI - Stage 1]
<!---
Here you will need to put a number of solutions that would fit for this problem.
Add the solutions that you rejected as well.
--->
1. **Dedicated `IPartitionStorageService` (Recommended)**: Define a custom service interface tailored exactly to the needs of the `PartitionManager`. This interface will orchestrate the `IPartitionStreamProvider` and a serialization service internally. It will expose domain-specific methods like `SavePartitionAsync(propertyName, partition)`, `GetPartitionsAsync(propertyName, logicalKey)`, and `GetHeaderPartitionAsync(propertyName)`. 
*Reasoning*: This provides the highest level of cohesion and accurately models the domain language of CRDT partitions. It thoroughly isolates the `PartitionManager` from I/O and serialization concerns, allowing it to focus purely on CRDT logic like partition splitting, merging, and state management.

2. **Generic Repository Pattern (`IRepository<IPartition>`)**: Use a standard repository interface (e.g., `Add`, `Update`, `GetById`, `FindAsync`).
*Reasoning*: While familiar, this generic approach does not cleanly map to the specialized concepts of "Header Partitions" versus "Data Partitions" or the streaming nature of continuous partition loading based on composite logical keys. It would force awkward query specifications onto the implementation. Rejected.

3. **Fat `IPartitionStreamProvider`**: Expand the existing `IPartitionStreamProvider` to not just return `Stream` instances, but to also serialize/deserialize and return strongly-typed `IPartition` objects.
*Reasoning*: This violates the Single Responsibility Principle. The stream provider's job should strictly be about abstracting the storage medium (File System, Blob Storage, Memory) and providing raw streams. Making it handle polymorphic JSON serialization mixes I/O with data translation logic. Rejected.

<!---AI - Stage 1--->
# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->
1. Create a new interface file `$/Ama.CRDT/Services/Partitioning/IPartitionStorageService.cs`.
2. Define the strongly-typed asynchronous contract methods within `IPartitionStorageService`:
   - `Task SavePartitionAsync(string propertyName, IPartition partition, CancellationToken cancellationToken = default);`
   - `Task SaveHeaderPartitionAsync(string propertyName, HeaderPartition headerPartition, CancellationToken cancellationToken = default);`
   - `IAsyncEnumerable<IPartition> GetPartitionsAsync(string propertyName, IComparable? logicalKey = null, CancellationToken cancellationToken = default);`
   - `Task<HeaderPartition?> GetHeaderPartitionAsync(string propertyName, CancellationToken cancellationToken = default);`
3. Create a unit test file `$/Ama.CRDT.UnitTests/Services/Partitioning/PartitionStorageServiceContractTests.cs` to write tests using `Moq` that validate the interface shape against expected operations.
4. Update the high-level architecture documentation in `$/README.md` to mention `IPartitionStorageService` as the new abstraction layer replacing direct stream management in `PartitionManager`.

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
- `$/Ama.CRDT/Services/Partitioning/IPartitionManager.cs` (To understand the required operations that the storage interface needs to support)
- `$/Ama.CRDT/Models/Partitioning/IPartition.cs` (The core domain models the storage interface will handle)
- `$/Ama.CRDT/Models/Partitioning/HeaderPartition.cs` (To ensure header-specific methods are appropriately accommodated)
- `$/Ama.CRDT/Services/Partitioning/PartitionManager.cs` (To analyze the current stream usage that needs to be abstracted into the new service)
- `$/Ama.CRDT/Services/Partitioning/IPartitionStreamProvider.cs` (To understand the layer just beneath the proposed storage service)
- `$/README.md` (To evaluate where architecture documentation updates are necessary)

<!---AI - Stage 2--->
# Changes Done
<!---
Here you add detailed information about all the changes actually done.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
Add all the things that you did in a different way than expected.
--->
- `$/Ama.CRDT/Services/Partitioning/IPartitionStorageService.cs` (Created the new interface to abstract stream usage).
- `$/Ama.CRDT.UnitTests/Services/Partitioning/PartitionStorageServiceContractTests.cs` (Created mock unit tests to verify the interface contract).
- `$/README.md` (Updated high-level architecture documentation).
- Adjusted the proposed interface method signatures. The original proposed signatures in Stage 1 like `SavePartitionAsync(string propertyName, IPartition partition)` lacked the generic `TData` and `CrdtMetadata` required to actually persist content without streams. Therefore, methods like `SavePartitionContentAsync` and `LoadPartitionContentAsync` were added to better encapsulate the serialization and stream offset updates. Additionally, `logicalKey` was added where appropriate (e.g., header partitions) as headers don't strictly relate to a property name.

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
The `IPartitionStorageService` interface introduces a large surface area. Future refactoring might separate the index querying methods (`GetPartitionsAsync`) from the pure data storage methods (`LoadPartitionContentAsync`), but keeping them unified simplifies the `PartitionManager` dependencies for now.

<!---AI - Stage 2--->
## Last notes and implementation details
<!---
Here you add comments about the implementation that didn't fit on the previous section.
--->
The new `IPartitionStorageService` interface successfully hides `Stream` usage. Once implemented, it will allow `PartitionManager` to be completely unaware of `IPartitionStreamProvider` and `IPartitionSerializationService`, focusing purely on CRDT logic like splitting and merging.

# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->