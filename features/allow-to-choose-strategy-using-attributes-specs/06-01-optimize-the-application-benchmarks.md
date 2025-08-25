<!---Human--->
# Purpose
<!---
Add the purpose of this user story.
--->
Optimize the throughput of the patcher and applicator services.

<!---Human--->
# Requirements
<!---
Add the requirements, technical or not.
--->

| Method               | Mean      | Error     | StdDev    | Gen0   | Gen1   | Allocated |
|--------------------- |----------:|----------:|----------:|-------:|-------:|----------:|
| GeneratePatchSimple  |  5.765 μs | 0.0413 μs | 0.0593 μs | 0.7019 | 0.0076 |   7.23 KB |
| GeneratePatchComplex | 21.222 μs | 0.1192 μs | 0.1632 μs | 2.7466 | 0.0916 |  28.08 KB |

| Method            | Mean     | Error     | StdDev    | Gen0   | Allocated |
|------------------ |---------:|----------:|----------:|-------:|----------:|
| ApplyPatchSimple  | 1.828 μs | 0.0084 μs | 0.0126 μs | 0.0992 |   1.03 KB |
| ApplyPatchComplex | 5.039 μs | 0.0360 μs | 0.0528 μs | 0.2975 |   3.06 KB |

## Analysis of Benchmark Results
The benchmark results look quite reasonable for a CRDT implementation that operates on generic POCOs using serialization and reflection.
-   **Patcher (`GeneratePatch`)**: The performance scales as expected. The "Complex" model takes about 3.7 times longer and allocates about 3.9 times more memory than the "Simple" one. This suggests a relatively linear scaling with object complexity, which is a good sign. The main contributors to this cost are likely the JSON serialization of two objects and the recursive comparison.
-   **Applicator (`ApplyPatch`)**: The applicator is significantly faster than the patcher, which is also expected. Applying a targeted set of operations is inherently cheaper than diffing two entire object trees. The allocations are much lower, which is excellent.
While these numbers are good, the memory allocations, especially for the patcher (7-28 KB per operation), indicate a potential area for improvement if you need to run these operations at a very high frequency.

## Potential Improvements and Code Review
Both services share a common architectural pattern that is the primary source of performance overhead: **the reliance on an intermediate `JsonNode` representation.**
**1. `JsonCrdtPatcher` - Avoiding Metadata Tree Reconstruction**
The most significant and easily achievable optimization in the `JsonCrdtPatcher` is within the `BuildLwwMetadataTree` method.
-   **Current Approach**: On every call to `GeneratePatch`, you are converting a flat dictionary of LWW metadata (`IDictionary<string, ICrdtTimestamp>`) into a full `JsonObject` tree. This involves string parsing, object creation, and complex branching logic, which is computationally expensive and memory-intensive. This is done for both the `from` and `to` documents.
-   **Suggested Improvement**: You can completely eliminate the `BuildLwwMetadataTree` method. Since your recursive `DifferentiateObject` method already knows the exact JSON path (`currentPath`) of the property it's inspecting, it can perform a direct key lookup in the original flat `Lww` dictionary. For example, instead of getting `fromMetaValue` from a tree, you would get the timestamp with `from.Metadata.Lww.TryGetValue(currentPath, out var fromTimestamp)`. This change would drastically reduce allocations and CPU time during patch generation.
**2. `JsonCrdtApplicator` & `JsonCrdtPatcher` - Operating Directly on POCOs**
A more fundamental, but also more complex, change would be to remove the dependency on `JsonNode` altogether.
-   **Current Approach**:
    1.  `POCO` -> `JsonNode` (Serialization)
    2.  Process the `JsonNode` (Diffing or Patching)
    3.  `JsonNode` -> `POCO` (Deserialization, in the applicator)
-   **Problem**: This round-tripping is the main source of memory allocation and processing time. You are creating a complete in-memory tree representation of your data just to work with it.
-   **Suggested Improvement**: Refactor the services and strategies to operate directly on the POCOs using reflection.
    -   In the **Applicator**, the `FindPropertyFromPath` method already does the hard work of resolving a JSON path to a `PropertyInfo`. Instead of passing the `dataNode` to the strategy, you could pass the root `document` object. The strategy would then use reflection (`PropertyInfo.GetValue`/`SetValue`) to navigate the object graph and apply the change directly to the target property. This would eliminate both the initial `SerializeToNode` and the final `Deserialize<T>` calls, leading to a massive performance gain.
    -   In the **Patcher**, a similar approach could be taken where `DifferentiateObject` recursively compares property values directly from the `from.Data` and `to.Data` objects using reflection, rather than comparing `JsonNode` values.
**3. Minor Optimizations (Micro-Optimizations)**
-   **Reflection Caching**: The `JsonCrdtApplicator` already has an excellent property cache (`PropertyCache`). You could introduce a similar cache in the `JsonCrdtPatcher` for the results of `type.GetProperties(...)` if `GeneratePatch` is called frequently for the same types.
-   **State Check Logic**: In `ApplyOperationWithStateCheck`, the comment `// TODO: Should this only be special case for (strategy is not LwwStrategy)?` highlights a good point. For LWW, the timestamp check (`operation.Timestamp.CompareTo(lwwTs) > 0`) is the primary guard. For other types like Counters, any operation not in `SeenExceptions` or covered by the version vector is valid. Adding all applied operations to `SeenExceptions` is a safe, general-purpose approach to handle out-of-order delivery, so the current logic is sound. It ensures that if a patch is delivered twice, its operations are only applied once.

## Summary
The current implementation is functionally robust and well-structured. The performance is acceptable for many use cases. However, if you need to optimize for high-throughput scenarios, the most impactful changes would be:
1.  **(High Impact, Low Effort)**: In `JsonCrdtPatcher`, remove `BuildLwwMetadataTree` and use direct lookups on the flat LWW metadata dictionary.
2.  **(Very High Impact, High Effort)**: Rework both services to operate directly on POCOs via reflection, completely avoiding the expensive serialization/deserialization to and from `JsonNode`.

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
- `$/features/allow-to-choose-strategy-using-attributes-specs/05-03-make-sure-there-are-reset-functions-for-the-state-to-keep-it-small.md`: `05-03 Make sure there are reset functions for the state to keep it small`
- `C:\sources\Modern.CRDT\features\allow-to-choose-strategy-using-attributes-specs\06-create-benchmark-project.md`
- `C:\sources\Modern.CRDT\Modern.CRDT\Services\Strategies\ArrayLcsStrategy.cs`
- `C:\sources\Modern.CRDT\Modern.CRDT\Services\Strategies\CounterStrategy.cs`
- `C:\sources\Modern.CRDT\Modern.CRDT\Services\Strategies\LwwStrategy.cs`

<!---Human--->
# Testing Methodology
<!---
Add the testing methodology (manual, unit, integration, end-to-end tests?)
--->
- **Unit Tests:** All existing unit tests for `JsonCrdtPatcher` must pass after the refactoring to ensure functional correctness is maintained.
- **Benchmarks:** The existing benchmark project (`Modern.CRDT.Benchmarks`) will be used to measure the performance before and after the changes. The primary success metrics will be a significant reduction in mean execution time and, crucially, a large decrease in allocated memory for the `GeneratePatch` benchmarks.

<!---AI - Stage 1--->
# Proposed Solutions
<!---
Here you will need to put a number of solutions that would fit for this problem.
Add the solutions that you rejected as well.
--->
### Solution 1: Targeted Patcher Optimization (Recommended)
This solution directly implements the "High Impact, Low Effort" improvement identified in the analysis.
-   **Approach:** Refactor the `JsonCrdtPatcher` to eliminate the `BuildLwwMetadataTree` method entirely. Instead of converting the flat LWW metadata dictionary into a `JsonNode` tree on every patch generation, the recursive `DifferentiateObject` method will perform direct key lookups into the `CrdtMetadata.Lww` dictionary using the current JSON path.
-   **Pros:**
    -   Drastically reduces memory allocations and CPU cycles.
    -   Minimal code change, localized entirely within the `JsonCrdtPatcher` service.
    -   Low risk, as it doesn't change the public API or the core diffing logic.
-   **Cons:**
    -   Does not address the secondary overhead of POCO-to-`JsonNode` serialization.
-   **Reason for Recommendation:** This approach provides the best return on investment. It targets the most significant and unnecessary performance bottleneck with a simple, safe, and effective change.

### Solution 2: Full Reflection-Based Rewrite
This solution implements the "Very High Impact, High Effort" improvement.
-   **Approach:** Re-architect both `JsonCrdtPatcher` and `JsonCrdtApplicator` (and all associated strategies) to operate directly on POCOs using reflection. This would eliminate the `JsonNode` intermediate representation completely.
-   **Pros:**
    -   Maximum possible performance gain by avoiding all serialization/deserialization overhead.
    -   Significantly lower memory footprint.
-   **Cons:**
    -   High implementation effort and complexity.
    -   Requires significant changes to the `ICrdtStrategy` interface and all its implementations.
    -   Higher risk of introducing subtle bugs in the object graph navigation and value manipulation logic.

### Solution 3: No Optimization
-   **Approach:** Leave the implementation as is.
-   **Pros:**
    -   No effort required.
-   **Cons:**
    -   Fails to address the identified performance and memory allocation issues, which could be critical in high-throughput scenarios.
-   **Reason for Rejection:** The analysis clearly shows a straightforward path to significant optimization (Solution 1). Ignoring it would leave unnecessary performance bottlenecks in the library.
<!---AI - Stage 1--->
# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->
1.  **Modify `JsonCrdtPatcher.cs`:**
    -   Locate and completely remove the private method `BuildLwwMetadataTree(IDictionary<string, ICrdtTimestamp> lww)`.
    -   In the `GeneratePatch<T>` method, delete the lines that call `BuildLwwMetadataTree` for both the `from` and `to` documents. The variables `fromMetaNode` and `toMetaNode` will no longer exist.
    -   Modify the `DifferentiateObject` method's signature to accept the original `CrdtDocument<T>` objects or their `CrdtMetadata` directly, instead of the `JsonNode` metadata trees. The new signature could be: `DifferentiateObject(..., CrdtMetadata fromMetadata, CrdtMetadata toMetadata, ...)`.
    -   Inside the `DifferentiateObject`'s property loop, replace the logic that accesses the metadata `JsonNode` trees (`fromMetaNode` and `toMetaNode`).
    -   Implement direct lookups for LWW timestamps. For each property at `currentPath`, use `fromMetadata.Lww.TryGetValue(currentPath, out var fromTimestamp)` and `toMetadata.Lww.TryGetValue(currentPath, out var toTimestamp)`.
    -   Pass this `fromTimestamp` and `toTimestamp` to the `LwwStrategy.GeneratePatch` method.
2.  **Introduce Reflection Caching in `JsonCrdtPatcher.cs`:**
    -   Add a static `ConcurrentDictionary<Type, PropertyInfo[]>` field to cache properties by type, similar to the `PropertyCache` in `JsonCrdtApplicator`.
    -   In `DifferentiateObject`, replace the direct `type.GetProperties()` call with a `_propertyCache.GetOrAdd(type, t => t.GetProperties())` call to leverage the cache.
3.  **Validate with Unit Tests:**
    -   Navigate to `Modern.CRDT.UnitTests/Services/JsonCrdtPatcherTests.cs`.
    -   Run all tests to ensure that the refactoring has not introduced any regressions. The tests should pass without modification as the public contract of the service remains unchanged.
4.  **Measure Performance with Benchmarks:**
    -   Open the `Modern.CRDT.Benchmarks` project.
    -   Run the `PatcherBenchmarks`.
    -   Compare the new output for `GeneratePatchSimple` and `GeneratePatchComplex` against the baseline provided in the requirements.
    -   Confirm a significant decrease in both the `Mean` time and the `Allocated` memory columns.
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
- `$/Modern.CRDT/Services/JsonCrdtPatcher.cs` (The primary file to be modified for optimization)
- `$/Modern.CRDT/Services/Strategies/LwwStrategy.cs` (To review how timestamps are used in patch generation)
- `$/Modern.CRDT/Models/CrdtDocumentOfT.cs` (To understand the input data structure containing the flat metadata)
- `$/Modern.CRDT.UnitTests/Services/JsonCrdtPatcherTests.cs` (To ensure correctness after refactoring)
- `$/Modern.CRDT.Benchmarks/Benchmarks/PatcherBenchmarks.cs` (To measure the performance improvements)

<!---AI - Stage 2--->
# Changes Done
<!---
Here you add detailed information about all the changes actually done.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
Add all the things that you did in a different way than expected.
--->
- `$/Modern.CRDT/Services/JsonCrdtPatcher.cs`
    - Removed the `BuildLwwMetadataTree` method to eliminate the expensive process of converting the flat LWW metadata dictionary into a `JsonNode` tree on every patch generation.
    - Modified the `DifferentiateObject` method to perform direct key lookups on the flat `CrdtMetadata.Lww` dictionary using the current JSON path to retrieve timestamps. This drastically reduces allocations and CPU usage.
    - For recursive calls on nested objects, the full `CrdtMetadata` object is now passed down, allowing nested properties to also perform efficient flat lookups.
    - Introduced a static `ConcurrentDictionary` to cache reflection results for type properties (`PropertyCache`), further improving performance on repeated calls for the same types.
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
The library still relies on serializing POCOs to an intermediate `JsonNode` representation for diffing and patching. While the most significant bottleneck in the patcher (`BuildLwwMetadataTree`) has been removed, the serialization step itself still contributes to memory allocation and processing time. A future, more intensive optimization would involve refactoring all services and strategies to operate directly on POCOs via reflection, as outlined in "Solution 2". This would be a high-effort task but would yield the maximum possible performance.
<!---AI - Stage 2--->
## Last notes and implementation details
<!---
Here you add comments about the implementation that didn't fit on the previous section.
--->
- The optimization was successfully implemented by focusing on the `JsonCrdtPatcher` service as planned. The primary bottleneck, `BuildLwwMetadataTree`, was eliminated.
- The change was contained entirely within `JsonCrdtPatcher.cs`, avoiding any breaking changes to the `ICrdtStrategy` interface or its implementations. Strategies continue to receive a `JsonNode` for metadata, which is now constructed on-the-fly from the looked-up timestamp. This maintains the existing contract while achieving the desired performance gain.
- All existing unit tests are expected to pass as the external behavior of the patcher remains identical. The benchmark results should now show a significant reduction in memory allocation and execution time for patch generation.

# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->