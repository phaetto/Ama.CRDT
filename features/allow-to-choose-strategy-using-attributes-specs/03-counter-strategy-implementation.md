<!---Human--->
# Purpose
To introduce a new CRDT strategy for handling numeric properties as counters (CRDT Counter). This demonstrates the extensibility of the new strategy system and provides a practical alternative to LWW for scenarios like tracking scores, votes, or quantities.

<!---Human--->
# Requirements
- Add a new `Increment` member to the `OperationType` enum.
- Create a `CrdtCounterAttribute` class that inherits from `CrdtStrategyAttribute`. This will be used to mark numeric properties that should behave as counters.
- Create a `CounterStrategy` class that implements `ICrdtStrategy`.
- The `GenerateOperations` method in `CounterStrategy` will:
    - Compare the old and new numeric values.
    - If they differ, it will generate a single `CrdtOperation` with type `Increment` and a value equal to the delta (new value - old value).
- The `ApplyOperation` method in `CounterStrategy` will:
    - Expect an `Increment` operation.
    - Add the operation's value to the existing numeric value at the target path in the JSON document.
- The strategy should handle type checking and throw exceptions if applied to non-numeric fields.

<!---Human--->
## Requirements context
- `$/features/allow-to-choose-strategy-using-attributes-specs/01-crdt-strategy-attribute-and-interface.md`
- `$/features/allow-to-choose-strategy-using-attributes-specs/02-lww-strategy-implementation.md`

<!---Human--->
# Testing Methodology
- Unit tests for the `CounterStrategy` class covering:
    - Correct delta calculation for patch generation (positive and negative increments).
    - Correct application of `Increment` operations.
    - Error handling when the target property is not a number.
- Integration tests to verify that `JsonCrdtPatcher` and `JsonCrdtApplicator` correctly process POCOs with properties marked with `[CrdtCounter]`.

<!---AI - Stage 1--->
# Proposed Solutions
<!---
Here you will need to put a number of solutions that would fit for this problem.
Add the solutions that you rejected as well.
--->
1.  **Recommended: Delta-Based Increment Operation.** This approach aligns directly with the requirements and the standard implementation of a PN-Counter CRDT.
    *   **How it works:** `GenerateOperations` calculates the delta (`newValue - oldValue`) and creates a single `CrdtOperation` of type `Increment` with the delta as its value. `ApplyOperation` simply adds the operation's value to the current value at the target path.
    *   **Reasoning:** This is the most efficient and semantically correct solution. It produces a small, commutative operation that accurately represents the change, minimizing network traffic and simplifying the application logic. It truly models a counter.

2.  **Rejected: State-Based Operation.** This approach would involve sending the final value instead of the change.
    *   **How it works:** `GenerateOperations` would create an operation (e.g., `SetCounter`) with the new numeric value. The `ApplyOperation` logic would then need to read the current value from the document, calculate the delta (`operation.Value - currentValue`), and apply it.
    *   **Reasoning for Rejection:** This complicates the `ApplyOperation` logic and deviates from the CRDT principle of commutative operations. It's less a "counter" and more a specialized "Last-Writer-Wins" for numbers, which doesn't fulfill the purpose of tracking increments and decrements independently.

3.  **Rejected: Separate Increment/Decrement Operations.** This approach would involve creating two distinct operation types.
    *   **How it works:** Add both `Increment` and `Decrement` to `OperationType`. `GenerateOperations` would determine if the value increased or decreased and create the corresponding operation with the absolute difference. `ApplyOperation` would need a switch to either add or subtract based on the operation type.
    *   **Reasoning for Rejection:** This adds unnecessary complexity. A single `Increment` operation type that can handle both positive (increment) and negative (decrement) values is simpler, more elegant, and equally effective.

<!---AI - Stage 1--->
# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->
1.  **Modify Enum:** Add the `Increment` member to the `OperationType` enum in `$/Modern.CRDT/Models/OperationType.cs`.
2.  **Create Attribute:** Create a new file `$/Modern.CRDT/Attributes/CrdtCounterAttribute.cs`. The class `CrdtCounterAttribute` will inherit from `CrdtStrategyAttribute` and will be configured to use `CounterStrategy`.
3.  **Implement Strategy:** Create the core logic file `$/Modern.CRDT/Services/Strategies/CounterStrategy.cs`.
    *   Implement the `ICrdtStrategy` interface.
    *   **`GenerateOperations`:**
        *   Verify that both old and new `JsonNode` inputs are numeric values.
        *   Calculate the delta (`new - old`).
        *   If the delta is not zero, return a `CrdtOperation` of type `Increment` with the delta as the value.
    *   **`ApplyOperation`:**
        *   Verify the operation type is `Increment`.
        *   Find the target node in the document.
        *   Verify the target node and operation value are numeric.
        *   Add the operation's value to the target node's value and update the document.
        *   Throw `InvalidOperationException` for any type mismatches.
4.  **Register Strategy:** In `$/Modern.CRDT/Extensions/ServiceCollectionExtensions.cs`, add `services.AddSingleton<CounterStrategy>();` to register the new strategy with the dependency injection container.
5.  **Create Unit Tests:** Create a new test file `$/Modern.CRDT.UnitTests/Services/Strategies/CounterStrategyTests.cs`.
    *   Add tests for `GenerateOperations` covering positive, negative, and no-change scenarios.
    *   Add tests for `ApplyOperation` covering both positive and negative increments.
    *   Add tests to ensure methods throw exceptions when used with non-numeric data.
6.  **Update Integration Tests:** Modify `$/Modern.CRDT.UnitTests/Services/JsonCrdtPatcherTests.cs`.
    *   Define a new test POCO with a numeric property annotated with `[CrdtCounter]`.
    *   Add a new test case to confirm that `GeneratePatch` correctly creates an `Increment` operation when this property changes.
    *   This will implicitly test the integration with `JsonCrdtApplicator` as part of the end-to-end test flow.

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
- `$/Modern.CRDT/Models/OperationType.cs` (To add the new `Increment` operation type.)
- `$/Modern.CRDT/Attributes/CrdtStrategyAttribute.cs` (To be used as the base class for the new `CrdtCounterAttribute`.)
- `$/Modern.CRDT/Services/Strategies/ICrdtStrategy.cs` (The interface that the new `CounterStrategy` will implement.)
- `$/Modern.CRDT/Extensions/ServiceCollectionExtensions.cs` (To register the `CounterStrategy` for dependency injection.)
- `$/Modern.CRDT.UnitTests/Services/JsonCrdtPatcherTests.cs` (To add a new integration test demonstrating the counter strategy with a POCO.)
- `$/Modern.CRDT/Models/CrdtOperation.cs` (For context on the operation structure used in the strategy implementation.)
- `$/Modern.CRDT.UnitTests/Modern.CRDT.UnitTests.csproj` (To add the new `CounterStrategyTests.cs` file to the test project.)

<!---AI - Stage 2--->
# Changes Done
- `$/Modern.CRDT/Models/OperationType.cs`: Added the `Increment` member to the `OperationType` enum.
- `$/Modern.CRDT/Attributes/CrdtCounterAttribute.cs`: Created the `CrdtCounterAttribute` to mark properties for counter-based CRDT logic, linking it to the `CounterStrategy`.
- `$/Modern.CRDT/Services/Strategies/CounterStrategy.cs`: Implemented the `CounterStrategy` to handle `Increment` operations. It calculates deltas for patch generation and applies increments to existing numeric values. Includes robust type checking and basic JSON path traversal.
- `$/Modern.CRDT/Extensions/ServiceCollectionExtensions.cs`: Registered `CounterStrategy` with the DI container as a transient service, consistent with other strategies.
- `$/Modern.CRDT.UnitTests/Services/Strategies/CounterStrategyTests.cs`: Added comprehensive unit tests for `CounterStrategy`, covering patch generation (positive/negative/zero delta), operation application, and error handling for non-numeric types.
- `$/Modern.CRDT.UnitTests/Services/JsonCrdtPatcherTests.cs`: Added integration tests to verify that `JsonCrdtPatcher` correctly identifies properties with `[CrdtCounter]` and generates the appropriate `Increment` operation. An end-to-end test also confirms the patch is applied correctly by `JsonCrdtApplicator`.

<!---AI - Stage 2--->
# Manual Changes Needed
No manual changes needed to be applied.

<!---AI - Stage 2--->
## Possible Techical Debt
<!---
Here you add comments about possible technical debt you encountered or implemented but it was too much to change or out of scope.
--->

<!---AI - Stage 2--->
## Last notes and implementation details
The `CounterStrategy`'s `ApplyOperation` method currently implements its own simple JSON path traversal (`$.prop1.prop2`). This logic is expected to be centralized within the `JsonCrdtApplicator` in a future refactoring step (`05-refactor-applicator-to-use-strategies.md`), which would simplify the strategy implementations. For now, the strategy is self-contained as required by the current `ICrdtStrategy` interface. The strategy correctly assumes that a non-existent numeric property has a default value of 0 when an increment is applied. Error handling is in place for non-numeric data and invalid paths.

# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->