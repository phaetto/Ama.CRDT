| File Path | Description |
| --- | --- |
| `$/.gitignore` | No description provided. |
| `$/Ama.CRDT.Benchmarks/Ama.CRDT.Benchmarks.csproj` | The project file for the benchmarks application. |
| `$/Ama.CRDT.Benchmarks/AntiVirusFriendlyConfig.cs` | No description provided. |
| `$/Ama.CRDT.Benchmarks/Benchmarks/ApplicatorBenchmarks.cs` | Contains benchmarks for the `JsonCrdtApplicator` service. |
| `$/Ama.CRDT.Benchmarks/Benchmarks/PatcherBenchmarks.cs` | Contains benchmarks for the `JsonCrdtPatcher` service. |
| `$/Ama.CRDT.Benchmarks/Models/ComplexPoco.cs` | A complex data model with nested objects and arrays for benchmarking recursive and collection-based scenarios. |
| `$/Ama.CRDT.Benchmarks/Models/SimplePoco.cs` | A simple data model for benchmarking basic scenarios. |
| `$/Ama.CRDT.Benchmarks/Program.cs` | The entry point for the benchmark runner. |
| `$/Ama.CRDT.ShowCase/Ama.CRDT.ShowCase.csproj` | The project file for the showcase console application. |
| `$/Ama.CRDT.ShowCase/Models/User.cs` | A simple data model representing a user, used as an element in the CRDT-managed array. |
| `$/Ama.CRDT.ShowCase/Models/UserStats.cs` | The main POCO representing the shared state, decorated with CRDT strategy attributes (`CrdtCounter`, `CrdtArrayLcsStrategy`, `LwwStrategy`). |
| `$/Ama.CRDT.ShowCase/Program.cs` | The main entry point of the console application, responsible for setting up dependency injection and starting the simulation. |
| `$/Ama.CRDT.ShowCase/Services/CaseInsensitiveStringComparer.cs` | A custom implementation of `IElementComparer` that allows the `ArrayLcsStrategy` to identify unique strings using a case-insensitive comparison. |
| `$/Ama.CRDT.ShowCase/Services/IInMemoryDatabaseService.cs` | Defines the contract for a simple in-memory key-value store to simulate persistence for each replica's state (document and metadata). |
| `$/Ama.CRDT.ShowCase/Services/InMemoryDatabaseService.cs` | An implementation of `IInMemoryDatabaseService` using `ConcurrentDictionary` to simulate a database for CRDT documents and metadata. |
| `$/Ama.CRDT.ShowCase/SimulationRunner.cs` | Orchestrates the distributed map-reduce simulation using concurrent producers, mappers, and convergers communicating via channels to demonstrate CRDT convergence. |
| `$/Ama.CRDT.sln` | No description provided. |
| `$/Ama.CRDT.UnitTests/Ama.CRDT.UnitTests.csproj` | No description provided. |
| `$/Ama.CRDT.UnitTests/Models/EpochTimestampTests.cs` | Contains unit tests for the `EpochTimestamp` implementation of `ICrdtTimestamp`. |
| `$/Ama.CRDT.UnitTests/Services/CrdtApplicatorTests.cs` | No description provided. |
| `$/Ama.CRDT.UnitTests/Services/CrdtMetadataManagerTests.cs` | Contains unit tests for the `CrdtMetadataManager`, verifying LWW pruning and version vector advancement logic. |
| `$/Ama.CRDT.UnitTests/Services/CrdtPatcherTests.cs` | No description provided. |
| `$/Ama.CRDT.UnitTests/Services/CrdtServiceTests.cs` | No description provided. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/ArrayLcsStrategyTests.cs` | Contains unit tests for `ArrayLcsStrategy`, focusing on convergence properties under concurrent operations. This file includes a test that specifically reproduces a known bug related to the non-commutative application of array insertion patches. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/CounterStrategyTests.cs` | Contains unit tests for the `CounterStrategy` implementation, verifying both patch generation and its simplified, unconditional data application logic. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/LwwStrategyTests.cs` | Contains unit tests for the `LwwStrategy` implementation, verifying both patch generation and its simplified, unconditional data application logic. |
| `$/Ama.CRDT/Ama.CRDT.csproj` | No description provided. |
| `$/Ama.CRDT/Attributes/CounterStrategyAttribute.cs` | No description provided. |
| `$/Ama.CRDT/Attributes/CrdtArrayLcsStrategyAttribute.cs` | An attribute to explicitly mark a collection property to use the Longest Common Subsequence (LCS) strategy. |
| `$/Ama.CRDT/Attributes/CrdtCounterAttribute.cs` | An attribute to explicitly mark a numeric property to use the CRDT Counter strategy. |
| `$/Ama.CRDT/Attributes/CrdtStrategyAttribute.cs` | The base abstract attribute for marking properties with a specific CRDT merge strategy. Contains the strategy type. |
| `$/Ama.CRDT/Attributes/LwwStrategyAttribute.cs` | An attribute to explicitly mark a property to use the Last-Writer-Wins (LWW) strategy. |
| `$/Ama.CRDT/Attributes/SortedSetStrategyAttribute.cs` | An attribute to explicitly mark a collection property to use the Sorted Set strategy, which maintains a sorted order. |
| `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs` | Provides DI extension methods for easy library setup, including registration of strategies, the strategy manager, custom array element comparers, a customizable timestamp provider, and a factory for creating replica-specific patchers. |
| `$/Ama.CRDT/Models/CrdtDocumentOfT.cs` | A generic version of `CrdtDocument` that holds a POCO and its associated `CrdtMetadata`, unifying the API for patch generation and application. |
| `$/Ama.CRDT/Models/CrdtMetadata.cs` | Encapsulates the state required for conflict resolution (LWW timestamps, seen operation IDs), externalizing it from the data model. |
| `$/Ama.CRDT/Models/CrdtOperation.cs` | Represents a single CRDT operation in a patch, including the target JSON Path, type, value, and timestamp. The value is represented as a plain object. |
| `$/Ama.CRDT/Models/CrdtOptions.cs` | Provides configuration options for the CRDT library, such as the `ReplicaId`. |
| `$/Ama.CRDT/Models/CrdtPatch.cs` | Encapsulates a list of CRDT operations that represent the difference between two JSON documents. Includes the ID of the replica that generated it. |
| `$/Ama.CRDT/Models/EpochTimestamp.cs` | A default, backward-compatible implementation of `ICrdtTimestamp` that wraps a `long` value representing Unix milliseconds. |
| `$/Ama.CRDT/Models/ICrdtTimestamp.cs` | Represents a logical point in time for a CRDT operation, allowing for different timestamping mechanisms. |
| `$/Ama.CRDT/Models/OperationType.cs` | Defines the types of operations (Upsert, Remove) for a CRDT patch. |
| `$/Ama.CRDT/Services/CrdtApplicator.cs` | No description provided. |
| `$/Ama.CRDT/Services/CrdtMetadataManager.cs` | Implements the logic for managing and compacting CRDT metadata. It provides helper methods like `Initialize(document)` to create a metadata object from a POCO by reflecting on its properties. |
| `$/Ama.CRDT/Services/CrdtPatcher.cs` | Implements the logic to recursively compare two objects and generate a CRDT patch by delegating to property-specific strategies. |
| `$/Ama.CRDT/Services/CrdtPatcherFactory.cs` | No description provided. |
| `$/Ama.CRDT/Services/CrdtService.cs` | No description provided. |
| `$/Ama.CRDT/Services/EpochTimestampProvider.cs` | The default implementation of `ICrdtTimestampProvider` that generates `EpochTimestamp` based on Unix milliseconds. |
| `$/Ama.CRDT/Services/Helpers/PocoPathHelper.cs` | No description provided. |
| `$/Ama.CRDT/Services/ICrdtApplicator.cs` | No description provided. |
| `$/Ama.CRDT/Services/ICrdtMetadataManager.cs` | Defines a service for managing and compacting CRDT metadata to prevent unbounded state growth. It also provides methods to initialize metadata based on a document's state. |
| `$/Ama.CRDT/Services/ICrdtPatcher.cs` | Defines the contract for a service that compares two versions of a data model and generates a CRDT patch. |
| `$/Ama.CRDT/Services/ICrdtPatcherFactory.cs` | No description provided. |
| `$/Ama.CRDT/Services/ICrdtService.cs` | No description provided. |
| `$/Ama.CRDT/Services/ICrdtTimestampProvider.cs` | Defines a service for generating CRDT timestamps, allowing for custom timestamp implementations. |
| `$/Ama.CRDT/Services/Strategies/ArrayLcsStrategy.cs` | Implements a CRDT strategy for arrays using LCS, with support for type-specific element comparers. `GeneratePatch` creates diffs, and `ApplyOperation` unconditionally manipulates the data array. |
| `$/Ama.CRDT/Services/Strategies/CounterStrategy.cs` | Implements the CRDT Counter strategy. `GeneratePatch` creates `Increment` operations, and the simplified `ApplyOperation` unconditionally applies the numeric delta. |
| `$/Ama.CRDT/Services/Strategies/CrdtStrategyManager.cs` | Implements the strategy resolution logic, finding the correct strategy for a property via reflection or returning a default (LWW or ArrayLcs). |
| `$/Ama.CRDT/Services/Strategies/ElementComparerProvider.cs` | Implements the provider logic to select a registered IElementComparer or a default, for use by ArrayLcsStrategy. |
| `$/Ama.CRDT/Services/Strategies/ICrdtStrategy.cs` | Defines the contract for a strategy, including `GeneratePatch` for creating operations and a simplified `ApplyOperation` for unconditional data manipulation. |
| `$/Ama.CRDT/Services/Strategies/ICrdtStrategyManager.cs` | Defines the contract for a service that resolves the appropriate CRDT strategy for a property. |
| `$/Ama.CRDT/Services/Strategies/IElementComparer.cs` | Defines the contract for a type-specific equality comparer for collection elements, used by ArrayLcsStrategy. |
| `$/Ama.CRDT/Services/Strategies/IElementComparerProvider.cs` | Defines the contract for a service that provides the correct IEqualityComparer<object> for a given array element type. |
| `$/Ama.CRDT/Services/Strategies/LwwStrategy.cs` | Implements the LWW strategy. `GeneratePatch` creates operations based on timestamps, and the simplified `ApplyOperation` unconditionally applies changes to nodes. |
| `$/Ama.CRDT/Services/Strategies/SortedSetStrategy.cs` | Implements a CRDT strategy for collections that are treated as sorted sets. It uses LCS for diffing and ensures the collection remains sorted after operations. |
| `$/CodingStandards.md` | No description provided. |
| `$/features/allow-to-choose-strategy-using-attributes-specs/01-crdt-strategy-attribute-and-interface.md` | No description provided. |
| `$/features/allow-to-choose-strategy-using-attributes-specs/02-lww-strategy-implementation.md` | No description provided. |
| `$/features/allow-to-choose-strategy-using-attributes-specs/03-counter-strategy-implementation.md` | No description provided. |
| `$/features/allow-to-choose-strategy-using-attributes-specs/04-refactor-patcher-to-use-strategies.md` | No description provided. |
| `$/features/allow-to-choose-strategy-using-attributes-specs/05-01-arraylcsstrategy-needs-to-check-deep-objects.md` | No description provided. |
| `$/features/allow-to-choose-strategy-using-attributes-specs/05-02-manage-metadata-state-deifferently-in-strategies.md` | No description provided. |
| `$/features/allow-to-choose-strategy-using-attributes-specs/05-03-make-sure-there-are-reset-functions-for-the-state-to-keep-it-small.md` | No description provided. |
| `$/features/allow-to-choose-strategy-using-attributes-specs/05-refactor-applicator-to-use-strategies.md` | No description provided. |
| `$/features/allow-to-choose-strategy-using-attributes-specs/06-01-optimize-the-application-benchmarks.md` | No description provided. |
| `$/features/allow-to-choose-strategy-using-attributes-specs/06-02-rewrite-node-management-to-reflection.md` | No description provided. |
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
| `$/LICENSE` | No description provided. |
| `$/README.md` | The main documentation for the Ama.CRDT library, including usage examples, architecture overview, and guides for advanced extensibility points like custom comparers and timestamp providers. |
| `$/Specs/create-example-console-app-that-show-cases-the-crdts-with-out-locks.md` | No description provided. |