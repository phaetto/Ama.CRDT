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
1. **Comprehensive DI Update + Constructor Refactoring (Recommended)**: Update `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs` to register `IPartitionSerializationService` (mapping to the default implementation) and `IPartitionStorageService` (mapping to `BPlusTreePartitionStorageService`). Update the showcase's `Program.cs` to ensure these new services are resolved correctly and injected. Update all unit tests to mock these new services instead of the raw stream providers. This ensures the decoupling goal of the epic is fully realized across the entire solution.
2. **Minimal DI Update with Default Fallbacks (Rejected)**: Modify `PartitionManager` and `BPlusTreePartitioningStrategy` to instantiate default storage and serialization services internally if they are not provided via DI. This would minimize updates to the showcase and unit tests. However, this is rejected because it tightly couples the core logic to specific implementations, violating the decoupled design principles established in the previous specs.
3. **Legacy Constructor Overloads (Rejected)**: Keep the old constructors in `PartitionManager` and test classes that accept `IPartitionStreamProvider` directly, and internally wrap them with the new storage service. While this provides backward compatibility, it leaves technical debt and fragmentation in the codebase. Since we are in the process of a major refactoring, it is better to cleanly break and update the consumers immediately.

<!---AI - Stage 1--->
# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->
1. **Update DI Registrations**: Modify `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs` to include default `.AddScoped<>()` or `.AddSingleton<>()` registrations for `IPartitionSerializationService` and `IPartitionStorageService`.
2. **Update Showcase Setup**: Modify `$/Ama.CRDT.ShowCase.LargerThanMemory/Program.cs` to ensure the new storage services are registered in the DI container and any direct constructor calls are updated.
3. **Adapt Stream Provider**: Review `$/Ama.CRDT.ShowCase.LargerThanMemory/Services/FileSystemPartitionStreamProvider.cs` to ensure its methods align with what the new `IPartitionStorageService` expects, making any necessary adjustments to stream handling.
4. **Fix PartitionManager Unit Tests**: Update `$/Ama.CRDT.UnitTests/Services/Partitioning/PartitionManagerTests.cs` to inject mocked `IPartitionStorageService` instances rather than directly injecting stream providers into `PartitionManager`.
5. **Fix Strategy Unit Tests**: Update `$/Ama.CRDT.UnitTests/Services/Partitioning/BPlusTreePartitioningStrategyTests.cs` to reflect the new dependency graph and storage abstractions.
6. **Verify Benchmarks**: Check `$/Ama.CRDT.Benchmarks/Program.cs` and `ApplicatorBenchmarks`/`StrategyBenchmarks`. If they manually instantiate `PartitionManager` or partitioning strategies, update their constructors or DI setups.
7. **Run Validations**: Execute `dotnet build` and `dotnet test` to verify all compilation errors are resolved and tests pass. Manually run the Showcase app to confirm E2E functionality.

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
- `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs` (To update DI registration)
- `$/Ama.CRDT.ShowCase.LargerThanMemory/Program.cs` (To update application bootstrap logic)
- `$/Ama.CRDT.ShowCase.LargerThanMemory/Services/FileSystemPartitionStreamProvider.cs` (To adapt the stream provider usage if necessary)
- `$/Ama.CRDT.UnitTests/Services/Partitioning/PartitionManagerTests.cs` (To fix failing unit tests due to updated constructors)
- `$/Ama.CRDT.UnitTests/Services/Partitioning/BPlusTreePartitioningStrategyTests.cs` (To fix failing unit tests due to updated constructors)
- `$/Ama.CRDT.Benchmarks/Program.cs` (To ensure benchmarks setup correctly resolves the new dependencies)

<!---AI - Stage 2--->
# Changes Done
<!---
Here you add detailed information about all the changes actually done.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
Add all the things that you did in a different way than expected.
--->
- `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs` (Registered `IPartitionSerializationService` and `IPartitionStorageService` into the DI container with scope-validated instances.)
- `$/Ama.CRDT.ShowCase.LargerThanMemory/Program.cs` (Updated dependency injection to use the new extensions correctly.)
- `$/Ama.CRDT.ShowCase.LargerThanMemory/Services/FileSystemPartitionStreamProvider.cs` (Simplified strictly to handle stream resolution for headers and properties without leaking partition management specifics.)
- `$/Ama.CRDT.UnitTests/Services/Partitioning/PartitionManagerTests.cs` (Refactored to mock `IPartitionStorageService` instead of `IPartitionStreamProvider`, streamlining the test logic for partition limits and operations.)
- `$/Ama.CRDT.UnitTests/Services/Partitioning/BPlusTreePartitioningStrategyTests.cs` (Adapted constructor usage to accommodate the new `DefaultPartitionSerializationService` dependency internally.)
- `$/Ama.CRDT.Benchmarks/Program.cs` (Verified functionality; no functional changes required for the benchmark runner entry point since benchmarking classes fetch scopes internally.)

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
The DI registration method `AddCrdt()` inside `ServiceCollectionExtensions` is growing quite large. In the future, it might be beneficial to split it into smaller extension methods (e.g., `AddCrdtCore()`, `AddCrdtStrategies()`, `AddCrdtPartitioning()`) to improve readability and allow consumers to opt-in only to the sub-features they need. Additionally, the test-based in-memory partition streams do not enforce tight disposal validation, which is acceptable for unit testing but might leak if used in more extensive integration scenarios.

<!---AI - Stage 2--->
## Last notes and implementation details
<!---
Here you add comments about the implementation that didn't fit on the previous section.
--->
The refactoring successfully abstracts away the low-level stream and B+ Tree operations from the `PartitionManager`, making it much easier to test, configure, and maintain. The unit tests now focus purely on the partition split/merge and data loading logic, relying entirely on the `IPartitionStorageService` mock to handle the mechanics without touching physical bytes or serialization.

# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->