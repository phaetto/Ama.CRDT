<!---Human--->
# Purpose
<!---
Add the purpose of this user story.
--->
Implement the State Machine Strategy.

<!---Human--->
# Requirements
<!---
Add the requirements, technical or not.
--->
**State Machine Strategy:** Enforces valid state transitions. For example, a property can only change from `PENDING` to `PROCESSING`, but not directly to `SHIPPED`. Invalid transitions are rejected.

Make only the ones that do not break the public API, except the metadata and other models. Avoid breaking the interface public APIs.

Use the `CrdtMetadataManager` to provide utilities for metadata management.

Define the state machine by passing a `Type` to the attribute. This type would implement an interface that provides the transition graph. It would also provide all the validation logic about strings and transitions. It should be grabbed from DI.
Make it so the devs that implement that service can provide their own types, like string or enum, to have a better experience defining the state graph (avoinf object implementations, unless we can use it a a proxy).

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
- `C:\sources\Ama.CRDT\Ama.CRDT\Services\Helpers\PocoPathHelper.cs`

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
### Solution 1: LWW-Based State Resolution with a Generic Validator Interface (Recommended)
- **Description:** This approach implements the state machine by layering validation logic on top of a standard Last-Writer-Wins (LWW) conflict resolution mechanism. A new attribute, `CrdtStateMachineStrategyAttribute`, will accept a `Type` that implements a new generic interface, `IStateMachine<TState>`. This interface, retrieved via DI, will define valid transitions (e.g., `bool IsValidTransition(TState from, TState to)`).
- **Patch Generation:** The `GeneratePatch` method will consult the `IStateMachine` service to check if a change from the old value to the new value is a valid transition. If it is, a standard LWW `Upsert` operation is generated. If not, no operation is created.
- **Patch Application:** The `ApplyOperation` method is crucial for convergence. It will retrieve the document's *current* state for the given property path, and use the `IStateMachine` service to validate if the incoming operation represents a valid transition from that current state. Only if the transition is valid will it proceed with the LWW timestamp check to resolve conflicts and apply the update. This prevents out-of-order patches from violating the state machine rules.
- **Reason for Recommendation:** This solution effectively balances correctness and implementation simplicity. It reuses the well-established LWW pattern for conflict resolution while ensuring state machine rules are enforced during both patch generation and application, which is critical for achieving a convergent state. The use of a generic, DI-driven interface makes it highly extensible and testable for developers.

### Solution 2: Operation-Based State Machine with State Tracking in Metadata
- **Description:** This solution would introduce a new, more explicit `Transition` operation type. The operation payload would contain the `from` and `to` states. The strategy would maintain the authoritative "current state" for the property directly within the `CrdtMetadata`. When an operation is applied, the strategy would validate that the operation's `from` state matches the state stored in the metadata before applying the transition and updating the metadata state.
- **Reason for Rejection:** While this approach is more explicit and potentially more robust for very complex state machines, it introduces significant complexity. It would require creating a new `OperationType`, custom serialization for the payload, and more intricate management of the `CrdtMetadata` object. For the specified requirements, this is likely over-engineering and less aligned with the existing, simpler `Upsert`-based strategies.

### Solution 3: Hybrid LWW with Validation on Patch Generation Only
- **Description:** This approach would simplify Solution 1 by only performing state transition validation during `GeneratePatch`. The `ApplyOperation` method would be a pure LWW application, applying any change with a newer timestamp without re-validating the transition.
- **Reason for Rejection:** This approach is fundamentally flawed and would not guarantee convergence or correctness. If patches arrive out of order at a replica, it could apply a state that is not a valid transition from its current state (e.g., applying a `PROCESSING -> SHIPPED` patch when the local state is still `PENDING`). This violates the core requirement of the strategy. Correctness requires validation to occur at the time of application.

<!---AI - Stage 1--->
# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->
1.  **Define `IStateMachine<TState>` Interface:**
    -   Create a new file `$/Ama.CRDT/Services/Strategies/IStateMachine.cs`.
    -   This public generic interface will define the contract for state machine validators. It will contain a single method: `bool IsValidTransition(TState from, TState to)`.

2.  **Create `CrdtStateMachineStrategyAttribute`:**
    -   Create a new file `$/Ama.CRDT/Attributes/CrdtStateMachineStrategyAttribute.cs`.
    -   This attribute will inherit from `CrdtStrategyAttribute`.
    -   It will have a constructor that takes a `Type` parameter, which will be stored in a public property. This type represents the implementation of `IStateMachine<TState>` to be used for validation.

3.  **Implement `StateMachineStrategy`:**
    -   Create a new file `$/Ama.CRDT/Services/Strategies/StateMachineStrategy.cs`.
    -   This class will implement `ICrdtStrategy`. It will depend on `IServiceProvider` to resolve the validator type provided in the attribute.
    -   **`GeneratePatch` Logic:**
        -   Reflect on the property to get the `CrdtStateMachineStrategyAttribute` and the validator `Type`.
        -   Resolve the validator instance from the `IServiceProvider`.
        -   If the property value has changed, invoke `IsValidTransition` on the validator.
        -   If the transition is valid, generate an `Upsert` operation using LWW principles (i.e., comparing timestamps in metadata).
    -   **`ApplyOperation` Logic:**
        -   Resolve the validator instance similarly.
        -   Get the current value of the property from the target object.
        -   Invoke `IsValidTransition` using the current value and the value from the incoming operation.
        -   If the transition is valid, perform an LWW timestamp check against the metadata.
        -   If the timestamp is newer, update the object's property and the timestamp in the metadata.

4.  **Integrate the New Strategy:**
    -   Modify `$/Ama.CRDT/Services/Strategies/CrdtStrategyManager.cs`: Add a case to the strategy resolution logic to recognize `CrdtStateMachineStrategyAttribute` and return a `StateMachineStrategy` instance.
    -   Modify `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs`: Register `StateMachineStrategy` as a transient service in the dependency injection container.

5.  **Create Unit Tests:**
    -   Create a new file `$/Ama.CRDT.UnitTests/Services/Strategies/StateMachineStrategyTests.cs`.
    -   Implement a sample state machine for testing (e.g., `TestOrderStateMachine : IStateMachine<string>`).
    -   Write tests to cover:
        -   Generation of patches for valid transitions.
        -   Rejection of patch generation for invalid transitions.
        -   Correct application of valid patches.
        -   Ignoring patches that represent an invalid transition from the current state.
        -   LWW conflict resolution for concurrent valid transitions from the same state.
        -   Idempotence, and commutativity of conflict resolution.

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
- `$/Ama.CRDT/Attributes/CrdtStrategyAttribute.cs` (To serve as the base class for the new attribute.)
- `$/Ama.CRDT/Services/Strategies/ICrdtStrategy.cs` (To implement the contract for the new strategy.)
- `$/Ama.CRDT/Services/Strategies/LwwStrategy.cs` (As a reference for implementing the LWW logic for patch generation and application.)
- `$/Ama.CRDT/Services/Strategies/CrdtStrategyManager.cs` (To register the new strategy for resolution based on the attribute.)
- `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs` (To register the new strategy service in the DI container.)
- `$/Ama.CRDT/Models/CrdtMetadata.cs` (To understand how to interact with LWW timestamps.)
- `$/Ama.CRDT/Models/CrdtOperation.cs` (To understand how to construct patch operations.)
- `$/Ama.CRDT.UnitTests/Services/Strategies/LwwStrategyTests.cs` (As a template for structuring the new strategy's unit tests.)

<!---AI - Stage 2--->
# Changes Done
<!---
Here you add detailed information about all the changes actually done.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
Add all the things that you did in a different way than expected.
--->
- `$/Ama.CRDT/Services/Strategies/IStateMachine.cs`: Created the new generic interface for state machine validators.
- `$/Ama.CRDT/Attributes/CrdtStateMachineStrategyAttribute.cs`: Created the new attribute to mark properties and provide the validator type.
- `$/Ama.CRDT/Services/Strategies/StateMachineStrategy.cs`: Implemented the core strategy logic, which validates transitions and uses LWW for conflict resolution. It leverages the existing `Lww` dictionary in `CrdtMetadata`, so no changes to the metadata object were needed.
- `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs`: Registered the `StateMachineStrategy` in the DI container.
- `$/Ama.CRDT/Services/Strategies/LwwStrategy.cs`: Changed the `DeserializeValue` helper method from `private static` to `public static` to allow `StateMachineStrategy` to reuse it for deserializing operation values.
- `$/Ama.CRDT.UnitTests/Services/Strategies/StateMachineStrategyTests.cs`: Created a comprehensive set of unit tests to validate patch generation, application, and conflict resolution logic for the new strategy.
- `$/FilesDescription.md`: Updated with the newly created files.

The implementation followed the proposed solution closely. No change was needed for `CrdtStrategyManager` because the base `CrdtStrategyAttribute` already provides the `StrategyType`, which the manager uses for resolution.

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
As this change introduces a new feature, the `MINOR_VERSION` environment variable in `/.github/workflows/publish-nuget.yml` should be reviewed and potentially incremented before the next release to master.

<!---AI - Stage 2--->
## Possible Techical Debt
<!---
Here you add comments about possible technical debt you encountered or implemented but it was too much to change or out of scope.
--->
The `StateMachineStrategy` relies on reflection to invoke the `IsValidTransition` method on the user-provided validator. While this is necessary for a generic implementation, it is slightly less performant than a direct-cast invocation. For performance-critical applications, a source-generated or pre-compiled delegate approach could be considered in the future, but for typical use cases, the current implementation is sufficient and much simpler.

<!---AI - Stage 2--->
## Last notes and implementation details
<!---
Here you add comments about the implementation that didn't fit on the previous section.
--->
The State Machine strategy was implemented as a layer on top of the Last-Writer-Wins (LWW) strategy. This means it reuses the LWW conflict resolution logic: if two replicas make a *valid* transition concurrently, the one with the higher timestamp wins. The strategy ensures that an operation is only generated or applied if it represents a valid state transition according to the user-defined `IStateMachine<TState>` validator. This validation happens both at patch generation time and, crucially, at patch application time to prevent out-of-order patches from violating the state machine rules. The strategy uses the existing `CrdtMetadata.Lww` dictionary to track timestamps, avoiding the need to introduce new state into the metadata object.

# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->