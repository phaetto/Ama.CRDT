<!---Human--->
# Purpose
<!---
Add the purpose of this user story.
--->
The purpose of this spec is to extract and consolidate all partition and index serialization logic into a dedicated, reusable `IPartitionSerializationService`. This is the first step in decoupling stream and storage management from the `PartitionManager` and `BPlusTreePartitioningStrategy`.

<!---Human--->
# Requirements
<!---
Add the requirements, technical or not.
--->
1. Create a unified interface `IPartitionSerializationService` (or expand existing ones) that handles serializing/deserializing strongly-typed objects like `DataPartition`, `HeaderPartition`, and B+ Tree structures (`BPlusTreeNode`, `BTreeHeader`).
2. Extract the existing inline serialization logic found in `PartitionManager` and `BPlusTreePartitioningStrategy` into default implementations of this new service.
3. Ensure the service works directly with streams, bytes, or JSON based on the existing mechanisms but abstracts the act of serialization from the caller.
4. Try to reuse the existing `IndexDefaultSerializationHelper` where appropriate, possibly renaming or wrapping it to fit the broader serialization service scope.
5. Register the new service in the dependency injection configuration.

<!---Human--->
## Requirements context
<!---
Add files that we will load for the UI to add context for the solution design.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
--->
- `$/Ama.CRDT/Services/Partitioning/Serialization/IIndexSerializationHelper.cs` (Current interface for index serialization to be expanded or integrated)
- `$/Ama.CRDT/Services/Partitioning/Serialization/IndexDefaultSerializationHelper.cs` (Current implementation of index serialization)
- `$/Ama.CRDT/Services/Partitioning/PartitionManager.cs` (Contains inline serialization logic that needs to be extracted)
- `$/Ama.CRDT/Services/Partitioning/Strategies/BPlusTreePartitioningStrategy.cs` (Contains inline serialization logic that needs to be extracted)
- `$/Ama.CRDT/Models/Partitioning/DataPartition.cs` (Model that needs serialization)
- `$/Ama.CRDT/Models/Partitioning/HeaderPartition.cs` (Model that needs serialization)

<!---Human--->
# Testing Methodology
<!---
Add the testing methodology (manual, unit, integration, end-to-end tests?)
--->
- Unit tests: Create new unit tests for the default implementation of `IPartitionSerializationService` to guarantee it can accurately serialize and deserialize all required partition and index types using mock streams.

<!---AI - Stage 1--->
# Proposed Solutions [AI - Stage 1]
<!---
Here you will need to put a number of solutions that would fit for this problem.
Add the solutions that you rejected as well.
--->
**Approach 1: Expand and rename existing `IIndexSerializationHelper` (Recommended)**
Rename `IIndexSerializationHelper` to `IPartitionSerializationService` and rename its default implementation to `DefaultPartitionSerializationService`. We then add new methods to this interface for serializing and deserializing `IPartition` (which covers both `DataPartition` and `HeaderPartition`). 
*Reasoning:* This is the cleanest approach. Both index components (B+ Tree nodes) and partition objects are inherently tied to how we persist the CRDT. Having a single unified serialization service simplifies DI, reduces service fragmentation, and directly meets requirement #4 by reusing and expanding the existing helper.

**Approach 2: Create a new `IPartitionSerializationService` that composes `IIndexSerializationHelper`**
Keep `IIndexSerializationHelper` strictly for B+ Tree components. Create a separate `IPartitionSerializationService` for `DataPartition` and `HeaderPartition` that internally injects and relies on `IIndexSerializationHelper` for shared JSON context, or vice-versa.
*Reasoning (Rejected):* This creates unnecessary layers of abstraction. Because `IndexDefaultSerializationHelper` and the inline partition serialization both use `System.Text.Json` heavily, separating them into two services just adds DI overhead without a real separation of concerns.

**Approach 3: Create a fully generic `ISerializationService<T>`**
Abstract away partitions entirely and create a generic service that serializes any `<T>` to/from a stream using the `CrdtJsonContext`.
*Reasoning (Rejected):* While highly reusable, it strips away the explicit domain intent. The requirements explicitly call for handling strongly-typed objects like `DataPartition` and `BPlusTreeNode`. A generic service might also make it harder to inject specific optimizations for how nodes vs partitions are streamed in the future.

<!---AI - Stage 1--->
# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->
1. **Rename and Update Interfaces/Implementations:**
   - Rename `IIndexSerializationHelper.cs` to `IPartitionSerializationService.cs`. Update the interface name and add methods: `SerializePartitionAsync`, `DeserializePartitionAsync`.
   - Rename `IndexDefaultSerializationHelper.cs` to `DefaultPartitionSerializationService.cs`. Update the class name and implement the new interface methods.
2. **Consolidate Serialization Logic:**
   - Move the `JsonSerializer.SerializeAsync` and `JsonSerializer.DeserializeAsync` logic currently residing inline within `PartitionManager.cs` (for `DataPartition` and `HeaderPartition`) into the new methods in `DefaultPartitionSerializationService`. Ensure it utilizes the centralized `CrdtJsonContext.Default.Options`.
3. **Refactor Consumers:**
   - Update `PartitionManager.cs` to inject `IPartitionSerializationService` instead of performing inline JSON serialization. Replace existing stream serialization calls with the new service methods.
   - Update `BPlusTreePartitioningStrategy.cs` to inject `IPartitionSerializationService` instead of `IIndexSerializationHelper`.
4. **Update Dependency Injection:**
   - In `ServiceCollectionExtensions.cs`, replace the registration of `IIndexSerializationHelper` with `IPartitionSerializationService` mapping to `DefaultPartitionSerializationService`.
5. **Update Unit Tests:**
   - Rename `IndexDefaultSerializationHelperTests.cs` to `DefaultPartitionSerializationServiceTests.cs`. 
   - Add new tests in this file to verify the successful serialization and deserialization of `DataPartition` and `HeaderPartition` to/from a `MemoryStream`.
   - Update `PartitionManagerTests.cs` and `BPlusTreePartitioningStrategyTests.cs` to mock `IPartitionSerializationService` instead of the old interfaces/inline expectations.

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
- `$/Ama.CRDT/Services/Partitioning/Serialization/IIndexSerializationHelper.cs` (To rename to IPartitionSerializationService and add partition methods)
- `$/Ama.CRDT/Services/Partitioning/Serialization/IndexDefaultSerializationHelper.cs` (To rename to DefaultPartitionSerializationService and implement new methods)
- `$/Ama.CRDT/Services/Partitioning/PartitionManager.cs` (To remove inline serialization and inject the new service)
- `$/Ama.CRDT/Services/Partitioning/Strategies/BPlusTreePartitioningStrategy.cs` (To update injected interface from IIndexSerializationHelper to IPartitionSerializationService)
- `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs` (To update the DI registrations for the renamed serialization service)
- `$/Ama.CRDT.UnitTests/Services/Partitioning/Serialization/IndexDefaultSerializationHelperTests.cs` (To rename and add unit tests for partition serialization)
- `$/Ama.CRDT.UnitTests/Services/Partitioning/PartitionManagerTests.cs` (To update mocks from inline JSON/setup to IPartitionSerializationService)
- `$/Ama.CRDT.UnitTests/Services/Partitioning/BPlusTreePartitioningStrategyTests.cs` (To update mocks to IPartitionSerializationService)

<!---AI - Stage 2--->
# Changes Done
<!---
Here you add detailed information about all the changes actually done.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
Add all the things that you did in a different way than expected.
--->
- `$/Ama.CRDT/Services/Partitioning/Serialization/IPartitionSerializationService.cs` (Created interface from `IIndexSerializationHelper.cs`, adding `SerializeObjectAsync`, `DeserializeObjectAsync`, and `CloneObject` to fully abstract away JSON serialization from streams.)
- `$/Ama.CRDT/Services/Partitioning/Serialization/DefaultPartitionSerializationService.cs` (Created from `IndexDefaultSerializationHelper.cs` implementing the new unified interface.)
- `$/Ama.CRDT/Services/Partitioning/PartitionManager.cs` (Injected the new `IPartitionSerializationService` to handle `T` document, `CrdtMetadata`, and cloning serialization operations, completely removing `JsonSerializer` dependencies.)
- `$/Ama.CRDT/Services/Partitioning/Strategies/BPlusTreePartitioningStrategy.cs` (Updated to inject and use the new `IPartitionSerializationService` for node operations.)
- `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs` (Updated the DI registrations to use `DefaultPartitionSerializationService` mapped to `IPartitionSerializationService`.)
- `$/Ama.CRDT.UnitTests/Services/Partitioning/Serialization/DefaultPartitionSerializationServiceTests.cs` (Created from `IndexDefaultSerializationHelperTests.cs` and added generic serialization/cloning tests.)
- `$/Ama.CRDT.UnitTests/Services/Partitioning/PartitionManagerTests.cs` (Verified tests run properly with the updated DI setup and new injected service.)
- `$/Ama.CRDT.UnitTests/Services/Partitioning/BPlusTreePartitioningStrategyTests.cs` (Updated class instantiation to pass the `DefaultPartitionSerializationService`.)

*Deviation from Proposed Techical Steps*: In Stage 1, it was assumed `PartitionManager` explicitly serialized `DataPartition` objects. In reality, `DataPartition` elements are serialized by `BPlusTreePartitioningStrategy` as a property of `BPlusTreeNode`. `PartitionManager` serializes the *underlying data payload* (`T` and `CrdtMetadata`). Therefore, the interface methods were generalized to `SerializeObjectAsync` and `DeserializeObjectAsync<T>`, plus a `CloneObject<T>` method to handle the initialization clone seamlessly without bleeding JSON contexts back into the Manager.

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
Because files were virtually renamed by creating new ones and abandoning the old ones in this output context, please ensure that you explicitly delete `IIndexSerializationHelper.cs`, `IndexDefaultSerializationHelper.cs`, and `IndexDefaultSerializationHelperTests.cs` in your local environment, replacing them with the new files provided.

<!---AI - Stage 2--->
## Possible Techical Debt
<!---
Here you add comments about possible technical debt you encountered or implemented but it was too much to change or out of scope.
--->
Currently, `CloneObject<T>` processes cloning fully in memory using the synchronization-over-asynchronous equivalents internally (such as byte array allocations). For massive memory setups, this might cause LOH (Large Object Heap) allocations, though it mirrors the previously existing inline serialization logic.

<!---AI - Stage 2--->
## Last notes and implementation details
<!---
Here you add comments about the implementation that didn't fit on the previous section.
--->
The refactoring correctly establishes the serialization layer as a boundary. Any persistence or streaming backend now interfaces via `IPartitionSerializationService`, meaning all custom serialization options (`PolymorphicObjectJsonConverter`, `CrdtJsonContext`) are completely isolated away from the managers and strategies.

# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->