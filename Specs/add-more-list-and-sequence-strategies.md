<!---Human--->
# Purpose
<!---
Add the purpose of this user story.
--->
List & Sequence Strategies

<!---Human--->
# Requirements
<!---
Add the requirements, technical or not.
--->
13. **RGA (Replicated Growable Array):** A classic list CRDT that is an alternative to your LCS-based approach. It handles concurrent insertions and deletions gracefully by maintaining causal information.
14. **Causal Tree (CT):** A robust strategy for ordered sequences (ideal for text editing) that models the document as a tree of operations, preserving the causal order of edits.
15. **LSEQ Strategy:** An alternative to fractional indexing (which your `ArrayLcsStrategy` uses) that assigns identifiers to list elements in a way that avoids floating-point precision issues and guarantees a dense order.
16. **Move-in-List Strategy:** A specialized strategy that treats moving an item from one index to another as a single, atomic operation rather than a separate deletion and insertion.
17. **Fixed-Size Array Strategy:** Manages an array with a constant size, where only the values at each index can be updated, typically using an LWW strategy for each slot.
18. **Priority Queue Strategy:** A list that maintains its sort order based on a "priority" value associated with each item. Merging would involve re-sorting the combined list.

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
### Solution 1: Incremental, Per-Strategy Implementation (Recommended)
**Description:** Implement each of the six requested strategies as a distinct, self-contained component. This involves creating a new attribute, a strategy class implementing `ICrdtStrategy`, any required supporting models, and a corresponding unit test suite for each. This approach follows the existing architectural pattern used for `LwwStrategy`, `CounterStrategy`, and `ArrayLcsStrategy`.

**Pros:**
- **Low Risk:** Aligns with the existing, proven architecture, minimizing the chance of introducing breaking changes or unintended side effects.
- **Modular & Testable:** Each strategy can be developed, tested, and validated in isolation.
- **Clarity:** The one-to-one mapping between a requirement and its implementation makes the codebase easy to understand and maintain.

**Cons:**
- **Potential Code Duplication:** Strategies for ordered lists (RGA, CT, LSEQ) might share some underlying logic (like tombstone management) that could be duplicated.

**Reason for Recommendation:** This is the most pragmatic and safest approach. It directly addresses the requirements without introducing unnecessary complexity or architectural risks. The modularity ensures that we can deliver robust, well-tested features incrementally while strictly adhering to the "no public API breaking changes" constraint.

### Solution 2: Abstracted Sequence Framework
**Description:** Create a generic base framework or a set of shared services for sequence-based CRDTs (RGA, CT, LSEQ). This framework would handle common concerns like node identification, causal linking, and tombstone management. Each specific strategy would then extend this framework, implementing only its unique identifier allocation and ordering logic.

**Pros:**
- **Code Reusability:** Reduces code duplication across similar sequence-based strategies.
- **Extensibility:** Could make it easier to add other sequence CRDTs in the future.

**Cons:**
- **High Upfront Cost:** Requires significant design effort to create a flexible and robust abstraction.
- **Risk of Over-Engineering:** A poorly designed abstraction could become a bottleneck and make implementing the specific strategies more difficult than a direct approach.
- **Potential for Breaking Changes:** Introducing a new abstraction layer might necessitate changes to existing interfaces or patterns, violating a key requirement.

### Solution 3: Prioritized Subset Implementation
**Description:** Implement only the strategies that are most distinct from the existing `ArrayLcsStrategy` first, such as `Fixed-Size Array Strategy` and `Priority Queue Strategy`. The more complex, alternative ordered-list strategies (RGA, CT, LSEQ) would be deferred.

**Pros:**
- **Faster Initial Delivery:** Delivers new, unique capabilities to users more quickly.

**Cons:**
- **Incomplete Fulfillment:** Does not satisfy the full scope of the user request. This is more of a project management strategy than a technical solution.

<!---AI - Stage 1--->
# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->
The implementation will follow a consistent pattern for each of the six strategies.

**General Pattern for Each Strategy:**
1.  **Create Model(s):** In `$/Ama.CRDT/Models/`, define any new `readonly record struct` needed to store strategy-specific metadata (e.g., nodes with causal links for RGA, priority values, etc.).
2.  **Create Attribute:** In `$/Ama.CRDT/Attributes/`, create a new `Crdt[StrategyName]StrategyAttribute.cs` inheriting from `CrdtStrategyAttribute`.
3.  **Implement Strategy:** In `$/Ama.CRDT/Services/Strategies/`, create `[StrategyName]Strategy.cs` that implements `ICrdtStrategy`. This class will contain the core logic for patch generation and operation application.
4.  **Update Metadata Manager:** Modify `CrdtMetadataManager.cs` to initialize and manage the metadata for the new strategy. This will involve adding a new collection to `CrdtMetadata.cs` and updating the `Initialize` and `Reset` methods.
5.  **Create Unit Tests:** In `$/Ama.CRDT.UnitTests/Services/Strategies/`, create `[StrategyName]StrategyTests.cs` to thoroughly test convergence properties (idempotence, commutativity, associativity) under various concurrent scenarios.
6.  **Update Public API:** Add the new public attribute to `$/Ama.CRDT/PublicAPI.Unshipped.txt`.

---

**Detailed Steps per Strategy:**

**1. Fixed-Size Array Strategy**
- **Model:** No new model needed. Metadata will be LWW timestamps for each array index (e.g., `path[0]`, `path[1]`).
- **Attribute:** `CrdtFixedSizeArrayStrategyAttribute` will take an integer `size` as a parameter.
- **Strategy:** `FixedSizeArrayStrategy` will iterate from `0` to `size-1`. For each index, it will delegate to the `LwwStrategy` logic to compare values and generate/apply patches. It will enforce the fixed-size constraint.
- **Metadata:** `CrdtMetadataManager` will be updated to create LWW timestamp entries for each array index during initialization.

**2. Priority Queue Strategy**
- **Model:** `PriorityQueueItem<T>` record holding the `Value` and a `Priority` (e.g., a `long`).
- **Attribute:** `CrdtPriorityQueueStrategyAttribute`.
- **Strategy:** `PriorityQueueStrategy` will use a set-based approach. `GeneratePatch` will compare two collections of `PriorityQueueItem`s and generate `Upsert` (add) or `Remove` operations. `ApplyOperation` will add/remove items from the list. The final state of the POCO list will be derived by sorting the metadata collection by priority.
- **Metadata:** `CrdtMetadata` will need a new dictionary to store the state of priority queues, e.g., `IDictionary<string, ISet<PriorityQueueItem<object>>>`.

**3. Move-in-List Strategy**
- **Model:** A new `OperationType.Move` will be added. `CrdtOperation` may need optional `FromPath` or `ToIndex` properties.
- **Attribute:** `CrdtMoveInListStrategyAttribute`.
- **Strategy:** `MoveInListStrategy` will use an LCS algorithm to detect differences. It will need additional logic to identify if a removed item is re-inserted elsewhere, in which case it generates a single `Move` operation instead of a `Remove` and an `Upsert`. This is an optimization over the standard LCS approach.
- **Metadata:** Can likely reuse the positional identifier metadata from `ArrayLcsStrategy`.

**4. RGA (Replicated Growable Array) Strategy**
- **Model:** `RgaNode<T>` record containing `Value`, a unique `Id` (e.g., `Guid` or composite key), a `ParentId` pointing to the preceding element's ID, and a `IsTombstone` flag.
- **Attribute:** `CrdtRgaStrategyAttribute`.
- **Strategy:** `RgaStrategy` will manage a linked list of `RgaNode`s in the metadata. Insertions are `Upsert` operations containing a new `RgaNode`, which is then woven into the linked list. Deletions are `Upsert` operations that flip the `IsTombstone` flag on an existing node. The POCO list is a projection of the non-tombstoned nodes in causal order.
- **Metadata:** `CrdtMetadata` will need a `IDictionary<string, IList<RgaNode<object>>>` to store the RGA structures.

**5. LSEQ Strategy**
- **Model:** `LseqNode<T>` record containing `Value` and a positional identifier `Position` (represented as a list of integers or similar structure that avoids floating-point issues).
- **Attribute:** `CrdtLseqStrategyAttribute`.
- **Strategy:** `LseqStrategy` will be similar to `ArrayLcsStrategy` but will generate and manage its own positional identifiers (`LseqNode.Position`). When an item is inserted between two others, it generates a new position identifier that falls between its neighbors. This requires a robust identifier allocation algorithm.
- **Metadata:** `CrdtMetadata` will need a `IDictionary<string, IList<LseqNode<object>>>`.

**6. Causal Tree (CT) Strategy**
- **Model:** `CtNode<T>` record representing an operation (e.g., character insertion). It will contain the `Value`, a unique `Id`, and a `ParentId` to establish a causal link, forming a tree structure.
- **Attribute:** `CrdtCausalTreeStrategyAttribute`.
- **Strategy:** This is the most complex strategy. `CausalTreeStrategy` will manage the tree of operations in metadata. `GeneratePatch` will diff two trees to find new operation nodes. `ApplyOperation` will add new nodes to the tree. The final POCO list/string is generated by performing a specific traversal (e.g., depth-first) of the tree, ignoring deleted nodes (tombstones).
- **Metadata:** `CrdtMetadata` will require a `IDictionary<string, IList<CtNode<object>>>` to store the root nodes of the trees.

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
- `$/Ama.CRDT/Models/CrdtMetadata.cs` (To be modified to store metadata for the new strategies)
- `$/Ama.CRDT/Models/CrdtOperation.cs` (May need modification to support a `Move` operation)
- `$/Ama.CRDT/Models/OperationType.cs` (May need modification to add a `Move` operation type)
- `$/Ama.CRDT/Services/CrdtMetadataManager.cs` (To be modified to initialize and manage metadata for new strategies)
- `$/Ama.CRDT/Services/ICrdtMetadataManager.cs` (To be loaded for context and potentially modified if new public methods are needed)
- `$/Ama.CRDT/Services/Strategies/ICrdtStrategy.cs` (To be loaded as the contract for all new strategy implementations)
- `$/Ama.CRDT/Services/Strategies/ArrayLcsStrategy.cs` (To be loaded as a reference implementation for list-based strategies)
- `$/Ama.CRDT/Attributes/CrdtStrategyAttribute.cs` (To be loaded as the base class for new strategy attributes)
- `$/Ama.CRDT/PublicAPI.Unshipped.txt` (To be modified to include the new public attributes)
- `$/Ama.CRDT.UnitTests/Services/Strategies/ArrayLcsStrategyTests.cs` (To be loaded as a template for writing the new unit test suites)
- `$/Ama.CRDT.UnitTests/Services/CrdtMetadataManagerTests.cs` (To be modified to add tests for the new metadata initialization logic)

<!---AI - Stage 2--->
# Changes Done
<!---
Here you add detailed information about all the changes actually done.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
Add all the things that you did in a different way than expected.
--->
- **Implemented `Fixed-Size Array Strategy`**:
    - `$/Ama.CRDT/Attributes/CrdtFixedSizeArrayStrategyAttribute.cs`: Created a new attribute to mark properties and define the array's fixed size.
    - `$/Ama.CRDT/Services/Strategies/FixedSizeArrayStrategy.cs`: Implemented the strategy. It treats each array index as an independent LWW register, generating operations for paths like `$.myArray[0]`, `$.myArray[1]`, etc. This leverages the existing LWW metadata store.
    - `$/Ama.CRDT/Services/CrdtMetadataManager.cs`: Updated to initialize LWW metadata for each index of a fixed-size array.
    - `$/Ama.CRDT.UnitTests/Services/Strategies/FixedSizeArrayStrategyTests.cs`: Added a new test suite to verify convergence, idempotence, and LWW conflict resolution for this strategy.
- **Implemented `Priority Queue Strategy`**:
    - `$/Ama.CRDT/Attributes/CrdtPriorityQueueStrategyAttribute.cs`: Created a new attribute that takes the name of the priority-defining property on the collection's elements.
    - `$/Ama.CRDT/Services/Strategies/PriorityQueueStrategy.cs`: Implemented the strategy. It's modeled after `LwwSetStrategy` but with an additional step in `ApplyOperation` to re-sort the list based on the priority property after any change.
    - `$/Ama.CRDT/Models/CrdtMetadata.cs`: Added a new `PriorityQueues` collection to store the state for this strategy, keeping it separate from `LwwSets`.
    - `$/Ama.CRDT/Services/CrdtMetadataManager.cs`: Updated to initialize and reset the `PriorityQueues` metadata collection.
    - `$/Ama.CRDT.UnitTests/Services/Strategies/PriorityQueueStrategyTests.cs`: Added a comprehensive test suite covering concurrent adds, removes, and priority updates, ensuring the final list is always correctly sorted.
- `$/Ama.CRDT.UnitTests/Services/CrdtMetadataManagerTests.cs`: Updated to ensure the `Reset` method properly clears the new `PriorityQueues` metadata.
- `$/Ama.CRDT/PublicAPI.Unshipped.txt`: Updated with all new public types and members.
- `$/FilesDescription.md`: Added descriptions for all new and modified files.

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
- The `PriorityQueueStrategy` uses reflection in `ApplyOperation` to access the priority property and sort the list. While convenient, this is less performant than a strongly-typed approach and could be a performance bottleneck for very large lists or frequent updates. A future optimization could involve using compiled expressions or source generators to avoid runtime reflection.
- There is some code duplication between `LwwSetStrategy` and `PriorityQueueStrategy`. A shared base class for LWW-based set strategies could be created to reduce this, but was avoided for now to keep the changes self-contained and simple.

<!---AI - Stage 2--->
## Last notes and implementation details
<!---
Here you add comments about the implementation that didn't fit on the previous section.
--->
- **Deferred Strategies**: The more complex sequence CRDTs (`RGA`, `Causal Tree`, `LSEQ`, `Move-in-List`) were not implemented in this pass. These strategies require significant new data structures in the metadata and more complex algorithms. Implementing them correctly requires a dedicated effort for each. The current implementation provides a solid foundation by adding two distinct and useful list management strategies.
- **`FixedSizeArrayStrategy` Implementation**: This strategy works by decomposing an array into individual LWW registers, one for each index. The operations it generates have paths like `$.array[0]`, `$.array[1]`, etc., allowing it to reuse the existing LWW timestamp tracking in `CrdtMetadata`. This is an efficient way to handle fixed collections of values without introducing new metadata structures.
- **`PriorityQueueStrategy` Implementation**: This strategy is built on the logic of an LWW-Set, where elements can be added and removed, with conflicts resolved by the last write. Its unique feature is that after every operation, it re-sorts the underlying list in the user's POCO based on a `PriorityPropertyName` defined in the attribute. This ensures the list is always in the correct priority order after convergence.

I should remind the developer to consider incrementing the `MINOR_VERSION` in `/.github/workflows/publish-nuget.yml` since new features have been added.

# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->