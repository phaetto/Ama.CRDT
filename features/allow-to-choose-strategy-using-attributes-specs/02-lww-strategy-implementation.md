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
1.  **Recommended: Strategy-Driven Refactoring of `JsonCrdtPatcher`**.
    *   **Description:** This approach involves creating the `LwwStrategyAttribute` and the `LwwStrategy` class as required. The core LWW comparison logic currently within `JsonCrdtPatcher` will be extracted and moved into the `LwwStrategy` class. The `JsonCrdtPatcher` will then be refactored to act as a coordinator. It will use reflection to inspect properties for any `CrdtStrategyAttribute`. If an attribute is found, it will instantiate and delegate the patch generation to the corresponding strategy. If no attribute is found, it will default to using the `LwwStrategy`.
    *   **Reason for Recommendation:** This is the cleanest and most scalable solution. It fully embraces the Strategy Pattern, centralizing the LWW logic in one place. It makes the default behavior explicit, avoids code duplication, and establishes a clear pattern for adding new strategies in the future without modifying the core patcher service.

2.  **Rejected: Hybrid Approach with Implicit Default Logic**.
    *   **Description:** In this solution, we would still create the `LwwStrategyAttribute` and `LwwStrategy`. However, the `JsonCrdtPatcher` would only delegate to a strategy class if an attribute is explicitly found on a property. If no attribute is present, it would fall back to using its own, original, embedded LWW comparison logic.
    *   **Reason for Rejection:** This approach violates the Don't Repeat Yourself (DRY) principle. The same LWW logic would exist in two places: inside `JsonCrdtPatcher` for the default case, and inside `LwwStrategy` for the explicit case. This leads to code duplication and increases the maintenance burden, as any change to the LWW logic would need to be applied in both locations.

3.  **Rejected: Attribute-Led Default Strategy**.
    *   **Description:** This approach would involve defining the default strategy within the base `CrdtStrategyAttribute` itself, perhaps via a static property or method. The `JsonCrdtPatcher` would then simply ask the attribute resolution logic to provide a strategy for a property, and that logic would be responsible for returning the default `LwwStrategy` instance if no specific attribute is defined.
    *   **Reason for Rejection:** This solution gives too much responsibility to the attribute. Attributes should ideally be simple metadata containers. The responsibility for selecting and applying a default strategy should reside within the service that consumes them (`JsonCrdtPatcher`), not within the metadata definition itself. This keeps concerns separated and makes the service's behavior more explicit and easier to understand.

<!---AI - Stage 1--->
# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->
1.  **Create `LwwStrategyAttribute.cs`**:
    *   Define a new sealed class `LwwStrategyAttribute` in `$/Modern.CRDT/Attributes/`.
    *   It will inherit from `CrdtStrategyAttribute`.
    *   Its constructor will pass `typeof(LwwStrategy)` to the base attribute, linking the attribute to its concrete strategy implementation.

2.  **Create `ICrdtStrategyManager.cs` and `CrdtStrategyManager.cs`**:
    *   Define a new service to manage strategy resolution. This decouples `JsonCrdtPatcher` from reflection logic.
    *   The `ICrdtStrategyManager` interface will define a method `GetStrategy(PropertyInfo propertyInfo)`.
    *   The `CrdtStrategyManager` implementation will contain the logic to inspect a `PropertyInfo` for a `CrdtStrategyAttribute`.
    *   If an attribute is found, it will resolve and return an instance of the specified strategy.
    *   If no attribute is found, it will return a new instance of `LwwStrategy` as the default. This service will be registered for dependency injection.

3.  **Create `LwwStrategy.cs`**:
    *   Define a new sealed class `LwwStrategy` in `$/Modern.CRDT/Services/Strategies/`.
    *   It will implement the `ICrdtStrategy` interface.
    *   Extract the existing node comparison and patch generation logic from `JsonCrdtPatcher.CompareNodes` and move it into the `LwwStrategy.GeneratePatch` method. This method will handle comparing timestamps and creating `Upsert` or `Remove` operations.

4.  **Refactor `JsonCrdtPatcher.cs`**:
    *   Inject `ICrdtStrategyManager` into the `JsonCrdtPatcher`'s constructor.
    *   Modify the POCO comparison logic. For each property, call `_crdtStrategyManager.GetStrategy(property)` to get the appropriate strategy.
    *   Delegate the patch generation for that property to the `GeneratePatch` method of the returned strategy instance.
    *   Remove the old, now-redundant LWW comparison logic from this class.

5.  **Update Service Registration**:
    *   In `ServiceCollectionExtensions.cs`, register the new `ICrdtStrategyManager` and `CrdtStrategyManager` as scoped services. Also, register `LwwStrategy` and any other future strategies as transient services so the manager can resolve them.

6.  **Create `LwwStrategyTests.cs`**:
    *   Create a new unit test file `$/Modern.CRDT.UnitTests/Services/Strategies/LwwStrategyTests.cs`.
    *   Add tests to comprehensively validate the `LwwStrategy.GeneratePatch` method, covering scenarios like adding a value, updating with a newer timestamp, ignoring an older timestamp, removing a value, and handling identical nodes.

7.  **Update `JsonCrdtPatcherTests.cs`**:
    *   Update existing tests to work with the refactored `JsonCrdtPatcher`.
    *   Add new tests using mock POCOs to verify that:
        *   Properties explicitly decorated with `[LwwStrategy]` correctly use the `LwwStrategy`.
        *   Properties with *no* CRDT attribute correctly fall back to using the default `LwwStrategy`.

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
- `$/Modern.CRDT/Services/JsonCrdtPatcher.cs` (This file contains the core LWW logic that needs to be extracted and will be refactored to use the strategy pattern.)
- `$/Modern.CRDT/Services/Strategies/ICrdtStrategy.cs` (This is the interface that the new `LwwStrategy` class will implement.)
- `$/Modern.CRDT/Attributes/CrdtStrategyAttribute.cs` (This is the base class that `LwwStrategyAttribute` will inherit from.)
- `$/Modern.CRDT/Extensions/ServiceCollectionExtensions.cs` (This file will need to be updated to register the new strategy manager and strategy classes for dependency injection.)
- `$/Modern.CRDT/Models/CrdtOperation.cs` (Required to understand the return type for strategy methods.)
- `$/Modern.CRDT/Models/CrdtDocument.cs` (Required to understand the data and metadata structure passed to the patcher.)
- `$/Modern.CRDT.UnitTests/Services/JsonCrdtPatcherTests.cs` (These tests need to be updated to accommodate the refactoring of `JsonCrdtPatcher` and to test the new default strategy behavior.)

<!---AI - Stage 2--->
# Changes Done
- `$/Modern.CRDT/Attributes/CrdtStrategyAttribute.cs`: Modified the base attribute to accept the `Type` of the strategy it represents, enabling the link between an attribute and its implementation.
- `$/Modern.CRDT/Attributes/LwwStrategyAttribute.cs`: Created a new attribute to explicitly mark properties for LWW strategy, inheriting from the base strategy attribute.
- `$/Modern.CRDT/Services/Strategies/ICrdtStrategyManager.cs` & `CrdtStrategyManager.cs`: Introduced a new manager service responsible for resolving which strategy to use for a given property. It inspects property attributes and defaults to `LwwStrategy` if no attribute is found. It uses the DI container to create strategy instances.
- `$/Modern.CRDT/Services/Strategies/LwwStrategy.cs`: Created the concrete `LwwStrategy` class. The non-recursive LWW comparison logic (checking timestamps and creating an operation) from the old `JsonCrdtPatcher` was moved here.
- `$/Modern.CRDT/Services/IJsonCrdtPatcher.cs`: The interface was updated to be generic (`GeneratePatch<T>`), reflecting the shift from `JsonNode`-based comparison to POCO-based comparison. The non-generic version was removed as it's superseded by the new strategy-based approach.
- `$/Modern.CRDT/Services/JsonCrdtPatcher.cs`: Completely refactored this class to be the orchestrator. It no longer contains any hardcoded comparison logic. Instead, it recursively traverses a POCO's properties, uses the `ICrdtStrategyManager` to get the correct strategy for each property, and delegates the patch generation for that property's value to the resolved strategy.
- `$/Modern.CRDT/Extensions/ServiceCollectionExtensions.cs`: Updated the DI registration to include the new `ICrdtStrategyManager` and the `LwwStrategy`.
- `$/Modern.CRDT.UnitTests/Services/Strategies/LwwStrategyTests.cs`: Added new unit tests to specifically validate the behavior of the isolated `LwwStrategy`.
- `$/Modern.CRDT.UnitTests/Services/JsonCrdtPatcherTests.cs`: Rewrote the tests for `JsonCrdtPatcher` to use POCO models instead of raw `JsonNode`s. The new tests verify that the patcher correctly traverses the object graph and invokes the strategy manager for each property.

<!---AI - Stage 2--->
# Manual Changes Needed
No manual changes needed to be applied.

<!---AI - Stage 2--->
## Possible Techical Debt
The current implementation of `JsonCrdtPatcher`'s `CompareJsonArrays` method defaults to using `LwwStrategy` for all array elements. This is because, during traversal, there is no `PropertyInfo` available for individual array items to resolve a potentially different strategy. A more advanced implementation could involve attributes on the collection property itself to specify a strategy for its elements, but this is out of the current scope.

<!---AI - Stage 2--->
## Last notes and implementation details
The core of this change was shifting `JsonCrdtPatcher` from a `JsonNode`-centric to a POCO-centric service. This was a necessary breaking change to fulfill the requirement of using attributes on POCO properties to define behavior. The `JsonCrdtPatcher` now acts as a recursive engine that navigates the POCO structure, while the new `ICrdtStrategy` implementations (starting with `LwwStrategy`) are responsible for the actual comparison logic at the property level. This creates a clean, extensible pattern for adding new CRDT algorithms in the future without modifying the core patching engine. The default behavior is now explicitly managed by the `CrdtStrategyManager`, which provides `LwwStrategy` when no other strategy is specified via an attribute.

# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->