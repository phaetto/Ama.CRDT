<!---Human--->
# Purpose
<!---
Add the purpose of this user story.
--->
The purpose of this spec is to propagate the structural DI (Dependency Injection) changes to the wider solution. Since we introduced new services for storage and serialization, the dependency graphs in unit tests, DI extension methods, and the `LargerThanMemory` showcase need to be updated.

<!---Human--->
# Requirements
<!---
Add the requirements, technical or not.
--->
1. Update `ServiceCollectionExtensions.cs` to correctly register `IPartitionSerializationService`, `IPartitionStorageService`, and the B+ Tree implementations.
2. Update `Ama.CRDT.ShowCase.LargerThanMemory/Program.cs` and `FileSystemPartitionStreamProvider.cs` setup to align with the newly decoupled storage flow.
3. Fix any broken unit tests across the suite that were manually instantiating `PartitionManager` with stream providers.
4. Ensure the application benchmarks (`Ama.CRDT.Benchmarks/Program.cs`) remain functional if they touched partition logic.

<!---Human--->
## Requirements context
<!---
Add files that we will load for the UI to add context for the solution design.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
--->
- `$/features/refactoring-of-streams-to-be-handled-by-special-service-specs/01-create-partition-serialization-service.md` (Context)
- `$/features/refactoring-of-streams-to-be-handled-by-special-service-specs/02-define-partition-storage-interface.md` (Context)
- `$/features/refactoring-of-streams-to-be-handled-by-special-service-specs/03-implement-bplustree-storage-service.md` (Context)
- `$/features/refactoring-of-streams-to-be-handled-by-special-service-specs/04-refactor-partition-manager.md` (Context)
- `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs` (To update DI registration)
- `$/Ama.CRDT.ShowCase.LargerThanMemory/Program.cs` (To update application bootstrap logic)
- `$/Ama.CRDT.UnitTests/Services/Partitioning/PartitionManagerTests.cs` (To fix failing unit tests)

<!---Human--->
# Testing Methodology
<!---
Add the testing methodology (manual, unit, integration, end-to-end tests?)
--->
- End-to-end testing: Run the `LargerThanMemory` showcase app manually to verify data generation and loading works without exception.
- Unit tests: Run the entire XUnit test suite to ensure all `Ama.CRDT.UnitTests` pass successfully after the DI adjustments.

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