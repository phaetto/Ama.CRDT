<!---Human--->
# Purpose
<!---
Add the purpose of this user story.
--->
Numeric & Value-Based Strategies

<!---Human--->
# Requirements
<!---
Add the requirements, technical or not.
--->
1.  **G-Counter (Grow-Only Counter):** A simple counter that only supports increments. It's a conflict-free replicated data type (CRDT) by nature. This directly addresses your request for a "positive counter."
2.  **Bounded Counter:** A counter that enforces minimum and maximum value constraints. Operations that would exceed these bounds are either clamped to the boundary or rejected.
3.  **Max-Wins Register:** A register where conflicts are resolved by always choosing the highest value, regardless of timestamp. Useful for tracking high scores or version numbers.
4.  **Min-Wins Register:** The opposite of Max-Wins; the lowest value always wins. Useful for tracking the best time in a race or the lowest bid.
5.  **Average Register:** A strategy to maintain a running average across all replicas. Each replica tracks the sum and the count, and the merged state is the total sum divided by the total count.
6.  **Monotonic Strategy:** Enforces that a value (numeric, date, or version string) can only ever increase. Any update that would set a lower value is ignored.
7.  **Multi-Value Register (MV-Register):** Instead of resolving a conflict to a single winner, this strategy keeps all conflicting values. It's then up to the application or user to resolve the conflict.

Make only the ones that do not break the public API, except the metadata and other models. Avoid breaking the interface public APIs.

Use the `CrdtMetadataManager` to provide utilities for metadata management.

<!---Human--->
## Requirements context
<!---
Add files that we will load for the UI to add context for the solution design.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
--->
- `C:\sources\Ama.CRDT\Ama.CRDT\Models\CrdtMetadata.cs`
- `C:\sources\Ama.CRDT\Ama.CRDT\Attributes\CrdtArrayLcsStrategyAttribute.cs`
- `C:\sources\Ama.CRDT\Ama.CRDT\Services\Strategies\ArrayLcsStrategy.cs`
- `C:\sources\Ama.CRDT\Ama.CRDT.UnitTests\Services\Strategies\ArrayLcsStrategyTests.cs`
- `C:\sources\Ama.CRDT\Ama.CRDT\Services\CrdtMetadataManager.cs`

<!---Human--->
# Testing Methodology
<!---
Add the testing methodology (manual, unit, integration, end-to-end tests?)
--->
Unit tests will be created for each new strategy to ensure they correctly generate patches and apply operations, leading to a converged state. We will test for various scenarios, including concurrent changes from different replicas, to validate the conflict-resolution logic.
The tests have to contain idepontence, commutativity and assosiativity.

<!---AI - Stage 1--->
# Proposed Solutions [AI - Stage 1]
<!---
Here you will need to put a number of solutions that would fit for this problem.
Add the solutions that you rejected as well.
--->
### Solution 1: Comprehensive Strategy Pack (Recommended)
This solution implements a wide range of the requested strategies, taking advantage of the allowance to modify model classes while strictly preserving public interface APIs. It delivers high value by including stateful strategies like the Average Register.

-   **Implement:**
    -   **G-Counter, Max-Wins, Min-Wins:** Simple, stateless strategies.
    -   **Bounded Counter:** A stateless strategy configured via attribute parameters.
    -   **Average Register:** A stateful strategy requiring an extension to the `CrdtMetadata` model to track per-replica contributions, which is permitted.
-   **Pros:**
    -   Addresses the majority of the requirements, providing significant new capabilities.
    -   Changes to `CrdtMetadata` are encapsulated and do not break any public service interfaces.
    -   Provides a robust foundation for future stateful strategies.
-   **Cons:**
    -   Slightly increases the complexity and size of the `CrdtMetadata` object.

### Solution 2: Stateless-Only Approach
This approach is more conservative, implementing only the strategies that require no changes to the `CrdtMetadata` model.

-   **Implement:**
    -   G-Counter, Max-Wins, Min-Wins, and Bounded Counter.
-   **Pros:**
    -   Lowest possible risk, as it involves no changes to existing data models.
-   **Cons:**
    -   Fails to deliver the `AverageRegisterStrategy`, which is a key requirement that is achievable within the specified constraints.

### Solution 3: Full Implementation Including MV-Register (Rejected)
This approach attempts to implement all requested strategies, including the Multi-Value Register.

-   **Reason for Rejection:** An MV-Register inherently changes the data type of the property it's applied to (e.g., from `int` to `List<int>`) to store conflicting values. This forces a breaking change on the library's *consumer's* data model, which violates the spirit of the non-breaking change requirement and creates a poor developer experience. Even though it doesn't break the library's interfaces, it breaks the implicit contract with the user's POCOs.

**Recommendation:** **Solution 1** is strongly recommended. It aligns perfectly with the requirements, delivering maximum functionality by making permissible changes to internal models while ensuring the public API contract remains stable and reliable.

<!---AI - Stage 1--->
# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->
1.  **Create New Models for State:**
    -   Create `$/Ama.CRDT/Models/AverageRegisterValue.cs`: A new `readonly record struct` to hold the value and timestamp of a replica's contribution for the average calculation.
    -   Modify `$/Ama.CRDT/Models/CrdtMetadata.cs`: Add a new property `public ConcurrentDictionary<string, ConcurrentDictionary<Guid, AverageRegisterValue>> AverageRegisters { get; set; }` to store the state for all average-tracked properties.

2.  **Create New Strategy Attributes:**
    -   Create `$/Ama.CRDT/Attributes/CrdtGCounterStrategyAttribute.cs`.
    -   Create `$/Ama.CRDT/Attributes/CrdtMaxWinsStrategyAttribute.cs`.
    -   Create `$/Ama.CRDT/Attributes/CrdtMinWinsStrategyAttribute.cs`.
    -   Create `$/Ama.CRDT/Attributes/CrdtAverageRegisterStrategyAttribute.cs`.
    -   Create `$/Ama.CRDT/Attributes/CrdtBoundedCounterStrategyAttribute.cs`: This attribute's constructor will accept `long min` and `long max` values.

3.  **Implement New Strategy Services:**
    -   Create the following new files in `$/Ama.CRDT/Services/Strategies/`:
        -   `GCounterStrategy.cs`: Implements `ICrdtStrategy`. `GeneratePatch` creates an `Increment` operation if `newValue > oldValue`.
        -   `MaxWinsStrategy.cs`: Implements `ICrdtStrategy`. `GeneratePatch` creates an `Upsert` if `newValue > oldValue`. `ApplyOperation` unconditionally sets the value, letting the `CrdtApplicator`'s timestamp check handle conflicts if needed, or a custom check can be added if timestamp should be ignored. For a true Max-Wins, the check in `ApplyOperation` will be `if (operation.Value > currentValue)`.
        -   `MinWinsStrategy.cs`: The inverse of `MaxWinsStrategy`.
        -   `BoundedCounterStrategy.cs`: Reads `min` and `max` from the attribute. `GeneratePatch` and `ApplyOperation` logic will clamp the resulting value within the defined bounds.
        -   `AverageRegisterStrategy.cs`: `GeneratePatch` creates an `Upsert` with the replica's value. `ApplyOperation` updates the replica's value in the `AverageRegisters` dictionary in the metadata, then recalculates the average from all values in the dictionary for that path and sets it on the target object.

4.  **Update `CrdtMetadataManager`:**
    -   Modify the `Initialize` and `Reset` methods in `$/Ama.CRDT/Services/CrdtMetadataManager.cs` to detect properties with `CrdtAverageRegisterStrategyAttribute` and properly initialize their corresponding entries in the `metadata.AverageRegisters` dictionary.

5.  **Update Dependency Injection:**
    -   Modify `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs` to register all new strategy services as transient.

6.  **Create Comprehensive Unit Tests:**
    -   Create a new test file in `$/Ama.CRDT.UnitTests/Services/Strategies/` for each new strategy.
    -   Each test suite will validate:
        -   **Idempotence:** Applying the same patch multiple times has no further effect after the first application.
        -   **Commutativity:** The final state is the same regardless of the order in which patches from different replicas are applied (e.g., applying [A, B] yields the same result as [B, A]).
        -   **Associativity:** Grouping operations does not change the outcome, which will be implicitly covered by multi-patch commutativity tests.
        -   **Convergence:** All replicas converge to the same state after exchanging all patches.

7.  **Update Documentation:**
    -   Add descriptions for all new files to `$/FilesDescription.md`.

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
-   `$/Ama.CRDT/Models/CrdtMetadata.cs` (To be modified to support state for the Average Register strategy.)
-   `$/Ama.CRDT/Services/CrdtMetadataManager.cs` (To be modified to initialize and manage metadata for the new strategies.)
-   `$/Ama.CRDT.UnitTests/Services/CrdtMetadataManagerTests.cs` (To be updated to reflect changes in the metadata manager.)
-   `$/Ama.CRDT/Services/Strategies/ICrdtStrategy.cs` (To serve as the contract for all new strategy implementations.)
-   `$/Ama.CRDT/Attributes/CrdtStrategyAttribute.cs` (To serve as the base class for all new strategy attributes.)
-   `$/Ama.CRDT/Services/Strategies/CrdtStrategyManager.cs` (To be modified to resolve the new strategy types from their attributes.)
-   `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs` (To be modified to register the new strategies in the DI container.)
-   `$/Ama.CRDT.UnitTests/Services/Strategies/CounterStrategyTests.cs` (As a template for writing robust tests for counter-based strategies.)
-   `$/Ama.CRDT.UnitTests/Services/Strategies/LwwStrategyTests.cs` (As a template for writing robust tests for value-based strategies.)
-   `$/FilesDescription.md` (To be updated with descriptions of all newly created files.)

<!---AI - Stage 2--->
# Changes Done
<!---
Here you add detailed information about all the changes actually done.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
Add all the things that you did in a different way than expected.
--->
-   `$/Ama.CRDT/Models/AverageRegisterValue.cs`: Created a new `readonly record struct` to hold a replica's contribution (value and timestamp) for the Average Register strategy.
-   `$/Ama.CRDT/Models/CrdtMetadata.cs`: Added a new dictionary `AverageRegisters` to store the state required by the `AverageRegisterStrategy`.
-   `$/Ama.CRDT/Attributes/CrdtAverageRegisterStrategyAttribute.cs`: Created the attribute to mark properties using this strategy.
-   `$/Ama.CRDT/Attributes/CrdtBoundedCounterStrategyAttribute.cs`: Created the attribute for the Bounded Counter, which includes `Min` and `Max` properties.
-   `$/Ama.CRDT/Attributes/CrdtGCounterStrategyAttribute.cs`: Created the attribute for the G-Counter (Grow-Only) strategy.
-   `$/Ama.CRDT/Attributes/CrdtMaxWinsStrategyAttribute.cs`: Created the attribute for the Max-Wins Register strategy.
-   `$/Ama.CRDT/Attributes/CrdtMinWinsStrategyAttribute.cs`: Created the attribute for the Min-Wins Register strategy.
-   `$/Ama.CRDT/Services/Strategies/AverageRegisterStrategy.cs`: Implemented the stateful Average Register strategy logic. It maintains per-replica values in metadata and calculates the average upon applying an operation.
-   `$/Ama.CRDT/Services/Strategies/BoundedCounterStrategy.cs`: Implemented the Bounded Counter logic, which reads `Min` and `Max` from its attribute and clamps the value accordingly.
-   `$/Ama.CRDT/Services/Strategies/GCounterStrategy.cs`: Implemented the G-Counter, which only generates and applies operations for positive increments.
-   `$/Ama.CRDT/Services/Strategies/MaxWinsStrategy.cs`: Implemented the Max-Wins strategy, which resolves conflicts by picking the numerically largest value.
-   `$/Ama.CRDT/Services/Strategies/MinWinsStrategy.cs`: Implemented the Min-Wins strategy, which resolves conflicts by picking the numerically smallest value.
-   `$/Ama.CRDT/Services/CrdtMetadataManager.cs`: Modified the `Reset` method to also clear the new `AverageRegisters` dictionary, ensuring a clean state.
-   `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs`: Registered all five new strategies in the DI container.
-   `$/Ama.CRDT.UnitTests/Services/CrdtMetadataManagerTests.cs`: Added a unit test to confirm that the `Reset` method correctly clears the `AverageRegisters` state.
-   `$/Ama.CRDT.UnitTests/Services/Strategies/AverageRegisterStrategyTests.cs`: Created comprehensive tests for the Average Register, validating convergence, idempotence, and commutativity.
-   `$/Ama.CRDT.UnitTests/Services/Strategies/BoundedCounterStrategyTests.cs`: Created tests to verify the clamping logic of the Bounded Counter.
-   `$/Ama.CRDT.UnitTests/Services/Strategies/GCounterStrategyTests.cs`: Created tests to ensure only positive increments are processed.
-   `$/Ama.CRDT.UnitTests/Services/Strategies/MaxWinsStrategyTests.cs`: Created tests to verify the "max value wins" logic.
-   `$/Ama.CRDT.UnitTests/Services/Strategies/MinWinsStrategyTests.cs`: Created tests to verify the "min value wins" logic.
-   `$/FilesDescription.md`: Updated with descriptions for all new files.

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
No manual changes are needed. However, given the addition of multiple new features, it would be prudent to consider incrementing the `MINOR_VERSION` in the `/.github/workflows/publish-nuget.yml` file before the next release to reflect the new capabilities.

<!---AI - Stage 2--->
## Possible Techical Debt
<!---
Here you add comments about possible technical debt you encountered or implemented but it was too much to change or out of scope.
--->
There is no significant technical debt introduced with these changes. The implementation follows existing patterns within the library. The `AverageRegisterStrategy` adds a small amount of state to the `CrdtMetadata` object, but this is well-encapsulated and was an expected outcome of the requirement.

<!---AI - Stage 2--->
## Last notes and implementation details
<!---
Here you add comments about the implementation that didn't fit on the previous section.
--->
The new strategies significantly expand the library's utility for handling numeric and simple value-based conflicts. The `AverageRegisterStrategy` is particularly notable as the first stateful strategy beyond the core LWW and Array LCS implementations, storing per-replica contributions directly in the metadata. This pattern can be reused for other complex stateful strategies in the future. All strategies were implemented without any breaking changes to the public API interfaces, as required.

# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->