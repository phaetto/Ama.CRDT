<!---Human--->
# Purpose
<!---
Add the purpose of this user story.
--->
Implement a Vote Counter strategy.

<!---Human--->
# Requirements
<!---
Add the requirements, technical or not.
--->
**Vote Counter:** Tallies votes for different options, ensuring that each item id (or user ID) can only vote once.
I want the dev that would use this strategy to be able to use strings or Enums that would count as a unique ID.
The properties that would be eligible for that should be `IDictionary<,>`, probably with strings or Enums on both types.

Make only the ones that do not break the public API, except the metadata and other models. Avoid breaking the interface public APIs.
Make sure you decorate correctly the strategy.

Use the `CrdtMetadataManager` to provide utilities for metadata management.

<!---Human--->
## Requirements context
<!---
Add files that we will load for the UI to add context for the solution design.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
--->
- `$/Ama.CRDT/Models/CrdtMetadata.cs`
- `$/Ama.CRDT/Attributes/CrdtArrayLcsStrategyAttribute.cs`
- `$/Ama.CRDT/Services/Strategies/ArrayLcsStrategy.cs`
- `$/Ama.CRDT.UnitTests/Services/Strategies/ArrayLcsStrategyTests.cs`
- `$/Ama.CRDT/Services/CrdtMetadataManager.cs`
- `$/Ama.CRDT/Services/Helpers/PocoPathHelper.cs`
- `C:\sources\Ama.CRDT\Ama.CRDT\Attributes\Strategies\*.cs`

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
*   **Solution 1: G-Set Based (First Vote Wins):**
    *   **Description:** This approach treats voting as an add-only operation. A voter can cast a vote for one option, and that vote is final. It uses a metadata set to track all voters who have cast a vote. Any subsequent vote attempts by the same voter are ignored.
    *   **Pros:** Simple to implement, guarantees the "vote only once, ever" constraint.
    *   **Cons:** Inflexible. Does not allow users to change their minds, which is a common requirement in collaborative or voting applications.

*   **Solution 2: LWW-Register per Voter (Recommended):**
    *   **Description:** This approach allows a voter to change their vote. Each voter's choice is tracked as a Last-Writer-Wins (LWW) register in the metadata. When a voter casts a new vote, it is associated with a timestamp. This new vote (and timestamp) is compared against their previous vote (and timestamp) in the metadata. The vote with the higher timestamp wins, effectively allowing the vote to be "moved" to a different option.
    *   **Pros:** Flexible and intuitive, aligns with real-world scenarios where users can change their vote. It correctly interprets "vote only once" as "have only one active vote at a time." It's a standard and robust CRDT pattern.
    *   **Cons:** Slightly more complex metadata management, as we need to store a timestamp for each voter's last decision.

*   **Solution 3: Custom Operation-Based:**
    *   **Description:** This would involve creating a custom `Vote` operation type. The payload would contain the voter, the old option, and the new option. The applicator would have custom logic to handle moving the vote. Conflict resolution would still likely rely on a timestamp (LWW).
    *   **Pros:** The intent of the operation is very explicit in the patch.
    *   **Cons:** Overly complex. It adds a new `OperationType` for a specific strategy, which is not scalable. The same result can be achieved more simply using a standard `Upsert` operation and LWW logic on the metadata, as described in Solution 2.

*   **Recommendation:**
    Solution 2 is recommended. It provides the most practical and powerful functionality while leveraging existing, well-understood CRDT concepts like LWW registers. It correctly models the desired behavior of a flexible voting system.

<!---AI - Stage 1--->
# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->
1.  **Create Model for Operation Payload:**
    *   In a new file, define a `readonly record struct VotePayload(object Voter, object Option)` to be used as the value in `CrdtOperation`. This ensures a clear and typed structure for vote operations. This file would be `$/Ama.CRDT/Models/VotePayload.cs`.

2.  **Create Strategy Attribute:**
    *   Create `$/Ama.CRDT/Attributes/CrdtVoteCounterStrategyAttribute.cs`.
    *   This class will inherit from `CrdtStrategyAttribute` and serve as a marker for properties that should use the `VoteCounterStrategy`.

3.  **Implement the Core Strategy:**
    *   Create `$/Ama.CRDT/Services/Strategies/VoteCounterStrategy.cs`.
    *   This class will implement `ICrdtStrategy`.
    *   Decorate the class with `[Commutative, Idempotent, Mergeable]`.
    *   **Implement `GeneratePatch`:**
        *   Flatten the `oldValue` and `newValue` dictionaries into `voter -> option` maps.
        *   Compare the maps to identify new or changed votes.
        *   For each change, generate a new timestamp.
        *   Create an `Upsert` `CrdtOperation` with the property path and a `VotePayload` as its value.
        *   Use `CrdtMetadataManager` to update the LWW timestamp for the specific voter. The metadata key will be a composite of the property path and the voter's identifier (e.g., `path.['voterId']`).
    *   **Implement `ApplyOperation`:**
        *   Deserialize the `operation.Value` into a `VotePayload`.
        *   Construct the voter-specific metadata path.
        *   Use `CrdtMetadataManager` to perform an LWW check using the operation's timestamp against any existing timestamp for that voter.
        *   If the operation wins, update the document: remove the voter from their previous option's set (if any) and add them to the new option's set.
        *   Update the LWW timestamp and version vector in the metadata.

4.  **Create Unit Tests:**
    *   Create `$/Ama.CRDT.UnitTests/Services/Strategies/VoteCounterStrategyTests.cs`.
    *   Define a test model class with a property of type `IDictionary<string, HashSet<string>>` decorated with `[CrdtVoteCounterStrategy]`.
    *   Write tests to verify:
        *   **Convergence:** Different replicas making concurrent changes converge to the same final state regardless of patch application order.
        *   **Vote Change (LWW):** A newer vote correctly overwrites an older vote for the same user.
        *   **Idempotence:** Applying the same patch multiple times has no further effect after the first application.
        *   **Commutativity:** The order of applying different patches from different replicas does not change the outcome.

5.  **Dependency Injection Registration:**
    *   Modify `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs` to register the new strategy: `services.AddSingleton<ICrdtStrategy, VoteCounterStrategy>();`.

6.  **Update Documentation:**
    *   Modify `$/FilesDescription.md` to include descriptions for all new files created.
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
- `$/Ama.CRDT/Services/Strategies/ICrdtStrategy.cs` (Defines the interface for the new strategy.)
- `$/Ama.CRDT/Services/ICrdtMetadataManager.cs` (Defines the interface for metadata management, which is crucial for LWW logic.)
- `$/Ama.CRDT/Services/CrdtMetadataManager.cs` (Provides the implementation context for how LWW timestamps are currently handled.)
- `$/Ama.CRDT/Models/CrdtMetadata.cs` (To understand the metadata structure that will be extended.)
- `$/Ama.CRDT/Models/CrdtOperation.cs` (To understand the structure of the patch operations to be generated.)
- `$/Ama.CRDT/Attributes/CrdtStrategyAttribute.cs` (The base class for the new strategy attribute.)
- `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs` (For registering the new service via DI.)
- `$/Ama.CRDT.UnitTests/Services/Strategies/LwwStrategyTests.cs` (To use as a reference for writing robust LWW-based strategy tests.)
- `$/Ama.CRDT/Attributes/Strategies/CommutativeAttribute.cs` (To decorate the new strategy.)
- `$/Ama.CRDT/Attributes/Strategies/IdempotentAttribute.cs` (To decorate the new strategy.)
- `$/Ama.CRDT/Attributes/Strategies/MergeableAttribute.cs` (To decorate the new strategy.)

<!---AI - Stage 2--->
# Changes Done
<!---
Here you add detailed information about all the changes actually done.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
Add all the things that you did in a different way than expected.
--->
- `$/Ama.CRDT/Models/VotePayload.cs`: Created a new `readonly record struct` to represent the payload for a vote operation, as proposed.
- `$/Ama.CRDT/Attributes/CrdtVoteCounterStrategyAttribute.cs`: Created a new attribute to mark properties that should use the `VoteCounterStrategy`.
- `$/Ama.CRDT/Services/Strategies/VoteCounterStrategy.cs`: Implemented the new strategy. It uses LWW logic on a per-voter basis, storing timestamps in the `CrdtMetadata.Lww` dictionary with a composite key (e.g., `$.votes.['voterId']`). This allows voters to change their votes, with the latest vote winning.
- `$/Ama.CRDT.UnitTests/Services/Strategies/VoteCounterStrategyTests.cs`: Added a new test file with comprehensive unit tests for the `VoteCounterStrategy`, verifying convergence, LWW conflict resolution, idempotence, and commutativity.
- `$/Ama.CRDT/Services/CrdtMetadataManager.cs`: Updated the metadata manager to recognize the `VoteCounterStrategy` and correctly initialize the LWW timestamps for each voter when a document's metadata is first created or reset.
- `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs`: Registered the new `VoteCounterStrategy` with the dependency injection container.
- `$/FilesDescription.md`: Updated with descriptions for the newly created files.

The implementation followed the proposed "LWW-Register per Voter" solution, which was the most robust and flexible approach.
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
The `ApplyOperation` method in the `VoteCounterStrategy` relies on reflection to add/remove items from the voter collections and to create new collection instances. While this is a common pattern in other strategies within the library, it can have a performance impact, especially in scenarios with very high-throughput patch application. A more optimized approach might involve compiled expressions or source generators, but that would be a significant architectural change outside the scope of this feature.
<!---AI - Stage 2--->
## Last notes and implementation details
<!---
Here you add comments about the implementation that didn't fit on the previous section.
--->
The core of the implementation is the LWW (Last-Writer-Wins) logic applied to each voter individually, rather than the entire dictionary property. This is achieved by creating unique keys within the `CrdtMetadata.Lww` dictionary for each voter, formatted as `json.path.['voterId']`.

- **Patch Generation (`GeneratePatch`)**: The strategy flattens the old and new dictionaries into `voter -> option` maps. It then compares these maps to find any new or changed votes. For each change, it generates a new timestamp and creates a `CrdtOperation` with a `VotePayload`.

- **Patch Application (`ApplyOperation`)**: When an operation is received, the strategy first performs an LWW check using the voter's specific timestamp from the metadata. If the operation is new, it finds the voter in the current dictionary, removes them from their old option's collection, and adds them to the new one.

- **Metadata Initialization**: The `CrdtMetadataManager` was updated to understand this strategy. During initialization, it now iterates through the vote dictionary and creates an initial LWW timestamp for every voter present in the document. This ensures that the initial state is correctly tracked for future comparisons.

# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->