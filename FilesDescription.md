| File Path | Description |
| --- | --- |
| `$/.gitignore` | No description provided. |
| `$/CodingStandards.md` | No description provided. |
| `$/features/allow-to-choose-strategy-using-attributes-specs/01-crdt-strategy-attribute-and-interface.md` | No description provided. |
| `$/features/allow-to-choose-strategy-using-attributes-specs/02-lww-strategy-implementation.md` | No description provided. |
| `$/features/allow-to-choose-strategy-using-attributes-specs/03-counter-strategy-implementation.md` | No description provided. |
| `$/features/allow-to-choose-strategy-using-attributes-specs/04-refactor-patcher-to-use-strategies.md` | No description provided. |
| `$/features/allow-to-choose-strategy-using-attributes-specs/05-refactor-applicator-to-use-strategies.md` | No description provided. |
| `$/features/allow-to-choose-strategy-using-attributes-specs/06-create-benchmark-project.md` | No description provided. |
| `$/features/allow-to-choose-strategy-using-attributes-specs/07-update-readme-documentation.md` | No description provided. |
| `$/features/allow-to-choose-strategy-using-attributes.md` | No description provided. |
| `$/features/i-want-to-create-a-crdt-structure-for-all-json-to-be-able-to-replicate-across-services-specs/01-core-crdt-data-structures.md` | No description provided. |
| `$/features/i-want-to-create-a-crdt-structure-for-all-json-to-be-able-to-replicate-across-services-specs/02-json-diff-and-patch-generation.md` | No description provided. |
| `$/features/i-want-to-create-a-crdt-structure-for-all-json-to-be-able-to-replicate-across-services-specs/03-json-patch-application.md` | No description provided. |
| `$/features/i-want-to-create-a-crdt-structure-for-all-json-to-be-able-to-replicate-across-services-specs/04-public-api-and-integration.md` | No description provided. |
| `$/features/i-want-to-create-a-crdt-structure-for-all-json-to-be-able-to-replicate-across-services-specs/put-the-lww-structures-in-metadata.md` | No description provided. |
| `$/features/i-want-to-create-a-crdt-structure-for-all-json-to-be-able-to-replicate-across-services.md` | No description provided. |
| `$/FilesDescription.md` | No description provided. |
| `$/Modern.CRDT.sln` | No description provided. |
| `$/Modern.CRDT.UnitTests/Modern.CRDT.UnitTests.csproj` | No description provided. |
| `$/Modern.CRDT.UnitTests/Services/JsonCrdtApplicatorTests.cs` | Contains unit tests for the `JsonCrdtApplicator` service. |
| `$/Modern.CRDT.UnitTests/Services/JsonCrdtPatcherTests.cs` | Contains unit tests for the `JsonCrdtPatcher` service, covering POCO-based diffing with CRDT strategies. |
| `$/Modern.CRDT.UnitTests/Services/JsonCrdtServiceTests.cs` | Contains end-to-end integration tests for the `JsonCrdtService`. |
| `$/Modern.CRDT.UnitTests/Services/Strategies/LwwStrategyTests.cs` | Contains unit tests for the non-recursive `LwwStrategy` implementation. |
| `$/Modern.CRDT/Attributes/CrdtStrategyAttribute.cs` | The base abstract attribute for marking properties with a specific CRDT merge strategy. Contains the strategy type. |
| `$/Modern.CRDT/Attributes/LwwStrategyAttribute.cs` | An attribute to explicitly mark a property to use the Last-Writer-Wins (LWW) strategy. |
| `$/Modern.CRDT/Extensions/ServiceCollectionExtensions.cs` | Provides DI extension methods for easy library setup, including registration of strategies and the strategy manager. |
| `$/Modern.CRDT/Models/CrdtDocument.cs` | Encapsulates a JSON document and its associated LWW metadata as `JsonNode`s. |
| `$/Modern.CRDT/Models/CrdtDocumentOfT.cs` | A generic version of `CrdtDocument` for working with POCOs. |
| `$/Modern.CRDT/Models/CrdtOperation.cs` | Represents a single CRDT operation in a patch, including the target JSON Path, type, value, and timestamp. |
| `$/Modern.CRDT/Models/CrdtPatch.cs` | Encapsulates a list of CRDT operations that represent the difference between two JSON documents. |
| `$/Modern.CRDT/Models/OperationType.cs` | Defines the types of operations (Upsert, Remove) for a CRDT patch. |
| `$/Modern.CRDT/Modern.CRDT.csproj` | No description provided. |
| `$/Modern.CRDT/Services/IJsonCrdtApplicator.cs` | Defines the contract for a service that applies a CRDT patch to a JSON document, respecting Last-Writer-Wins (LWW) semantics. |
| `$/Modern.CRDT/Services/IJsonCrdtPatcher.cs` | Defines the contract for a service that compares two POCOs and generates a CRDT patch based on property-specific strategies. |
| `$/Modern.CRDT/Services/IJsonCrdtService.cs` | Defines the public facade service for orchestrating CRDT operations, providing a high-level API for patch generation and merging. |
| `$/Modern.CRDT/Services/JsonCrdtApplicator.cs` | Implements the patch application logic, including path creation and Last-Writer-Wins (LWW) conflict resolution. |
| `$/Modern.CRDT/Services/JsonCrdtPatcher.cs` | Implements the logic to recursively compare two POCOs, using a strategy manager to delegate property-level comparisons. |
| `$/Modern.CRDT/Services/JsonCrdtService.cs` | Implements the high-level facade service for CRDT operations. |
| `$/Modern.CRDT/Services/Strategies/CrdtStrategyManager.cs` | Implements the strategy resolution logic, finding the correct strategy for a property via reflection or returning the default (LWW). |
| `$/Modern.CRDT/Services/Strategies/ICrdtStrategy.cs` | Defines the contract for a strategy that handles CRDT patch generation and application for a property. |
| `$/Modern.CRDT/Services/Strategies/ICrdtStrategyManager.cs` | Defines the contract for a service that resolves the appropriate CRDT strategy for a property. |
| `$/Modern.CRDT/Services/Strategies/LwwStrategy.cs` | Implements the non-recursive Last-Writer-Wins (LWW) strategy for comparing two nodes. |
| `$/README.md` | The main documentation for the Modern.CRDT library, including usage examples and an overview of the architecture. |
