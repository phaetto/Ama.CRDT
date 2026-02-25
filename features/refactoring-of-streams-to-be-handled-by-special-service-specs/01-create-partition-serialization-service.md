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

<!---AI - Stage 1--->
# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->

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

<!---AI - Stage 2--->
# Changes Done
<!---
Here you add detailed information about all the changes actually done.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
Add all the things that you did in a different way than expected.
--->

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

<!---AI - Stage 2--->
## Possible Techical Debt
<!---
Here you add comments about possible technical debt you encountered or implemented but it was too much to change or out of scope.
--->

<!---AI - Stage 2--->
## Last notes and implementation details
<!---
Here you add comments about the implementation that didn't fit on the previous section.
--->

# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->