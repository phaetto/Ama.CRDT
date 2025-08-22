<!---Human--->
# Purpose
To establish the foundational components for a strategy-based approach to CRDT patch generation and application. This involves creating a base attribute to mark properties on POCOs and defining a common interface that all merge/patch strategies must implement. This infrastructure will allow for extensible and customizable CRDT behavior.

<!---Human--->
# Requirements
- Create a new abstract base attribute class named `CrdtStrategyAttribute` that inherits from `System.Attribute`.
- This attribute should be applicable only to properties (`AttributeTargets.Property`).
- Define a new public interface named `ICrdtStrategy`.
- The `ICrdtStrategy` interface must define the contract for handling both patch creation and application for a property.
- The interface should have methods to:
    - Generate a list of `CrdtOperation`s by comparing the old and new values of a property.
    - Apply a `CrdtOperation` to a target `JsonNode`.
- These components should be placed in appropriate new folders within the `Modern.CRDT` project (e.g., `Attributes` and `Services/Strategies`).

<!---Human--->
## Requirements context
<!---
Add files that we will load for the UI to add context for the solution design.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
--->

<!---Human--->
# Testing Methodology
- Unit tests will be created to verify the behavior of any concrete implementations of the `ICrdtStrategy` interface.
- Reflection-based tests to ensure the `CrdtStrategyAttribute` can be correctly identified on POCO properties.

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