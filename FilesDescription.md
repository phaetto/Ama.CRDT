| File Path | Description |
| --- | --- |
| `$/.gitignore` | No description provided. |
| `$/CodingStandards.md` | No description provided. |
| `$/Modern.CRDT.Benchmarks/Benchmarks/ApplicatorBenchmarks.cs` | Contains benchmarks for the `JsonCrdtApplicator` service. |
| `$/Modern.CRDT.Benchmarks/Benchmarks/PatcherBenchmarks.cs` | Contains benchmarks for the `JsonCrdtPatcher` service. |
| `$/Modern.CRDT.Benchmarks/Models/ComplexPoco.cs` | A complex data model with nested objects and arrays for benchmarking recursive and collection-based scenarios. |
| `$/Modern.CRDT.Benchmarks/Models/SimplePoco.cs` | A simple data model for benchmarking basic scenarios. |
| `$/Modern.CRDT.Benchmarks/Modern.CRDT.Benchmarks.csproj` | The project file for the benchmarks application. |
| `$/Modern.CRDT.Benchmarks/Program.cs` | The entry point for the benchmark runner. |
| `$/Modern.CRDT.ShowCase/Models/User.cs` | A simple data model representing a user, used as an element in the CRDT-managed array. |
| `$/Modern.CRDT.ShowCase/Models/UserStats.cs` | The main POCO representing the shared state, decorated with CRDT strategy attributes (`CrdtCounter`, `CrdtArrayLcsStrategy`, `LwwStrategy`). |
| `$/Modern.CRDT.ShowCase/Modern.CRDT.ShowCase.csproj` | The project file for the showcase console application. |
| `$/Modern.CRDT.ShowCase/Program.cs` | The main entry point of the console application, responsible for setting up dependency injection and starting the simulation. |
| `$/Modern.CRDT.ShowCase/Services/IInMemoryDatabaseService.cs` | Defines the contract for a simple in-memory key-value store to simulate persistence for each replica's state (document and metadata). |
| `$/Modern.CRDT.ShowCase/Services/InMemoryDatabaseService.cs` | An implementation of `IInMemoryDatabaseService` using `ConcurrentDictionary` to simulate a database for CRDT documents and metadata. |
| `$/Modern.CRDT.ShowCase/Services/UserByIdComparer.cs` | A custom implementation of `IJsonNodeComparer` that allows the `ArrayLcsStrategy` to identify unique `User` objects based on their `Id` property. |
| `$/Modern.CRDT.ShowCase/SimulationRunner.cs` | Orchestrates the distributed map-reduce simulation using concurrent producers, mappers, and convergers communicating via channels to demonstrate CRDT convergence. |
| `$/Modern.CRDT.UnitTests/Modern.CRDT.UnitTests.csproj` | No description provided. |
| `$/Modern.CRDT.UnitTests/Models/EpochTimestampTests.cs` | Contains unit tests for the `EpochTimestamp` implementation of `ICrdtTimestamp`. |
| `$/Modern.CRDT.UnitTests/Services/CrdtMetadataManagerTests.cs` | Contains unit tests for the `CrdtMetadataManager`, verifying LWW pruning and version vector advancement logic. |
| `$/Modern.CRDT.UnitTests/Services/JsonCrdtApplicatorTests.cs` | Contains unit tests for the strategy-driven, generic `JsonCrdtApplicator` service. Verifies correct conflict resolution, state management, and CRDT properties like idempotency and commutativity. |
| `$/Modern.CRDT.UnitTests/Services/JsonCrdtPatcherTests.cs` | Contains unit tests for the `JsonCrdtPatcher` service, covering POCO-based diffing with CRDT strategies including advanced array diffing. |
| `$/Modern.CRDT.UnitTests/Services/JsonCrdtServiceTests.cs` | Contains unit tests for the `JsonCrdtService`, verifying that it correctly delegates patching and merging operations to the appropriate services. |
| `$/Modern.CRDT.UnitTests/Services/Strategies/ArrayLcsStrategyTests.cs` | Contains unit tests for `ArrayLcsStrategy`, focusing on convergence properties under concurrent operations. This file includes a test that specifically reproduces a known bug related to the non-commutative application of array insertion patches. |
| `$/Modern.CRDT.UnitTests/Services/Strategies/CounterStrategyTests.cs` | Contains unit tests for the `CounterStrategy` implementation, verifying both patch generation and its simplified, unconditional data application logic. |
| `$/Modern.CRDT.UnitTests/Services/Strategies/LwwStrategyTests.cs` | Contains unit tests for the `LwwStrategy` implementation, verifying both patch generation and its simplified, unconditional data application logic. |
| `$/Modern.CRDT/Attributes/CrdtArrayLcsStrategyAttribute.cs` | An attribute to explicitly mark a collection property to use the Longest Common Subsequence (LCS) strategy. |
| `$/Modern.CRDT/Attributes/CrdtCounterAttribute.cs` | An attribute to explicitly mark a numeric property to use the CRDT Counter strategy. |
| `$/Modern.CRDT/Attributes/CrdtStrategyAttribute.cs` | The base abstract attribute for marking properties with a specific CRDT merge strategy. Contains the strategy type. |
| `$/Modern.CRDT/Attributes/LwwStrategyAttribute.cs` | An attribute to explicitly mark a property to use the Last-Writer-Wins (LWW) strategy. |
| `$/Modern.CRDT/Extensions/ServiceCollectionExtensions.cs` | Provides DI extension methods for easy library setup, including registration of strategies, the strategy manager, custom array element comparers, a customizable timestamp provider, and a factory for creating replica-specific patchers. |
| `$/Modern.CRDT/Models/CrdtDocument.cs` | Encapsulates a JSON document and its associated LWW metadata as `JsonNode`s. Used for patch generation. |
| `$/Modern.CRDT/Models/CrdtDocumentOfT.cs` | A generic version of `CrdtDocument` that holds a POCO and its associated `CrdtMetadata`, unifying the API for patch generation and application. |
| `$/Modern.CRDT/Models/CrdtMetadata.cs` | Encapsulates the state required for conflict resolution (LWW timestamps, seen operation IDs), externalizing it from the data model. |
| `$/Modern.CRDT/Models/CrdtOperation.cs` | Represents a single CRDT operation in a patch, including the target JSON Path, type, value, and timestamp. |
| `$/Modern.CRDT/Models/CrdtOptions.cs` | Provides configuration options for the CRDT library, such as the `ReplicaId`. |
| `$/Modern.CRDT/Models/CrdtPatch.cs` | Encapsulates a list of CRDT operations that represent the difference between two JSON documents. |
| `$/Modern.CRDT/Models/EpochTimestamp.cs` | A default, backward-compatible implementation of `ICrdtTimestamp` that wraps a `long` value representing Unix milliseconds. |
| `$/Modern.CRDT/Models/ICrdtTimestamp.cs` | Represents a logical point in time for a CRDT operation, allowing for different timestamping mechanisms. |
| `$/Modern.CRDT/Models/OperationType.cs` | Defines the types of operations (Upsert, Remove) for a CRDT patch. |
| `$/Modern.CRDT/Modern.CRDT.csproj` | No description provided. |
| `$/Modern.CRDT/Services/CrdtMetadataManager.cs` | Implements the logic for managing and compacting CRDT metadata. It can also initialize LWW metadata for a document by reflecting on its properties. |
| `$/Modern.CRDT/Services/EpochTimestampProvider.cs` | The default implementation of `ICrdtTimestampProvider` that generates `EpochTimestamp` based on Unix milliseconds. |
| `$/Modern.CRDT/Services/Helpers/JsonNodePathHelper.cs` | A static utility class containing shared helper methods for parsing JSON paths and manipulating JsonNode structures. |
| `$/Modern.CRDT/Services/ICrdtMetadataManager.cs` | Defines a service for managing and compacting CRDT metadata to prevent unbounded state growth. It also provides methods to initialize metadata based on a document's state. |
| `$/Modern.CRDT/Services/ICrdtTimestampProvider.cs` | Defines a service for generating CRDT timestamps, allowing for custom timestamp implementations. |
| `$/Modern.CRDT/Services/IJsonCrdtApplicator.cs` | Defines the contract for a service that applies a CRDT patch to a document, using an external `CrdtMetadata` object for conflict resolution. |
| `$/Modern.CRDT/Services/IJsonCrdtPatcher.cs` | Defines the contract for a service that compares two POCOs and generates a CRDT patch based on property-specific strategies. |
| `$/Modern.CRDT/Services/IJsonCrdtPatcherFactory.cs` | Defines a factory for creating `IJsonCrdtPatcher` instances that are configured for a specific replica ID. |
| `$/Modern.CRDT/Services/IJsonCrdtService.cs` | Defines the public facade service for orchestrating CRDT operations, providing a high-level API for patch generation and merging with externalized state management. |
| `$/Modern.CRDT/Services/JsonCrdtApplicator.cs` | Implements the patch application logic for POCOs. It acts as the central authority for conflict resolution by using an external `CrdtMetadata` object to check operations before delegating the data manipulation to the appropriate strategy. |
| `$/Modern.CRDT/Services/JsonCrdtPatcher.cs` | Implements the logic to recursively compare two POCOs, using a strategy manager to delegate property-level comparisons, including array diffing. |
| `$/Modern.CRDT/Services/JsonCrdtPatcherFactory.cs` | Implements the `IJsonCrdtPatcherFactory` to construct a replica-specific dependency graph for `IJsonCrdtPatcher`. |
| `$/Modern.CRDT/Services/JsonCrdtService.cs` | Implements the high-level facade service for CRDT operations, delegating to the patcher and applicator services. |
| `$/Modern.CRDT/Services/Strategies/ArrayLcsStrategy.cs` | Implements a CRDT strategy for arrays using LCS, with support for type-specific element comparers. `GeneratePatch` creates diffs, and `ApplyOperation` unconditionally manipulates the data array. |
| `$/Modern.CRDT/Services/Strategies/CounterStrategy.cs` | Implements the CRDT Counter strategy. `GeneratePatch` creates `Increment` operations, and the simplified `ApplyOperation` unconditionally applies the numeric delta. |
| `$/Modern.CRDT/Services/Strategies/CrdtStrategyManager.cs` | Implements the strategy resolution logic, finding the correct strategy for a property via reflection or returning a default (LWW or ArrayLcs). |
| `$/Modern.CRDT/Services/Strategies/ICrdtStrategy.cs` | Defines the contract for a strategy, including `GeneratePatch` for creating operations and a simplified `ApplyOperation` for unconditional data manipulation. |
| `$/Modern.CRDT/Services/Strategies/ICrdtStrategyManager.cs` | Defines the contract for a service that resolves the appropriate CRDT strategy for a property. |
| `$/Modern.CRDT/Services/Strategies/IJsonNodeComparer.cs` | Defines the contract for a type-specific equality comparer for JsonNode instances, used by ArrayLcsStrategy. |
| `$/Modern.CRDT/Services/Strategies/IJsonNodeComparerProvider.cs` | Defines the contract for a service that provides the correct IEqualityComparer<JsonNode> for a given array element type. |
| `$/Modern.CRDT/Services/Strategies/JsonNodeComparerProvider.cs` | Implements the provider logic to select a registered IJsonNodeComparer or a default, for use by ArrayLcsStrategy. |
| `$/Modern.CRDT/Services/Strategies/LwwStrategy.cs` | Implements the LWW strategy. `GeneratePatch` creates operations based on timestamps, and the simplified `ApplyOperation` unconditionally applies changes to nodes. |
| `$/Modern.CRDT.sln` | No description provided. |
| `$/README.md` | The main documentation for the Modern.CRDT library, including usage examples and an overview of the architecture. |
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