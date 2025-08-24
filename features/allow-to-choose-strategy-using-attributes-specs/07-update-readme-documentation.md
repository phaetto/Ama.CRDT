<!---Human--->
# Purpose
To update the main `README.md` file to thoroughly document the new attribute-based CRDT strategy feature. Clear documentation is essential for users to understand, adopt, and extend the new capabilities of the library.

<!---Human--->
# Requirements
- Update the `README.md` file in the project root.
- Add a new major section detailing the "CRDT Strategy" system.
- This section must explain the concept of using attributes on POCOs to control merge behavior.
- Provide clear, copy-pasteable C# code examples for:
    - Defining a POCO with properties using `[LwwStrategy]` (or no attribute for the default) and `[CrdtCounter]`.
    - Using the high-level `JsonCrdtService` (or refactored patcher/applicator) to generate and apply patches for instances of this POCO.
- Add a subsection on "Extensibility" or "Creating Custom Strategies".
- This subsection should briefly explain the steps to create a new strategy:
    1. Create a custom attribute inheriting from `CrdtStrategyAttribute`.
    2. Create a class implementing the `ICrdtStrategy` interface.
    3. Register the new strategy with the DI container.
- Review the entire README for any other sections that may be outdated due to the API changes.

<!---Human--->
## Requirements context
- `$/features/allow-to-choose-strategy-using-attributes-specs/01-crdt-strategy-attribute-and-interface.md`
- `$/features/allow-to-choose-strategy-using-attributes-specs/02-lww-strategy-implementation.md`
- `$/features/allow-to-choose-strategy-using-attributes-specs/03-counter-strategy-implementation.md`
- `$/features/allow-to-choose-strategy-using-attributes-specs/04-refactor-patcher-to-use-strategies.md`
- `$/features/allow-to-choose-strategy-using-attributes-specs/05-refactor-applicator-to-use-strategies.md`
- `$/features/allow-to-choose-strategy-using-attributes-specs/05-01-arraylcsstrategy-needs-to-check-deep-objects.md`: `05-01 ArrayLcsStrategy needs to check deep objects`
- `$/features/allow-to-choose-strategy-using-attributes-specs/05-02-manage-metadata-state-deifferently-in-strategies.md`: `05-02 Manage metadata state deifferently in strategies`
- `$/features/allow-to-choose-strategy-using-attributes-specs/05-03-make-sure-there-are-reset-functions-for-the-state-to-keep-it-small.md`: `05-03 Make sure are reset functions for the state to keep it small`
- `$/features/allow-to-choose-strategy-using-attributes-specs/06-create-benchmark-project.md`
- `C:\sources\Modern.CRDT\features\allow-to-choose-strategy-using-attributes-specs\06-01-optimize-the-application-benchmarks.md`
- `C:\sources\Modern.CRDT\features\allow-to-choose-strategy-using-attributes-specs\06-02-rewrite-node-management-to-reflection.md`
- `C:\sources\Modern.CRDT\Modern.CRDT\Services\CrdtService.cs`

<!---Human--->
# Testing Methodology
Manual review of the final `README.md` file by a developer. The review should check for clarity, accuracy of code examples, and completeness of the documentation for the new features.

<!---AI - Stage 1--->
# Proposed Solutions [AI - Stage 1]
<!---
Here you will need to put a number of solutions that would fit for this problem.
Add the solutions that you rejected as well.
--->
1.  **Recommended: Tiered Documentation Approach.** This solution involves structuring the `README.md` to cater to different user needs. It would start with a high-level "Usage" or "Quickstart" section that provides immediate, copy-pasteable code for the most common scenarios (LWW, Counter, Array LCS). This allows new users to get started quickly. Following this, a more detailed "Advanced Topics" or "Extensibility" section would explain the underlying interfaces (`ICrdtStrategy`), base classes (`CrdtStrategyAttribute`), and DI registration, guiding advanced users on how to create their own custom strategies. This layered approach is optimal because it serves both beginners and experts effectively without overwhelming the former.
2.  **Rejected: Comprehensive Monolith Approach.** This would involve writing a single, long-form guide that explains the entire feature set from the ground up: theory, interfaces, built-in strategies, examples, and finally, extensibility. While thorough, this approach forces all users, regardless of their immediate needs, to parse a large amount of information, which can be intimidating and inefficient for those who just want to implement a standard feature.
3.  **Rejected: API Reference Style.** This approach would document each component (`[LwwStrategy]`, `ICrdtStrategy`, `ServiceCollectionExtensions`, etc.) as a separate entry, much like auto-generated API documentation. This is too fragmented for a `README.md`, as it fails to provide a cohesive narrative or practical, end-to-end examples of how the components work together to solve a problem.

<!---AI - Stage 1--->
# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->
1.  **Analyze Existing `README.md`:** Review the current `README.md` to identify the best placement for the new "CRDT Strategies" section and to locate any existing content that has become outdated due to the recent API changes.
2.  **Add "Controlling Merge Behavior with CRDT Strategies" Section:**
    *   Introduce the core concept: using attributes on POCO properties to define conflict-resolution logic.
    *   Explain the built-in strategies:
        *   `[LwwStrategy]` (Last-Writer-Wins): The default for simple properties, can be omitted.
        *   `[CrdtCounter]` (PN-Counter): For numeric properties that should be incremented/decremented.
        *   `[CrdtArrayLcsStrategy]` (Array LCS): The default for collections, uses Longest Common Subsequence for intelligent diffing.
3.  **Provide a Complete Usage Example:**
    *   Define a sample POCO (e.g., `UserStats`) decorated with the various strategy attributes.
    *   Show the necessary DI setup using `services.AddModernCrdt()`.
    *   Demonstrate the end-to-end workflow:
        *   Create two replicas of the POCO.
        *   Use `ICrdtPatcher` to generate a patch.
        *   Use `ICrdtApplicator` to merge the patch.
        *   Assert that the state has converged correctly.
4.  **Add "Extensibility: Creating Custom Strategies" Subsection:**
    *   Briefly explain when and why a custom strategy might be needed.
    *   Clearly outline the three required steps with minimal, illustrative code snippets for each:
        1.  Create a custom attribute inheriting from `CrdtStrategyAttribute`.
        2.  Implement the `ICrdtStrategy` interface.
        3.  Register the new strategy in the DI container via `services.AddModernCrdtStrategy<TStrategy, TAttribute>()`.
5.  **Final Review:** Perform a full read-through of the modified `README.md` to ensure technical accuracy, clarity, and correct formatting of all code examples.

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
- `$/README.md` (The primary file to be modified.)
- `$/Modern.CRDT/Extensions/ServiceCollectionExtensions.cs` (To provide accurate DI registration examples for both the core library and custom strategies.)
- `$/Modern.CRDT/Services/Strategies/ICrdtStrategy.cs` (To correctly explain the contract for custom strategy implementations.)
- `$/Modern.CRDT/Attributes/CrdtStrategyAttribute.cs` (To show the base attribute class for extensibility examples.)
- `$/Modern.CRDT/Attributes/LwwStrategyAttribute.cs` (To use as a concrete example of a built-in strategy.)
- `$/Modern.CRDT/Attributes/CrdtCounterAttribute.cs` (To use as a concrete example of a built-in strategy.)
- `$/Modern.CRDT/Attributes/CrdtArrayLcsStrategyAttribute.cs` (To use as a concrete example of a built-in strategy.)
- `$/Modern.CRDT/Services/ICrdtPatcher.cs` (To document the public API for generating patches.)
- `$/Modern.CRDT/Services/ICrdtApplicator.cs` (To document the public API for applying patches.)
- `$/Modern.CRDT.ShowCase/Models/UserStats.cs` (To serve as a practical, real-world POCO for code examples.)
- `$/Modern.CRDT.ShowCase/Program.cs` (To reference a complete, working DI setup.)

<!---AI - Stage 2--->
# Changes Done
- `$/README.md`: Completely overhauled the file to document the new attribute-driven CRDT strategy system.
    - Updated the "Features" section to highlight POCO-first design, attribute-based strategies, and multi-replica support.
    - Rewrote the "Getting Started" and "Usage" sections with a new DI setup (`AddJsonCrdt`) and a comprehensive example demonstrating the end-to-end workflow with `ICrdtPatcherFactory`, `ICrdtPatcher`, `ICrdtApplicator`, and `ICrdtMetadataManager`.
    - Added a new major section, "Controlling Merge Behavior with CRDT Strategies," explaining the purpose of `[LwwStrategy]`, `[CrdtCounter]`, and `[CrdtArrayLcsStrategy]`.
    - Added a new "Extensibility" section detailing the three steps required to create and register a custom `ICrdtStrategy`.
    - Rewrote the "How It Works" section to accurately describe the roles of the new services (`ICrdtPatcher`, `ICrdtApplicator`, `ICrdtStrategyManager`, `ICrdtPatcherFactory`, `ICrdtMetadataManager`).
<!---AI - Stage 2--->
# Manual Changes Needed
No manual changes needed to be applied.
<!---AI - Stage 2--->
## Possible Techical Debt
The dependency injection for custom strategies is functional but could be improved. A dedicated extension method like `services.AddCrdtStrategy<TStrategy, TAttribute>()` would provide a more fluent and discoverable API for developers looking to extend the library, reducing the chance of misconfiguration.
<!---AI - Stage 2--->
## Last notes and implementation details
The new `README.md` adopts the "Tiered Documentation Approach" as proposed. It starts with a simple "Getting Started" guide and a clear usage example for common cases. It then progresses to more advanced topics like creating custom strategies in the "Extensibility" section. This structure caters to both new users who need to get running quickly and advanced users who require deeper customization.
The code examples were crafted to be practical and copy-pasteable, using a `UserStats` POCO that showcases all major built-in strategies (`LwwStrategy`, `CrdtCounter`, `CrdtArrayLcsStrategy`).
The documentation for registering a custom strategy reflects the current implementation, requiring two separate DI registrations. This is accurate but highlights an area for future API improvement.

# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->