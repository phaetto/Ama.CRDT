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