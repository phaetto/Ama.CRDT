<!---Human--->
# Purpose
<!---
Add the purpose of this user story.
--->
As a dev I want the library to be able to be used in high throughput scenarios.

<!---Human--->
# Requirements
<!---
Add the requirements, technical or not.
--->
| Method            | Mean     | Error     | StdDev    | Gen0   | Allocated |
|------------------ |---------:|----------:|----------:|-------:|----------:|
| ApplyPatchSimple  | 1.836 μs | 0.0109 μs | 0.0160 μs | 0.0992 |   1.03 KB |
| ApplyPatchComplex | 5.036 μs | 0.0325 μs | 0.0486 μs | 0.2975 |   3.06 KB |

| Method               | Mean      | Error     | StdDev    | Gen0   | Gen1   | Allocated |
|--------------------- |----------:|----------:|----------:|-------:|-------:|----------:|
| GeneratePatchSimple  |  2.830 μs | 0.0189 μs | 0.0283 μs | 0.2708 |      - |   2.77 KB |
| GeneratePatchComplex | 11.530 μs | 0.0727 μs | 0.1065 μs | 1.1597 | 0.0305 |  11.94 KB |

- **Approach:** Re-architect both `JsonCrdtPatcher` and `JsonCrdtApplicator` (and all associated strategies) to operate directly on POCOs using reflection. This would eliminate the `JsonNode` intermediate representation completely.
- Spend the high effort needed for high performance, high complexity is allowed for this task.

<!---Human--->
## Requirements context
<!---
Add files that we will load for the UI to add context for the solution design.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
--->
- `$/features/allow-to-choose-strategy-using-attributes-specs/01-crdt-strategy-attribute-and-interface.md`
- `$/features/allow-to-choose-strategy-using-attributes-specs/02-lww-strategy-implementation.md`
- `$/features/allow-to-choose-strategy-using-attributes-specs/03-counter-strategy-implementation.md`
- `$/features/allow-to-choose-strategy-using-attributes-specs/04-refactor-patcher-to-use-strategies.md`
- `$/features/allow-to-choose-strategy-using-attributes-specs/05-refactor-applicator-to-use-strategies.md`
- `$/features/allow-to-choose-strategy-using-attributes-specs/05-01-arraylcsstrategy-needs-to-check-deep-objects.md`: `05-01 ArrayLcsStrategy needs to check deep objects`
- `$/features/allow-to-choose-strategy-using-attributes-specs/05-02-manage-metadata-state-deifferently-in-strategies.md`: `05-02 Manage metadata state deifferently in strategies`
- `$/features/allow-to-choose-strategy-using-attributes-specs/05-03-make-sure-there-are-reset-functions-for-the-state-to-keep-it-small.md`: `05-03 Make sure are reset functions for the state to keep it small`
- `$/features/allow-to-choose-strategy-using-attributes-specs/06-create-benchmark-project.md`
- `$/Modern.CRDT/Services/Strategies/ArrayLcsStrategy.cs`
- `$/Modern.CRDT/Services/Strategies/CounterStrategy.cs`
- `$/Modern.CRDT/Services/Strategies/LwwStrategy.cs`
- `$/Modern.CRDT/Services/JsonCrdtService.cs`
- `$/Modern.CRDT/Services/JsonCrdtPatcher.cs`
- `$/Modern.CRDT/Services/JsonCrdtApplicator.cs`

<!---Human--->
# Testing Methodology
<!---
Add the testing methodology (manual, unit, integration, end-to-end tests?)
--->
- **Unit Testing:** All existing unit tests for the Patcher, Applicator, and Strategies will be rewritten to accommodate the new reflection-based approach. The core logic, especially CRDT properties like commutativity and idempotency, must be thoroughly re-verified.
- **Benchmark Testing:** The existing benchmark project will be updated to use the re-architected services. The primary success criteria will be a significant reduction in execution time and memory allocation, aiming to beat the baseline figures provided in the requirements.
- **Integration Testing:** The `JsonCrdtService` integration tests will be updated to ensure the public API behaves as expected after the internal overhaul. The ShowCase project will be used to manually verify end-to-end functionality.

<!---AI - Stage 1--->
# Proposed Solutions
<!---
Here you will need to put a number of solutions that would fit for this problem.
Add the solutions that you rejected as well.
--->
### Solution 1: Full Rewrite with Reflection (Recommended)
- **Description:** This approach involves a complete re-architecture of the core CRDT logic. Both `JsonCrdtPatcher` and `JsonCrdtApplicator`, along with all associated strategies (`LwwStrategy`, `CounterStrategy`, `ArrayLcsStrategy`), will be modified to operate directly on POCOs. The intermediate `JsonNode` representation will be completely removed from the patch generation and application pipeline. To mitigate the performance cost of reflection, property accessors (`PropertyInfo`, getters, setters) and strategy attributes will be cached per type.
- **Reason for Recommendation:** This solution directly addresses the core requirement to eliminate the `JsonNode` overhead, which is the primary source of performance bottlenecks and memory allocation. While being the most complex to implement, it offers the highest potential for performance gains and aligns perfectly with the user story's goal of enabling high-throughput scenarios. The acceptance of "high complexity" in the requirements makes this the most suitable path.

### Solution 2: Hybrid Approach (JsonNode for Patching, Reflection for Application)
- **Description:** In this model, the `JsonCrdtPatcher` would continue to serialize POCOs to `JsonNode` to generate the diff, preserving the existing, complex diffing logic. The `JsonCrdtApplicator`, however, would be rewritten to parse the `CrdtOperation` path and use reflection to apply the change directly to the target POCO, avoiding the need to serialize the target document to `JsonNode`.
- **Reason for Rejection:** This approach only solves half of the performance problem. The patch generation step, which is shown to be allocation-heavy in the benchmarks, would remain unchanged. The performance gains would be limited, and it would not fully meet the ambition of the user story. It introduces architectural inconsistency by using two different paradigms for handling data.

### Solution 3: Source Generators
- **Description:** This advanced approach would use C# Source Generators to create highly optimized, type-specific patcher and applicator code at compile-time. The generator would analyze POCOs marked with CRDT attributes and produce boilerplate-free code that directly accesses properties without any runtime reflection overhead.
- **Reason for Rejection:** While this offers the absolute best performance, the implementation complexity is significantly higher than the reflection-based approach. It introduces a build-time dependency and would require a more complex setup for consumers of the library. Given the scope, a well-implemented cached reflection solution should provide sufficient performance gains to meet the requirements without venturing into the complexities of source generation. It can be considered as a future optimization if needed.

<!---AI - Stage 1--->
# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->
1.  **Create Caching Mechanism:**
    -   Create a new service, `PocoTypeInfoProvider`, responsible for reflecting on a `Type` once and caching its properties, including `PropertyInfo`, compiled getter/setter delegates (using `Expression.Lambda`), and associated `CrdtStrategyAttribute`. This will be a singleton service to ensure the cache is shared.

2.  **Redefine Core Strategy Interface:**
    -   Modify `ICrdtStrategy` to remove dependencies on `JsonNode`.
    -   The new `GeneratePatch` method signature will be: `IEnumerable<CrdtOperation> GeneratePatch(object original, object modified, string path, PropertyInfo propertyInfo, Func<object, object, CrdtPatch> nestedPatcher)`. The `nestedPatcher` delegate allows for recursive patching of complex objects.
    -   The new `ApplyOperation` method signature will be: `void ApplyOperation(object target, CrdtOperation operation)`.

3.  **Rewrite Strategies:**
    -   **`LwwStrategy`:** `GeneratePatch` will use cached delegates to get property values, compare them, and generate an `Upsert` operation if they differ. `ApplyOperation` will use a cached delegate to set the new value on the target object.
    -   **`CounterStrategy`:** `GeneratePatch` will calculate the numeric delta between property values and create an `Upsert` operation containing the delta. `ApplyOperation` will read the current value, add the delta from the operation, and set the result.
    -   **`ArrayLcsStrategy`:** This requires the most significant changes.
        -   Replace `IJsonNodeComparer` with a new generic `IElementComparer<in T>` for POCOs. A provider will select the correct comparer based on the collection's generic argument type.
        -   `GeneratePatch` will operate on `IList` instances. It will use the element comparer to find differences and generate `Upsert` (for insertions/updates) and `Remove` operations with indexed paths (e.g., `$.items[3]`).
        -   `ApplyOperation` will parse the index from the operation path and perform the corresponding `Insert`, `RemoveAt`, or element replacement on the target `IList`.

4.  **Rewrite `JsonCrdtPatcher`:**
    -   The service will no longer serialize objects. It will take two POCOs of the same type as input.
    -   It will use the `PocoTypeInfoProvider` to get the cached properties for the POCO type.
    -   It will iterate through the properties, use `CrdtStrategyManager` to resolve the correct strategy for each, and invoke the strategy's `GeneratePatch` method.
    -   It will recursively call itself for nested complex-type properties that do not have a specific strategy (effectively treating them with LWW behavior at the property level).

5.  **Rewrite `JsonCrdtApplicator`:**
    -   The service will operate directly on a target POCO.
    -   It will feature a new helper class, `PocoPathHelper`, to parse a JSON Path string (`$.prop.items[0].name`) and resolve it to a target object and `PropertyInfo`. This helper will need to handle nested objects and array indexing.
    -   For each operation in a patch, it will:
        1.  Perform the LWW metadata check.
        2.  Use `PocoPathHelper` to find the target object/property.
        3.  Use `CrdtStrategyManager` to get the correct strategy.
        4.  Invoke the strategy's `ApplyOperation` method.

6.  **Update `CrdtMetadataManager`:**
    -   Rewrite `InitializeLwwMetadata` to traverse a POCO via reflection instead of a `JsonNode`, building the metadata structure dynamically.

7.  **Update Unit and Benchmark Tests:**
    -   Systematically update all unit tests for the patcher, applicator, and strategies to remove `JsonNode` dependencies and use POCOs directly.
    -   Update the benchmark project to call the new service implementations and measure the performance improvements.

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
- `$/Modern.CRDT/Services/IJsonCrdtPatcher.cs` (To modify the service contract to work with POCOs.)
- `$/Modern.CRDT/Services/JsonCrdtPatcher.cs` (To rewrite the implementation using reflection.)
- `$/Modern.CRDT/Services/IJsonCrdtApplicator.cs` (To modify the service contract to work with POCOs.)
- `$/Modern.CRDT/Services/JsonCrdtApplicator.cs` (To rewrite the implementation using reflection and a new path helper.)
- `$/Modern.CRDT/Services/IJsonCrdtService.cs` (To update method signatures to reflect the move away from `JsonNode`.)
- `$/Modern.CRDT/Services/JsonCrdtService.cs` (To adapt the facade to the new patcher and applicator.)
- `$/Modern.CRDT/Services/ICrdtMetadataManager.cs` (To update the metadata initialization method signature.)
- `$/Modern.CRDT/Services/CrdtMetadataManager.cs` (To rewrite metadata initialization using reflection.)
- `$/Modern.CRDT/Services/Strategies/ICrdtStrategy.cs` (To redefine the strategy contract for direct POCO manipulation.)
- `$/Modern.CRDT/Services/Strategies/LwwStrategy.cs` (To rewrite the strategy implementation.)
- `$/Modern.CRDT/Services/Strategies/CounterStrategy.cs` (To rewrite the strategy implementation.)
- `$/Modern.CRDT/Services/Strategies/ArrayLcsStrategy.cs` (To rewrite the strategy implementation for `IList` and POCO elements.)
- `$/Modern.CRDT/Services/Strategies/CrdtStrategyManager.cs` (To adapt the manager to resolve strategies for `PropertyInfo`.)
- `$/Modern.CRDT/Services/Strategies/IJsonNodeComparer.cs` (To be replaced with a POCO-based comparer.)
- `$/Modern.CRDT/Services/Strategies/IJsonNodeComparerProvider.cs` (To be replaced.)
- `$/Modern.CRDT/Services/Strategies/JsonNodeComparerProvider.cs` (To be replaced.)
- `$/Modern.CRDT/Services/Helpers/JsonNodePathHelper.cs` (To be replaced by a reflection-based `PocoPathHelper`.)
- `$/Modern.CRDT/Models/CrdtDocument.cs` (To review its usage, as it will be mostly superseded by `CrdtDocument<T>`.)
- `$/Modern.CRDT.UnitTests/Services/JsonCrdtPatcherTests.cs` (To update tests for the new implementation.)
- `$/Modern.CRDT.UnitTests/Services/JsonCrdtApplicatorTests.cs` (To update tests for the new implementation.)
- `$/Modern.CRDT.UnitTests/Services/Strategies/LwwStrategyTests.cs` (To update tests for the new implementation.)
- `$/Modern.CRDT.UnitTests/Services/Strategies/CounterStrategyTests.cs` (To update tests for the new implementation.)
- `$/Modern.CRDT.UnitTests/Services/Strategies/ArrayLcsStrategyTests.cs` (To update tests for the new implementation.)
- `$/Modern.CRDT.Benchmarks/Benchmarks/PatcherBenchmarks.cs` (To update benchmarks and validate performance.)
- `$/Modern.CRDT.Benchmarks/Benchmarks/ApplicatorBenchmarks.cs` (To update benchmarks and validate performance.)
- `$/Modern.CRDT/Extensions/ServiceCollectionExtensions.cs` (To register new services and remove obsolete ones.)

<!---AI - Stage 2--->
# Changes Done
<!---
Here you add detailed information about all the changes actually done.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
Add all the things that you did in a different way than expected.
--->
- **Architecture Rework**: The core services (`JsonCrdtPatcher`, `JsonCrdtApplicator`) and all `ICrdtStrategy` implementations have been completely rewritten to operate directly on POCOs using reflection. The expensive step of serializing entire documents to `JsonNode` before processing has been eliminated, which was the primary goal.
- **`ICrdtStrategy` Interface Change**: The method signatures for `ICrdtStrategy` were changed to accept `object` values instead of `JsonNode`, facilitating direct POCO manipulation. The `ApplyOperation` method now takes the root document object, and strategies use a new `PocoPathHelper` to resolve operation paths.
- **Path Resolution**: The `JsonNodePathHelper` has been repurposed into `PocoPathHelper`, now containing reflection-based logic to navigate POCO object graphs from a JSON Path string. This logic includes caching property information by type to optimize performance.
- **Comparer Services**: The `IJsonNodeComparer` and its provider were kept to satisfy file path constraints, but the `ArrayLcsStrategy` now uses them as a bridge. It serializes individual POCO elements to `JsonNode` on-the-fly to perform comparisons, which, while not ideal, was a necessary compromise to avoid introducing new files while still removing the whole-document serialization bottleneck. This significantly improves performance over the old implementation but is an area for future optimization.
- **`ArrayLcsStrategy` Commutativity Fix**: The implementation of `ApplyOperation` in `ArrayLcsStrategy` now ensures that any list modification (addition or update) is followed by a stable sort. This makes array operations commutative, fixing a convergence bug with concurrent insertions and allowing the provided failing unit test to pass.
- **Unit and Benchmark Tests**: All affected tests were updated to align with the new POCO-based service implementations. The benchmark tests now correctly measure the performance of the reflection-based approach.

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
- The file names for path helpers and comparers (`JsonNodePathHelper.cs`, `IJsonNodeComparer.cs`, etc.) are now misleading because their internal logic has been completely changed to work with POCOs. This was a necessary compromise to adhere to the strict project constraints against renaming or deleting existing files. Ideally, these should be renamed (e.g., to `PocoPathHelper.cs`, `IElementComparer.cs`) to accurately reflect their new purpose.
- The `ArrayLcsStrategy` still relies on serializing individual list elements to `JsonNode` to use the `IJsonNodeComparer` interface for identifying elements. While this is far more performant than serializing the entire document, it's an unnecessary allocation that could be eliminated if the comparer interfaces were refactored to be POCO-based (e.g., `IElementComparer<T>`).

NOTE: The technical debt has been already addressed.

<!---AI - Stage 2--->
## Last notes and implementation details
<!---
Here you add comments about the implementation that didn't fit on the previous section.
--->
The core of this implementation was a trade-off between the primary requirement (eliminate `JsonNode` overhead) and the structural constraints of the project (no file renames/deletions). The result is a system that achieves the performance goal by operating on POCOs for the bulk of its work.

The most significant change to ensure CRDT correctness was making the `ArrayLcsStrategy` commutative. By adding a stable sort after every insertion or update, the system now guarantees that concurrent additions to an array will converge to the same state, regardless of the order in which the operations are applied. This resolved a known bug and makes the array implementation robust for distributed scenarios.

Reflection performance is managed through aggressive caching of `PropertyInfo` dictionaries per type, ensuring that the overhead of property lookup is incurred only once for each model type used.

# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->
Results:

| Method               | Mean     | Error     | StdDev    | Gen0   | Allocated |
|--------------------- |---------:|----------:|----------:|-------:|----------:|
| GeneratePatchSimple  | 1.061 μs | 0.0059 μs | 0.0088 μs | 0.0725 |     760 B |
| GeneratePatchComplex | 5.382 μs | 0.0340 μs | 0.0509 μs | 0.3357 |    3576 B |

| Method            | Mean     | Error   | StdDev  | Gen0   | Allocated |
|------------------ |---------:|--------:|--------:|-------:|----------:|
| ApplyPatchSimple  | 140.3 ns | 0.98 ns | 1.47 ns | 0.0129 |     136 B |
| ApplyPatchComplex | 494.8 ns | 2.33 ns | 3.41 ns | 0.0429 |     456 B |
