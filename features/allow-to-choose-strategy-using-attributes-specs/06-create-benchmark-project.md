<!---Human--->
# Purpose
To create a dedicated benchmark project to measure and monitor the performance of the CRDT library. This is crucial to ensure that the new reflection-based strategy pattern does not introduce significant overhead and that the core operations remain highly performant.

<!---Human--->
# Requirements
- Create a new .NET Console Application project named `Modern.CRDT.Benchmarks` within the solution.
- Add the `BenchmarkDotNet` NuGet package to this project.
- Implement benchmark classes that cover the key public APIs:
    - Patch Generation: `JsonCrdtPatcher.GeneratePatch<T>`
    - Patch Application: `JsonCrdtApplicator.ApplyPatch<T>`
- The benchmarks should include scenarios with:
    - Simple POCOs with a few properties.
    - Complex, nested POCOs to test recursion and path generation performance.
    - POCOs using a mix of default (LWW) and custom (Counter) strategies.
- The results should be used to identify any performance bottlenecks in the reflection or strategy resolution logic.

<!---Human--->
## Requirements context
- `$/features/allow-to-choose-strategy-using-attributes-specs/01-crdt-strategy-attribute-and-interface.md`
- `$/features/allow-to-choose-strategy-using-attributes-specs/02-lww-strategy-implementation.md`
- `$/features/allow-to-choose-strategy-using-attributes-specs/03-counter-strategy-implementation.md`
- `$/features/allow-to-choose-strategy-using-attributes-specs/04-refactor-patcher-to-use-strategies.md`
- `$/features/allow-to-choose-strategy-using-attributes-specs/05-refactor-applicator-to-use-strategies.md`

<!---Human--->
# Testing Methodology
The testing methodology is the benchmark process itself. By running the benchmarks using `BenchmarkDotNet`, we will get detailed performance statistics (mean execution time, allocations, etc.) for the core CRDT operations. The results will be analyzed to validate performance goals. This project will not have traditional unit tests.

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