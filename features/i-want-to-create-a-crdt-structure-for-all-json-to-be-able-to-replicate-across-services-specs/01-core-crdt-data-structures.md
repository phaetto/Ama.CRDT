# Purpose
To define and implement the fundamental Conflict-free Replicated Data Type (CRDT) structures that will serve as the building blocks for the JSON CRDT library. This foundational layer will ensure that the properties of CRDTs (associativity, commutativity, idempotence) are correctly implemented before building more complex logic on top.

# Requirements
- Implement a state-based Grow-Only Counter (G-Counter).
- Implement a state-based Positive-Negative Counter (PN-Counter).
- Implement a state-based Last-Writer-Wins Register (LWW-Register).
- Implement a state-based Grow-Only Set (G-Set).
- Implement a state-based 2-Phase Set (2P-Set).
- All implementations must provide a `Merge` operation that combines the state of two CRDT instances.
- The structures should be designed to be serializable and deserializable.
- Use generics where appropriate to allow for flexibility in the data types they hold (e.g., for Sets and Registers).

## Requirements context
No previous context is required for this initial implementation.

# Testing Methodology
The primary testing method will be unit testing. Each CRDT implementation will have a dedicated test suite.
- **Property-based tests:** Verify that the `Merge` operation for each CRDT is associative, commutative, and idempotent.
- **State tests:** For each CRDT, create multiple replicas, apply different operations, merge them in various orders, and assert that all replicas converge to the same final state.
- **Edge cases:** Test initial states, merging with empty/default instances, and other boundary conditions.
- **All of the above need to have unit tests**

# Proposed Solutions
<!---
Here you will need to put a number of solutions that would fit for this problem.
Add the solutions that you rejected as well.
--->
### Solution 1: Interface-Based Approach (Recommended)
- **Description:** Define a common `ICrdt<T>` interface where `T` is the implementing CRDT type itself (using the Curiously Recurring Template Pattern). This interface will enforce the presence of a `Merge(T other)` method. Each CRDT will be implemented as a `readonly record struct`, ensuring immutability and value-type semantics, which are highly beneficial for CRDTs. This approach is aligned with modern C# best practices, promoting loose coupling and high performance.
- **Pros:**
    - Enforces a common contract across all CRDTs.
    - `readonly record struct` provides immutability out-of-the-box, preventing accidental state mutation.
    - Promotes composition over inheritance, leading to a more flexible and maintainable design.
    - Aligns with the project's coding standards.
- **Cons:**
    - No shared implementation logic, but the internal state and merge logic of each CRDT are distinct enough that a base class would offer little benefit.

### Solution 2: Abstract Base Class
- **Description:** Create an abstract base class, e.g., `StateBasedCrdt`, that all CRDT implementations would inherit from. This class could define the abstract `Merge` method signature.
- **Pros:**
    - Could potentially hold common logic if any were identified in the future.
- **Cons:**
    - Creates a tighter coupling between the CRDT types.
    - The internal state representations are very different (e.g., `IDictionary<Guid, ulong>` for G-Counter vs. `ISet<T>` for G-Set), limiting the usefulness of a common base class.
    - Inheritance is less flexible than interface implementation, especially with value types like structs.

### Solution 3: Functional Static Methods
- **Description:** Implement each CRDT as a simple data-only `record struct`. All operations (e.g., `Add`, `Increment`, `Merge`) would be implemented as static extension methods in separate helper classes.
- **Pros:**
    - Clearly separates data from behavior, following a functional programming paradigm.
    - Guarantees immutability as operations would always return a new instance.
- **Cons:**
    - Can feel less idiomatic in an object-oriented language like C#.
    - Method discovery can be less straightforward for developers not accustomed to this pattern.

# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->
1.  **Create a common CRDT interface:**
    - Create `$/Modern.CRDT/Models/ICrdt.cs` defining a generic interface `public interface ICrdt<T> where T : ICrdt<T>` with a single method: `T Merge(T other);`.

2.  **Implement G-Counter:**
    - Create `$/Modern.CRDT/Models/G_Counter.cs`.
    - Implement as `public readonly record struct G_Counter : ICrdt<G_Counter>`.
    - Internal state: `IDictionary<Guid, ulong> Counts`.
    - Public members: `Increment(Guid nodeId)`, `Merge(G_Counter other)`, `Value` property.
    - Create `$/Modern.CRDT.UnitTests/Models/G_CounterTests.cs` with tests for properties, state convergence, and edge cases.

3.  **Implement PN-Counter:**
    - Create `$/Modern.CRDT/Models/PN_Counter.cs`.
    - Implement as `public readonly record struct PN_Counter : ICrdt<PN_Counter>`.
    - Internal state: Two `G_Counter` instances (for increments and decrements).
    - Public members: `Increment(Guid nodeId)`, `Decrement(Guid nodeId)`, `Merge(PN_Counter other)`, `Value` property.
    - Create `$/Modern.CRDT.UnitTests/Models/PN_CounterTests.cs` with comprehensive unit tests.

4.  **Implement LWW-Register:**
    - Create `$/Modern.CRDT/Models/LWW_Register.cs`.
    - Implement as `public readonly record struct LWW_Register<T> : ICrdt<LWW_Register<T>>`.
    - Internal state: `T Value`, `long Timestamp`, `Guid NodeId` (as a tie-breaker).
    - Public members: `Assign(T value, long timestamp, Guid nodeId)`, `Merge(LWW_Register<T> other)`.
    - Create `$/Modern.CRDT.UnitTests/Models/LWW_RegisterTests.cs` with tests for the generic implementation.

5.  **Implement G-Set:**
    - Create `$/Modern.CRDT/Models/G_Set.cs`.
    - Implement as `public readonly record struct G_Set<T> : ICrdt<G_Set<T>>`.
    - Internal state: `ISet<T> Elements`.
    - Public members: `Add(T element)`, `Merge(G_Set<T> other)`, `Lookup(T element)`, `Values` property.
    - Create `$/Modern.CRDT.UnitTests/Models/G_SetTests.cs` to validate its behavior.

6.  **Implement 2P-Set (Two-Phase Set):**
    - Create `$/Modern.CRDT/Models/TwoP_Set.cs`.
    - Implement as `public readonly record struct TwoP_Set<T> : ICrdt<TwoP_Set<T>>`.
    - Internal state: Two `G_Set<T>` instances (one for additions, one for tombstones/removals).
    - Public members: `Add(T element)`, `Remove(T element)`, `Merge(TwoP_Set<T> other)`, `Lookup(T element)`, `Values` property. The `Remove` operation must be final.
    - Create `$/Modern.CRDT.UnitTests/Models/TwoP_SetTests.cs` with tests focusing on the "remove-wins" logic.

7.  **Update Project and Documentation:**
    - Add all new files to the `$/FilesDescription.md` file with brief descriptions.

# Proposed Files Needed
<!---
Here you need to list the files you need to load in order to get the correct context for your solution to build and test.
Put in this list only the exising files that need to be modified/loaded. Not the new ones that need to be created.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
With each file in one line.
Remember to ask to load any unit tests if they are related to any files you will want to change.
--->
- `$/Modern.CRDT/Modern.CRDT.csproj` (To understand project dependencies and structure)
- `$/Modern.CRDT.UnitTests/Modern.CRDT.UnitTests.csproj` (To understand testing framework and dependencies)
- `$/FilesDescription.md` (To update with the newly created files)
- `$/CodingStandards.md` (To ensure all new code adheres to the defined standards)

# Changes Done
<!---
Here you add detailed information about all the changes actually done.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
Add all the things that you did in a different way than expected.
--->
- Created `$/Modern.CRDT/Models/ICrdt.cs` to define the common interface for all CRDTs.
- Implemented `$/Modern.CRDT/Models/G_Counter.cs` as a `readonly record struct` using `ImmutableDictionary` for its state.
- Implemented `$/Modern.CRDT/Models/PN_Counter.cs` by composing two `G_Counter` instances for increments and decrements.
- Implemented `$/Modern.CRDT/Models/LWW_Register.cs` as a generic `readonly record struct` to store a value with a timestamp and node ID for conflict resolution.
- Implemented `$/Modern.CRDT/Models/G_Set.cs` as a generic `readonly record struct` using `ImmutableHashSet`.
- Implemented `$/Modern.CRDT/Models/TwoP_Set.cs` by composing two `G_Set` instances for additions and removals (tombstones).
- Added the `System.Collections.Immutable` NuGet package to the `Modern.CRDT` project.
- Created comprehensive xUnit and Shouldly unit tests for each CRDT type to verify correctness, property-based requirements (associativity, commutativity, idempotence), and convergence.
- Added a `ProjectReference` from the unit test project to the main project.
- Added the `Shouldly` NuGet package to the `Modern.CRDT.UnitTests` project.
- Updated `$/FilesDescription.md` with all newly created files.

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

## Possible Techical Debt
<!---
Here you add comments about possible technical debt you encountered or implemented but it was too much to change or out of scope.
--->
None.

## Last notes and implementation details
<!---
Here you add comments about the implementation that didn't fit on the previous section.
--->
- All CRDTs are implemented as `readonly record struct` to enforce immutability, which is a key characteristic for predictable state-based CRDTs.
- The implementations strictly follow the "state-based" or "convergent" CRDT model, where the full state is merged.
- Used `System.Collections.Immutable` collections (`ImmutableDictionary`, `ImmutableHashSet`) to ensure thread safety and immutability without manual cloning, simplifying the implementation of CRDT operations.
- The `LWW_Register` uses a `Guid` as a tie-breaker when timestamps are identical. This ensures that merges always result in a deterministic state.
- The `TwoP_Set` correctly implements the "remove-wins" semantic, meaning an element once removed cannot be re-added, as the tombstone entry persists.
- All public methods that accept arguments perform null/empty checks as per the coding standards.

# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->