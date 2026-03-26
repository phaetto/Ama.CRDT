| File Path | Description |
| --- | --- |
| `$/.editorconfig` | No description provided. |
| `$/.github/workflows/ci.yml` | GitHub Actions workflow for building and testing the project on pushes to non-master branches and on pull requests to master. This ensures code quality before merging. |
| `$/.github/workflows/publish-nuget-manual.yml` | GitHub Actions workflow for manually building, testing, and publishing a stable, non-prerelease version of the `Ama.CRDT` NuGet package. It includes a check to ensure no unshipped public APIs are present in a stable release. |
| `$/.github/workflows/publish-nuget.yml` | GitHub Actions workflow for building, testing, and publishing the `Ama.CRDT` NuGet package (including its Roslyn analyzers) on pushes to the `master` branch. |
| `$/.gitignore` | No description provided. |
| `$/Ama.CRDT.Analyzers.UnitTests/Ama.CRDT.Analyzers.UnitTests.csproj` | The project file for the unit tests of the Roslyn analyzers. |
| `$/Ama.CRDT.Analyzers.UnitTests/CrdtIntentUsageAnalyzerTests.cs` | Contains unit tests for the `CrdtIntentUsageAnalyzer`, verifying that it correctly identifies valid and invalid usages of explicit operation intents. |
| `$/Ama.CRDT.Analyzers.UnitTests/CrdtStrategyTypeAnalyzerTests.cs` | Contains unit tests for the `CrdtStrategyTypeAnalyzer`, verifying that it correctly identifies valid and invalid applications of CRDT strategy attributes. |
| `$/Ama.CRDT.Analyzers/Ama.CRDT.Analyzers.csproj` | The project file for the Roslyn analyzers that validate CRDT strategy usage. It is configured to be bundled with the main `Ama.CRDT` NuGet package and not as a standalone package. |
| `$/Ama.CRDT.Analyzers/CrdtIntentUsageAnalyzer.cs` | Roslyn analyzer to validate that explicit operation intents are supported by the corresponding property's CRDT strategy. |
| `$/Ama.CRDT.Analyzers/CrdtStrategyTypeAnalyzer.cs` | No description provided. |
| `$/Ama.CRDT.Benchmarks/Ama.CRDT.Benchmarks.csproj` | The project file for the benchmarks application. |
| `$/Ama.CRDT.Benchmarks/AntiVirusFriendlyConfig.cs` | No description provided. |
| `$/Ama.CRDT.Benchmarks/Benchmarks/ApplicatorBenchmarks.cs` | Contains benchmarks for the `JsonCrdtApplicator` service. |
| `$/Ama.CRDT.Benchmarks/Benchmarks/PatcherBenchmarks.cs` | Contains benchmarks for the `JsonCrdtPatcher` service. |
| `$/Ama.CRDT.Benchmarks/Benchmarks/StrategyApplyBenchmarks.cs` | Contains benchmarks for `ApplyOperation` for every individual CRDT strategy in isolation. |
| `$/Ama.CRDT.Benchmarks/Benchmarks/StrategyGenerateBenchmarks.cs` | Contains benchmarks for `GeneratePatch` for every individual CRDT strategy in isolation. |
| `$/Ama.CRDT.Benchmarks/Benchmarks/StrategyGenerateOperationBenchmarks.cs` | Contains benchmarks for `GenerateOperation` for every individual CRDT strategy in isolation using explicit operation intents. |
| `$/Ama.CRDT.Benchmarks/Models/ComplexPoco.cs` | A complex data model with nested objects and arrays for benchmarking recursive and collection-based scenarios. |
| `$/Ama.CRDT.Benchmarks/Models/SimplePoco.cs` | A simple data model for benchmarking basic scenarios. |
| `$/Ama.CRDT.Benchmarks/Models/StrategyPoco.cs` | A data model containing properties decorated with attributes for each supported CRDT strategy, used for isolated strategy benchmarking. |
| `$/Ama.CRDT.Benchmarks/Program.cs` | The entry point for the benchmark runner. |
| `$/Ama.CRDT.Benchmarks/README.md` | No description provided. |
| `$/Ama.CRDT.IntegrationTests/Ama.CRDT.IntegrationTests.csproj` | No description provided. |
| `$/Ama.CRDT.IntegrationTests/NetworkSimulationTests.cs` | Contains integration tests simulating complex network scenarios (out-of-order delivery, partitions, long-term offline nodes, duplicate delivery, and high concurrency conflicts) to guarantee CRDT convergence and idempotency over time. |
| `$/Ama.CRDT.Partitioning.Streams.UnitTests/Ama.CRDT.Partitioning.Streams.UnitTests.csproj` | No description provided. |
| `$/Ama.CRDT.Partitioning.Streams.UnitTests/Serialization/DefaultPartitionSerializationServiceTests.cs` | No description provided. |
| `$/Ama.CRDT.Partitioning.Streams.UnitTests/Services/StreamSpaceAllocatorTests.cs` | Contains unit tests for the `StreamSpaceAllocator` class, verifying block allocation, free list reuse with best-fit logic, and free list size limits. |
| `$/Ama.CRDT.Partitioning.Streams.UnitTests/StreamPartitionStorageServiceDataTests.cs` | No description provided. |
| `$/Ama.CRDT.Partitioning.Streams.UnitTests/StreamPartitionStorageServiceIndexTests.cs` | No description provided. |
| `$/Ama.CRDT.Partitioning.Streams/Ama.CRDT.Partitioning.Streams.csproj` | No description provided. |
| `$/Ama.CRDT.Partitioning.Streams/Extensions/StreamPartitioningServiceCollectionExtensions.cs` | Provides dependency injection extension methods to register the new stream-based partitioning module. |
| `$/Ama.CRDT.Partitioning.Streams/Models/BPlusTreeNode.cs` | No description provided. |
| `$/Ama.CRDT.Partitioning.Streams/Models/BTreeHeader.cs` | No description provided. |
| `$/Ama.CRDT.Partitioning.Streams/Models/DataStreamHeader.cs` | No description provided. |
| `$/Ama.CRDT.Partitioning.Streams/Models/FreeSpaceState.cs` | No description provided. |
| `$/Ama.CRDT.Partitioning.Streams/PublicAPI.Shipped.txt` | No description provided. |
| `$/Ama.CRDT.Partitioning.Streams/PublicAPI.Unshipped.txt` | No description provided. |
| `$/Ama.CRDT.Partitioning.Streams/README.md` | Details the features and provides usage examples for setting up dependency injection and integrating stream providers for partition persistence. |
| `$/Ama.CRDT.Partitioning.Streams/Services/IPartitionStreamProvider.cs` | No description provided. |
| `$/Ama.CRDT.Partitioning.Streams/Services/Metrics/StreamsCrdtMetrics.cs` | No description provided. |
| `$/Ama.CRDT.Partitioning.Streams/Services/Serialization/DefaultPartitionSerializationService.cs` | No description provided. |
| `$/Ama.CRDT.Partitioning.Streams/Services/Serialization/IPartitionSerializationService.cs` | No description provided. |
| `$/Ama.CRDT.Partitioning.Streams/Services/StreamPartitionStorageService.cs` | No description provided. |
| `$/Ama.CRDT.Partitioning.Streams/Services/StreamSpaceAllocator.cs` | No description provided. |
| `$/Ama.CRDT.Project.Analyzers.UnitTests/Ama.CRDT.Project.Analyzers.UnitTests.csproj` | No description provided. |
| `$/Ama.CRDT.Project.Analyzers.UnitTests/PropertyInfoUsageAnalyzerTests.cs` | No description provided. |
| `$/Ama.CRDT.Project.Analyzers.UnitTests/SystemConvertUsageAnalyzerTests.cs` | No description provided. |
| `$/Ama.CRDT.Project.Analyzers/Ama.CRDT.Project.Analyzers.csproj` | No description provided. |
| `$/Ama.CRDT.Project.Analyzers/PropertyInfoUsageAnalyzer.cs` | No description provided. |
| `$/Ama.CRDT.Project.Analyzers/SystemConvertUsageAnalyzer.cs` | No description provided. |
| `$/Ama.CRDT.PropertyTests/Ama.CRDT.PropertyTests.csproj` | Contains the project definition for the property tests, incorporating FsCheck for automated generative testing validation. |
| `$/Ama.CRDT.PropertyTests/Attributes/CrdtPropertyAttribute.cs` | Custom FsCheck property attribute (`[CrdtProperty]`) that centrally configures the default number of test permutations (`MaxTest`) and supports environment-variable overrides for CI/CD environments. |
| `$/Ama.CRDT.PropertyTests/Strategies/ArrayLcsStrategyProperties.cs` | Contains property-based tests for `ArrayLcsStrategy` validating convergence and commutativity using `FsCheck`. |
| `$/Ama.CRDT.PropertyTests/Strategies/AverageRegisterStrategyProperties.cs` | Contains property-based tests for `AverageRegisterStrategy` validating convergence, commutativity, and idempotence using `FsCheck`. |
| `$/Ama.CRDT.PropertyTests/Strategies/BoundedCounterStrategyProperties.cs` | Contains property-based tests for `BoundedCounterStrategy` validating convergence and commutativity using `FsCheck`. |
| `$/Ama.CRDT.PropertyTests/Strategies/CounterMapStrategyProperties.cs` | Contains property-based tests for `CounterMapStrategy` validating convergence and commutativity using `FsCheck`. |
| `$/Ama.CRDT.PropertyTests/Strategies/CounterStrategyProperties.cs` | Contains property-based tests for `CounterStrategy` validating convergence and commutativity using `FsCheck`. |
| `$/Ama.CRDT.PropertyTests/Strategies/Decorators/ApprovalQuorumStrategyProperties.cs` | Contains property-based tests for `ApprovalQuorumStrategy` validating convergence, commutativity, and idempotence using `FsCheck`. |
| `$/Ama.CRDT.PropertyTests/Strategies/Decorators/EpochBoundStrategyProperties.cs` | Contains property-based tests for `EpochBoundStrategy` validating convergence, commutativity, and idempotence using `FsCheck`. |
| `$/Ama.CRDT.PropertyTests/Strategies/FixedSizeArrayStrategyProperties.cs` | Contains property-based tests for `FixedSizeArrayStrategy` validating convergence, commutativity, and idempotence using `FsCheck`. |
| `$/Ama.CRDT.PropertyTests/Strategies/FwwMapStrategyProperties.cs` | Contains property-based tests for `FwwMapStrategy` validating convergence, commutativity, and idempotence using `FsCheck`. |
| `$/Ama.CRDT.PropertyTests/Strategies/FwwSetStrategyProperties.cs` | Contains property-based tests for `FwwSetStrategy` validating convergence, commutativity, and idempotence using `FsCheck`. |
| `$/Ama.CRDT.PropertyTests/Strategies/FwwStrategyProperties.cs` | Contains property-based tests for `FwwStrategy` validating convergence, commutativity, and idempotence using `FsCheck`. |
| `$/Ama.CRDT.PropertyTests/Strategies/GCounterStrategyProperties.cs` | Contains property-based tests for `GCounterStrategy` validating convergence and commutativity using `FsCheck`. |
| `$/Ama.CRDT.PropertyTests/Strategies/GSetStrategyProperties.cs` | Contains property-based tests for `GSetStrategy` validating convergence, commutativity, and idempotence using `FsCheck`. |
| `$/Ama.CRDT.PropertyTests/Strategies/GraphStrategyProperties.cs` | Contains property-based tests for `GraphStrategy` validating convergence, commutativity, and idempotence using `FsCheck`. |
| `$/Ama.CRDT.PropertyTests/Strategies/LseqStrategyProperties.cs` | Contains property-based tests for `LseqStrategy` validating convergence, commutativity, and idempotence using `FsCheck`. |
| `$/Ama.CRDT.PropertyTests/Strategies/LwwMapStrategyProperties.cs` | Contains property-based tests for `LwwMapStrategy` validating convergence, commutativity, and idempotence using `FsCheck`. |
| `$/Ama.CRDT.PropertyTests/Strategies/LwwSetStrategyProperties.cs` | Contains property-based tests for `LwwSetStrategy` validating convergence, commutativity, and idempotence using `FsCheck`. |
| `$/Ama.CRDT.PropertyTests/Strategies/LwwStrategyProperties.cs` | Contains property-based tests for `LwwStrategy` validating core CRDT mathematical properties (Idempotence, Commutativity, Convergence) using `FsCheck`. |
| `$/Ama.CRDT.PropertyTests/Strategies/MaxWinsMapStrategyProperties.cs` | Contains property-based tests for `MaxWinsMapStrategy` validating convergence, commutativity, and idempotence using `FsCheck`. |
| `$/Ama.CRDT.PropertyTests/Strategies/MaxWinsStrategyProperties.cs` | Contains property-based tests for `MaxWinsStrategy` validating convergence, commutativity, and idempotence using `FsCheck`. |
| `$/Ama.CRDT.PropertyTests/Strategies/MinWinsMapStrategyProperties.cs` | Contains property-based tests for `MinWinsMapStrategy` validating convergence, commutativity, and idempotence using `FsCheck`. |
| `$/Ama.CRDT.PropertyTests/Strategies/MinWinsStrategyProperties.cs` | Contains property-based tests for `MinWinsStrategy` validating convergence, commutativity, and idempotence using `FsCheck`. |
| `$/Ama.CRDT.PropertyTests/Strategies/OrMapStrategyProperties.cs` | Contains property-based tests for `OrMapStrategy` validating convergence, commutativity, and idempotence using `FsCheck`. |
| `$/Ama.CRDT.PropertyTests/Strategies/OrSetStrategyProperties.cs` | Contains property-based tests for `OrSetStrategy` validating convergence, commutativity, and idempotence using `FsCheck`. |
| `$/Ama.CRDT.PropertyTests/Strategies/PriorityQueueStrategyProperties.cs` | Contains property-based tests for `PriorityQueueStrategy` validating convergence, commutativity, and idempotence using `FsCheck`. |
| `$/Ama.CRDT.PropertyTests/Strategies/ReplicatedTreeStrategyProperties.cs` | Contains property-based tests for `ReplicatedTreeStrategy` validating convergence, commutativity, and idempotence using `FsCheck`. |
| `$/Ama.CRDT.PropertyTests/Strategies/RgaStrategyProperties.cs` | Contains property-based tests for `RgaStrategy` validating convergence, commutativity, and idempotence using `FsCheck`. |
| `$/Ama.CRDT.PropertyTests/Strategies/SortedSetStrategyProperties.cs` | Contains property-based tests for `SortedSetStrategy` validating convergence, commutativity, and idempotence using `FsCheck`. |
| `$/Ama.CRDT.PropertyTests/Strategies/StateMachineStrategyProperties.cs` | Contains property-based tests for `StateMachineStrategy` validating convergence, commutativity, and idempotence using `FsCheck`. |
| `$/Ama.CRDT.PropertyTests/Strategies/TwoPhaseGraphStrategyProperties.cs` | Contains property-based tests for `TwoPhaseGraphStrategy` validating convergence, commutativity, and idempotence using `FsCheck`. |
| `$/Ama.CRDT.PropertyTests/Strategies/TwoPhaseSetStrategyProperties.cs` | Contains property-based tests for `TwoPhaseSetStrategy` validating convergence, commutativity, and idempotence using `FsCheck`. |
| `$/Ama.CRDT.PropertyTests/Strategies/VoteCounterStrategyProperties.cs` | Contains property-based tests for `VoteCounterStrategy` validating convergence, commutativity, and idempotence using `FsCheck`. |
| `$/Ama.CRDT.ShowCase.LargerThanMemory/Ama.CRDT.ShowCase.LargerThanMemory.csproj` | The project file for the larger-than-memory showcase console application. |
| `$/Ama.CRDT.ShowCase.LargerThanMemory/Models/BlogPost.cs` | The root data model for the showcase, representing a blog post. It is decorated with `[PartitionKey]` and its `Comments` list uses `[CrdtArrayLcsStrategy]` to enable partitioning. |
| `$/Ama.CRDT.ShowCase.LargerThanMemory/Models/Comment.cs` | A simple record representing a comment in the blog post. |
| `$/Ama.CRDT.ShowCase.LargerThanMemory/Program.cs` | The main entry point for the showcase application, responsible for setting up dependency injection and starting the simulation. |
| `$/Ama.CRDT.ShowCase.LargerThanMemory/README.md` | Provides an overview of the larger-than-memory showcase, explaining how to run it, the features it demonstrates (partitioning, journaling, disconnected sync), and how to interact with the terminal UI. |
| `$/Ama.CRDT.ShowCase.LargerThanMemory/Services/DataGeneratorService.cs` | A service responsible for programmatically generating a configurable number of blog posts, each with a random number of comments, to demonstrate the system's ability to handle large, partitioned datasets. |
| `$/Ama.CRDT.ShowCase.LargerThanMemory/Services/FileSystemOperationJournal.cs` | An example implementation of `ICrdtOperationJournal` that persists applied CRDT operations to a local JSON file per replica, demonstrating how to save operations for external sync protocols or offline usage. |
| `$/Ama.CRDT.ShowCase.LargerThanMemory/Services/FileSystemPartitionStreamProvider.cs` | An implementation of `IPartitionStreamProvider` that stores CRDT index and data files on the local filesystem, organized into directories for each replica. It now explicitly separates header and property streams. |
| `$/Ama.CRDT.ShowCase.LargerThanMemory/Services/UiService.cs` | Implements the `Terminal.Gui`-based user interface for browsing blog posts. It displays post titles, content, and comments, demonstrating on-demand loading of data from partitions. |
| `$/Ama.CRDT.ShowCase.LargerThanMemory/SimulationRunner.cs` | Orchestrates the showcase by checking for existing data, triggering the data generation process if needed, and launching the user interface. It uses `IPartitionManager` to discover existing documents at startup. |
| `$/Ama.CRDT.ShowCase/Ama.CRDT.ShowCase.csproj` | The project file for the showcase console application. |
| `$/Ama.CRDT.ShowCase/Models/User.cs` | A simple data model representing a user, used as an element in the CRDT-managed array. |
| `$/Ama.CRDT.ShowCase/Models/UserStats.cs` | The main POCO representing the shared state, decorated with CRDT strategy attributes (`CrdtCounter`, `CrdtArrayLcsStrategy`, `LwwStrategy`). |
| `$/Ama.CRDT.ShowCase/Program.cs` | The main entry point of the console application, responsible for setting up dependency injection and starting the simulation. |
| `$/Ama.CRDT.ShowCase/README.md` | Provides an overview and explanation of the multi-replica, lock-free distributed simulation showcase for the CRDT library. |
| `$/Ama.CRDT.ShowCase/Services/CaseInsensitiveStringComparer.cs` | A custom implementation of `IElementComparer` that allows the `ArrayLcsStrategy` to identify unique strings using a case-insensitive comparison. |
| `$/Ama.CRDT.ShowCase/Services/IInMemoryDatabaseService.cs` | Defines the contract for a simple in-memory key-value store to simulate persistence for each replica's state (document and metadata). |
| `$/Ama.CRDT.ShowCase/Services/InMemoryDatabaseService.cs` | An implementation of `IInMemoryDatabaseService` using `ConcurrentDictionary` to simulate a database for CRDT documents and metadata. |
| `$/Ama.CRDT.ShowCase/SimulationRunner.cs` | Orchestrates the distributed map-reduce simulation using concurrent producers, mappers, and convergers communicating via channels to demonstrate CRDT convergence. |
| `$/Ama.CRDT.UnitTests/Ama.CRDT.UnitTests.csproj` | No description provided. |
| `$/Ama.CRDT.UnitTests/Architecture/StrategyConventionTests.cs` | An architecture convention unit test that reflects over all strategies to enforce the presence of unit tests, property tests, benchmarks, and documentation. |
| `$/Ama.CRDT.UnitTests/Models/DottedVersionVectorTests.cs` | No description provided. |
| `$/Ama.CRDT.UnitTests/Models/EpochTimestampTests.cs` | Contains unit tests for the `EpochTimestamp` implementation of `ICrdtTimestamp`. |
| `$/Ama.CRDT.UnitTests/Models/Partitioning/CompositePartitionKeyTests.cs` | No description provided. |
| `$/Ama.CRDT.UnitTests/Models/Serialization/CrdtMetadataSerializationTests.cs` | Contains unit tests for the serialization and deserialization of the `CrdtMetadata` class, verifying both default and compact serialization options and ensuring polymorphic and complex data (e.g., non-string dictionary keys, nested collections) is handled correctly. |
| `$/Ama.CRDT.UnitTests/Models/Serialization/CrdtTimestampJsonConverterTests.cs` | Contains unit tests for the `CrdtTimestampJsonConverter`, verifying polymorphic serialization and deserialization of `ICrdtTimestamp` implementations. |
| `$/Ama.CRDT.UnitTests/Services/Adapters/AsyncCrdtApplicatorAdapterTests.cs` | Contains unit tests for `AsyncCrdtApplicatorAdapter`, verifying its bridging of synchronous execution to asynchronous interfaces and cancellation handling. |
| `$/Ama.CRDT.UnitTests/Services/Adapters/AsyncCrdtPatcherAdapterTests.cs` | Contains unit tests for `AsyncCrdtPatcherAdapter`, validating proper adaptation of patch and operation generation with cancellation checks. |
| `$/Ama.CRDT.UnitTests/Services/CrdtApplicatorTests.cs` | No description provided. |
| `$/Ama.CRDT.UnitTests/Services/CrdtComposableArchitectureTests.cs` | Contains integration tests for the CRDT composable architecture, verifying that the Patcher and Applicator correctly handle deep nesting, complex model traversal, intent-based operation generation, and resolution across multiple nested CRDT strategies (LSEQ, Min-Wins Map, State Machine, Graph, etc.). |
| `$/Ama.CRDT.UnitTests/Services/CrdtMetadataManagerTests.cs` | Contains unit tests for the `CrdtMetadataManager`, verifying LWW pruning and version vector advancement logic. |
| `$/Ama.CRDT.UnitTests/Services/CrdtPatcherTests.cs` | No description provided. |
| `$/Ama.CRDT.UnitTests/Services/Decorators/JournalingApplicatorDecoratorTests.cs` | Contains unit tests for `JournalingApplicatorDecorator`, validating that only successfully applied CRDT operations are dispatched to the operation journal. |
| `$/Ama.CRDT.UnitTests/Services/Decorators/JournalingPatcherDecoratorTests.cs` | Contains unit tests for `JournalingPatcherDecorator`, verifying that generated patches, intents, and explicit operations are properly captured and journaled. |
| `$/Ama.CRDT.UnitTests/Services/Decorators/PartitioningApplicatorDecoratorTests.cs` | Contains unit tests for `PartitioningApplicatorDecorator`, validating the patch interception logic, partition splitting and merging, and ensuring operations are properly delegated to the inner `IAsyncCrdtApplicator`. |
| `$/Ama.CRDT.UnitTests/Services/Helpers/Models.cs` | Contains simple data models for unit testing path conversion and resolution helpers. |
| `$/Ama.CRDT.UnitTests/Services/Helpers/PocoPathHelperTests.cs` | Contains unit tests for `PocoPathHelper`, verifying JSON path parsing and resolution against POCOs, and testing new centralized reflection helpers for getting/setting values and retrieving type information. |
| `$/Ama.CRDT.UnitTests/Services/Journaling/JournalManagerTests.cs` | Contains unit tests for `JournalManager`, verifying the retrieval of missing operations based on synchronization requirements, range bounds, and missing dots. |
| `$/Ama.CRDT.UnitTests/Services/Partitioning/PartitionManagerTests.cs` | Contains unit tests for `PartitionManager`, focusing on verifying storage initialization, partition querying, and index management via `IPartitionStorageService`. |
| `$/Ama.CRDT.UnitTests/Services/Partitioning/PartitionStorageServiceContractTests.cs` | Contains mock unit tests to verify the `IPartitionStorageService` interface contract. |
| `$/Ama.CRDT.UnitTests/Services/Providers/CrdtFluentConfigurationTests.cs` | Contains unit tests verifying the Fluent Builder API (`CrdtModelBuilder`) correctly maps CRDT strategies and that the `CrdtStrategyProvider` prioritizes these mappings over attributes. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/ArrayLcsStrategyTests.cs` | Contains unit tests for `ArrayLcsStrategy`, focusing on convergence properties under concurrent operations. This file includes a test that specifically reproduces a known bug related to the non-commutative application of array insertion patches. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/AverageRegisterStrategyTests.cs` | Contains unit tests for the `AverageRegisterStrategy`, verifying convergence, idempotence, and commutativity. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/BoundedCounterStrategyTests.cs` | Contains unit tests for the `BoundedCounterStrategy`, verifying that values are correctly clamped within their defined bounds. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/CounterMapStrategyTests.cs` | Contains unit tests for `CounterMapStrategy`, verifying convergence and correct patch generation for concurrent increments and decrements on dictionary keys. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/CounterStrategyTests.cs` | Contains unit tests for the `CounterStrategy` implementation, verifying both patch generation and its simplified, unconditional data application logic. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/Decorators/ApprovalQuorumStrategyTests.cs` | Contains unit tests for the `ApprovalQuorumStrategy` and `CrdtApprovalQuorumAttribute`, verifying the correct tracking of proposals, replica quorum resolution, payload wrapping, and clean up. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/Decorators/ChainedDecoratorsTests.cs` | Integration tests verifying the correct behavioral chaining and nesting of multiple decorator attributes (`EpochBound` and `ApprovalQuorum`) on a single model property. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/Decorators/EpochBoundStrategyTests.cs` | No description provided. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/FixedSizeArrayStrategyTests.cs` | Contains unit tests for the `FixedSizeArrayStrategy`, verifying convergence and idempotence for concurrent updates. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/FwwMapStrategyTests.cs` | Contains unit tests for `FwwMapStrategy`, verifying FWW-based convergence, patch generation for map changes, and correct partitioning logic (split/merge). |
| `$/Ama.CRDT.UnitTests/Services/Strategies/FwwSetStrategyTests.cs` | Contains unit tests for the `FwwSetStrategy`, validating FWW-based convergence on collection items, explicit intents, and associative operation properties. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/FwwStrategyTests.cs` | Contains unit tests for the `FwwStrategy`, verifying correct First-Writer-Wins precedence, commutativity, and idempotence for scalar values. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/GCounterStrategyTests.cs` | Contains unit tests for the `GCounterStrategy`, ensuring it only generates and applies positive increments. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/GSetStrategyTests.cs` | Contains unit tests for the `GSetStrategy`, verifying its add-only behavior, idempotence, and commutativity. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/GraphStrategyTests.cs` | Contains unit tests for `GraphStrategy`, verifying convergence and correct patch generation for concurrent additions of vertices and edges. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/LseqStrategyTests.cs` | Contains unit tests for the `LseqStrategy`, verifying convergence, idempotence, and the new partitioning interface methods (`Split`, `Merge`, `GetMinimumKey`, etc.). |
| `$/Ama.CRDT.UnitTests/Services/Strategies/LwwMapStrategyTests.cs` | Contains unit tests for the `LwwMapStrategy`, verifying convergence, idempotence, and LWW-based conflict resolution for concurrent dictionary operations. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/LwwSetStrategyTests.cs` | Contains unit tests for the `LwwSetStrategy`, verifying that conflicts are resolved based on the last-write-wins rule, allowing elements to be re-added after removal. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/LwwStrategyTests.cs` | Contains unit tests for the `LwwStrategy` implementation, verifying both patch generation and its simplified, unconditional data application logic. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/MaxWinsMapStrategyTests.cs` | Contains unit tests for `MaxWinsMapStrategy`, verifying value-based convergence for concurrent dictionary operations. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/MaxWinsStrategyTests.cs` | Contains unit tests for the `MaxWinsStrategy`, verifying that conflicts are resolved by choosing the highest value. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/MinWinsMapStrategyTests.cs` | Contains unit tests for `MinWinsMapStrategy`, verifying value-based convergence for concurrent dictionary operations. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/MinWinsStrategyTests.cs` | Contains unit tests for the `MinWinsStrategy`, verifying that conflicts are resolved by choosing the lowest value. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/OrMapStrategyTests.cs` | Contains unit tests for the `OrMapStrategy`, verifying convergence, LWW-based conflict resolution, re-addition of elements, and correct behavior for partitioning (split/merge). |
| `$/Ama.CRDT.UnitTests/Services/Strategies/OrSetStrategyTests.cs` | Contains unit tests for the `OrSetStrategy`, verifying that it correctly handles concurrent additions and removals without anomalies, allowing for proper re-addition of elements. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/PriorityQueueStrategyTests.cs` | Contains unit tests for the `PriorityQueueStrategy`, verifying that concurrent updates converge and the list remains sorted by priority. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/ReplicatedTreeStrategyTests.cs` | No description provided. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/RgaStrategyTests.cs` | Contains unit tests for `RgaStrategy`, verifying correct calculation of tombstones, insertions and tree based flattening. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/Serialization/StrategyPayloadSerializationTests.cs` | No description provided. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/SortedSetStrategyTests.cs` | No description provided. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/StateMachineStrategyTests.cs` | Contains unit tests for `StateMachineStrategy`, verifying valid/invalid transitions and LWW-based conflict resolution. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/TwoPhaseGraphStrategyTests.cs` | No description provided. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/TwoPhaseSetStrategyTests.cs` | Contains unit tests for the `TwoPhaseSetStrategy`, verifying that elements can be added and removed, but not re-added after removal. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/VoteCounterStrategyTests.cs` | Contains unit tests for the `VoteCounterStrategy`, verifying convergence, idempotence, and LWW-based conflict resolution for concurrent voting scenarios. |
| `$/Ama.CRDT.UnitTests/Services/VersionVectorSyncServiceTests.cs` | Contains comprehensive unit tests for `VersionVectorSyncService`, validating synchronization requirements calculations between different `DottedVersionVector` states. |
| `$/Ama.CRDT.sln` | The Visual Studio solution file that groups all related projects (`Ama.CRDT`, `Ama.CRDT.Analyzers`, unit tests, benchmarks, etc.) together. |
| `$/Ama.CRDT/Ama.CRDT.csproj` | The main project file for the CRDT library, configured for NuGet packaging and to automatically include its associated Roslyn analyzers. |
| `$/Ama.CRDT/Attributes/CrdtIntentMappingAttribute.cs` | An attribute to map intent builder extension methods to the specific explicit intent types they generate, enabling compile-time validation via Roslyn analyzers without hardcoded mappings. |
| `$/Ama.CRDT/Attributes/CrdtStrategyAttribute.cs` | The base abstract attribute for marking properties with a specific CRDT merge strategy. Contains the strategy type. |
| `$/Ama.CRDT/Attributes/CrdtStrategyDecoratorAttribute.cs` | A base attribute for specifying a CRDT decorator strategy for a property, allowing multiple stacked decorators like `[CrdtEpochBound]` alongside core strategy attributes. |
| `$/Ama.CRDT/Attributes/CrdtSupportedIntentAttribute.cs` | An attribute to explicitly mark which explicit `IOperationIntent` types a given CRDT strategy supports. Used by Roslyn analyzers for validation. |
| `$/Ama.CRDT/Attributes/CrdtSupportedTypeAttribute.cs` | An attribute used to decorate a CRDT strategy class, specifying a property type (e.g., `int`, `IEnumerable`) that it supports. This enables compile-time validation via Roslyn analyzers. |
| `$/Ama.CRDT/Attributes/Decorators/CrdtApprovalQuorumAttribute.cs` | A decorator attribute that requires a specified number of approvals from different replicas before applying the underlying CRDT operation. |
| `$/Ama.CRDT/Attributes/Decorators/CrdtEpochBoundAttribute.cs` | No description provided. |
| `$/Ama.CRDT/Attributes/PartitionKeyAttribute.cs` | No description provided. |
| `$/Ama.CRDT/Attributes/Strategies/CrdtArrayLcsStrategyAttribute.cs` | No description provided. |
| `$/Ama.CRDT/Attributes/Strategies/CrdtAverageRegisterStrategyAttribute.cs` | No description provided. |
| `$/Ama.CRDT/Attributes/Strategies/CrdtBoundedCounterStrategyAttribute.cs` | No description provided. |
| `$/Ama.CRDT/Attributes/Strategies/CrdtCounterMapStrategyAttribute.cs` | No description provided. |
| `$/Ama.CRDT/Attributes/Strategies/CrdtCounterStrategyAttribute.cs` | No description provided. |
| `$/Ama.CRDT/Attributes/Strategies/CrdtFixedSizeArrayStrategyAttribute.cs` | No description provided. |
| `$/Ama.CRDT/Attributes/Strategies/CrdtFwwMapStrategyAttribute.cs` | No description provided. |
| `$/Ama.CRDT/Attributes/Strategies/CrdtFwwSetStrategyAttribute.cs` | No description provided. |
| `$/Ama.CRDT/Attributes/Strategies/CrdtFwwStrategyAttribute.cs` | No description provided. |
| `$/Ama.CRDT/Attributes/Strategies/CrdtGCounterStrategyAttribute.cs` | No description provided. |
| `$/Ama.CRDT/Attributes/Strategies/CrdtGSetStrategyAttribute.cs` | No description provided. |
| `$/Ama.CRDT/Attributes/Strategies/CrdtGraphStrategyAttribute.cs` | No description provided. |
| `$/Ama.CRDT/Attributes/Strategies/CrdtLseqStrategyAttribute.cs` | No description provided. |
| `$/Ama.CRDT/Attributes/Strategies/CrdtLwwMapStrategyAttribute.cs` | No description provided. |
| `$/Ama.CRDT/Attributes/Strategies/CrdtLwwSetStrategyAttribute.cs` | No description provided. |
| `$/Ama.CRDT/Attributes/Strategies/CrdtLwwStrategyAttribute.cs` | No description provided. |
| `$/Ama.CRDT/Attributes/Strategies/CrdtMaxWinsMapStrategyAttribute.cs` | No description provided. |
| `$/Ama.CRDT/Attributes/Strategies/CrdtMaxWinsStrategyAttribute.cs` | No description provided. |
| `$/Ama.CRDT/Attributes/Strategies/CrdtMinWinsMapStrategyAttribute.cs` | No description provided. |
| `$/Ama.CRDT/Attributes/Strategies/CrdtMinWinsStrategyAttribute.cs` | No description provided. |
| `$/Ama.CRDT/Attributes/Strategies/CrdtOrMapStrategyAttribute.cs` | No description provided. |
| `$/Ama.CRDT/Attributes/Strategies/CrdtOrSetStrategyAttribute.cs` | No description provided. |
| `$/Ama.CRDT/Attributes/Strategies/CrdtPriorityQueueStrategyAttribute.cs` | No description provided. |
| `$/Ama.CRDT/Attributes/Strategies/CrdtReplicatedTreeStrategyAttribute.cs` | No description provided. |
| `$/Ama.CRDT/Attributes/Strategies/CrdtRgaStrategyAttribute.cs` | No description provided. |
| `$/Ama.CRDT/Attributes/Strategies/CrdtSortedSetStrategyAttribute.cs` | No description provided. |
| `$/Ama.CRDT/Attributes/Strategies/CrdtStateMachineStrategyAttribute.cs` | No description provided. |
| `$/Ama.CRDT/Attributes/Strategies/CrdtTwoPhaseGraphStrategyAttribute.cs` | No description provided. |
| `$/Ama.CRDT/Attributes/Strategies/CrdtTwoPhaseSetStrategyAttribute.cs` | No description provided. |
| `$/Ama.CRDT/Attributes/Strategies/CrdtVoteCounterStrategyAttribute.cs` | No description provided. |
| `$/Ama.CRDT/Attributes/Strategies/Semantic/AssociativeAttribute.cs` | No description provided. |
| `$/Ama.CRDT/Attributes/Strategies/Semantic/CommutativeAttribute.cs` | No description provided. |
| `$/Ama.CRDT/Attributes/Strategies/Semantic/IdempotentAttribute.cs` | No description provided. |
| `$/Ama.CRDT/Attributes/Strategies/Semantic/OperationBasedAttribute.cs` | No description provided. |
| `$/Ama.CRDT/Attributes/Strategies/Semantic/StateBasedAttribute.cs` | No description provided. |
| `$/Ama.CRDT/Extensions/IStateMachine.cs` | No description provided. |
| `$/Ama.CRDT/Extensions/IntentBuilderExtensions.cs` | Provides strongly-typed extension methods for `IIntentBuilder<TProperty>` to fluent build CRDT operations without boxing. |
| `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs` | Provides dependency injection extension methods for easy library setup, unifying `AddCrdt`, applicator/patcher pipeline decoration (`AddCrdtApplicatorDecorator`, `AddCrdtJournaling`), comparers, timestamp providers, and serialization polymorphism. |
| `$/Ama.CRDT/Models/ApplyPatchResult.cs` | Data structure containing the result of a patch application, including the document and any unapplied operations. |
| `$/Ama.CRDT/Models/AverageRegisterValue.cs` | A data structure that holds a replica's contribution (value and timestamp) for the Average Register strategy. |
| `$/Ama.CRDT/Models/BidirectionalSyncRequirements.cs` | A data structure containing the synchronization requirements for two replicas to fully catch up with each other. |
| `$/Ama.CRDT/Models/CrdtDocument.cs` | No description provided. |
| `$/Ama.CRDT/Models/CrdtGraph.cs` | A data model for a graph structure with vertices and edges, suitable for CRDT management. |
| `$/Ama.CRDT/Models/CrdtMetadata.cs` | Encapsulates CRDT state for various strategies using dedicated, serializable record types instead of tuples. For serialization, use the recommended options from `CrdtJsonContext`. |
| `$/Ama.CRDT/Models/CrdtOperation.cs` | Represents a single CRDT operation. For serialization, use the recommended options from `CrdtJsonContext`. |
| `$/Ama.CRDT/Models/CrdtOperationStatus.cs` | Enum representing the status or failure reason of a CRDT operation during application. |
| `$/Ama.CRDT/Models/CrdtPatch.cs` | Encapsulates a list of CRDT operations, now with a logical key of type `IComparable?` to support strongly-typed, sortable partition keys. |
| `$/Ama.CRDT/Models/CrdtTree.cs` | A data model for a tree data structure with nodes that can be added, removed, and moved, suitable for CRDT management. |
| `$/Ama.CRDT/Models/Decorators/EpochPayload.cs` | No description provided. |
| `$/Ama.CRDT/Models/Decorators/QuorumPayload.cs` | A data structure for the payload of a quorum-bound operation. |
| `$/Ama.CRDT/Models/DottedVersionVector.cs` | Represents a Dotted Version Vector (DVV) to track causality and operations seen by a replica. Combines a contiguous version vector with discrete "dots" for out-of-order events. |
| `$/Ama.CRDT/Models/Edge.cs` | No description provided. |
| `$/Ama.CRDT/Models/EpochTimestamp.cs` | A default, backward-compatible implementation of `ICrdtTimestamp` that wraps a `long` value representing Unix milliseconds. |
| `$/Ama.CRDT/Models/GraphEdgePayload.cs` | A data structure for the payload of a graph edge operation. |
| `$/Ama.CRDT/Models/GraphVertexPayload.cs` | A data structure for the payload of a graph vertex operation. |
| `$/Ama.CRDT/Models/ICompactionPolicy.cs` | Defines a policy that determines whether a CRDT tombstone or metadata entry is safe to delete. |
| `$/Ama.CRDT/Models/ICrdtTimestamp.cs` | Represents a logical point in time for a CRDT operation, allowing for different timestamping mechanisms. |
| `$/Ama.CRDT/Models/Intents/AddEdgeIntent.cs` | Represents the intent to explicitly add an edge to a graph. |
| `$/Ama.CRDT/Models/Intents/AddIntent.cs` | Represents the intent to explicitly add an item to an unordered collection or set. |
| `$/Ama.CRDT/Models/Intents/AddNodeIntent.cs` | Represents the intent to explicitly add a node to a replicated tree. |
| `$/Ama.CRDT/Models/Intents/AddVertexIntent.cs` | Represents the intent to explicitly add a vertex to a graph. |
| `$/Ama.CRDT/Models/Intents/ClearIntent.cs` | Represents the intent to explicitly clear a property, collection, or state, effectively resetting it for FWW and other strategies. |
| `$/Ama.CRDT/Models/Intents/Decorators/EpochClearIntent.cs` | Represents the intent to explicitly clear the state within an Epoch-bound decorator, incrementing the epoch and effectively clearing all local data for that path without clashing with base strategy intents. |
| `$/Ama.CRDT/Models/Intents/IIntentBuilder.cs` | No description provided. |
| `$/Ama.CRDT/Models/Intents/IOperationIntent.cs` | A marker interface for defining explicit CRDT operations intent directly triggered by user actions, bypassing diff generation. |
| `$/Ama.CRDT/Models/Intents/IncrementIntent.cs` | Represents the intent to explicitly increment or decrement a numeric value or counter. |
| `$/Ama.CRDT/Models/Intents/InsertIntent.cs` | Defines an explicit intention to insert a value into an ordered sequence at a specific index. |
| `$/Ama.CRDT/Models/Intents/MapIncrementIntent.cs` | Represents the intent to explicitly increment or decrement a numeric value for a specific key within a dictionary or map. |
| `$/Ama.CRDT/Models/Intents/MapRemoveIntent.cs` | Represents the intent to explicitly remove a key and its associated value from a dictionary or map. |
| `$/Ama.CRDT/Models/Intents/MapSetIntent.cs` | Represents the intent to explicitly set a value for a specific key within a dictionary or map. |
| `$/Ama.CRDT/Models/Intents/MoveNodeIntent.cs` | Represents the intent to explicitly move a node in a replicated tree to a new parent. |
| `$/Ama.CRDT/Models/Intents/RemoveEdgeIntent.cs` | Represents the intent to explicitly remove an edge from a graph. |
| `$/Ama.CRDT/Models/Intents/RemoveIntent.cs` | Defines an explicit intention to remove an item from a collection or sequence, specified either by index or key. |
| `$/Ama.CRDT/Models/Intents/RemoveNodeIntent.cs` | Represents the intent to explicitly remove a node from a replicated tree by its identifier. |
| `$/Ama.CRDT/Models/Intents/RemoveValueIntent.cs` | Represents the intent to explicitly remove a specific value from a collection or set. |
| `$/Ama.CRDT/Models/Intents/RemoveVertexIntent.cs` | Represents the intent to explicitly remove a vertex from a graph. |
| `$/Ama.CRDT/Models/Intents/SetIndexIntent.cs` | Represents the intent to explicitly set a value at a specific index within a collection or sequence. |
| `$/Ama.CRDT/Models/Intents/SetIntent.cs` | Represents the intent to explicitly set a value for a property or register. |
| `$/Ama.CRDT/Models/Intents/VoteIntent.cs` | Represents the intent to explicitly cast a vote for a specific option. |
| `$/Ama.CRDT/Models/JournaledOperation.cs` | An envelope record struct used by the operation journal interfaces to bundle a core `CrdtOperation` with its target `DocumentId` (logical key), facilitating correct data routing when synchronizing independent documents. |
| `$/Ama.CRDT/Models/LseqIdentifier.cs` | A record struct for the dense, ordered identifier used in LSEQ, composed of a path of `LseqPathSegment` instances. |
| `$/Ama.CRDT/Models/LseqItem.cs` | A record struct that pairs an LseqIdentifier with its corresponding value in the LSEQ metadata. |
| `$/Ama.CRDT/Models/LseqPathSegment.cs` | Represents a single, serializable segment in an LSEQ identifier's path, containing a position and a replica ID. |
| `$/Ama.CRDT/Models/LwwSetState.cs` | No description provided. |
| `$/Ama.CRDT/Models/OperationType.cs` | Defines the types of operations (Upsert, Remove, Increment, Move) for a CRDT patch. |
| `$/Ama.CRDT/Models/OrMapItem.cs` | Contains payload record structs (`OrMapAddItem`, `OrMapRemoveItem`) for OR-Map (Observed-Remove Map) operations, bundling keys and values with unique tags. |
| `$/Ama.CRDT/Models/OrSetItem.cs` | Contains payload record structs (`OrSetAddItem`, `OrSetRemoveItem`) for OR-Set (Observed-Remove Set) operations, bundling values with unique tags. |
| `$/Ama.CRDT/Models/OrSetState.cs` | No description provided. |
| `$/Ama.CRDT/Models/OriginSyncRequirement.cs` | A data structure representing the causality gap for a specific origin replica between a target and a source. |
| `$/Ama.CRDT/Models/Partitioning/CompositePartitionKey.cs` | Represents a composite key for partitioning, consisting of a logical key and a range key. It now uses `IComparable` for keys to support natural sorting of different key types and implements `IComparable` for consistent ordering. |
| `$/Ama.CRDT/Models/Partitioning/DataPartition.cs` | No description provided. |
| `$/Ama.CRDT/Models/Partitioning/HeaderPartition.cs` | No description provided. |
| `$/Ama.CRDT/Models/Partitioning/IPartition.cs` | No description provided. |
| `$/Ama.CRDT/Models/Partitioning/PartitionContent.cs` | A data structure representing the data and metadata content of a single partition. |
| `$/Ama.CRDT/Models/Partitioning/SplitResult.cs` | A data structure representing the result of a partition split operation, containing the content for the two new partitions and the key that divides them. It now uses `IComparable` for the split key to support various key types. |
| `$/Ama.CRDT/Models/PnCounterState.cs` | No description provided. |
| `$/Ama.CRDT/Models/PositionalIdentifier.cs` | No description provided. |
| `$/Ama.CRDT/Models/PositionalItem.cs` | A data structure used in operation payloads for positional array updates, bundling a stable position with the actual value. |
| `$/Ama.CRDT/Models/ReplicaSyncRequirement.cs` | A data structure representing the data a target replica needs to request from a source replica to catch up with its causal history. |
| `$/Ama.CRDT/Models/RgaIdentifier.cs` | Readonly record struct containing logical timestamp and replicaId used as a unique identifier for each RGA node. |
| `$/Ama.CRDT/Models/RgaItem.cs` | Data structure representing an RGA node with a pointer to its predecessor, its payload value, and a tombstone flag. |
| `$/Ama.CRDT/Models/Serialization/Converters/CrdtTimestampJsonConverter.cs` | No description provided. |
| `$/Ama.CRDT/Models/Serialization/Converters/ObjectKeyDictionaryJsonConverter.cs` | A `JsonConverterFactory` that creates converters for dictionaries with non-string keys. It serializes them as a JSON array of [key, value] pairs to work around `System.Text.Json` limitations. |
| `$/Ama.CRDT/Models/Serialization/Converters/PolymorphicComparableJsonConverter.cs` | A custom `JsonConverter` for `IComparable` that enables polymorphic serialization by embedding a `$type` discriminator. It uses the same shared type registry as `PolymorphicObjectJsonConverter`. |
| `$/Ama.CRDT/Models/Serialization/Converters/PolymorphicObjectJsonConverter.cs` | A powerful `JsonConverter` for `object` types that enables robust polymorphic serialization by embedding a `$type` discriminator. It maintains a registry of known types for clean, short identifiers. |
| `$/Ama.CRDT/Models/Serialization/Converters/PolymorphicPartitionJsonConverter.cs` | A custom `JsonConverter` for `IPartition` that enables polymorphic serialization by embedding a `$type` discriminator. It uses the same shared type registry as `PolymorphicObjectJsonConverter`. |
| `$/Ama.CRDT/Models/Serialization/Converters/SeenExceptionsJsonConverter.cs` | A specialized `JsonConverter` for `CrdtMetadata.SeenExceptions` that ensures `CrdtOperation` elements are serialized and deserialized with polymorphism enabled for their `Value` property. |
| `$/Ama.CRDT/Models/Serialization/CrdtJsonContext.cs` | Provides centralized, pre-configured `JsonSerializerOptions` for CRDT models, establishing a best practice for serialization. |
| `$/Ama.CRDT/Models/Serialization/CrdtJsonTypeInfoResolver.cs` | A custom `IJsonTypeInfoResolver` that applies the `PolymorphicObjectJsonConverter` to all properties of type `object`, enabling robust polymorphic serialization. |
| `$/Ama.CRDT/Models/Serialization/CrdtMetadataJsonResolver.cs` | A custom `IJsonTypeInfoResolver` that modifies the serialization contract for `CrdtMetadata` to omit empty collections, resulting in a more compact JSON output. |
| `$/Ama.CRDT/Models/TreeAddNodePayload.cs` | A data structure for the payload of a tree 'add' operation. |
| `$/Ama.CRDT/Models/TreeMoveNodePayload.cs` | A data structure for the payload of a tree 'move' operation. |
| `$/Ama.CRDT/Models/TreeNode.cs` | No description provided. |
| `$/Ama.CRDT/Models/TreeRemoveNodePayload.cs` | A data structure for the payload of a tree 'remove' operation. |
| `$/Ama.CRDT/Models/TwoPhaseGraphState.cs` | No description provided. |
| `$/Ama.CRDT/Models/TwoPhaseSetState.cs` | No description provided. |
| `$/Ama.CRDT/Models/UnappliedOperation.cs` | Data structure representing a CRDT operation that could not be applied and its reason. |
| `$/Ama.CRDT/Models/VotePayload.cs` | A data structure for the payload of a vote operation, containing the voter's identifier and their chosen option. |
| `$/Ama.CRDT/PublicAPI.Shipped.txt` | Tracks the shipped public API surface of the library to detect breaking changes. This file should be updated when new APIs are officially released in a stable version. |
| `$/Ama.CRDT/PublicAPI.Unshipped.txt` | Tracks new public APIs that have not yet been included in a stable release. This file must be empty before a manual, stable publish. Build will fail if new public APIs are added without being added to this file first. |
| `$/Ama.CRDT/Services/Adapters/AsyncCrdtApplicatorAdapter.cs` | No description provided. |
| `$/Ama.CRDT/Services/Adapters/AsyncCrdtPatcherAdapter.cs` | An adapter that bridges the synchronous `ICrdtPatcher` to the asynchronous `IAsyncCrdtPatcher` pipeline, acting as the base layer in the patcher decorator chain. |
| `$/Ama.CRDT/Services/CrdtApplicator.cs` | No description provided. |
| `$/Ama.CRDT/Services/CrdtMetadataManager.cs` | Implements the `ICrdtMetadataManager` for managing and compacting CRDT metadata. It provides helper methods like Initialize(document) to create a metadata object from a POCO by reflecting on its properties, and Reset(metadata, document) to clear and re-initialize an existing metadata object. The initialization logic correctly traverses nested objects and collections. |
| `$/Ama.CRDT/Services/CrdtPatcher.cs` | Implements the logic to recursively compare two objects and generate a CRDT patch by delegating to property-specific strategies. It now also supports generating operations based on explicit intents via expression trees. |
| `$/Ama.CRDT/Services/CrdtScopeFactory.cs` | An implementation of `ICrdtScopeFactory` that uses the root `IServiceProvider` to create a new `IServiceScope` and configure it with a `ReplicaContext` holding the unique replica ID. |
| `$/Ama.CRDT/Services/Decorators/JournalingApplicatorDecorator.cs` | A decorator for `IAsyncCrdtApplicator` that intercepts patch applications and forwards successfully applied operations to an `ICrdtOperationJournal`. |
| `$/Ama.CRDT/Services/Decorators/JournalingIntentBuilderDecorator.cs` | A decorator for `IIntentBuilder` that ensures operations generated via fluent intents are recorded to the journal. |
| `$/Ama.CRDT/Services/Decorators/JournalingPatcherDecorator.cs` | A decorator for `IAsyncCrdtPatcher` that intercepts patch generation and explicitly created operations, forwarding them to an `ICrdtOperationJournal`. |
| `$/Ama.CRDT/Services/Decorators/PartitioningApplicatorDecorator.cs` | A global decorator implementation of `IAsyncCrdtApplicator` that intercepts patch application, skips non-partitionable types, handles partition streaming, and delegates internal CRDT operations to the inner applicator. |
| `$/Ama.CRDT/Services/DifferentiateObjectContext.cs` | Defines the context object for the `ICrdtPatcher.DifferentiateObject` method, encapsulating all necessary parameters. |
| `$/Ama.CRDT/Services/GarbageCollection/CompositeCompactionPolicy.cs` | Implements a composite compaction policy that evaluates an item as safe to compact if any of its underlying policies consider it safe. |
| `$/Ama.CRDT/Services/GarbageCollection/GlobalMinimumVersionPolicy.cs` | Implements a mathematically safe compaction policy based on the Global Minimum Version Vector (GMVV) across a cluster of replicas. |
| `$/Ama.CRDT/Services/GarbageCollection/ICompactionPolicy.cs` | Defines a policy that determines whether a CRDT tombstone or metadata entry is safe to delete, supporting both wall-clock timestamps and causal versions. |
| `$/Ama.CRDT/Services/GarbageCollection/ThresholdCompactionPolicy.cs` | Implements a heuristic-based compaction policy (TTL) that considers any timestamp or version older than a specified threshold as safe to compact. |
| `$/Ama.CRDT/Services/Helpers/PocoPathHelper.cs` | A utility class that centralizes reflection-based logic for CRDT strategies. It handles parsing JSON paths, resolving them against POCOs, getting and setting property values, and retrieving type information for collections and dictionaries. |
| `$/Ama.CRDT/Services/IAsyncCrdtApplicator.cs` | Defines the asynchronous contract for a service that applies a CRDT patch to a document. |
| `$/Ama.CRDT/Services/IAsyncCrdtPatcher.cs` | Defines the asynchronous contract for a service that compares two versions of a data model and generates a CRDT patch. |
| `$/Ama.CRDT/Services/ICrdtApplicator.cs` | No description provided. |
| `$/Ama.CRDT/Services/ICrdtMetadataManager.cs` | Defines a service for managing CRDT metadata. Its responsibilities include initializing, resetting, cloning, merging, and compacting metadata state such as LWW timestamps, positional trackers, and version vectors. This service is critical for enabling conflict-free merges by externalizing the state needed for resolution. |
| `$/Ama.CRDT/Services/ICrdtPatcher.cs` | Defines the contract for a service that compares two versions of a data model and generates a CRDT patch, as well as an intent-based method for creating patches directly. |
| `$/Ama.CRDT/Services/ICrdtScopeFactory.cs` | Defines the contract for a factory that creates isolated `IServiceScope` instances for CRDT replicas, each configured with a unique replica ID. |
| `$/Ama.CRDT/Services/Journaling/ICrdtOperationJournal.cs` | Defines a contract for an operation journal that captures explicitly generated and successfully applied CRDT operations, and allows retrieval of operations. |
| `$/Ama.CRDT/Services/Journaling/IJournalManager.cs` | No description provided. |
| `$/Ama.CRDT/Services/Journaling/JournalManager.cs` | Implements `IJournalManager` to retrieve missing operations based on `ReplicaSyncRequirement` by querying an underlying `ICrdtOperationJournal`. |
| `$/Ama.CRDT/Services/Metrics/MetricTimer.cs` | A helper `IDisposable` struct that uses a `Stopwatch` to measure the duration of a code block and records it to a `Histogram` upon disposal. |
| `$/Ama.CRDT/Services/Metrics/PartitionManagerCrdtMetrics.cs` | Provides `System.Diagnostics.Metrics` instruments for monitoring the performance and behavior of the `PartitionManager`. |
| `$/Ama.CRDT/Services/Partitioning/IPartitionManager.cs` | Defines the contract for managing a partitioned CRDT document, now supporting asynchronous streaming of partitions via `IAsyncEnumerable` and efficient counting of data partitions for a given logical key. It provides a user-friendly API using property names (`nameof`) and specific methods for header partitions. |
| `$/Ama.CRDT/Services/Partitioning/IPartitionStorageService.cs` | Defines a high-level abstraction for saving and loading partitioned CRDT data and metadata, hiding underlying stream operations. |
| `$/Ama.CRDT/Services/Partitioning/IPartitionableCrdtStrategy.cs` | Extends `ICrdtStrategy` for strategies that support data partitioning. It defines methods for splitting and merging partition data and metadata, and for extracting partition keys from operations and data models. |
| `$/Ama.CRDT/Services/Partitioning/PartitionManager.cs` | Manages a partitioned CRDT document, allowing it to scale beyond memory. It now explicitly separates logic for header and property partitions, using dedicated stream providers and strategy methods to avoid ambiguity. |
| `$/Ama.CRDT/Services/Providers/CrdtEntityBuilder.cs` | A generic scope builder mapping CRDT strategy selections to a specific type. |
| `$/Ama.CRDT/Services/Providers/CrdtModelBuilder.cs` | A fluent builder used to define CRDT mapping configurations across different models during service setup. |
| `$/Ama.CRDT/Services/Providers/CrdtModelRegistry.cs` | Implements `ICrdtModelRegistry` acting as a fast read-only dictionary for the Fluent API configuration mapping. |
| `$/Ama.CRDT/Services/Providers/CrdtPropertyBuilder.cs` | A generic scope builder defining strategies and decorators for a specific target property. |
| `$/Ama.CRDT/Services/Providers/CrdtStrategyProvider.cs` | No description provided. |
| `$/Ama.CRDT/Services/Providers/ElementComparerProvider.cs` | No description provided. |
| `$/Ama.CRDT/Services/Providers/EpochTimestampProvider.cs` | The default implementation of `ICrdtTimestampProvider` that generates `EpochTimestamp` based on Unix milliseconds. |
| `$/Ama.CRDT/Services/Providers/ICrdtModelRegistry.cs` | Defines an interface for a registry that maps POCO properties to their explicit CRDT strategies and decorators, bypassing attributes. |
| `$/Ama.CRDT/Services/Providers/ICrdtStrategyProvider.cs` | No description provided. |
| `$/Ama.CRDT/Services/Providers/ICrdtTimestampProvider.cs` | Defines a service for generating CRDT timestamps, allowing for custom timestamp implementations. |
| `$/Ama.CRDT/Services/Providers/IElementComparer.cs` | No description provided. |
| `$/Ama.CRDT/Services/Providers/IElementComparerProvider.cs` | No description provided. |
| `$/Ama.CRDT/Services/ReplicaContext.cs` | A scoped service that holds the unique identifier for a CRDT replica, making it available to other scoped services within the same `IServiceScope`. |
| `$/Ama.CRDT/Services/Strategies/ApplyOperationContext.cs` | Defines the context for an <see cref="ICrdtStrategy.ApplyOperation"/> call, encapsulating all necessary parameters for applying a single CRDT operation to a document. This context is now simplified as strategies use centralized helpers for reflection. |
| `$/Ama.CRDT/Services/Strategies/ArrayLcsStrategy.cs` | Implements a CRDT strategy for arrays using LCS. It supports type-specific element comparers and partitioning, using centralized reflection helpers. |
| `$/Ama.CRDT/Services/Strategies/AverageRegisterStrategy.cs` | Implements the Average Register strategy. It now uses centralized reflection helpers from `PocoPathHelper` to apply the calculated average value. |
| `$/Ama.CRDT/Services/Strategies/BoundedCounterStrategy.cs` | Implements a counter that is clamped within a specified minimum and maximum value. It now uses centralized reflection helpers from `PocoPathHelper`. |
| `$/Ama.CRDT/Services/Strategies/CompactionContext.cs` | Defines the context for an `ICrdtStrategy.Compact` call, encapsulating parameters for garbage collection. |
| `$/Ama.CRDT/Services/Strategies/CounterMapStrategy.cs` | Implements the Counter-Map strategy, where each key in a dictionary is treated as an independent PN-Counter. |
| `$/Ama.CRDT/Services/Strategies/CounterStrategy.cs` | Implements the CRDT Counter strategy. It now uses centralized reflection helpers from `PocoPathHelper` to get the current value and apply the increment. |
| `$/Ama.CRDT/Services/Strategies/Decorators/ApprovalQuorumStrategy.cs` | A decorator strategy that tracks pending proposals and requires a quorum of replicas to propose the exact same operation before it is applied. |
| `$/Ama.CRDT/Services/Strategies/Decorators/EpochBoundStrategy.cs` | No description provided. |
| `$/Ama.CRDT/Services/Strategies/FixedSizeArrayStrategy.cs` | Implements a strategy for fixed-size arrays where each index is an LWW-Register. It now uses centralized reflection helpers from `PocoPathHelper`. |
| `$/Ama.CRDT/Services/Strategies/FwwMapStrategy.cs` | Implements the FWW-Map strategy, a partitioned dictionary where each key-value pair resolves conflicts by picking the value with the lowest timestamp. |
| `$/Ama.CRDT/Services/Strategies/FwwSetStrategy.cs` | Implements the FWW-Set strategy, a partitioned collection where conflicts are resolved by retaining the state of the earliest operation. |
| `$/Ama.CRDT/Services/Strategies/FwwStrategy.cs` | Implements the First-Writer-Wins (FWW) strategy for conflict resolution, resolving concurrent modifications by selecting the value with the lowest timestamp. |
| `$/Ama.CRDT/Services/Strategies/GCounterStrategy.cs` | Implements the G-Counter (Grow-Only Counter) strategy, which only allows for positive increments. |
| `$/Ama.CRDT/Services/Strategies/GSetStrategy.cs` | Implements the G-Set (Grow-Only Set) CRDT strategy. It now uses centralized reflection helpers from `PocoPathHelper` to get collection element types. |
| `$/Ama.CRDT/Services/Strategies/GenerateOperationContext.cs` | Defines the context for explicitly generating intent-based operations in strategies. |
| `$/Ama.CRDT/Services/Strategies/GeneratePatchContext.cs` | Defines the context object for the `ICrdtStrategy.GeneratePatch` method, encapsulating all necessary parameters. |
| `$/Ama.CRDT/Services/Strategies/GraphStrategy.cs` | Implements a CRDT strategy for graph data structures, treating vertices and edges as a grow-only set, suitable for modeling relationships and networks. |
| `$/Ama.CRDT/Services/Strategies/ICrdtStrategy.cs` | Defines the contract for a strategy, including `GeneratePatch` for creating operations, explicit intent operations generation, and `ApplyOperation` for data manipulation, using context objects for parameters. |
| `$/Ama.CRDT/Services/Strategies/LseqStrategy.cs` | Implements the LSEQ strategy for ordered sequences. It now uses centralized reflection helpers from `PocoPathHelper`. |
| `$/Ama.CRDT/Services/Strategies/LwwMapStrategy.cs` | Implements the LWW-Map (Last-Writer-Wins Map) CRDT strategy. It now uses centralized reflection helpers from `PocoPathHelper` to get dictionary key/value types. |
| `$/Ama.CRDT/Services/Strategies/LwwSetStrategy.cs` | Implements the LWW-Set (Last-Writer-Wins Set) CRDT strategy. It now uses centralized reflection helpers from `PocoPathHelper`. |
| `$/Ama.CRDT/Services/Strategies/LwwStrategy.cs` | Implements the LWW strategy. It now uses centralized reflection helpers from `PocoPathHelper` to apply changes to nodes. |
| `$/Ama.CRDT/Services/Strategies/MaxWinsMapStrategy.cs` | Implements a value-based Max-Wins Map strategy. For each key, conflicts are resolved by choosing the highest value, making the map's keys grow-only. |
| `$/Ama.CRDT/Services/Strategies/MaxWinsStrategy.cs` | Implements the Max-Wins Register strategy. It now uses centralized reflection helpers from `PocoPathHelper` to get the current value and apply updates. |
| `$/Ama.CRDT/Services/Strategies/MinWinsMapStrategy.cs` | Implements a value-based Min-Wins Map strategy. For each key, conflicts are resolved by choosing the lowest value, making the map's keys grow-only. |
| `$/Ama.CRDT/Services/Strategies/MinWinsStrategy.cs` | Implements the Min-Wins Register strategy. It now uses centralized reflection helpers from `PocoPathHelper` to get the current value and apply updates. |
| `$/Ama.CRDT/Services/Strategies/OrMapStrategy.cs` | Implements the OR-Map (Observed-Remove Map) CRDT strategy. It uses centralized reflection helpers and supports partitioning. |
| `$/Ama.CRDT/Services/Strategies/OrSetStrategy.cs` | Implements the OR-Set (Observed-Remove Set) CRDT strategy. It now uses centralized reflection helpers from `PocoPathHelper`. |
| `$/Ama.CRDT/Services/Strategies/PriorityQueueStrategy.cs` | Implements a strategy for collections that behave as a priority queue. It now uses centralized reflection helpers from `PocoPathHelper`. |
| `$/Ama.CRDT/Services/Strategies/ReplicatedTreeStrategy.cs` | Implements a CRDT strategy for tree data structures, allowing nodes to be added, removed, and moved concurrently. It uses unique tags (similar to OR-Set) for adds and LWW for moves to ensure convergence. |
| `$/Ama.CRDT/Services/Strategies/RgaStrategy.cs` | Implements the Replicated Growable Array (RGA) strategy. Evaluates insertions as an ordered linked set and applies deletes by emitting tombstones. Also handles intent based operation generation. |
| `$/Ama.CRDT/Services/Strategies/SortedSetStrategy.cs` | Implements a CRDT strategy for sorted sets using LCS. It now uses centralized reflection helpers from `PocoPathHelper`. |
| `$/Ama.CRDT/Services/Strategies/StateMachineStrategy.cs` | Implements the State Machine strategy. It now uses centralized reflection helpers from `PocoPathHelper` to get/set state values. |
| `$/Ama.CRDT/Services/Strategies/TwoPhaseGraphStrategy.cs` | Implements a 2P-Graph strategy where vertices and edges can be added and removed, but not re-added after removal, ensuring monotonic growth of the tombstone sets. |
| `$/Ama.CRDT/Services/Strategies/TwoPhaseSetStrategy.cs` | Implements the 2P-Set (Two-Phase Set) CRDT strategy. It now uses centralized reflection helpers from `PocoPathHelper`. |
| `$/Ama.CRDT/Services/Strategies/VoteCounterStrategy.cs` | Implements the Vote Counter strategy. It now uses centralized reflection helpers from `PocoPathHelper` to get dictionary key/value types. |
| `$/Ama.CRDT/Services/Versioning/IVersionVectorSyncService.cs` | No description provided. |
| `$/Ama.CRDT/Services/Versioning/VersionVectorSyncService.cs` | No description provided. |
| `$/CodingStandards.md` | No description provided. |
| `$/FilesDescription.md` | No description provided. |
| `$/LICENSE` | No description provided. |
| `$/README.md` | No description provided. |
| `$/Scripts/refactor-strategies.csx` | A C# script used to automate the injection of the `Compact` method into all strategy files using an AI agent. |
| `$/Specs/add-approval-quorum-strategy.md` | No description provided. |
| `$/Specs/add-leader-election-strategy.md` | No description provided. |
| `$/Specs/add-more-text-specific-strategies.md` | No description provided. |
| `$/Specs/create-larger-than-memory-showcase.md` | No description provided. |
| `$/Specs/done/add-composite-partition-keys.md` | No description provided. |
| `$/Specs/done/add-counter-map-strategy.md` | No description provided. |
| `$/Specs/done/add-exclusive-lock-strategy.md` | No description provided. |
| `$/Specs/done/add-more-list-and-sequence-strategies.md` | No description provided. |
| `$/Specs/done/add-more-numeric-and-value-based-strategies.md` | No description provided. |
| `$/Specs/done/add-more-object-and-map-strategies.md` | No description provided. |
| `$/Specs/done/add-more-set-strategies.md` | No description provided. |
| `$/Specs/done/add-more-specialized-data-structure-strategies.md` | No description provided. |
| `$/Specs/done/add-roslyn-analyzers.md` | No description provided. |
| `$/Specs/done/add-state-machine-strategy.md` | No description provided. |
| `$/Specs/done/add-vote-counter-strategy.md` | No description provided. |
| `$/Specs/done/create-example-console-app-that-show-cases-the-crdts-with-out-locks.md` | No description provided. |
| `$/Specs/done/fix-the-brittle-header-partition.md` | No description provided. |
| `$/Specs/done/implement-correctly-lcs-list-strategy.md` | No description provided. |
| `$/Specs/done/introduce-partitioning-for-larger-than-memory-data.md` | No description provided. |
| `$/Specs/done/make-package-dev-friendly.md` | No description provided. |
| `$/Specs/done/make-the-api-surface-better.md` | No description provided. |
| `$/Specs/done/partition-manager-needs-to-be-data-neutral.md` | No description provided. |
| `$/Specs/done/publish-as-a-nuget-package.md` | No description provided. |
| `$/Specs/done/readme-update-2025-08-24.md` | No description provided. |
| `$/Specs/done/support-multiple-properties-for-partition-manager.md` | No description provided. |
| `$/Templates/CreateDecoratorStrategy.md` | No description provided. |
| `$/Templates/CreateStrategy.md` | No description provided. |
| `$/Templates/CreateStrategyUnitTests.md` | No description provided. |
| `$/Templates/UpdateSerializationTests.md` | No description provided. |
| `$/Templates/UpdateStrategyBenchmarks.md` | No description provided. |
| `$/docs/architecture.md` | Contains a high-level overview of the library's internal abstractions such as ICrdtPatcher and ICrdtApplicator. |
| `$/docs/explicit-intents.md` | Documents the fluent builder API for generating exact, strongly-typed CRDT intent operations. |
| `$/docs/extensibility.md` | Provides examples and guides for extending Ama.CRDT, including custom strategies, IElementComparer, and ICrdtTimestampProvider. |
| `$/docs/journaling.md` | Explains the operation journaling feature, how to configure decorators to automatically record operations, and how to use it for advanced synchronization. |
| `$/docs/metadata-management.md` | Contains best practices for managing CRDT metadata size, detailing techniques like tombstone pruning, version vector compaction, and optimized serialization. |
| `$/docs/multi-replica-and-serialization.md` | Explains advanced synchronization mechanics for multi-node setups and demonstrates configuring `CrdtPatch` JSON serialization correctly. |
| `$/docs/partitioning.md` | Documents the setup and usage of the library's Larger-Than-Memory partitioning features and how to use the IPartitionManager. |
| `$/docs/strategies-reference.md` | Provides a comprehensive table detailing all available CRDT strategies (e.g., LWW, Counters, Sets, Maps) and their ideal use cases. |
| `$/dotnet-tools.json` | No description provided. |
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
| `$/features/design.png` | No description provided. |
| `$/features/i-want-to-create-a-crdt-structure-for-all-json-to-be-able-to-replicate-across-services-specs/01-core-crdt-data-structures.md` | No description provided. |
| `$/features/i-want-to-create-a-crdt-structure-for-all-json-to-be-able-to-replicate-across-services-specs/02-json-diff-and-patch-generation.md` | No description provided. |
| `$/features/i-want-to-create-a-crdt-structure-for-all-json-to-be-able-to-replicate-across-services-specs/03-json-patch-application.md` | No description provided. |
| `$/features/i-want-to-create-a-crdt-structure-for-all-json-to-be-able-to-replicate-across-services-specs/04-public-api-and-integration.md` | No description provided. |
| `$/features/i-want-to-create-a-crdt-structure-for-all-json-to-be-able-to-replicate-across-services-specs/put-the-lww-structures-in-metadata.md` | No description provided. |
| `$/features/i-want-to-create-a-crdt-structure-for-all-json-to-be-able-to-replicate-across-services.md` | No description provided. |
| `$/features/refactoring-of-streams-to-be-handled-by-special-service-specs/01-create-partition-serialization-service.md` | No description provided. |
| `$/features/refactoring-of-streams-to-be-handled-by-special-service-specs/02-define-partition-storage-interface.md` | No description provided. |
| `$/features/refactoring-of-streams-to-be-handled-by-special-service-specs/03-implement-bplustree-storage-service.md` | No description provided. |
| `$/features/refactoring-of-streams-to-be-handled-by-special-service-specs/04-refactor-partition-manager.md` | No description provided. |
| `$/features/refactoring-of-streams-to-be-handled-by-special-service-specs/05-update-tests-and-showcase.md` | No description provided. |
| `$/features/refactoring-of-streams-to-be-handled-by-special-service.md` | No description provided. |
| `$/solution.settings.json` | No description provided. |
