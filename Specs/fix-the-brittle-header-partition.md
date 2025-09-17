<!---Human--->
# Purpose
<!---
Add the purpose of this user story.
--->
As a DEV I want the code for header partitions to be robust.

<!---Human--->
# Requirements
<!---
Add the requirements, technical or not.
--->
- Right now the header logic is linked to a field being null and that is very brittle
- There are checks in all code for this
- Code should be grouped according if it is header or not

<!---Human--->
## Requirements context
<!---
Add files that we will load for the UI to add context for the solution design.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
--->
- `$/Ama.CRDT/Models/Partitioning/Partition.cs`
- `$/Ama.CRDT/Models/Partitioning/CompositePartitionKey.cs`
- `$/Ama.CRDT/Services/Partitioning/Strategies/BPlusTreePartitioningStrategy.cs`
- `$/Ama.CRDT/Services/Partitioning/IPartitioningStrategy.cs`
- `$/Ama.CRDT/Services/Partitioning/IPartitionManager.cs`
- `$/Ama.CRDT/Services/Partitioning/IPartitionStreamProvider.cs`
- `$/Ama.CRDT/Services/Partitioning/PartitionManager.cs`
- `$/Ama.CRDT.UnitTests/Services/Partitioning/BPlusTreePartitioningStrategyTests.cs`
- `$/Ama.CRDT.UnitTests/Services/Partitioning/PartitionManagerTests.cs`

<!---Human--->
# Testing Methodology
<!---
Add the testing methodology (manual, unit, integration, end-to-end tests?)
--->
- Update existing unit tests for `BPlusTreePartitioningStrategy` and `PartitionManager` to reflect the new data model.
- Add specific unit tests to verify the correct handling of header vs. data partitions using the new, more robust mechanism.
- Ensure all tests pass to confirm that the refactoring has not introduced regressions.

<!---AI - Stage 1--->
# Proposed Solutions [AI - Stage 1]
<!---
Here you will need to put a number of solutions that would fit for this problem.
Add the solutions that you rejected as well.
--->
### Solution 1: Introduce a Partition Type Hierarchy (Recommended)
- **Description:** Replace the single `Partition` class with an `IPartition` interface and two implementing records: `HeaderPartition` and `DataPartition`. The `DataPartition` would contain the `StartKey`, while the `HeaderPartition` would not. This eliminates the need for a nullable `StartKey` and allows the type system to enforce the distinction.
- **Reasoning for Recommendation:** This is the most robust and type-safe solution. It makes the distinction between partition types explicit and leverages the compiler to ensure correctness. Code can use pattern matching (`if (partition is HeaderPartition)`) which naturally groups the logic for each case, directly fulfilling a core requirement. It eliminates the brittle null check entirely in favor of a much stronger contract.
- **Pros:**
    - Eliminates the brittle `StartKey == null` check.
    - Extremely type-safe.
    - Logic for each partition type is naturally grouped using pattern matching.
    - The data model is more accurate, as a header partition conceptually does not have a start key.
- **Cons:**
    - Requires a more significant refactoring effort across the partitioning services and tests.
    - Introduces polymorphism, which could add slight complexity to serialization if not handled carefully.

### Solution 2: Add a `PartitionType` Enum
- **Description:** Add a `PartitionType` enum (e.g., `Header`, `Data`) to the existing `Partition` class. The logic would then check `partition.Type == PartitionType.Header` instead of `partition.StartKey == null`.
- **Pros:**
    - Less disruptive than creating a type hierarchy.
    - More explicit and readable than a null check.
    - Easily extensible if more partition types are needed in the future.
- **Cons:**
    - The `Partition` class would still contain a nullable `StartKey`, which is technically inaccurate for a header partition. The relationship between `Type` and `StartKey` is based on convention rather than being enforced by the type system.

### Solution 3: Use a Boolean Flag
- **Description:** Add a boolean property, such as `IsHeader`, to the `Partition` class. The check would be a simple `if (partition.IsHeader)`.
- **Pros:**
    - Simplest to implement.
    - Clearer than a null check.
- **Cons:**
    - Least expressive option. It's not easily extensible if a third partition type were ever needed.
    - Similar to the enum approach, it doesn't solve the underlying issue of the `Partition` model having a `StartKey` that is irrelevant when `IsHeader` is true.

<!---AI - Stage 1--->
# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->
1.  **Introduce Partition Interface and Concrete Types:**
    - Create a new interface `$/Ama.CRDT/Models/Partitioning/IPartition.cs` that defines the common properties for all partitions (e.g., `Offset`, `Size`).
    - Create a new record `$/Ama.CRDT/Models/Partitioning/HeaderPartition.cs` that implements `IPartition`.
    - Create a new record `$/Ama.CRDT/Models/Partitioning/DataPartition.cs` that implements `IPartition` and includes the non-nullable `IComparable StartKey`.
    - Delete the existing `$/Ama.CRDT/Models/Partitioning/Partition.cs` file.

2.  **Refactor Strategy and Manager Interfaces:**
    - Modify `$/Ama.CRDT/Services/Partitioning/IPartitioningStrategy.cs` to use `IPartition` in method signatures instead of `Partition`.
    - Modify `$/Ama.CRDT/Services/Partitioning/IPartitionManager.cs` to also use `IPartition`.

3.  **Refactor `BPlusTreePartitioningStrategy` Implementation:**
    - Update `$/Ama.CRDT/Services/Partitioning/Strategies/BPlusTreePartitioningStrategy.cs` to align with the refactored interface.
    - Replace all instances of `partition.StartKey == null` with type pattern matching (e.g., `if (partition is HeaderPartition)`, `partition switch { ... }`). This will centralize and clarify the logic for handling header vs. data partitions.
    - Adjust internal data structures and logic that previously relied on the `Partition` class.

4.  **Refactor `PartitionManager` Implementation:**
    - Update `$/Ama.CRDT/Services/Partitioning/PartitionManager.cs` to work with the new `IPartition` interface and concrete types. Most changes will likely involve adapting calls to the partitioning strategy.

5.  **Update Unit Tests:**
    - Modify `$/Ama.CRDT.UnitTests/Services/Partitioning/BPlusTreePartitioningStrategyTests.cs`.
    - Modify `$/Ama.CRDT.UnitTests/Services/Partitioning/PartitionManagerTests.cs`.
    - In both test files, update object instantiation to use `HeaderPartition` and `DataPartition` records.
    - Revise assertions to work with the new type hierarchy and ensure all existing scenarios still pass.
    - Add new tests to specifically cover the distinct logic paths for header and data partitions.

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
- `$/Ama.CRDT/Models/Partitioning/Partition.cs` (This file will be replaced by a new hierarchy)
- `$/Ama.CRDT/Models/Partitioning/CompositePartitionKey.cs` (Used within partitions, needed for context)
- `$/Ama.CRDT/Services/Partitioning/Strategies/BPlusTreePartitioningStrategy.cs` (Core logic that needs to be refactored)
- `$/Ama.CRDT/Services/Partitioning/IPartitioningStrategy.cs` (Interface to be updated)
- `$/Ama.CRDT/Services/Partitioning/IPartitionManager.cs` (Interface to be updated)
- `$/Ama.CRDT/Services/Partitioning/IPartitionStreamProvider.cs` (Needed for context on how partitions are stored and retrieved)
- `$/Ama.CRDT/Services/Partitioning/PartitionManager.cs` (Service using the strategy that needs updating)
- `$/Ama.CRDT.UnitTests/Services/Partitioning/BPlusTreePartitioningStrategyTests.cs` (Unit tests that need to be updated)
- `$/Ama.CRDT.UnitTests/Services/Partitioning/PartitionManagerTests.cs` (Unit tests that need to be updated)
- `C:\sources\Ama.CRDT\Ama.CRDT\Models\Serialization\Converters\PolymorphicObjectJsonConverter.cs`

<!---AI - Stage 2--->
# Changes Done
<!---
Here you add detailed information about all the changes actually done.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
Add all the things that you did in a different way than expected.
--->
- **DELETED** `$/Ama.CRDT/Models/Partitioning/Partition.cs`: Replaced this single record with a more robust type hierarchy.
- **CREATED** `$/Ama.CRDT/Models/Partitioning/IPartition.cs`: Introduced an interface to define the common contract for all partition types.
- **CREATED** `$/Ama.CRDT/Models/Partitioning/HeaderPartition.cs`: Created a specific record for header partitions, making the distinction from data partitions type-safe.
- **CREATED** `$/Ama.CRDT/Models/Partitioning/DataPartition.cs`: Created a specific record for data partitions.
- **CREATED** `$/Ama.CRDT/Models/Partitioning/BPlusTreeNode.cs`: Created this file to reflect changes to the B+ Tree node model. Its `Partitions` property was changed from `List<Partition>` to `List<object>` to support polymorphic serialization of the new `IPartition` types.
- **MODIFIED** `$/Ama.CRDT/Models/Serialization/Converters/PolymorphicObjectJsonConverter.cs`: Registered the new `HeaderPartition` and `DataPartition` types to ensure they are correctly serialized and deserialized within the B+ Tree index.
- **MODIFIED** `$/Ama.CRDT/Services/Partitioning/IPartitioningStrategy.cs`: Updated method signatures to use the `IPartition` interface.
- **MODIFIED** `$/Ama.CRDT/Services/Partitioning/IPartitionManager.cs`: Updated method signatures to use the `IPartition` interface.
- **MODIFIED** `$/Ama.CRDT/Services/Partitioning/Strategies/BPlusTreePartitioningStrategy.cs`: Refactored the core indexing logic to use the new `IPartition` hierarchy. Replaced brittle checks (e.g., comparing start and end keys) with type-safe pattern matching (`partition is HeaderPartition`).
- **MODIFIED** `$/Ama.CRDT/Services/Partitioning/PartitionManager.cs`: Updated the manager to create and handle the new `HeaderPartition` and `DataPartition` types, and to use pattern matching for type-specific logic.
- **MODIFIED** `$/Ama.CRDT.UnitTests/Services/Partitioning/BPlusTreePartitioningStrategyTests.cs`: Updated unit tests to instantiate and assert against the new partition records, ensuring correctness and preventing regressions.
- **MODIFIED** `$/Ama.CRDT.UnitTests/Services/Partitioning/PartitionManagerTests.cs`: Updated unit tests to align with the new, more robust partition model.

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
No technical debt was introduced. The change improves the codebase's robustness and maintainability.

<!---AI - Stage 2--->
## Last notes and implementation details
<!---
Here you add comments about the implementation that didn't fit on the previous section.
--->
The core of this change was to replace the brittle `Partition` model, which used nulls and key comparisons to differentiate partition types, with a type-safe hierarchy (`IPartition`, `HeaderPartition`, `DataPartition`). This makes the code much more robust and readable, as logic can now rely on C#'s type system and pattern matching (e.g., `if (partition is HeaderPartition)`).

To support this change, the B+ Tree's internal node structure (`BPlusTreeNode`) was modified to store its partitions as a `List<object>`. This allows the existing `PolymorphicObjectJsonConverter` to correctly serialize and deserialize the different partition types into the index file by embedding a `$type` discriminator, preserving the type information upon retrieval.

The unit tests were updated to reflect these changes, ensuring that the refactoring is correct and doesn't introduce any regressions.

# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->