| File Path | Description |
| --- | --- |
| `$/.gitignore` | No description provided. |
| `$/CodingStandards.md` | No description provided. |
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
| `$/Modern.CRDT.UnitTests/Services/JsonCrdtPatcherTests.cs` | Contains unit tests for the `JsonCrdtPatcher` service, covering various JSON diffing scenarios. |
| `$/Modern.CRDT.UnitTests/Services/JsonCrdtServiceTests.cs` | Contains end-to-end integration tests for the `JsonCrdtService`. |
| `$/Modern.CRDT/Attributes/CrdtStrategyAttribute.cs` | The base abstract attribute for marking properties with a specific CRDT merge strategy. |
| `$/Modern.CRDT/Extensions/ServiceCollectionExtensions.cs` | Provides DI extension methods for easy library setup. |
| `$/Modern.CRDT/Models/CrdtDocument.cs` | Encapsulates a JSON document and its associated LWW metadata as `JsonNode`s. |
| `$/Modern.CRDT/Models/CrdtDocumentOfT.cs` | A generic version of `CrdtDocument` for working with POCOs. |
| `$/Modern.CRDT/Models/CrdtOperation.cs` | Represents a single CRDT operation in a patch, including the target JSON Path, type, value, and timestamp. |
| `$/Modern.CRDT/Models/CrdtPatch.cs` | Encapsulates a list of CRDT operations that represent the difference between two JSON documents. |
| `$/Modern.CRDT/Models/OperationType.cs` | Defines the types of operations (Upsert, Remove) for a CRDT patch. |
| `$/Modern.CRDT/Modern.CRDT.csproj` | No description provided. |
| `$/Modern.CRDT/Services/IJsonCrdtApplicator.cs` | Defines the contract for a service that applies a CRDT patch to a JSON document, respecting Last-Writer-Wins (LWW) semantics. |
| `$/Modern.CRDT/Services/IJsonCrdtPatcher.cs` | Defines the contract for a service that compares two JSON documents and generates a CRDT patch based on Last-Writer-Wins (LWW) semantics. |
| `$/Modern.CRDT/Services/IJsonCrdtService.cs` | Defines the public facade service for orchestrating CRDT operations, providing a high-level API for patch generation and merging. |
| `$/Modern.CRDT/Services/JsonCrdtApplicator.cs` | Implements the patch application logic, including path creation and Last-Writer-Wins (LWW) conflict resolution. |
| `$/Modern.CRDT/Services/JsonCrdtPatcher.cs` | Implements the logic to recursively compare two `JsonNode` objects and generate a list of `CrdtOperation`s. |
| `$/Modern.CRDT/Services/JsonCrdtService.cs` | Implements the high-level facade service for CRDT operations. |
| `$/Modern.CRDT/Services/Strategies/ICrdtStrategy.cs` | Defines the contract for a strategy that handles CRDT patch generation and application for a property. |
| `$/README.md` | The main documentation for the Modern.CRDT library, including usage examples and an overview of the architecture. |