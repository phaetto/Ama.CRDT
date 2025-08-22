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
# Proposed Solutions [AI - Stage 1]
<!---
Here you will need to put a number of solutions that would fit for this problem.
Add the solutions that you rejected as well.
--->

<!---AI - Stage 1--->
# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->

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

<!---AI - Stage 2--->
# Changes Done
<!---
Here you add detailed information about all the changes actually done.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
Add all the things that you did in a different way than expected.
--->

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

<!---AI - Stage 2--->
## Possible Techical Debt
<!---
Here you add comments about possible technical debt you encountered or implemented but it was too much to change or out of scope.
--->

<!---AI - Stage 2--->
## Last notes and implementation details
<!---
Here you add comments about the implementation that didn't fit on the previous section.
--->

# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->