<!---Human--->
# Purpose
<!---
Add the purpose of this user story.
--->
As a DEV I want the partition manager to be data neutral. Right now it assumes that the structure is a dictionary.

<!---Human--->
# Requirements
<!---
Add the requirements, technical or not.
--->
- Refactor `IPartitionableCrdtStrategy` to be able to manage data and metadata of a partition that is about to be merged or split.
- `PartitionManager` should check if partitions need to me merged
- On private methods for `PartitionManager`, when merging or spliting partitions, the data/metadata management should be passed to the relative strategy.

<!---Human--->
## Requirements context
<!---
Add files that we will load for the UI to add context for the solution design.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
--->
- `C:\sources\Ama.CRDT\Ama.CRDT\Services\Partitioning\PartitionManager.cs` (The partition manager implementation)
- `C:\sources\Ama.CRDT\Ama.CRDT\Services\Partitioning\IPartitionableCrdtStrategy.cs` (The crdt strategy interface)
- `C:\sources\Ama.CRDT\Ama.CRDT\Services\Strategies\OrMapStrategy.cs` (The only implementation of the interface)

<!---Human--->
# Testing Methodology
<!---
Add the testing methodology (manual, unit, integration, end-to-end tests?)
--->
- Unit tests will be created/updated for `PartitionManager` to verify that it correctly delegates partition management (split/merge) logic to the strategy.
- Unit tests for `OrMapStrategy` will be updated to cover its implementation of the new data and metadata splitting and merging logic.

<!---AI - Stage 1--->
# Proposed Solutions [AI - Stage 1]
<!---
Here you will need to put a number of solutions that would fit for this problem.
Add the solutions that you rejected as well.
--->
### Solution 1: Extend `IPartitionableCrdtStrategy` (Recommended)
This approach involves extending the `IPartitionableCrdtStrategy` interface with new methods responsible for handling the data and metadata logic during partition splits and merges.

- **Description**: The interface will gain methods like `SplitDataAndMetadata`, `MergeDataAndMetadata`, `IsOverfull`, and `IsUnderfull`. The `PartitionManager` will call these methods, delegating the data-specific operations to the strategy while retaining control over the partition lifecycle (e.g., creating/deleting partitions, updating the B+ Tree index).
- **Pros**:
    - Clean separation of concerns: `PartitionManager` handles the "what" (managing partition structures), and the strategy handles the "how" (manipulating the data within).
    - Minimal architectural changes, building upon the existing design.
    - Keeps all partitioning logic for a strategy within that strategy's class.
- **Cons**:
    - The `IPartitionableCrdtStrategy` interface becomes larger.

### Solution 2: Introduce a New `IPartitionDataHandler` Interface
This approach suggests creating a new interface specifically for data manipulation within partitions.

- **Description**: An `IPartitionDataHandler` interface would define methods for splitting and merging data. The `IPartitionableCrdtStrategy` would have a property that returns an implementation of this handler. The `PartitionManager` would then interact with this handler.
- **Pros**:
    - Follows the Single Responsibility Principle very strictly by separating data handling from strategy logic.
- **Cons**:
    - Adds an extra layer of abstraction that is likely unnecessary, as the data handling logic is intrinsically tied to the strategy itself.
    - Complicates the design and implementation, as we now have to manage and resolve another service.

### Solution 3: Use Generic Delegates
This approach would involve passing delegates to the `PartitionManager` for data manipulation.

- **Description**: The `PartitionManager` could be configured with `Func` or `Action` delegates for splitting and merging data/metadata. The strategy would be responsible for providing these delegates.
- **Pros**:
    - Highly flexible and decoupled.
- **Cons**:
    - Can lead to a complex and less readable API for the `PartitionManager`.
    - It's an unconventional pattern for this kind of problem within a dependency-injected architecture and can obscure the flow of logic.

**Recommendation:** Solution 1 is the most pragmatic and clean solution. It logically places the data management responsibilities with the component that understands the data's structure (the strategy) without over-engineering the system.

<!---AI - Stage 1--->
# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->
1.  **Modify `IPartitionableCrdtStrategy` Interface**:
    - Add `bool IsOverfull(Partition partition)`: Determines if a partition has exceeded its capacity and needs to be split.
    - Add `bool IsUnderfull(Partition partition)`: Determines if a partition is underutilized and could be merged with a sibling.
    - Add `(object newPartitionData, object newPartitionMetadata) Split(Partition partition)`: Takes a partition that is overfull, modifies its data and metadata in-place to contain the first half of the items, and returns the data and metadata for a new partition containing the second half.
    - Add `void Merge(Partition target, Partition source)`: Merges the data and metadata from the `source` partition into the `target` partition.

2.  **Update `OrMapStrategy` Implementation**:
    - Implement the four new methods from the `IPartitionableCrdtStrategy` interface.
    - `IsOverfull`/`IsUnderfull` will check the count of the dictionary in the partition's data against pre-defined thresholds.
    - `Split` will find a median key in the dictionary, move all items after it to a new dictionary, and do the same for associated metadata. It will return the new dictionary and new metadata.
    - `Merge` will combine the dictionaries and metadata from the source and target partitions.

3.  **Refactor `PartitionManager`**:
    - In the `ApplyOperationAsync` method, after applying an operation, add checks using `IsOverfull` to trigger a split and `IsUnderfull` to trigger a merge.
    - Create a new private method `MergePartitionsAsync` which will be called when `IsUnderfull` returns true. This method will find a suitable sibling partition, call the strategy's `Merge` method, update the B+ Tree index to remove the merged partition, and delete the old partition file.
    - Modify the existing private method for splitting partitions (`SplitPartitionIfNeededAsync` or similar) to use the strategy's `Split` method to get the data for the new partition, instead of performing dictionary-specific logic itself.

4.  **Update Unit Tests**:
    - In `PartitionManagerTests.cs`, update existing tests for splitting to use a mock of the `IPartitionableCrdtStrategy`'s new methods.
    - Add new tests to `PartitionManagerTests.cs` to verify the merge logic, ensuring the manager calls the strategy's `Merge` method and correctly updates the index.
    - In `OrMapStrategyTests.cs`, add new tests to directly validate the `Split` and `Merge` implementations within the strategy to ensure data and metadata are handled correctly.

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
- `$/Ama.CRDT/Services/Partitioning/IPartitionableCrdtStrategy.cs` (The main interface to be modified)
- `$/Ama.CRDT/Services/Partitioning/PartitionManager.cs` (The class whose logic will be made data-neutral)
- `$/Ama.CRDT/Services/Strategies/OrMapStrategy.cs` (The reference implementation of the partitionable strategy)
- `$/Ama.CRDT/Models/Partitioning/Partition.cs` (The model representing a partition, might be needed for context)
- `$/Ama.CRDT.UnitTests/Services/Partitioning/PartitionManagerTests.cs` (Unit tests for the PartitionManager that need to be updated)
- `$/Ama.CRDT.UnitTests/Services/Strategies/OrMapStrategyTests.cs` (Unit tests for the OrMapStrategy that need to be updated for the new partitioning logic)

<!---AI - Stage 2--->
# Changes Done
<!---
Here you add detailed information about all the changes actually done.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
Add all the things that you did in a different way than expected.
--->
- `$/Ama.CRDT/Models/Partitioning/PartitionContent.cs` & `$/Ama.CRDT/Models/Partitioning/SplitResult.cs`: Created new DTOs to replace tuples for return types in partitioning logic, improving code clarity and maintainability.
- `$/Ama.CRDT/Services/Partitioning/IPartitionableCrdtStrategy.cs`: The interface was redesigned to be more functional and robust. Instead of methods like `IsOverfull`, the new interface focuses purely on data transformation (`GetStartKey`, `Split`, `Merge`), leaving the `PartitionManager` in charge of I/O and index management. Return types were changed from tuples to the new DTOs.
- `$/Ama.CRDT/Services/Partitioning/PartitionManager.cs`: This class was heavily refactored to be data-neutral. All dictionary-specific logic was removed. A new `MergePartitionIfNeededAsync` method was added to handle merging of underutilized partitions. The manager now discovers and uses the appropriate `IPartitionableCrdtStrategy` to delegate all data-specific operations, consuming the new DTOs.
- `$/Ama.CRDT/Services/Strategies/OrMapStrategy.cs`: Implemented the new `IPartitionableCrdtStrategy` interface, including the logic for splitting and merging dictionary-based data and their associated CRDT metadata. The method signatures were updated to use the new DTOs instead of tuples.
- `$/Ama.CRDT.UnitTests/Services/Partitioning/PartitionManagerTests.cs`: The existing tests were updated to work with the refactored code. A new integration test, `ApplyPatchAsync_WhenPartitionIsUnderMinSize_ShouldMergePartitions`, was added to verify the complete merge workflow.
- `$/Ama.CRDT.UnitTests/Services/Strategies/OrMapStrategyTests.cs`: New unit tests were added to directly validate the behavior of the new `Split` and `Merge` methods in `OrMapStrategy`, ensuring both data and metadata are handled correctly and verifying the new DTO return types.

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
- The `PartitionManager` currently rebuilds the entire B+ Tree index from scratch after every split or merge operation. This is inefficient for large numbers of partitions. The `IPartitioningStrategy` and its `BPlusTreePartitioningStrategy` implementation should ideally support more granular operations like `Delete` and `Update` to avoid full rebuilds.
- The logic to find a sibling partition for merging is basic and always chooses an adjacent partition. More sophisticated heuristics could be developed to select better merge candidates, for example, by considering the size of multiple neighbors.

<!---AI - Stage 2--->
## Last notes and implementation details
<!---
Here you add comments about the implementation that didn't fit on the previous section.
--->
The decision was made to remove the `IsOverfull` and `IsUnderfull` methods from the `IPartitionableCrdtStrategy` interface. The `PartitionManager` already tracks the byte size of each partition's data and has configurable thresholds (`MaxPartitionDataSize`, `MinPartitionDataSize`). Placing the decision logic of *when* to split or merge within the manager simplifies the strategy's role, which is now solely focused on *how* to perform the data manipulation. This creates a clearer separation of concerns.

The `PartitionManager` now eagerly discovers the single partitionable property and its associated strategy in its constructor. This fails fast if the managed type `T` is not configured for partitioning, which is a desirable behavior.

# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->