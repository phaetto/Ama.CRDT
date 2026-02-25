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