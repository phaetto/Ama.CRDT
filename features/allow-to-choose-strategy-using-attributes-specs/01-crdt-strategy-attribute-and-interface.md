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
### Solution 1: Recommended - Unified `ICrdtStrategy` Interface
This approach defines a single, cohesive interface that encapsulates both the patch generation and application logic for a specific strategy.

-   **`ICrdtStrategy` Interface:**
    -   `IEnumerable<CrdtOperation> GeneratePatch(string path, JsonNode? originalValue, JsonNode? modifiedValue, JsonNode? originalMetadata, JsonNode? modifiedMetadata)`: This method is responsible for comparing the old and new states of a property (represented as `JsonNode`s) and generating the necessary `CrdtOperation`s. It includes metadata to handle conflict resolution (like LWW).
    -   `void ApplyOperation(JsonNode rootNode, JsonNode metadataNode, CrdtOperation operation)`: This method applies a single `CrdtOperation` to the document, using the strategy's specific logic (e.g., checking timestamps from the metadata before applying an upsert).

-   **`CrdtStrategyAttribute`:** An abstract base attribute that concrete strategy attributes will inherit from. It will hold an instance of the corresponding strategy.

**Reasoning:** This is the recommended approach because it aligns best with the single responsibility principle at the strategy level. A "strategy" (e.g., Last-Writer-Wins) dictates both how to detect a change and how to resolve a conflict when applying that change. These two functions are intrinsically linked and should reside together. This design is clean, extensible, and avoids unnecessary complexity.

### Solution 2: Rejected - Separate Patcher and Applicator Interfaces
This approach splits the logic into two distinct interfaces, `ICrdtPatcherStrategy` and `ICrdtApplicatorStrategy`.

-   **`ICrdtPatcherStrategy` Interface:** Would contain only the `GeneratePatch` method.
-   **`ICrdtApplicatorStrategy` Interface:** Would contain only the `ApplyOperation` method.
-   A concrete strategy class might implement one or both. The attribute would need a way to reference implementations of both interfaces.

**Reasoning for Rejection:** This approach unnecessarily separates tightly coupled logic. The rules for generating a patch are directly related to the rules for applying it. For instance, an LWW strategy generates a patch with a timestamp, and its application logic must know how to read and compare that timestamp. Separating the interfaces would lead to pairs of classes that always have to be used together, effectively defeating the purpose of the separation and adding boilerplate.

### Solution 3: Rejected - Generic `ICrdtStrategy<T>` Interface
This approach would make the strategy interface generic based on the property type.

-   **`ICrdtStrategy<T>` Interface:**
    -   `GeneratePatch(string path, T oldValue, T newValue, ...)`
    -   `ApplyOperation(...)` would still likely need to operate on `JsonNode`s, creating a disconnect.

**Reasoning for Rejection:** While it seems intuitive to work with POCO types, the core CRDT engine operates on a `JsonNode` representation to remain agnostic of the original data structure. Forcing each strategy to convert from `JsonNode` to a specific type `T` and back again would be inefficient and complex. It's more robust to have all strategies operate on the common `JsonNode` structure, which is the "lingua franca" of the CRDT system.

<!---AI - Stage 1--->
# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->
1.  **Create New Folders:**
    -   Create a new folder at `$/Modern.CRDT/Attributes`.
    -   Create a new folder at `$/Modern.CRDT/Services/Strategies`.

2.  **Create `CrdtStrategyAttribute.cs`:**
    -   Create the file `$/Modern.CRDT/Attributes/CrdtStrategyAttribute.cs`.
    -   Define a `public abstract class CrdtStrategyAttribute` that inherits from `System.Attribute`.
    -   Add the `[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]` attribute to the class definition.
    -   The class will be the base for all specific strategy attributes. It will not contain any methods initially, but will serve as a marker and base for future extensibility.

3.  **Create `ICrdtStrategy.cs`:**
    -   Create the file `$/Modern.CRDT/Services/Strategies/ICrdtStrategy.cs`.
    -   Define a `public interface ICrdtStrategy`.
    -   Add detailed XML documentation explaining the purpose of the interface and its methods.
    -   Define the first method: `IEnumerable<CrdtOperation> GeneratePatch(string path, JsonNode? originalValue, JsonNode? modifiedValue, JsonNode? originalMetadata, JsonNode? modifiedMetadata);`. This method will be responsible for diffing a property and creating operations.
    -   Define the second method: `void ApplyOperation(JsonNode rootNode, JsonNode metadataNode, CrdtOperation operation);`. This method will be responsible for applying a single operation to the target document according to the strategy's rules.

4.  **Update `FilesDescription.md`:**
    -   Add entries for the newly created files: `CrdtStrategyAttribute.cs` and `ICrdtStrategy.cs` with appropriate descriptions.

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
- `$/Modern.CRDT/Models/CrdtOperation.cs` (The `ICrdtStrategy` interface methods will use and return this model, so its definition is required.)
- `$/Modern.CRDT/Modern.CRDT.csproj` (To understand the project's structure, dependencies, and to ensure new folders/files are correctly placed.)
- `$/FilesDescription.md` (To update it with the new files that will be created.)

<!---AI - Stage 2--->
# Changes Done
<!---
Here you add detailed information about all the changes actually done.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
Add all the things that you did in a different way than expected.
--->
- `$/Modern.CRDT/Attributes/CrdtStrategyAttribute.cs`: Created the new abstract base attribute class as specified. It is marked for `AttributeTargets.Property` and serves as a base for custom strategy attributes.
- `$/Modern.CRDT/Services/Strategies/ICrdtStrategy.cs`: Created the new interface defining the contract for CRDT strategies, including `GeneratePatch` and `ApplyOperation` methods to handle both sides of the CRDT process.
- `$/FilesDescription.md`: Updated the file descriptions to include the two new files (`CrdtStrategyAttribute.cs` and `ICrdtStrategy.cs`).
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
None identified in this stage. The design is foundational and extensible.
<!---AI - Stage 2--->
## Last notes and implementation details
<!---
Here you add comments about the implementation that didn't fit on the previous section.
--->
The implementation follows the recommended solution (`Solution 1`) precisely.
The `CrdtStrategyAttribute` is abstract, enforcing that only concrete strategy attributes (to be defined later) can be used.
The `ICrdtStrategy` interface is designed to work directly with `JsonNode`s, which is the core data representation in the CRDT engine. This ensures strategies are decoupled from the specific POCO types and can operate universally on the JSON structure.
Detailed XML documentation has been added to both the attribute and the interface to guide future development and usage.
<!---AI - Stage 2--->
# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->