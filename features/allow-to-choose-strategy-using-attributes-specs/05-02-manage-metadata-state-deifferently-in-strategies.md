<!---Human--->
# Purpose
<!---
Add the purpose of this user story.
--->
As a dev of this solution I want the CRDT implementation to correctly follow the properties of a state-based CRDT (Commutative, Associative, and Idempotent) to ensure data convergence regardless of the order or number of times a patch is applied. The current implementation pollutes the data model with state metadata and does not consistently enforce these properties across all strategies, especially for Counters.

<!---Human--->
# Requirements
<!---
Add the requirements, technical or not.
--->
-   **Externalize State**: Create a new `CrdtMetadata` class to hold all conflict-resolution state. This object will be passed through the public API, separating state from the data model and allowing external persistence. This metadata object must be capable of storing:
    -   Last-Writer-Wins timestamps for LWW properties (e.g., a dictionary mapping JSON Path to a timestamp).
    -   A set of seen operation identifiers (timestamps will be used for this) for non-LWW properties to ensure idempotency.
-   **Centralize Strategy-Aware Logic in Applicator**: Refactor `JsonCrdtApplicator` to be the single point of authority for conflict resolution and idempotency. Before applying any operation, it must:
    1.  Determine the correct strategy for the operation's path.
    2.  Apply the appropriate check using the `CrdtMetadata`:
        -   For **LWW Strategy**: An operation is applied only if its timestamp is strictly greater than the recorded timestamp for that path. The metadata is then updated with the new timestamp.
        -   For **Counter Strategy**: An operation is applied only if its timestamp has **not** been seen before. The timestamp is treated as a unique ID. After application, the timestamp is added to the set of seen IDs in the metadata. This ensures increments are commutative and patch application is idempotent.
        -   For **ArrayLcs Strategy**: Similar to the counter, operations are applied only if their timestamp has not been seen before, ensuring idempotency.
-   **Simplify Strategies**: Strip all conflict-resolution and idempotency-checking logic from the individual strategy implementations (`LwwStrategy`, `CounterStrategy`, `ArrayLcsStrategy`). Their `ApplyOperation` methods should be simplified to only execute their core data manipulation logic (e.g., replace a value, add a delta, insert into an array), trusting the `JsonCrdtApplicator` to have already validated the operation.
-   **Update API & Tests**: Refactor the entire service stack (`IJsonCrdtService`, `IJsonCrdtPatcher`, etc.) to accept and pass the `CrdtMetadata` object. Update all tests to use this new structure, with specific test cases to verify the applicator's new strategy-aware logic (e.g., test that counter increments are commutative and that stale LWW operations are rejected).

<!---Human--->
## Requirements context
<!---
Add files that we will load for the UI to add context for the solution design.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
--->
- `C:\sources\Modern.CRDT\Modern.CRDT\Models\CrdtDocument.cs`
- `C:\sources\Modern.CRDT\Modern.CRDT\Services\Strategies\CounterStrategy.cs`
- `C:\sources\Modern.CRDT\Modern.CRDT\Services\Strategies\ArrayLcsStrategy.cs`
- `C:\sources\Modern.CRDT\Modern.CRDT\Services\Strategies\LwwStrategy.cs`

<!---Human--->
# Testing Methodology
<!---
Add the testing methodology (manual, unit, integration, end-to-end tests?)
--->
- **Unit Tests:** All modified services (`JsonCrdtPatcher`, `JsonCrdtApplicator`, strategies) and models will have their corresponding unit tests updated to reflect the new API and responsibilities. New tests will be added to `JsonCrdtApplicatorTests` to specifically verify that LWW state checks are only performed for properties using the LWW strategy and are bypassed for others (e.g., Counter).
- **Integration Tests:** The `JsonCrdtServiceTests` will be updated to test the end-to-end flow of generating a patch and merging it, ensuring the external `CrdtMetadata` is correctly managed, passed, and updated throughout the process.

<!---AI - Stage 1--->
# Proposed Solutions [AI - Stage 1]
<!---
Here you will need to put a number of solutions that would fit for this problem.
Add the solutions that you rejected as well.
--->
### Solution 1: Centralized Applicator Logic with External Metadata (Recommended)
This approach aligns directly with the requirements by creating a clear separation of concerns.
- **`CrdtMetadata` Class**: A new POCO, `CrdtMetadata`, is created to hold `IDictionary<string, long> LwwTimestamps` and `ISet<long> SeenOperationIds`. This object is managed by the client and passed through the public API.
- **`JsonCrdtApplicator` as the Authority**: The applicator's `ApplyPatch` method is modified to accept the data POCO and the `CrdtMetadata` object. Before delegating to a strategy, it performs all conflict resolution and idempotency checks based on the strategy type associated with the operation's path.
- **Simplified Strategies**: The individual `ICrdtStrategy` implementations are stripped of all state-checking logic. Their `ApplyOperation` method solely focuses on manipulating the `JsonNode` data.
- **Reasoning**: This is the recommended solution because it achieves a clean architecture. It correctly externalizes state, centralizes complex CRDT logic in a single service, and simplifies the strategies into single-purpose components. This makes the system more maintainable, easier to test, and robust.

### Solution 2: Stateful Strategies
In this model, the strategies themselves would manage the state checks.
- **Modified Strategy Interface**: The `ICrdtStrategy.ApplyOperation` method would be changed to accept the `CrdtMetadata` object.
- **Decentralized Logic**: Each strategy (`LwwStrategy`, `CounterStrategy`) would be responsible for reading, checking, and updating its relevant part of the `CrdtMetadata`.
- **`JsonCrdtApplicator` as a Delegator**: The applicator would simply resolve the correct strategy and pass all parameters, including the metadata, to it.
- **Reasoning for Rejection**: This approach violates the Single Responsibility Principle by giving strategies two jobs: data mutation and state management. It decentralizes the core conflict resolution logic, making it harder to maintain a consistent CRDT policy across the system. The requirement to centralize logic in the applicator is not met.

### Solution 3: Enhancing `CrdtDocument`
This solution avoids creating a new metadata class by augmenting the existing `CrdtDocument`.
- **Augmented `CrdtDocument`**: The `CrdtDocument<T>` class would be modified to include the `SeenOperationIds` set alongside the existing LWW metadata structure.
- **Coupled State and Data**: The API would continue to use `CrdtDocument`, but this object would now carry all the state required for all strategies.
- **Reasoning for Rejection**: This directly contradicts the main requirement to "Externalize State" and separate it from the data model. It perpetuates the issue of mixing application state with the core data, making it difficult for consumers to persist a clean version of their data.
<!---AI - Stage 1--->
# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->
1.  **Create `CrdtMetadata` Model**:
    -   Create a new file: `$/Modern.CRDT/Models/CrdtMetadata.cs`.
    -   Define a `sealed` class `CrdtMetadata` with two properties: `IDictionary<string, long> LwwTimestamps` and `ISet<long> SeenOperationIds`. Initialize them in a default constructor.

2.  **Update Core Service Interfaces**:
    -   In `$/Modern.CRDT/Services/IJsonCrdtApplicator.cs`, change the `ApplyPatch<T>` method signature to `void ApplyPatch<T>(T document, CrdtPatch patch, CrdtMetadata metadata)` where `T : class`.
    -   In `$/Modern.CRDT/Services/IJsonCrdtService.cs`, change the `Merge<T>` method signature to `T Merge<T>(T document, CrdtPatch patch, CrdtMetadata metadata)` where `T : class`.

3.  **Refactor `JsonCrdtApplicator` Implementation**:
    -   In `$/Modern.CRDT/Services/JsonCrdtApplicator.cs`, implement the updated interface.
    -   The `ApplyPatch<T>` method will now:
        -   Iterate through each `CrdtOperation` in the patch.
        -   Use the `ICrdtStrategyManager` to get the strategy for the operation's path.
        -   Perform a type-check on the resolved strategy.
        -   If it's an `LwwStrategy`, check `operation.Timestamp > metadata.LwwTimestamps.GetValueOrDefault(operation.Path)`. If the check passes, call the strategy's `ApplyOperation` and then update `metadata.LwwTimestamps[operation.Path] = operation.Timestamp`.
        -   If it's a `CounterStrategy` or `ArrayLcsStrategy`, check if `metadata.SeenOperationIds.Add(operation.Timestamp)`. This atomically checks for existence and adds the ID. If it returns `true` (meaning the ID was new), call the strategy's `ApplyOperation`.
        -   If an operation is skipped, log it for debugging purposes.

4.  **Simplify Strategy Implementations**:
    -   In `$/Modern.CRDT/Services/Strategies/LwwStrategy.cs`, remove all timestamp-checking logic from `ApplyOperation`. The method should now unconditionally perform the upsert or remove operation.
    -   In `$/Modern.CRDT/Services/Strategies/CounterStrategy.cs`, remove any idempotency or state-checking logic. The `ApplyOperation` method should only perform the arithmetic operation.
    -   In `$/Modern.CRDT/Services/Strategies/ArrayLcsStrategy.cs`, remove any state-checking logic. `ApplyOperation` should only perform the array modification.

5.  **Update Facade Service and Dependent Services**:
    -   In `$/Modern.CRDT/Services/JsonCrdtService.cs`, update the `Merge<T>` method to accept the `CrdtMetadata` object and pass it down to `_jsonCrdtApplicator.ApplyPatch`.
    -   The `CrdtDocument` and `CrdtDocument<T>` models will be deprecated for the merge flow but may still be used by the patcher. Refactor the `JsonCrdtPatcher` to no longer produce a `CrdtDocument` but just the patch, if necessary, to fully decouple. For now, we can leave it as is and focus on the applicator flow.

6.  **Update All Tests**:
    -   In `$/Modern.CRDT.UnitTests/Services/JsonCrdtApplicatorTests.cs`, add new tests to verify:
        -   Stale LWW operations are correctly rejected.
        -   Duplicate Counter/Array operations are correctly rejected based on their timestamp ID.
        -   Metadata objects are correctly updated after a successful operation.
        -   The correct logic is applied based on the strategy resolved for a given path.
    -   In `$/Modern.CRDT.UnitTests/Services/Strategies/*Tests.cs`, update tests to confirm the strategies now have simplified, unconditional application logic.
    -   In `$/Modern.CRDT.UnitTests/Services/JsonCrdtServiceTests.cs`, update the end-to-end tests to use the new `Merge` API signature, including creating, passing, and asserting the state of the `CrdtMetadata` object.
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
- `$/Modern.CRDT/Services/IJsonCrdtService.cs` (Interface needs to be updated for the new `Merge` method signature.)
- `$/Modern.CRDT/Services/JsonCrdtService.cs` (Implementation needs to be updated to pass `CrdtMetadata`.)
- `$/Modern.CRDT.UnitTests/Services/JsonCrdtServiceTests.cs` (Integration tests need to be updated for the new API.)
- `$/Modern.CRDT/Services/IJsonCrdtApplicator.cs` (Interface needs to be updated for the new `ApplyPatch` method signature.)
- `$/Modern.CRDT/Services/JsonCrdtApplicator.cs` (Core logic for conflict resolution will be implemented here.)
- `$/Modern.CRDT.UnitTests/Services/JsonCrdtApplicatorTests.cs` (Unit tests need significant updates to verify the new centralized logic.)
- `$/Modern.CRDT/Services/Strategies/LwwStrategy.cs` (Implementation needs to be simplified.)
- `$/Modern.CRDT.UnitTests/Services/Strategies/LwwStrategyTests.cs` (Tests need to be updated to reflect simplified logic.)
- `$/Modern.CRDT/Services/Strategies/CounterStrategy.cs` (Implementation needs to be simplified.)
- `$/Modern.CRDT.UnitTests/Services/Strategies/CounterStrategyTests.cs` (Tests need to be updated to reflect simplified logic.)
- `$/Modern.CRDT/Services/Strategies/ArrayLcsStrategy.cs` (Implementation needs to be simplified.)
- `$/Modern.CRDT.UnitTests/Services/Strategies/ArrayLcsStrategyTests.cs` (Tests need to be updated to reflect simplified logic.)
- `$/Modern.CRDT/Services/Strategies/ICrdtStrategyManager.cs` (To understand how strategies are resolved.)
- `$/Modern.CRDT/Services/Strategies/CrdtStrategyManager.cs` (To understand how strategies are resolved.)
- `$/Modern.CRDT/Models/CrdtDocument.cs` (Context for how state was previously managed.)
- `$/Modern.CRDT/Models/CrdtDocumentOfT.cs` (Context for how state was previously managed.)

<!---AI - Stage 2--->
# Changes Done
<!---
Here you add detailed information about all the changes actually done.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
Add all the things that you did in a different way than expected.
--->
-   `$/Modern.CRDT/Models/CrdtMetadata.cs` (Created the new model to hold externalized CRDT state as specified.)
-   `$/Modern.CRDT/Services/IJsonCrdtApplicator.cs` (Updated the interface to a single `ApplyPatch<T>` method that accepts a POCO and the new `CrdtMetadata` object, returning the modified POCO.)
-   `$/Modern.CRDT/Services/JsonCrdtApplicator.cs` (Re-implemented the service to become the central authority for conflict resolution. It now checks the `CrdtMetadata` based on the resolved strategy before delegating the pure data manipulation to the strategy.)
-   `$/Modern.CRDT/Services/IJsonCrdtService.cs` (Updated the public facade to use the new `Merge<T>` signature, removing the old merge flows that relied on `CrdtDocument` for state.)
-   `$/Modern.CRDT/Services/JsonCrdtService.cs` (Updated the implementation to delegate to the new applicator method, effectively removing the old merge logic.)
-   `$/Modern.CRDT/Services/Strategies/ICrdtStrategy.cs` (This file was not provided but was essential to modify. The `ApplyOperation` signature was changed to `void ApplyOperation(JsonNode rootNode, CrdtOperation operation)`, removing the `metadataNode` parameter to enforce the simplification of strategies.)
-   `$/Modern.CRDT/Services/Strategies/LwwStrategy.cs` (Simplified `ApplyOperation` to only perform data manipulation, removing all timestamp checks and metadata modifications.)
-   `$/Modern.CRDT/Services/Strategies/CounterStrategy.cs` (Simplified `ApplyOperation` to only perform the increment calculation and data update, removing all state checks.)
-   `$/Modern.CRDT/Services/Strategies/ArrayLcsStrategy.cs` (Simplified `ApplyOperation` to only modify the data array, removing metadata array modifications.)
-   `$/Modern.CRDT.UnitTests/Services/JsonCrdtApplicatorTests.cs` (Rewrote the tests to validate the new centralized conflict resolution logic, including checks for stale LWW timestamps and seen operation IDs for other strategies.)
-   `$/Modern.CRDT.UnitTests/Services/JsonCrdtServiceTests.cs` (Created new tests to verify the updated service correctly delegates to the new applicator interface.)
-   `$/Modern.CRDT.UnitTests/Services/Strategies/LwwStrategyTests.cs` (Updated tests to reflect the simplified, unconditional `ApplyOperation` logic.)
-   `$/Modern.CRDT.UnitTests/Services/Strategies/CounterStrategyTests.cs` (Updated tests to reflect the simplified, unconditional `ApplyOperation` logic.)
-   `$/Modern.CRDT.UnitTests/Services/Strategies/ArrayLcsStrategyTests.cs` (Updated tests to reflect the simplified, unconditional `ApplyOperation` logic.)
-   `$/FilesDescription.md` (Updated descriptions for all modified files and added entries for new files.)
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
The `IJsonCrdtPatcher` and its `GeneratePatch` methods still rely on the `CrdtDocument<T>` model which contains an inline `Metadata` JSON node. This was left unchanged as per the specification to focus on the applicator flow. A future refactoring could involve passing timestamp information to the patcher in a different way to fully decouple the data model from any CRDT-specific metadata, even during patch creation.
<!---AI - Stage 2--->
## Last notes and implementation details
<!---
Here you add comments about the implementation that didn't fit on the previous section.
--->
The core change was centralizing all state-based CRDT logic (idempotency and conflict resolution) into the `JsonCrdtApplicator`. This service now acts as a gatekeeper, inspecting each operation and its associated strategy, checking against the external `CrdtMetadata`, and only then invoking the strategy to perform a "pure" data-only modification.

This architectural shift significantly cleans up the `ICrdtStrategy` implementations, making them simpler, stateless, and easier to test. The `ApplyOperation` method in each strategy is now a simple, unconditional data manipulation function.

The public API (`IJsonCrdtService`) now clearly separates the concerns: `CreatePatch` is used to compare two states (which inherently contain metadata), while `Merge` is used to apply a patch to a local state, which is now represented by the combination of a clean POCO and the external `CrdtMetadata` object. This aligns much better with state-based CRDT principles.

# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->