<!---Human--->
# Purpose
To implement the default Last-Writer-Wins (LWW) strategy using the new strategy pattern infrastructure. This will involve creating a specific attribute and a strategy class that encapsulates the existing LWW logic, ensuring backward compatibility and providing a default behavior for properties without a specified strategy.

<!---Human--->
# Requirements
- Create a `LwwStrategyAttribute` class that inherits from the base `CrdtStrategyAttribute`. This attribute will be used to explicitly mark properties that should use LWW semantics.
- Create a `LwwStrategy` class that implements the `ICrdtStrategy` interface.
- The `LwwStrategy` implementation will reuse the current logic for comparing two `JsonNode`s and their corresponding timestamps from the metadata.
- It will generate `Upsert` or `Remove` `CrdtOperation`s based on which value has a more recent timestamp.
- The system should be designed to use `LwwStrategy` as the default strategy if a property on a POCO is not decorated with any `CrdtStrategyAttribute`.

<!---Human--->
## Requirements context
- `$/features/allow-to-choose-strategy-using-attributes-specs/01-crdt-strategy-attribute-and-interface.md`

<!---Human--->
# Testing Methodology
- Unit tests for the `LwwStrategy` class to ensure it correctly generates patches for various scenarios (add, update, remove, no-change).
- Integration tests will be updated to verify that the `JsonCrdtPatcher` correctly uses the `LwwStrategy` by default and when the `LwwStrategyAttribute` is explicitly used.

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
Remember to ask to load any unit tests if they are related to any files you will want to change.
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