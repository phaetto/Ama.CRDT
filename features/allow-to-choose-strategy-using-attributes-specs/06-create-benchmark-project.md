<!---Human--->
# Purpose
To create a dedicated benchmark project to measure and monitor the performance of the CRDT library. This is crucial to ensure that the new reflection-based strategy pattern does not introduce significant overhead and that the core operations remain highly performant.

<!---Human--->
# Requirements
- Create a new .NET Console Application project named `Modern.CRDT.Benchmarks` within the solution.
- Add the `BenchmarkDotNet` NuGet package to this project.
- Implement benchmark classes that cover the key public APIs:
    - Patch Generation: `JsonCrdtPatcher.GeneratePatch<T>`
    - Patch Application: `JsonCrdtApplicator.ApplyPatch<T>`
- The benchmarks should include scenarios with:
    - Simple POCOs with a few properties.
    - Complex, nested POCOs to test recursion and path generation performance.
    - POCOs using a mix of default (LWW) and custom (Counter) strategies.
- The results should be used to identify any performance bottlenecks in the reflection or strategy resolution logic.

<!---Human--->
## Requirements context
- `$/features/allow-to-choose-strategy-using-attributes-specs/01-crdt-strategy-attribute-and-interface.md`
- `$/features/allow-to-choose-strategy-using-attributes-specs/02-lww-strategy-implementation.md`
- `$/features/allow-to-choose-strategy-using-attributes-specs/03-counter-strategy-implementation.md`
- `$/features/allow-to-choose-strategy-using-attributes-specs/04-refactor-patcher-to-use-strategies.md`
- `$/features/allow-to-choose-strategy-using-attributes-specs/05-refactor-applicator-to-use-strategies.md`
- `$/features/allow-to-choose-strategy-using-attributes-specs/05-01-arraylcsstrategy-needs-to-check-deep-objects.md`: `05-01 ArrayLcsStrategy needs to check deep objects`
- `$/features/allow-to-choose-strategy-using-attributes-specs/05-02-manage-metadata-state-deifferently-in-strategies.md`: `05-02 Manage metadata state deifferently in strategies`
- `$/features/allow-to-choose-strategy-using-attributes-specs/05-03-make-sure-there-are-reset-functions-for-the-state-to-keep-it-small.md`: `05-03 Make sure there are reset functions for the state to keep it small`

<!---Human--->
# Testing Methodology
The testing methodology is the benchmark process itself. By running the benchmarks using `BenchmarkDotNet`, we will get detailed performance statistics (mean execution time, allocations, etc.) for the core CRDT operations. The results will be analyzed to validate performance goals. This project will not have traditional unit tests.

<!---AI - Stage 1--->
# Proposed Solutions
<!---
Here you will need to put a number of solutions that would fit for this problem.
Add the solutions that you rejected as well.
--->
1.  **Single Benchmark Class:** Create one large benchmark class containing methods for all scenarios (patching, applying, simple POCOs, complex POCOs).
    *   **Pros:** Quickest to implement; all logic is consolidated in one file.
    *   **Cons:** Becomes difficult to manage, read, and run specific subsets of benchmarks as more scenarios are added. The setup method could become overly complex.
    *   **Decision:** Rejected due to poor scalability and maintainability.

2.  **Separate Classes per Scenario:** Create benchmark classes based on the data model's complexity (e.g., `SimplePocoBenchmarks`, `ComplexPocoBenchmarks`). Each class would benchmark both patch generation and application for that specific data model.
    *   **Pros:** Good for directly comparing patch vs. apply performance for the same data model.
    *   **Cons:** Can lead to code duplication in setup logic for patcher and applicator. It's less aligned with the goal of testing specific API endpoints.
    *   **Decision:** Rejected as it focuses more on the data than the API being tested.

3.  **Separate Classes per API (Recommended):** Create distinct benchmark classes for each core API (`PatcherBenchmarks` and `ApplicatorBenchmarks`). Each class will contain benchmarks for different scenarios (simple, complex, etc.).
    *   **Pros:** Provides a clean separation of concerns, making the code organized and easy to understand. It allows for focused performance testing of each public API independently. It's easy to add new scenarios (e.g., a new POCO type) to both API benchmarks.
    *   **Cons:** Requires slight duplication of data model setup, but this can be managed with shared helper methods or base classes.
    *   **Decision:** Recommended. This approach best aligns with the requirements to measure the performance of `JsonCrdtPatcher` and `JsonCrdtApplicator` distinctly.

<!---AI - Stage 1--->
# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->
1.  **Project Setup:**
    *   Create a new .NET 9 Console Application project named `Modern.CRDT.Benchmarks`.
    *   Add a project reference from `Modern.CRDT.Benchmarks` to the `Modern.CRDT` project.
    *   Add the `BenchmarkDotNet` NuGet package to the `Modern.CRDT.Benchmarks` project.
    *   Add the new project to the `Modern.CRDT.sln` solution file.

2.  **Benchmark Models:**
    *   Create a `Models` folder within `Modern.CRDT.Benchmarks`.
    *   Create `SimplePoco.cs`: This will define a basic class with a few properties using `LwwStrategy` (default) and one property using `CrdtCounterAttribute`.
    *   Create `ComplexPoco.cs`: This will define a class with nested objects and arrays of objects to test recursive performance. It will also use a mix of strategies.

3.  **Patcher Benchmark Implementation:**
    *   Create the `Benchmarks/PatcherBenchmarks.cs` file.
    *   Annotate the class with `[MemoryDiagnoser]` to track allocations.
    *   Implement a `[GlobalSetup]` method to:
        *   Configure a `ServiceProvider` using `ServiceCollectionExtensions` from the `Modern.CRDT` library.
        *   Resolve an instance of `IJsonCrdtPatcher`.
        *   Create "before" and "after" instances of `SimplePoco` and `ComplexPoco` to generate diffs from.
    *   Create two `[Benchmark]` methods:
        *   `GeneratePatchSimple()`: Calls `_patcher.GeneratePatch` with the `SimplePoco` instances.
        *   `GeneratePatchComplex()`: Calls `_patcher.GeneratePatch` with the `ComplexPoco` instances.

4.  **Applicator Benchmark Implementation:**
    *   Create the `Benchmarks/ApplicatorBenchmarks.cs` file.
    *   Annotate the class with `[MemoryDiagnoser]`.
    *   Implement a `[GlobalSetup]` method to:
        *   Configure a `ServiceProvider`.
        *   Resolve an instance of `IJsonCrdtApplicator`.
        *   Pre-generate patches for both simple and complex scenarios using the patcher logic from the previous step.
        *   Initialize a base `CrdtDocument<T>` and `CrdtMetadata` to apply the patch to.
    *   Create two `[Benchmark]` methods:
        *   `ApplyPatchSimple()`: Calls `_applicator.ApplyPatch` with the simple POCO document and pre-generated patch.
        *   `ApplyPatchComplex()`: Calls `_applicator.ApplyPatch` with the complex POCO document and pre-generated patch.

5.  **Program Entry Point:**
    *   Modify `Modern.CRDT.Benchmarks/Program.cs` to use `BenchmarkSwitcher` to run the implemented benchmarks.

6.  **Update Solution Files:**
    *   Add the new project to `Modern.CRDT.sln`.
    *   Create and/or update `FilesDescription.md` with descriptions of the new benchmark files.

<!---AI - Stage 1--->
# Proposed Files Needed
<!---
Here you need to list the files you need to load in order to get the correct context for your solution to build and test.
Put in this list only the exising files that need to be modified/loaded. Not the new ones that need to be created.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
With each file in one line.
Remember to ask to load any unit tests if they are related to any files you will want tochange.
--->
	- `$/Modern.CRDT.sln` (To add the new benchmark project to the solution)
	- `$/Modern.CRDT/Modern.CRDT.csproj` (To reference it from the new benchmark project)
	- `$/Modern.CRDT/Extensions/ServiceCollectionExtensions.cs` (To correctly set up dependency injection in the benchmark setup)
	- `$/Modern.CRDT/Services/IJsonCrdtPatcher.cs` (To use the patcher service in benchmarks)
	- `$/Modern.CRDT/Services/IJsonCrdtApplicator.cs` (To use the applicator service in benchmarks)
	- `$/Modern.CRDT/Attributes/CrdtCounterAttribute.cs` (To define benchmark data models)
	- `$/Modern.CRDT/Attributes/LwwStrategyAttribute.cs` (To define benchmark data models)
	- `$/FilesDescription.md` (To update with the new files created)

<!---AI - Stage 2--->
# Changes Done
- `$/Modern.CRDT.Benchmarks/Modern.CRDT.Benchmarks.csproj`: Created the new .NET 9 console project for benchmarks and added the `BenchmarkDotNet` package.
- `$/Modern.CRDT.Benchmarks/Program.cs`: Implemented the entry point using `BenchmarkSwitcher` to run all benchmarks in the assembly.
- `$/Modern.CRDT.Benchmarks/Models/SimplePoco.cs`: Added a simple POCO with `LwwStrategy` (default) and `CrdtCounter` properties for basic benchmarks.
- `$/Modern.CRDT.Benchmarks/Models/ComplexPoco.cs`: Added a complex POCO with nested objects, lists, and mixed strategies to benchmark recursive performance.
- `$/Modern.CRDT.Benchmarks/Benchmarks/PatcherBenchmarks.cs`: Implemented benchmarks for `IJsonCrdtPatcher.GeneratePatch` covering both simple and complex models. The setup initializes two versions of each model to generate a diff.
- `$/Modern.CRDT.Benchmarks/Benchmarks/ApplicatorBenchmarks.cs`: Implemented benchmarks for `IJsonCrdtApplicator.ApplyPatch` covering both simple and complex models. The setup pre-generates the necessary patches to isolate the performance of the application logic.
- `$/Modern.CRDT.sln`: Updated the solution file to include the new `Modern.CRDT.Benchmarks` project.
- `$/FilesDescription.md`: Updated to include descriptions for all the newly created files in the benchmark project.
<!---AI - Stage 2--->
# Manual Changes Needed
No manual changes needed to be applied.
<!---AI - Stage 2--->
## Possible Techical Debt
The benchmark setup logic within `PatcherBenchmarks` and `ApplicatorBenchmarks` has some duplication in creating the data models and service provider. This could be refactored into a shared base class or helper methods if more benchmark classes are added in the future, but for two classes, it remains clear and manageable.
<!---AI - Stage 2--->
## Last notes and implementation details
The benchmarks are designed to measure the core, "hot path" operations of the library: patch generation and patch application.
- The `[GlobalSetup]` attribute is used to ensure that all test data preparation and service initialization happens once before the benchmarks run, so it's not included in the measurement.
- `[MemoryDiagnoser]` is added to both benchmark classes to track memory allocations, which is a key performance indicator.
- The setup code correctly uses the `ICrdtMetadataManager` to create and update metadata for the test documents. This ensures the benchmark accurately reflects a real-world usage scenario where timestamps and version vectors are managed.
- The chosen structure of separating benchmarks by API (`PatcherBenchmarks`, `ApplicatorBenchmarks`) provides a clear and focused analysis of each component's performance.

# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->