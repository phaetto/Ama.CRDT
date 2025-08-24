<!---Human--->
# Purpose
<!---
Add the purpose of this user story.
--->
As a dev I want the readme file to be updated with all the surface APIs correctly.

<!---Human--->
# Requirements
<!---
Add the requirements, technical or not.
--->
- Make sure that the readme file is updated with the latest API calls/names/methods.
- Only output the readme file for this spec and remember to escape correctly inside the blocks.

<!---Human--->
## Requirements context
<!---
Add files that we will load for the UI to add context for the solution design.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
--->
- `C:\sources\Ama.CRDT\Ama.CRDT\*.cs`
- `C:\sources\Ama.CRDT\README.md`

<!---Human--->
# Testing Methodology
<!---
Add the testing methodology (manual, unit, integration, end-to-end tests?)
--->
- **Manual Verification**: The `README.md` file will be manually reviewed to ensure all code examples and API descriptions are accurate and reflect the changes made.

<!---AI - Stage 1--->
# Proposed Solutions [AI - Stage 1]
<!---
Here you will need to put a number of solutions that would fit for this problem.
Add the solutions that you rejected as well.
--->
### Solution 1: Comprehensive Manual Review and Rewrite (Recommended)
This approach involves a thorough, manual review of the entire public API surface of the `Ama.CRDT` library. I will compare the current implementation of services (like `ICrdtPatcherFactory`, `ICrdtPatcher`), models, and DI extensions against the existing `README.md`. The README will then be rewritten to ensure accuracy, clarity, and completeness, including updated code examples for all primary use cases.

*   **Pros:**
    *   Ensures the highest level of accuracy and completeness.
    *   Catches all new features, API changes, and breaking changes.
    *   Allows for improving the overall narrative and quality of the documentation.
*   **Cons:**
    *   More time-intensive than a targeted update.

### Solution 2: Incremental Targeted Update
This solution focuses on finding the specific API calls currently mentioned in the `README.md` and verifying if their signatures or behavior have changed. Only the sections of the README corresponding to changed APIs would be updated.

*   **Pros:**
    *   Faster to implement.
*   **Cons:**
    *   High risk of missing new public APIs that are not yet documented.
    *   May lead to an inconsistent documentation style.
    *   Doesn't address outdated architectural descriptions.

### Solution 3: Automated Documentation Generation (Rejected)
This approach would involve setting up a tool like DocFX to generate API documentation directly from the source code's XML comments and then integrating this into the README.

*   **Pros:**
    *   Accurate for API signatures.
*   **Cons:**
    *   Out of scope for this task; requires setting up new tooling.
    *   Generated documentation is often too "dry" for a user-friendly README and lacks the narrative and usage examples needed.
    *   Significant effort to integrate seamlessly into a handcrafted markdown file.

**Recommendation:** Solution 1 is the most appropriate. A `README.md` is the primary entry point for new users, and it must be comprehensive and accurate. The risk of leaving out new features with an incremental update is too high.

<!---AI - Stage 1--->
# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->
1.  **Analyze Public API Surface**: Review all public classes, interfaces, methods, and attributes in the `Ama.CRDT` project to build a complete picture of the current API. Key files to review are listed in the "Proposed Files Needed" section.
2.  **Identify Outdated Sections**: Compare the current API with the existing `README.md` to identify all discrepancies, including:
    *   DI setup in `ServiceCollectionExtensions`.
    *   Usage of `ICrdtPatcherFactory` to create replica-specific patchers.
    *   Methods on `ICrdtPatcher` and `ICrdtApplicator`.
    *   Fluent API of the `ICrdtPatchBuilder`.
    *   Available CRDT strategy attributes.
3.  **Rewrite Core Usage Examples**: Update the "Getting Started" and "Usage" sections with new code examples that reflect the current API for:
    *   Configuring the library via dependency injection.
    *   Creating a patch between two states of an object.
    *   Applying a patch to an object.
4.  **Update Advanced Topics**: Revise the sections on advanced usage, ensuring the examples for custom attributes (`CrdtLwwStrategy`, `CrdtCounterStrategy`, `CrdtArrayLcsStrategy`, `CrdtSortedSetStrategy`), manual patch creation, and extensibility points (`IElementComparer`) are correct.
5.  **Final Polish**: Perform a full read-through of the updated `README.md` to check for grammatical errors, formatting issues, and clarity. Ensure all code blocks are correctly formatted with language identifiers and special characters are escaped as per the project's standards (`````, `$`).

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
- `$/README.md` (The primary file to be modified)
- `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs` (To verify DI setup and configuration)
- `$/Ama.CRDT/Services/ICrdtPatcherFactory.cs` (To document the factory pattern for creating patchers)
- `$/Ama.CRDT/Services/ICrdtPatcher.cs` (To document patch creation)
- `$/Ama.CRDT/Services/ICrdtApplicator.cs` (To document patch application)
- `$/Ama.CRDT/Services/ICrdtPatchBuilder.cs` (To document the manual patch building API)
- `$/Ama.CRDT/Services/ICrdtService.cs` (To document the high-level service facade)
- `$/Ama.CRDT/Models/CrdtDocumentOfT.cs` (To explain the core data wrapper)
- `$/Ama.CRDT/Attributes/CrdtLwwStrategyAttribute.cs` (To provide examples for LWW strategy)
- `$/Ama.CRDT/Attributes/CrdtCounterStrategyAttribute.cs` (To provide examples for Counter strategy)
- `$/Ama.CRDT/Attributes/CrdtArrayLcsStrategyAttribute.cs` (To provide examples for Array LCS strategy)
- `$/Ama.CRDT/Attributes/CrdtSortedSetStrategyAttribute.cs` (To provide examples for Sorted Set strategy)

<!---AI - Stage 2--->
# Changes Done
<!---
Here you add detailed information about all the changes actually done.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
Add all the things that you did in a different way than expected.
--->
- `$/README.md`
	- Reviewed and updated all code examples to align with the latest public API surface (`ICrdtService`, `ICrdtPatcherFactory`, `ICrdtApplicator`, etc.).
	- Corrected the usage of the counter attribute from `[CrdtCounter]` to the correct `[CrdtCounterStrategy]` in the documentation and code examples.
	- Verified the DI setup instructions and examples for registering the library, custom comparers (`AddCrdtComparer`), and custom timestamp providers (`AddCrdtTimestampProvider`).
	- Updated the "Controlling Merge Behavior" section to accurately list and describe all available strategy attributes: `CrdtLwwStrategy`, `CrdtCounterStrategy`, `CrdtArrayLcsStrategy`, and `CrdtSortedSetStrategy`.
	- Clarified the behavior of `CrdtSortedSetStrategy` in the usage examples, explicitly mentioning that the resulting collection is sorted.
	- Ensured all descriptions in the "How It Works" section accurately reflect the role of each major service in the library.

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
No technical debt was identified or introduced. The task was purely documentation-related.

<!---AI - Stage 2--->
## Last notes and implementation details
<!---
Here you add comments about the implementation that didn't fit on the previous section.
--->
The update was a comprehensive review of the existing `README.md` against the current library's public API. The main correction was fixing the name of the `CrdtCounterStrategyAttribute`, which was incorrectly documented as `[CrdtCounter]`. All examples were verified to be functional and demonstrate the intended use cases, from simple, single-replica scenarios with `ICrdtService` to complex, multi-replica synchronization using the `ICrdtPatcherFactory`.

# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->
```
```