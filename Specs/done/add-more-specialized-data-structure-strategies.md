<!---Human--->
# Purpose
<!---
Add the purpose of this user story.
--->
Specialized Data Structure Strategies

<!---Human--->
# Requirements
<!---
Add the requirements, technical or not.
--->
25. **Graph Strategy (Add-Only):** For distributed graphs, supporting concurrent additions of vertices and edges.
26. **2P-Graph Strategy:** A graph where vertices and edges can be both added and removed (using tombstones).
27. **Replicated Tree Strategy:** Manages a hierarchical tree structure, allowing nodes to be added, removed, and moved concurrently while preserving parent-child relationships.

Make only the ones that do not break the public API, except the metadata and other models. Avoid breaking the interface public APIs.

Use the `CrdtMetadataManager` to provide utilities for metadata management.

Add the new strategies to readme. ** Always remember to escape ``` blocks in readme! **

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
- `C:\sources\Ama.CRDT\Ama.CRDT.UnitTests\Services\Strategies\CounterStrategyTests.cs`
- `C:\sources\Ama.CRDT\Ama.CRDT\Services\CrdtMetadataManager.cs`
- `C:\sources\Ama.CRDT\Ama.CRDT\Services\Helpers\PocoPathHelper.cs`
- `C:\sources\Ama.CRDT\README.md`
- `C:\sources\Ama.CRDT\Ama.CRDT\Services\ICrdtPatcher.cs`

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
### Solution 1: Composition of Existing CRDT Primitives
- **Description:** This approach would involve representing the new data structures using combinations of existing primitives. For example, a graph could be a `Dictionary<TVertex, GSet<TEdge>>`. The strategies would then orchestrate operations on these underlying structures.
- **Rejected:** This would require complex, multi-property coordination which the current strategy architecture is not designed for. It would also create a leaky abstraction, forcing the user to structure their POCOs in a very specific and potentially unnatural way.

### Solution 2: Adjacency Matrix Representation
- **Description:** This approach would model graphs using a matrix structure where cells indicate the presence (or properties) of an edge between two vertices.
- **Rejected:** Adjacency matrices are inefficient for sparse graphs, which are a common use case. Managing a potentially large, multi-dimensional array as a CRDT would be complex and memory-intensive.

### Solution 3: Dedicated CRDT Data Models with Custom Operation Payloads (Recommended)
- **Description:** This solution introduces new, dedicated classes to represent the graph and tree structures (e.g., `CrdtGraph<TVertex, TEdgeData>`, `CrdtTree<TNode>`). The corresponding strategies will operate on these types. Instead of relying only on simple value replacements, the strategies will generate custom payloads for `CrdtOperation` objects to represent atomic actions like `AddVertex`, `AddEdge`, `MoveNode`, etc. This encapsulates the complexity within the strategy and provides a clean, expressive API for developers.
- **Reasoning:** This approach offers the best encapsulation and extensibility. It aligns with the existing architecture by using the `CrdtOperation` structure, but extends its expressiveness via custom payloads. It provides a clear contract for the user, avoids breaking changes to core interfaces like `ICrdtStrategy`, and allows for the most efficient and logical state management within the `CrdtMetadata` object.

<!---AI - Stage 1--->
# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->
1.  **Define New Data Models:**
    *   Create `CrdtGraph<TVertex, TEdge>`, `CrdtTree<TNode>` classes in `$/Ama.CRDT/Models/`. These will be the types users declare on their POCOs.
    *   Create supporting payload models: `GraphVertexPayload`, `GraphEdgePayload`, `TreeAddNodePayload`, `TreeMoveNodePayload` in `$/Ama.CRDT/Models/`.

2.  **Extend `CrdtMetadata`:**
    *   Modify `$/Ama.CRDT/Models/CrdtMetadata.cs` to store the state needed for the new strategies.
    *   For 2P-Graph: Add dictionaries for vertex and edge tombstones.
    *   For Replicated Tree: Add dictionaries to manage OR-Set tags for node existence and LWW timestamps for parent-child links to resolve move conflicts.

3.  **Implement Graph Strategies (Add-Only & 2P-Graph):**
    *   Create `CrdtGraphStrategyAttribute` and `CrdtTwoPhaseGraphStrategyAttribute`.
    *   Create `GraphStrategy` and `TwoPhaseGraphStrategy` in `$/Ama.CRDT/Services/Strategies/`.
    *   Implement `GeneratePatch` to detect added/removed vertices and edges, creating `Upsert` and `Remove` operations with custom payloads.
    *   Implement `ApplyOperation` to process these payloads, updating the `CrdtGraph` object and its associated metadata. The 2P strategy will use tombstones to prevent re-addition.

4.  **Implement Replicated Tree Strategy:**
    *   Create `CrdtReplicatedTreeStrategyAttribute`.
    *   Create `ReplicatedTreeStrategy` in `$/Ama.CRDT/Services/Strategies/`.
    *   Implement `GeneratePatch` to detect node additions, removals, and moves.
    *   Implement `ApplyOperation` using a combination of OR-Set logic (for node presence) and LWW logic (for resolving move conflicts).

5.  **Create Unit Tests:**
    *   For each new strategy, create a corresponding test file in `$/Ama.CRDT.UnitTests/Services/Strategies/`.
    *   Tests will cover concurrent operations (adds, removes, moves) from multiple replicas to ensure convergence.
    *   Verify idempotence, commutativity, and associativity properties for each strategy.

6.  **Update Core Services:**
    *   Register the new strategies in `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs`.
    *   Update the Roslyn analyzer in `$/Ama.CRDT.Analyzers/CrdtStrategyTypeAnalyzer.cs` to validate the new attributes and their target types. Add corresponding tests.

7.  **Update Documentation:**
    *   Modify `$/README.md` to add a new section for "Specialized Data Structure Strategies".
    *   Document each new strategy with a description, use case, and a code example.

8.  **Update Public API:**
    *   Add all new public classes and methods to `$/Ama.CRDT/PublicAPI.Unshipped.txt`.

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
- `$/Ama.CRDT/Models/CrdtMetadata.cs` (To be extended with new metadata fields for graphs and trees)
- `$/Ama.CRDT/Services/Strategies/ICrdtStrategy.cs` (The primary interface that all new strategies will implement)
- `$/Ama.CRDT/Services/Strategies/OrSetStrategy.cs` (To serve as a reference for implementing removal-with-tombstones logic for the tree strategy)
- `$/Ama.CRDT.UnitTests/Services/Strategies/OrSetStrategyTests.cs` (As a template for testing convergence with concurrent add/remove operations)
- `$/Ama.CRDT/Services/CrdtMetadataManager.cs` (May require updates to initialize and reset the new metadata structures)
- `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs` (For registering the new strategies into the DI container)
- `$/Ama.CRDT/PublicAPI.Unshipped.txt` (All new public types must be added to this file)
- `$/README.md` (To be updated with documentation for the new strategies)

<!---AI - Stage 2--->
# Changes Done
<!---
Here you add detailed information about all the changes actually done.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
Add all the things that you did in a different way than expected.
--->
- `$/Ama.CRDT/Models/CrdtGraph.cs`: (New file) Created the `CrdtGraph<TVertex, TEdgeData>` class and the supporting `Edge<TVertex, TEdgeData>` record to represent a graph structure.
- `$/Ama.CRDT/Models/CrdtTree.cs`: (New file) Created the `CrdtTree<TNode, TId>` class and the supporting `TreeNode<TNode, TId>` class to represent a hierarchical tree structure.
- `$/Ama.CRDT/Models/GraphPayload.cs`: (New file) Added `GraphVertexPayload` and `GraphEdgePayload` records for graph operations.
- `$/Ama.CRDT/Models/TreePayload.cs`: (New file) Added `TreeAddNodePayload`, `TreeRemoveNodePayload`, and `TreeMoveNodePayload` records for tree operations.
- `$/Ama.CRDT/Models/CrdtMetadata.cs`: (Modified) Extended with `TwoPhaseGraphs` and `ReplicatedTrees` dictionaries to store metadata for the new strategies.
- `$/Ama.CRDT/Attributes/CrdtGraphStrategyAttribute.cs`: (New file) Attribute for the add-only graph strategy.
- `$/Ama.CRDT/Attributes/CrdtTwoPhaseGraphStrategyAttribute.cs`: (New file) Attribute for the 2P-Graph strategy.
- `$/Ama.CRDT/Attributes/CrdtReplicatedTreeStrategyAttribute.cs`: (New file) Attribute for the replicated tree strategy.
- `$/Ama.CRDT/Services/Strategies/GraphStrategy.cs`: (New file) Implemented the G-Set based strategy for add-only graphs.
- `$/Ama.CRDT/Services/Strategies/TwoPhaseGraphStrategy.cs`: (New file) Implemented the 2P-Set based strategy for graphs that support removals.
- `$/Ama.CRDT/Services/Strategies/ReplicatedTreeStrategy.cs`: (New file) Implemented the combined OR-Set (for node existence) and LWW (for moves) strategy for trees.
- `$/Ama.CRDT/Services/CrdtMetadataManager.cs`: (Modified) Updated `Reset`, `Clone`, and `InitializeStrategyMetadata` to handle the new metadata structures for graphs and trees.
- `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs`: (Modified) Registered the three new strategies (`GraphStrategy`, `TwoPhaseGraphStrategy`, `ReplicatedTreeStrategy`) in the DI container.
- `$/Ama.CRDT.UnitTests/Services/Strategies/GraphStrategyTests.cs`: (New file) Added unit tests for `GraphStrategy` covering concurrent additions.
- `$/Ama.CRDT.UnitTests/Services/Strategies/TwoPhaseGraphStrategyTests.cs`: (New file) Added unit tests for `TwoPhaseGraphStrategy` covering concurrent add/remove and ensuring removed elements cannot be re-added.
- `$/Ama.CRDT.UnitTests/Services/Strategies/ReplicatedTreeStrategyTests.cs`: (New file) Added unit tests for `ReplicatedTreeStrategy` covering concurrent adds, LWW-based move resolution, and OR-Set based re-addition of nodes.
- `$/Ama.CRDT/PublicAPI.Unshipped.txt`: (Modified) Added all new public types to the unshipped API surface.
- `$/README.md`: (Modified) Added a new "Specialized Data Structure Strategies" section to document the new graph and tree strategies.

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
The new strategies rely heavily on `dynamic` and reflection to interact with the generic `CrdtGraph` and `CrdtTree` types. While this works, it bypasses compile-time type safety and can be less performant. A future refactoring could explore using compiled expressions or source generators to create type-specific accessors, improving both performance and maintainability. Additionally, the deserialization logic in the strategies for custom payloads is complex due to the need to handle `JsonElement` and generic types; this could be centralized into a shared helper service.

<!---AI - Stage 2--->
## Last notes and implementation details
<!---
Here you add comments about the implementation that didn't fit on the previous section.
--->
The implemented strategies follow the proposed design, encapsulating complex logic for managing graphs and trees.
- **GraphStrategy**: Behaves like a Grow-Only Set for both vertices and edges. It's simple and efficient for add-only scenarios.
- **TwoPhaseGraphStrategy**: Implements 2P-Set semantics. It uses tombstones in the metadata to prevent vertices and edges from being re-added after removal.
- **ReplicatedTreeStrategy**: This is a hybrid strategy. It uses OR-Set logic for node existence, which allows a node to be removed and re-added. Concurrently, it uses LWW logic to manage the parent of each node, ensuring that concurrent moves converge to the one with the latest timestamp. This combination provides a robust solution for collaborative tree editing.

Unit tests were created for each strategy to verify their convergence properties under concurrent operations, as required.

# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->