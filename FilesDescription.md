| File Path | Description |
| --- | --- |
| `$/.editorconfig` | Configures coding standards, formatting rules, and enables specific .NET analyzer rules such as CA2007 for enforcing `ConfigureAwait` usage. |
| `$/.github/workflows/ci.yml` | GitHub Actions workflow for building and testing the project on pushes to non-master branches and on pull requests to master. This ensures code quality before merging. |
| `$/.github/workflows/codeql.yml` | No description provided. |
| `$/.github/workflows/publish-preview-to-nuget.yml` | No description provided. |
| `$/.github/workflows/publish-release-to-nuget-manual.yml` | No description provided. |
| `$/.gitignore` | No description provided. |
| `$/Ama.CRDT.Analyzers.UnitTests/Ama.CRDT.Analyzers.UnitTests.csproj` | The project file for the unit tests of the Roslyn analyzers. |
| `$/Ama.CRDT.Analyzers.UnitTests/CrdtDecoratorBehaviorAnalyzerTests.cs` | Contains unit tests for `CrdtDecoratorBehaviorAnalyzer`, verifying it correctly allows valid behavior DI registrations and reports diagnostics for unsupported ones. |
| `$/Ama.CRDT.Analyzers.UnitTests/CrdtIntentUsageAnalyzerTests.cs` | Contains unit tests for the `CrdtIntentUsageAnalyzer`, verifying that it correctly identifies valid and invalid usages of explicit operation intents. |
| `$/Ama.CRDT.Analyzers.UnitTests/CrdtSerializablePropertyTypeAnalyzerTests.cs` | Contains unit tests for `CrdtSerializablePropertyTypeAnalyzer`, verifying registration checks for declared types and property initializers. |
| `$/Ama.CRDT.Analyzers.UnitTests/CrdtStrategyTypeAnalyzerTests.cs` | Contains unit tests for the `CrdtStrategyTypeAnalyzer`, verifying that it correctly identifies valid and invalid applications of CRDT strategy attributes. |
| `$/Ama.CRDT.Analyzers/Ama.CRDT.Analyzers.csproj` | The project file for the Roslyn analyzers that validate CRDT strategy usage. It is configured to be bundled with the main `Ama.CRDT` NuGet package and not as a standalone package. |
| `$/Ama.CRDT.Analyzers/CrdtDecoratorBehaviorAnalyzer.cs` | Roslyn analyzer (CRDT0004) to ensure that DI registrations of decorators specify a behavior that is permitted by the decorator's `[AllowedDecoratorBehavior]` attributes. |
| `$/Ama.CRDT.Analyzers/CrdtIntentUsageAnalyzer.cs` | Roslyn analyzer to validate that explicit operation intents are supported by the corresponding property's CRDT strategy. |
| `$/Ama.CRDT.Analyzers/CrdtSerializablePropertyTypeAnalyzer.cs` | Roslyn analyzer (CRDT0003) to ensure complex property types of registered models are also explicitly registered with `[CrdtAotType]` on the `CrdtAotContext`. |
| `$/Ama.CRDT.Analyzers/CrdtStrategyTypeAnalyzer.cs` | No description provided. |
| `$/Ama.CRDT.Benchmarks/Ama.CRDT.Benchmarks.csproj` | The project file for the benchmarks application. |
| `$/Ama.CRDT.Benchmarks/AntiVirusFriendlyConfig.cs` | No description provided. |
| `$/Ama.CRDT.Benchmarks/Benchmarks/ApplicatorBenchmarks.cs` | Contains benchmarks for the `CrdtApplicator` service. |
| `$/Ama.CRDT.Benchmarks/Benchmarks/PatcherBenchmarks.cs` | Contains benchmarks for the `CrdtPatcher` service. |
| `$/Ama.CRDT.Benchmarks/Benchmarks/StrategyApplyBenchmarks.cs` | Contains benchmarks for `ApplyOperation` for every individual CRDT strategy in isolation. |
| `$/Ama.CRDT.Benchmarks/Benchmarks/StrategyGenerateBenchmarks.cs` | Contains benchmarks for `GeneratePatch` for every individual CRDT strategy in isolation. |
| `$/Ama.CRDT.Benchmarks/Benchmarks/StrategyGenerateOperationBenchmarks.cs` | Contains benchmarks for `GenerateOperation` for every individual CRDT strategy in isolation using explicit operation intents. |
| `$/Ama.CRDT.Benchmarks/Models/BenchmarkCrdtContext.cs` | AOT JSON serialization and `CrdtAotContext` for the benchmark models. |
| `$/Ama.CRDT.Benchmarks/Models/ComplexPoco.cs` | A complex data model with nested objects and arrays for benchmarking recursive and collection-based scenarios. |
| `$/Ama.CRDT.Benchmarks/Models/SimplePoco.cs` | A simple data model for benchmarking basic scenarios. |
| `$/Ama.CRDT.Benchmarks/Models/StrategyPoco.cs` | A data model containing properties decorated with attributes for each supported CRDT strategy, used for isolated strategy benchmarking. |
| `$/Ama.CRDT.Benchmarks/Program.cs` | The entry point for the benchmark runner. |
| `$/Ama.CRDT.Benchmarks/README.md` | No description provided. |
| `$/Ama.CRDT.IntegrationTests/Ama.CRDT.IntegrationTests.csproj` | No description provided. |
| `$/Ama.CRDT.IntegrationTests/ClockSkewSimulationTests.cs` | Contains integration tests simulating clock skew scenarios using Hybrid Logical Clocks (HLC) and skewed timestamp providers with the `CrdtTimestamp` struct. |
| `$/Ama.CRDT.IntegrationTests/NetworkSimulationTests.cs` | Contains integration tests simulating complex network scenarios (out-of-order delivery, partitions, long-term offline nodes, duplicate delivery, and high concurrency conflicts) to guarantee CRDT convergence and idempotency over time. |
| `$/Ama.CRDT.IntegrationTests/ShardingSimulationTests.cs` | Contains integration tests confirming the accuracy of per-shard causal tracking and resolving Shard IDs from documents, document IDs, and hosted types. |
| `$/Ama.CRDT.LargerThanMemory.Streams.UnitTests/Ama.CRDT.LargerThanMemory.Streams.UnitTests.csproj` | No description provided. |
| `$/Ama.CRDT.LargerThanMemory.Streams.UnitTests/Serialization/DefaultPartitionSerializationServiceTests.cs` | No description provided. |
| `$/Ama.CRDT.LargerThanMemory.Streams.UnitTests/Services/StreamSpaceAllocatorTests.cs` | No description provided. |
| `$/Ama.CRDT.LargerThanMemory.Streams.UnitTests/StreamChunkStorageServiceDataTests.cs` | No description provided. |
| `$/Ama.CRDT.LargerThanMemory.Streams.UnitTests/StreamChunkStorageServiceIndexTests.cs` | No description provided. |
| `$/Ama.CRDT.LargerThanMemory.Streams/Ama.CRDT.LargerThanMemory.Streams.csproj` | The project file for the larger-than-memory streams package, targeting .NET 10 and configured for NuGet packaging and Native AOT compatibility. |
| `$/Ama.CRDT.LargerThanMemory.Streams/Extensions/StreamLargerThanMemoryServiceCollectionExtensions.cs` | No description provided. |
| `$/Ama.CRDT.LargerThanMemory.Streams/Models/BPlusTreeNode.cs` | No description provided. |
| `$/Ama.CRDT.LargerThanMemory.Streams/Models/BTreeHeader.cs` | No description provided. |
| `$/Ama.CRDT.LargerThanMemory.Streams/Models/DataStreamHeader.cs` | No description provided. |
| `$/Ama.CRDT.LargerThanMemory.Streams/Models/FreeSpaceState.cs` | No description provided. |
| `$/Ama.CRDT.LargerThanMemory.Streams/Models/Serialization/StreamsJsonContext.cs` | No description provided. |
| `$/Ama.CRDT.LargerThanMemory.Streams/PublicAPI.Shipped.txt` | No description provided. |
| `$/Ama.CRDT.LargerThanMemory.Streams/PublicAPI.Unshipped.txt` | No description provided. |
| `$/Ama.CRDT.LargerThanMemory.Streams/README.md` | Contains the README for the `Ama.CRDT.LargerThanMemory.Streams` package, including configuration examples, performance counter commands, and references to `IChunkStreamProvider`. |
| `$/Ama.CRDT.LargerThanMemory.Streams/Services/IChunkStreamProvider.cs` | No description provided. |
| `$/Ama.CRDT.LargerThanMemory.Streams/Services/Metrics/StreamsCrdtMetrics.cs` | No description provided. |
| `$/Ama.CRDT.LargerThanMemory.Streams/Services/Serialization/DefaultChunkSerializationService.cs` | No description provided. |
| `$/Ama.CRDT.LargerThanMemory.Streams/Services/Serialization/IChunkSerializationService.cs` | No description provided. |
| `$/Ama.CRDT.LargerThanMemory.Streams/Services/StreamChunkStorageService.cs` | No description provided. |
| `$/Ama.CRDT.LargerThanMemory.Streams/Services/StreamSpaceAllocator.cs` | No description provided. |
| `$/Ama.CRDT.Project.Analyzers.UnitTests/Ama.CRDT.Project.Analyzers.UnitTests.csproj` | No description provided. |
| `$/Ama.CRDT.Project.Analyzers.UnitTests/PropertyInfoUsageAnalyzerTests.cs` | Contains unit tests for `PropertyInfoUsageAnalyzer`, verifying that it catches dynamic reflection methods while ignoring AOT-safe metadata access. |
| `$/Ama.CRDT.Project.Analyzers.UnitTests/SystemConvertUsageAnalyzerTests.cs` | No description provided. |
| `$/Ama.CRDT.Project.Analyzers/Ama.CRDT.Project.Analyzers.csproj` | No description provided. |
| `$/Ama.CRDT.Project.Analyzers/PropertyInfoUsageAnalyzer.cs` | Roslyn analyzer to forbid the use of dynamic reflection methods (`GetValue`, `SetValue`, `Activator.CreateInstance`) to ensure strict Native AOT compatibility across the library, while allowing safe metadata access (`.Name`, `.PropertyType`). |
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
| `$/Ama.CRDT.ShowCase.CollaborativeEditing/Ama.CRDT.ShowCase.CollaborativeEditing.csproj` | No description provided. |
| `$/Ama.CRDT.ShowCase.CollaborativeEditing/Controls/IntentTextBox.cs` | A custom TextBox control that behaves like a standard multiline text editor, but internally uses linear diffing to calculate and emit explicit CRDT intents (Insert, SetIndex, Remove) during typing. It directly integrates `IAsyncCrdtPatcher` enabling Roslyn analyzers to validate its intent operations against the model strategies. |
| `$/Ama.CRDT.ShowCase.CollaborativeEditing/EditorForm.cs` | An editor GUI capable of syncing its local state and incoming patches correctly using timers. |
| `$/Ama.CRDT.ShowCase.CollaborativeEditing/EditorForm.resx` | No description provided. |
| `$/Ama.CRDT.ShowCase.CollaborativeEditing/MainForm.Designer.cs` | No description provided. |
| `$/Ama.CRDT.ShowCase.CollaborativeEditing/MainForm.cs` | No description provided. |
| `$/Ama.CRDT.ShowCase.CollaborativeEditing/MainForm.resx` | No description provided. |
| `$/Ama.CRDT.ShowCase.CollaborativeEditing/Models/CollaborativeEditingCrdtContext.cs` | AOT reflection context (`CrdtAotContext`) for the showcase models to be used by the internal reflection-free routines. |
| `$/Ama.CRDT.ShowCase.CollaborativeEditing/Models/CollaborativeEditingJsonContext.cs` | AOT JSON serialization context for the collaborative editing showcase models. |
| `$/Ama.CRDT.ShowCase.CollaborativeEditing/Models/NetworkMessage.cs` | DTO representing a network message containing a patch and sender ID. |
| `$/Ama.CRDT.ShowCase.CollaborativeEditing/Models/SharedDocument.cs` | Model representing a shared document of strings synced across replicas using ArrayLcsStrategy. |
| `$/Ama.CRDT.ShowCase.CollaborativeEditing/Program.cs` | No description provided. |
| `$/Ama.CRDT.ShowCase.CollaborativeEditing/README.md` | Provides an overview and user guide for the Collaborative Editing showcase, highlighting the use of the RGA Strategy, Explicit Intents, Operation Journaling, and GMVV-based Garbage Collection. |
| `$/Ama.CRDT.ShowCase.CollaborativeEditing/Services/MemoryJournal.cs` | An in-memory operation journal implementation providing missing operation histories and journal truncation based on the cluster's GMVV. |
| `$/Ama.CRDT.ShowCase.CollaborativeEditing/Services/NetworkBroker.cs` | Simulates a network passing CRDT patches to different editors in real-time. |
| `$/Ama.CRDT.ShowCase.LargerThanMemory/Ama.CRDT.ShowCase.LargerThanMemory.csproj` | The project file for the larger-than-memory showcase console application, including a reference to `Microsoft.Data.Sqlite`. |
| `$/Ama.CRDT.ShowCase.LargerThanMemory/Models/BlogPost.cs` | A model representing a blog post with a collection of comments for the larger-than-memory showcase. |
| `$/Ama.CRDT.ShowCase.LargerThanMemory/Models/Comment.cs` | A simple record representing a comment in the blog post. |
| `$/Ama.CRDT.ShowCase.LargerThanMemory/Models/LargerThanMemoryCrdtContext.cs` | Provides a Native AOT compatible `CrdtAotContext` generated for the models used in the Larger-Than-Memory showcase, allowing execution without runtime reflection. |
| `$/Ama.CRDT.ShowCase.LargerThanMemory/Models/LargerThanMemoryJsonContext.cs` | AOT JSON serialization context for the larger-than-memory showcase models. |
| `$/Ama.CRDT.ShowCase.LargerThanMemory/Program.cs` | The entry point for the larger-than-memory showcase application, configuring DI and registering `BlogPostDocumentIdProvider`. |
| `$/Ama.CRDT.ShowCase.LargerThanMemory/README.md` | Provides an overview of the larger-than-memory showcase, explaining how to run it, the features it demonstrates (partitioning, journaling, disconnected sync), and how to interact with the terminal UI. |
| `$/Ama.CRDT.ShowCase.LargerThanMemory/Services/BlogPostDocumentIdProvider.cs` | A custom implementation of `IDocumentIdProvider` for `BlogPost` explicitly bypassing reflection to ensure fast ID extractions. |
| `$/Ama.CRDT.ShowCase.LargerThanMemory/Services/BlogPostReadRepository.cs` | A straightforward SQLite read repository for providing ultra-fast, paginated access to projected CQRS data in the terminal interface. |
| `$/Ama.CRDT.ShowCase.LargerThanMemory/Services/BlogPostSqliteProjector.cs` | An implementation of `IVirtualDocumentProjector<T>` that listens to incoming CRDT applications and pushes the result natively to a per-replica SQLite DB for real-time reads. |
| `$/Ama.CRDT.ShowCase.LargerThanMemory/Services/DataGeneratorService.cs` | A service responsible for programmatically generating a configurable number of blog posts, each with a random number of comments, to demonstrate the system's ability to handle large, partitioned datasets. |
| `$/Ama.CRDT.ShowCase.LargerThanMemory/Services/FileSystemChunkStreamProvider.cs` | No description provided. |
| `$/Ama.CRDT.ShowCase.LargerThanMemory/Services/FileSystemOperationJournal.cs` | An example implementation of `ICrdtOperationJournal` that persists applied CRDT operations to a local JSON file per replica, demonstrating how to save operations for external sync protocols or offline usage. |
| `$/Ama.CRDT.ShowCase.LargerThanMemory/Services/SimpleFaker.cs` | A lightweight, Native AOT-compatible random data generator used for generating showcase items. |
| `$/Ama.CRDT.ShowCase.LargerThanMemory/Services/UiService.cs` | Implements the terminal user interface, reading presentation data via SQLite off the main CRDT flow to demonstrate CQRS. |
| `$/Ama.CRDT.ShowCase.LargerThanMemory/SimulationRunner.cs` | Orchestrates the showcase by checking for existing data, triggering the data generation process if needed, and launching the user interface using `IVirtualDocumentManager`. |
| `$/Ama.CRDT.ShowCase/Ama.CRDT.ShowCase.csproj` | The project file for the showcase console application. |
| `$/Ama.CRDT.ShowCase/Models/ShowcaseCrdtContext.cs` | AOT reflection context (`CrdtAotContext`) for the showcase models to be used by the internal reflection-free routines. |
| `$/Ama.CRDT.ShowCase/Models/ShowcaseJsonContext.cs` | AOT JSON serialization context for the showcase models. |
| `$/Ama.CRDT.ShowCase/Models/User.cs` | A simple data model representing a user, used as an element in the CRDT-managed array. |
| `$/Ama.CRDT.ShowCase/Models/UserStats.cs` | The main POCO representing the shared state, decorated with CRDT strategy attributes (`CrdtCounter`, `CrdtArrayLcsStrategy`, `LwwStrategy`). |
| `$/Ama.CRDT.ShowCase/Program.cs` | The main entry point of the console application, responsible for setting up dependency injection and starting the simulation. |
| `$/Ama.CRDT.ShowCase/README.md` | Provides an overview and explanation of the multi-replica, lock-free distributed simulation showcase for the CRDT library. |
| `$/Ama.CRDT.ShowCase/Services/CaseInsensitiveStringComparer.cs` | A custom implementation of `IElementComparer` that allows the `ArrayLcsStrategy` to identify unique strings using a case-insensitive comparison. |
| `$/Ama.CRDT.ShowCase/Services/IInMemoryDatabaseService.cs` | Defines the contract for a simple in-memory key-value store to simulate persistence for each replica's state (document and metadata). |
| `$/Ama.CRDT.ShowCase/Services/InMemoryDatabaseService.cs` | An implementation of `IInMemoryDatabaseService` using `ConcurrentDictionary` to simulate a database for CRDT documents and metadata. |
| `$/Ama.CRDT.ShowCase/SimulationRunner.cs` | Orchestrates the distributed map-reduce simulation using concurrent producers, mappers, and convergers communicating via channels to demonstrate CRDT convergence. |
| `$/Ama.CRDT.SourceGenerators.UnitTests/Ama.CRDT.SourceGenerators.UnitTests.csproj` | No description provided. |
| `$/Ama.CRDT.SourceGenerators.UnitTests/CrdtContextGeneratorTests.cs` | Tests the C# code output emitted by the `CrdtContextGenerator`, validating reflection-free attribute strategy bindings. |
| `$/Ama.CRDT.SourceGenerators/Ama.CRDT.SourceGenerators.csproj` | No description provided. |
| `$/Ama.CRDT.SourceGenerators/CrdtContextGenerator.cs` | The Roslyn Source Generator that implements `CrdtAotContext` and generates explicit property metadata and precalculated CRDT strategies to avoid Native AOT trim warnings. |
| `$/Ama.CRDT.UnitTests/Ama.CRDT.UnitTests.csproj` | No description provided. |
| `$/Ama.CRDT.UnitTests/Architecture/ModelConventionTests.cs` | An architecture convention unit test that reflects over all data models to enforce the presence of serialization tests by scanning the test source files. |
| `$/Ama.CRDT.UnitTests/Architecture/StrategyConventionTests.cs` | An architecture convention unit test that reflects over all strategies to enforce the presence of unit tests, property tests, benchmarks, and documentation. |
| `$/Ama.CRDT.UnitTests/Extensions/AsyncCrdtApplicatorExtensionsTests.cs` | Contains unit tests for `AsyncCrdtApplicatorExtensions`, verifying the correct asynchronous streaming of journaled operations and their translation into patches for the applicator. |
| `$/Ama.CRDT.UnitTests/Extensions/ServiceCollectionExtensionsTests.cs` | Unit tests for `ServiceCollectionExtensions`, validating service registrations, global modifier execution, and the aggregate JSON type info resolver. |
| `$/Ama.CRDT.UnitTests/Models/CrdtTimestampTests.cs` | Contains unit tests for the `CrdtTimestamp` struct, covering multi-component ordering (`Epoch`, `Ticks`, `Counter`, `ReplicaId`), relational operators, equality, and `MinValue`. |
| `$/Ama.CRDT.UnitTests/Models/DottedVersionVectorTests.cs` | No description provided. |
| `$/Ama.CRDT.UnitTests/Models/LargerThanMemory/CompositeChunkKeyTests.cs` | No description provided. |
| `$/Ama.CRDT.UnitTests/Models/Serialization/CoreModelSerializationTests.cs` | Contains unit tests for serializing core library models like `ApplyPatchResult`, `JournaledOperation`, `UnappliedOperation`, and `DottedVersionVector`. |
| `$/Ama.CRDT.UnitTests/Models/Serialization/CrdtDocumentSerializationTests.cs` | Contains unit tests for serializing the `CrdtDocument<T>` struct, including scenarios with null data or metadata, and compact serialization options. |
| `$/Ama.CRDT.UnitTests/Models/Serialization/CrdtMetadataSerializationTests.cs` | Contains unit tests for the serialization and deserialization of the `CrdtMetadata` class, verifying both default and compact serialization options and ensuring polymorphic and complex data (e.g., non-string dictionary keys, nested collections) is handled correctly. |
| `$/Ama.CRDT.UnitTests/Models/Serialization/CrdtTimestampJsonConverterTests.cs` | Contains unit tests for the JSON serialization of `CrdtTimestamp` (both directly and as a polymorphic payload) and `CrdtTypeRegistry` custom type registration. |
| `$/Ama.CRDT.UnitTests/Models/Serialization/IntentModelSerializationTests.cs` | Contains unit tests for serializing all explicit intent models to ensure they map successfully to JSON. |
| `$/Ama.CRDT.UnitTests/Models/Serialization/LargerThanMemoryModelSerializationTests.cs` | No description provided. |
| `$/Ama.CRDT.UnitTests/Models/Serialization/SyncRequirementSerializationTests.cs` | Contains unit tests for serializing synchronization requirement models like `ReplicaSyncRequirement`, `OriginSyncRequirement`, and `BidirectionalSyncRequirements`. |
| `$/Ama.CRDT.UnitTests/Models/Serialization/TestJsonSerializerContext.cs` | Provides test-specific `JsonSerializerContext` to enable AOT-compatible testing for internal mock classes and test-defined generics. |
| `$/Ama.CRDT.UnitTests/Models/Serialization/TestOptionsHelper.cs` | Helper for tests to initialize `JsonSerializerOptions` that merge the core library `CrdtJsonContext` with `TestJsonSerializerContext`. |
| `$/Ama.CRDT.UnitTests/Services/Adapters/AsyncCrdtApplicatorAdapterTests.cs` | Contains unit tests for `AsyncCrdtApplicatorAdapter`, verifying its bridging of synchronous execution to asynchronous interfaces and cancellation handling. |
| `$/Ama.CRDT.UnitTests/Services/Adapters/AsyncCrdtMergerAdapterTests.cs` | Contains unit tests for `AsyncCrdtMergerAdapter`, validating correct delegation to the synchronous inner merger and cancellation handling. |
| `$/Ama.CRDT.UnitTests/Services/Adapters/AsyncCrdtPatcherAdapterTests.cs` | Contains unit tests for `AsyncCrdtPatcherAdapter`, validating proper adaptation of patch and operation generation with cancellation checks. |
| `$/Ama.CRDT.UnitTests/Services/CrdtApplicatorTests.cs` | Contains unit tests for `CrdtApplicator`, verifying operation application, conflict resolution, and isolated per-shard version vector tracking in `ReplicaContext`. |
| `$/Ama.CRDT.UnitTests/Services/CrdtComposableArchitectureTests.cs` | Contains integration tests for the CRDT composable architecture, verifying that the Patcher and Applicator correctly handle deep nesting, complex model traversal, intent-based operation generation, and resolution across multiple nested CRDT strategies (LSEQ, Min-Wins Map, State Machine, Graph, etc.). |
| `$/Ama.CRDT.UnitTests/Services/CrdtMergerTests.cs` | Contains unit tests for `CrdtMerger`, verifying the synchronous LUB state merge logic and causal history merging. |
| `$/Ama.CRDT.UnitTests/Services/CrdtMetadataManagerTests.cs` | Contains unit tests for the `CrdtMetadataManager`, verifying LWW pruning and version vector advancement logic. |
| `$/Ama.CRDT.UnitTests/Services/CrdtPatcherTests.cs` | Contains unit tests for `CrdtPatcher`, verifying diff generation, explicit operation generation, and `ShardId` / `ShardClock` assignment. |
| `$/Ama.CRDT.UnitTests/Services/Decorators/AsyncCrdtApplicatorDecoratorBaseTests.cs` | Contains unit tests for `AsyncCrdtApplicatorDecoratorBase`, specifically verifying atomicity shielding behavior using `CancellationToken.None` in the After and Before phases. |
| `$/Ama.CRDT.UnitTests/Services/Decorators/AsyncCrdtMergerDecoratorBaseTests.cs` | Contains unit tests for `AsyncCrdtMergerDecoratorBase`, specifically verifying atomicity shielding behavior using `CancellationToken.None` in the After and Before phases. |
| `$/Ama.CRDT.UnitTests/Services/Decorators/AsyncCrdtPatcherDecoratorBaseTests.cs` | Contains unit tests for `AsyncCrdtPatcherDecoratorBase`, verifying atomicity shielding behavior in the After phase and proper cancellation propagation in Before and Complex phases. |
| `$/Ama.CRDT.UnitTests/Services/Decorators/CompactingApplicatorDecoratorTests.cs` | No description provided. |
| `$/Ama.CRDT.UnitTests/Services/Decorators/CompactingMergerDecoratorTests.cs` | Contains unit tests for `CompactingMergerDecorator`, verifying that it triggers metadata compaction following a state merge. |
| `$/Ama.CRDT.UnitTests/Services/Decorators/DecoratorsTestCrdtContext.cs` | A dedicated `CrdtAotContext` for the decorators unit tests to provide reflection-free property metadata for test-specific models. |
| `$/Ama.CRDT.UnitTests/Services/Decorators/JournalingApplicatorDecoratorTests.cs` | Contains unit tests for `JournalingApplicatorDecorator`, validating that operations are properly captured and journaled before application without unapplied filtering. |
| `$/Ama.CRDT.UnitTests/Services/Decorators/JournalingPatcherDecoratorTests.cs` | Contains unit tests for `JournalingPatcherDecorator`, verifying that generated patches, intents, and explicit operations are properly captured and journaled. |
| `$/Ama.CRDT.UnitTests/Services/Decorators/LargerThanMemoryApplicatorDecoratorTests.cs` | No description provided. |
| `$/Ama.CRDT.UnitTests/Services/Decorators/TestModel.cs` | A simple data model used for unit testing decorator services. |
| `$/Ama.CRDT.UnitTests/Services/GarbageCollection/GlobalMinimumVersionPolicyTests.cs` | Contains unit tests for `GlobalMinimumVersionPolicy`, verifying correct GMVV-based causal evaluation for safe compaction of candidates. |
| `$/Ama.CRDT.UnitTests/Services/GarbageCollection/ThresholdCompactionPolicyTests.cs` | Contains unit tests for `ThresholdCompactionPolicy`, testing time-to-live logic using wall-clock timestamps and logical version thresholds. |
| `$/Ama.CRDT.UnitTests/Services/Helpers/HelpersTestCrdtContext.cs` | No description provided. |
| `$/Ama.CRDT.UnitTests/Services/Helpers/MetadataPathHelperTests.cs` | Contains unit tests for `MetadataPathHelper`, verifying the correct generation, extraction, and matching of decorator-isolated metadata paths. |
| `$/Ama.CRDT.UnitTests/Services/Helpers/Models.cs` | Contains simple data models for unit testing path conversion and resolution helpers. |
| `$/Ama.CRDT.UnitTests/Services/Helpers/PocoPathHelperTests.cs` | Contains unit tests for `PocoPathHelper`, verifying JSON path parsing and resolution against POCOs, and testing new centralized reflection helpers for getting/setting values and retrieving type information. |
| `$/Ama.CRDT.UnitTests/Services/Journaling/JournalManagerTests.cs` | Contains unit tests for `JournalManager`, verifying missing operation retrieval and proper passing of `shardId` values to the underlying journal interface. |
| `$/Ama.CRDT.UnitTests/Services/LargerThanMemory/ChunkedDocumentManagerTests.cs` | No description provided. |
| `$/Ama.CRDT.UnitTests/Services/LargerThanMemory/PartitionStorageServiceContractTests.cs` | No description provided. |
| `$/Ama.CRDT.UnitTests/Services/LargerThanMemory/PartitioningTestCrdtContext.cs` | No description provided. |
| `$/Ama.CRDT.UnitTests/Services/Pooling/CrdtStringPoolTests.cs` | Unit tests for `CrdtStringPool`, `PooledStringJsonConverter`, `RootJsonPath`, and `MetadataPathHelper` string pooling. |
| `$/Ama.CRDT.UnitTests/Services/Providers/CrdtFluentConfigurationTests.cs` | Contains unit tests verifying the Fluent Builder API (`CrdtModelBuilder`) correctly maps CRDT strategies and that the `CrdtStrategyProvider` prioritizes these mappings over attributes. |
| `$/Ama.CRDT.UnitTests/Services/Providers/DefaultDocumentIdProviderTests.cs` | Contains unit tests for `DefaultDocumentIdProvider`, verifying correct document ID extraction and proper exception throwing for missing or invalid IDs. |
| `$/Ama.CRDT.UnitTests/Services/Providers/DefaultShardIdProviderTests.cs` | Contains unit tests for `DefaultShardIdProvider`, verifying Shard ID resolution from materialized documents, document IDs, and runtime types, including null argument guards. |
| `$/Ama.CRDT.UnitTests/Services/ServicesTestCrdtContext.cs` | A dedicated `CrdtAotContext` for the services unit tests, providing reflection-free metadata for test-specific models and collections. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/ArrayLcsStrategyTests.cs` | Contains unit tests for `ArrayLcsStrategy`, focusing on convergence properties under concurrent operations. This file includes a test that specifically reproduces a known bug related to the non-commutative application of array insertion patches. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/AverageRegisterStrategyTests.cs` | Contains unit tests for the `AverageRegisterStrategy`, verifying convergence, idempotence, and commutativity. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/BoundedCounterStrategyTests.cs` | Contains unit tests for `BoundedCounterStrategy`, verifying bounded increments/decrements and `CounterState` PN-Counter contribution merging. |
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
| `$/Ama.CRDT.UnitTests/Services/Strategies/LseqStrategyTests.cs` | Contains unit tests for the `LseqStrategy`, verifying convergence, idempotence, and chunkable collection methods (`Split`, `Merge`, `GetMinimumKey`, etc.). |
| `$/Ama.CRDT.UnitTests/Services/Strategies/LwwMapStrategyTests.cs` | Contains unit tests for the `LwwMapStrategy`, verifying convergence, idempotence, and LWW-based conflict resolution for concurrent dictionary operations. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/LwwSetStrategyTestCrdtContext.cs` | A dedicated `CrdtAotContext` for `LwwSetStrategy` unit tests to provide AOT-compatible property metadata. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/LwwSetStrategyTests.cs` | Contains unit tests for the `LwwSetStrategy`, verifying that conflicts are resolved based on the last-write-wins rule, allowing elements to be re-added after removal. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/LwwStrategyTestCrdtContext.cs` | A dedicated `CrdtAotContext` for `LwwStrategy` unit tests to provide AOT-compatible property metadata. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/LwwStrategyTests.cs` | Contains unit tests for the `LwwStrategy` implementation, verifying both patch generation and its simplified, unconditional data application logic. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/MaxWinsMapStrategyTestCrdtContext.cs` | A dedicated `CrdtAotContext` for `MaxWinsMapStrategy` unit tests to provide AOT-compatible property metadata. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/MaxWinsMapStrategyTests.cs` | Contains unit tests for `MaxWinsMapStrategy`, verifying value-based convergence for concurrent dictionary operations. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/MaxWinsStrategyTestCrdtContext.cs` | A dedicated `CrdtAotContext` for `MaxWinsStrategy` unit tests to provide AOT-compatible property metadata. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/MaxWinsStrategyTests.cs` | Contains unit tests for the `MaxWinsStrategy`, verifying that conflicts are resolved by choosing the highest value. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/MinWinsMapStrategyTestCrdtContext.cs` | A dedicated `CrdtAotContext` for `MinWinsMapStrategy` unit tests to provide AOT-compatible property metadata. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/MinWinsMapStrategyTests.cs` | Contains unit tests for `MinWinsMapStrategy`, verifying value-based convergence for concurrent dictionary operations. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/MinWinsStrategyTestCrdtContext.cs` | A dedicated `CrdtAotContext` for `MinWinsStrategy` unit tests to provide AOT-compatible property metadata. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/MinWinsStrategyTests.cs` | Contains unit tests for the `MinWinsStrategy`, verifying that conflicts are resolved by choosing the lowest value. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/OrMapStrategyTestCrdtContext.cs` | A dedicated `CrdtAotContext` for `OrMapStrategy` unit tests to provide AOT-compatible property metadata. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/OrMapStrategyTests.cs` | Contains unit tests for the `OrMapStrategy`, verifying convergence, LWW-based conflict resolution, re-addition of elements, and correct behavior for partitioning (split/merge). |
| `$/Ama.CRDT.UnitTests/Services/Strategies/OrSetStrategyTestCrdtContext.cs` | A dedicated `CrdtAotContext` for `OrSetStrategy` unit tests to provide AOT-compatible property metadata. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/OrSetStrategyTests.cs` | Contains unit tests for the `OrSetStrategy`, verifying that it correctly handles concurrent additions and removals without anomalies, allowing for proper re-addition of elements. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/PriorityQueueStrategyTestCrdtContext.cs` | A dedicated `CrdtAotContext` for `PriorityQueueStrategy` unit tests to provide AOT-compatible property metadata. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/PriorityQueueStrategyTests.cs` | Contains unit tests for the `PriorityQueueStrategy`, verifying that concurrent updates converge and the list remains sorted by priority. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/ReplicatedTreeStrategyTestCrdtContext.cs` | No description provided. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/ReplicatedTreeStrategyTests.cs` | No description provided. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/RgaStrategyTestCrdtContext.cs` | No description provided. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/RgaStrategyTests.cs` | Contains unit tests for `RgaStrategy`, verifying correct calculation of tombstones, insertions and tree based flattening. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/Serialization/SerializationTestCrdtContext.cs` | A dedicated `CrdtAotContext` for the `StrategyPayloadSerializationTests` to provide metadata for test-specific models and collections without relying on reflection. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/Serialization/StrategyPayloadSerializationTests.cs` | No description provided. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/SortedSetStrategyTestCrdtContext.cs` | No description provided. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/SortedSetStrategyTests.cs` | No description provided. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/StateMachineStrategyTestCrdtContext.cs` | A dedicated `CrdtAotContext` for `StateMachineStrategy` unit tests to provide AOT-compatible property metadata. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/StateMachineStrategyTests.cs` | Contains unit tests for `StateMachineStrategy`, verifying valid/invalid transitions and LWW-based conflict resolution. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/TwoPhaseGraphStrategyTestCrdtContext.cs` | A dedicated `CrdtAotContext` for `TwoPhaseGraphStrategy` unit tests to provide AOT-compatible property metadata. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/TwoPhaseGraphStrategyTests.cs` | No description provided. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/TwoPhaseSetStrategyTestCrdtContext.cs` | A dedicated `CrdtAotContext` for `TwoPhaseSetStrategy` unit tests to provide AOT-compatible property metadata. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/TwoPhaseSetStrategyTests.cs` | Contains unit tests for the `TwoPhaseSetStrategy`, verifying that elements can be added and removed, but not re-added after removal. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/VoteCounterStrategyTestCrdtContext.cs` | A dedicated `CrdtAotContext` for `VoteCounterStrategy` unit tests to provide AOT-compatible property metadata. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/VoteCounterStrategyTests.cs` | Contains unit tests for the `VoteCounterStrategy`, verifying convergence, idempotence, and LWW-based conflict resolution for concurrent voting scenarios. |
| `$/Ama.CRDT.UnitTests/Services/Versioning/VersionVectorSyncServiceTests.cs` | No description provided. |
| `$/Ama.CRDT.slnx` | No description provided. |
| `$/Ama.CRDT/Ama.CRDT.csproj` | The main project file for the CRDT library, targeting .NET 10, configured for NuGet packaging, auto-including associated Roslyn analyzers, and enforcing `<IsAotCompatible>true</IsAotCompatible>`. |
| `$/Ama.CRDT/Attributes/AllowedDecoratorBehaviorAttribute.cs` | Attribute used to tag decorator classes with their supported execution behaviors. Meant for use alongside Roslyn Analyzers to ensure DI validation. |
| `$/Ama.CRDT/Attributes/CrdtAotTypeAttribute.cs` | No description provided. |
| `$/Ama.CRDT/Attributes/CrdtIntentMappingAttribute.cs` | An attribute to map intent builder extension methods to the specific explicit intent types they generate, enabling compile-time validation via Roslyn analyzers without hardcoded mappings. |
| `$/Ama.CRDT/Attributes/CrdtStrategyAttribute.cs` | The base abstract attribute for marking properties with a specific CRDT merge strategy. Contains the strategy type. |
| `$/Ama.CRDT/Attributes/CrdtStrategyDecoratorAttribute.cs` | A base attribute for specifying a CRDT decorator strategy for a property, allowing multiple stacked decorators like `[CrdtEpochBound]` alongside core strategy attributes. |
| `$/Ama.CRDT/Attributes/CrdtSupportedIntentAttribute.cs` | An attribute to explicitly mark which explicit `IOperationIntent` types a given CRDT strategy supports. Used by Roslyn analyzers for validation. |
| `$/Ama.CRDT/Attributes/CrdtSupportedTypeAttribute.cs` | An attribute used to decorate a CRDT strategy class, specifying a property type (e.g., `int`, `IEnumerable`) that it supports. This enables compile-time validation via Roslyn analyzers. |
| `$/Ama.CRDT/Attributes/Decorators/CrdtApprovalQuorumAttribute.cs` | A decorator attribute that requires a specified number of approvals from different replicas before applying the underlying CRDT operation. |
| `$/Ama.CRDT/Attributes/Decorators/CrdtEpochBoundAttribute.cs` | No description provided. |
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
| `$/Ama.CRDT/Extensions/AsyncCrdtApplicatorExtensions.cs` | Provides extension methods for `IAsyncCrdtApplicator` to streamline common synchronization tasks, such as directly applying an asynchronous stream of missing operations. |
| `$/Ama.CRDT/Extensions/IStateMachine.cs` | No description provided. |
| `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs` | Provides dependency injection registration extensions for Ama.CRDT services, decorators, AOT contexts, and custom providers. |
| `$/Ama.CRDT/Models/Aot/CoreCrdtAotContext.cs` | No description provided. |
| `$/Ama.CRDT/Models/Aot/CrdtAotContext.cs` | No description provided. |
| `$/Ama.CRDT/Models/Aot/CrdtPropertyInfo.cs` | Contains AOT-compatible property metadata, including precomputed interned `RootJsonPath`. |
| `$/Ama.CRDT/Models/Aot/CrdtTypeInfo.cs` | Contains AOT-compatible metadata and factory methods for a specific type, including logic for lists and dictionaries. |
| `$/Ama.CRDT/Models/ApplyPatchResult.cs` | Data structure containing the result of a patch application, including the document and any unapplied operations. |
| `$/Ama.CRDT/Models/AverageRegisterState.cs` | A wrapper state structure implementing `ICrdtMetadataState` for Average Register contributions. |
| `$/Ama.CRDT/Models/AverageRegisterValue.cs` | A data structure that holds a replica's contribution (value and timestamp) for the Average Register strategy. |
| `$/Ama.CRDT/Models/BidirectionalSyncRequirements.cs` | A data structure containing the synchronization requirements for two replicas to fully catch up with each other. |
| `$/Ama.CRDT/Models/CausalTimestamp.cs` | A data structure that bundles a logical timestamp with the causal identity (replica and clock) of the operation, used to track metadata for tombstones and deletions safely for garbage collection. |
| `$/Ama.CRDT/Models/CounterMapState.cs` | A wrapper state structure implementing `ICrdtMetadataState` for Counter-Map trackers. |
| `$/Ama.CRDT/Models/CounterState.cs` | Represents the metadata state for properties managed by the `CounterStrategy`. Tracks positive and negative increment vectors per replica to mathematically enable State CRDT merges. |
| `$/Ama.CRDT/Models/CrdtDocument.cs` | No description provided. |
| `$/Ama.CRDT/Models/CrdtGraph.cs` | A data model for a graph structure with vertices and edges, suitable for CRDT management. |
| `$/Ama.CRDT/Models/CrdtMetadata.cs` | Encapsulates CRDT state for various strategies using dedicated, serializable record types. For serialization, use the recommended options from `CrdtJsonContext`. |
| `$/Ama.CRDT/Models/CrdtOperation.cs` | Represents a single CRDT operation, including `ShardId` and `ShardClock` for dynamic partitioning support. |
| `$/Ama.CRDT/Models/CrdtOperationStatus.cs` | Enum representing the status or failure reason of a CRDT operation during application. |
| `$/Ama.CRDT/Models/CrdtPatch.cs` | Encapsulates a list of CRDT operations with a logical key of type `IComparable?` to support strongly-typed, sortable partition keys. |
| `$/Ama.CRDT/Models/CrdtPropertyKey.cs` | A unique identifier representing a property without relying on `System.Reflection.PropertyInfo`, used for AOT-friendly mapping. |
| `$/Ama.CRDT/Models/CrdtTimestamp.cs` | A universal, allocation-free `readonly record struct` representing a logical point in time for a CRDT operation. |
| `$/Ama.CRDT/Models/CrdtTree.cs` | A data model for a tree data structure with nodes that can be added, removed, and moved, suitable for CRDT management. |
| `$/Ama.CRDT/Models/DecoratorBehavior.cs` | Enum defining the strict structural execution phases (`Before`, `After`, `Complex`) available to pipeline decorators. |
| `$/Ama.CRDT/Models/Decorators/EpochPayload.cs` | No description provided. |
| `$/Ama.CRDT/Models/Decorators/QuorumPayload.cs` | A data structure for the payload of a quorum-bound operation. |
| `$/Ama.CRDT/Models/DottedVersionVector.cs` | Represents a Dotted Version Vector (DVV) to track causality and operations seen by a replica. Combines a contiguous version vector with discrete "dots" for out-of-order events. |
| `$/Ama.CRDT/Models/Edge.cs` | No description provided. |
| `$/Ama.CRDT/Models/EpochState.cs` | A wrapper state structure implementing `ICrdtMetadataState` for Epoch-bound decorator tracking. |
| `$/Ama.CRDT/Models/FwwMapState.cs` | A wrapper state structure implementing `ICrdtMetadataState` for FWW-Map trackers. |
| `$/Ama.CRDT/Models/FwwSetState.cs` | A wrapper state structure implementing `ICrdtMetadataState` for FWW-Set trackers. |
| `$/Ama.CRDT/Models/FwwTimestamp.cs` | A wrapper state structure implementing `ICrdtMetadataState` for First-Writer-Wins single timestamps. |
| `$/Ama.CRDT/Models/GCounterState.cs` | Represents the metadata state for properties managed by the `GCounterStrategy`. Tracks absolute growth vector increments per replica to mathematically enable State CRDT merges. |
| `$/Ama.CRDT/Models/GraphEdgePayload.cs` | A data structure for the payload of a graph edge operation. |
| `$/Ama.CRDT/Models/GraphVertexPayload.cs` | A data structure for the payload of a graph vertex operation. |
| `$/Ama.CRDT/Models/ICrdtMetadataState.cs` | No description provided. |
| `$/Ama.CRDT/Models/Intents/AddEdgeIntent.cs` | Represents the intent to explicitly add an edge to a graph. |
| `$/Ama.CRDT/Models/Intents/AddIntent.cs` | Represents the intent to explicitly add an item to an unordered collection or set. |
| `$/Ama.CRDT/Models/Intents/AddNodeIntent.cs` | Represents the intent to explicitly add a node to a replicated tree. |
| `$/Ama.CRDT/Models/Intents/AddVertexIntent.cs` | Represents the intent to explicitly add a vertex to a graph. |
| `$/Ama.CRDT/Models/Intents/ClearIntent.cs` | Represents the intent to explicitly clear a property, collection, or state, effectively resetting it for FWW and other strategies. |
| `$/Ama.CRDT/Models/Intents/Decorators/EpochClearIntent.cs` | Represents the intent to explicitly clear the state within an Epoch-bound decorator, incrementing the epoch and effectively clearing all local data for that path without clashing with base strategy intents. |
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
| `$/Ama.CRDT/Models/JournalSyncResult.cs` | DTO representing the result of evaluating journal operations against synchronization requirements, avoiding tuple usage. |
| `$/Ama.CRDT/Models/JournaledOperation.cs` | An envelope record struct used by the operation journal interfaces to bundle a core `CrdtOperation` with its target `DocumentId` (logical key), facilitating correct data routing when synchronizing independent documents. |
| `$/Ama.CRDT/Models/LargerThanMemory/ChunkContent.cs` | No description provided. |
| `$/Ama.CRDT/Models/LargerThanMemory/ChunkSplitResult.cs` | No description provided. |
| `$/Ama.CRDT/Models/LargerThanMemory/CollectionChunk.cs` | No description provided. |
| `$/Ama.CRDT/Models/LargerThanMemory/CompositeChunkKey.cs` | No description provided. |
| `$/Ama.CRDT/Models/LargerThanMemory/HeaderChunk.cs` | No description provided. |
| `$/Ama.CRDT/Models/LargerThanMemory/IChunk.cs` | No description provided. |
| `$/Ama.CRDT/Models/LseqIdentifier.cs` | A record struct for the dense, ordered identifier used in LSEQ, composed of a path of `LseqPathSegment` instances. |
| `$/Ama.CRDT/Models/LseqItem.cs` | A record struct that pairs an LseqIdentifier with its corresponding value in the LSEQ metadata. |
| `$/Ama.CRDT/Models/LseqPathSegment.cs` | Represents a single, serializable segment in an LSEQ identifier's path, containing a position and a replica ID. |
| `$/Ama.CRDT/Models/LseqState.cs` | A wrapper state structure implementing `ICrdtMetadataState` for LSEQ trackers. |
| `$/Ama.CRDT/Models/LwwMapState.cs` | A wrapper state structure implementing `ICrdtMetadataState` for LWW-Map trackers. |
| `$/Ama.CRDT/Models/LwwSetState.cs` | No description provided. |
| `$/Ama.CRDT/Models/OperationType.cs` | Defines the types of operations (Upsert, Remove, Increment, Move) for a CRDT patch. |
| `$/Ama.CRDT/Models/OrMapItem.cs` | Contains payload record structs (`OrMapAddItem`, `OrMapRemoveItem`) for OR-Map (Observed-Remove Map) operations, bundling keys and values with unique tags. |
| `$/Ama.CRDT/Models/OrSetItem.cs` | Contains payload record structs (`OrSetAddItem`, `OrSetRemoveItem`) for OR-Set (Observed-Remove Set) operations, bundling values with unique tags. |
| `$/Ama.CRDT/Models/OrSetState.cs` | No description provided. |
| `$/Ama.CRDT/Models/OriginSyncRequirement.cs` | A data structure representing the causality gap for a specific origin replica between a target and a source. |
| `$/Ama.CRDT/Models/PnCounterState.cs` | A `readonly record struct` representing the positive and negative contribution state for a PN-Counter. |
| `$/Ama.CRDT/Models/PositionalIdentifier.cs` | No description provided. |
| `$/Ama.CRDT/Models/PositionalItem.cs` | A data structure used in operation payloads for positional array updates, bundling a stable position with the actual value. |
| `$/Ama.CRDT/Models/PositionalState.cs` | A wrapper state structure implementing `ICrdtMetadataState` for Array LCS positional trackers. |
| `$/Ama.CRDT/Models/QuorumState.cs` | A wrapper state structure implementing `ICrdtMetadataState` for Quorum decorator approvals. |
| `$/Ama.CRDT/Models/ReplicaSyncRequirement.cs` | A data structure representing causality gaps between replicas for a specific `ShardId`. |
| `$/Ama.CRDT/Models/RgaIdentifier.cs` | Readonly record struct containing logical timestamp and replicaId used as a unique identifier for each RGA node. |
| `$/Ama.CRDT/Models/RgaItem.cs` | Data structure representing an RGA node with a pointer to its predecessor, its payload value, and a tombstone flag. |
| `$/Ama.CRDT/Models/RgaState.cs` | A wrapper state structure implementing `ICrdtMetadataState` for RGA trackers. |
| `$/Ama.CRDT/Models/Serialization/Converters/CrdtPayloadJsonConverterFactory.cs` | Unified polymorphic JSON converter factory with fast-path `ReadOnlySpan<char>` discriminator resolution. |
| `$/Ama.CRDT/Models/Serialization/Converters/ObjectKeyDictionaryJsonConverter.cs` | A `JsonConverterFactory` that creates converters for dictionaries with non-string keys. It serializes them as a JSON array of [key, value] pairs to work around `System.Text.Json` limitations. |
| `$/Ama.CRDT/Models/Serialization/Converters/PooledStringJsonConverter.cs` | Custom JSON converters for pooling structural string properties and dictionary keys during deserialization without heap allocations. |
| `$/Ama.CRDT/Models/Serialization/Converters/SeenExceptionsJsonConverter.cs` | A specialized `JsonConverter` for `CrdtMetadata.SeenExceptions` that ensures `CrdtOperation` elements are serialized and deserialized with polymorphism enabled for their `Value` property. |
| `$/Ama.CRDT/Models/Serialization/CrdtAggregateJsonTypeInfoResolver.cs` | A custom JSON type info resolver that aggregates results from multiple resolvers to ensure all are inquired and merges critical metadata like PolymorphismOptions. |
| `$/Ama.CRDT/Models/Serialization/CrdtJsonContext.cs` | Provides centralized, pre-configured `JsonSerializerOptions` for CRDT models, establishing a best practice for serialization. |
| `$/Ama.CRDT/Models/Serialization/CrdtJsonTypeInfoResolver.cs` | Configures native polymorphism and attaches `PooledStringJsonConverter` to high-repetition structural CRDT properties. |
| `$/Ama.CRDT/Models/Serialization/CrdtMetadataJsonResolver.cs` | Configures compact serialization and pooled dictionary key converters for `CrdtMetadata`. |
| `$/Ama.CRDT/Models/Serialization/CrdtTypeRegistry.cs` | Centralized registry mapping discriminator strings to CRDT serialization types, supporting `ReadOnlySpan<char>` alternate lookups. |
| `$/Ama.CRDT/Models/ShardVersionVectorMap.cs` | Represents a strongly-typed map replacing IDictionary<string, DottedVersionVector> to track causality vectors per shard, implementing IEquatable for deep comparison safely. |
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
| `$/Ama.CRDT/Services/Adapters/AsyncCrdtMergerAdapter.cs` | An adapter that bridges the synchronous `ICrdtMerger` to the asynchronous `IAsyncCrdtMerger` pipeline. |
| `$/Ama.CRDT/Services/Adapters/AsyncCrdtPatcherAdapter.cs` | An adapter that bridges the synchronous `ICrdtPatcher` to the asynchronous `IAsyncCrdtPatcher` pipeline, acting as the base layer in the patcher decorator chain. |
| `$/Ama.CRDT/Services/CrdtApplicator.cs` | Applies CRDT patches to documents, resolving conflicts via strategies and tracking causal histories per shard in `ReplicaContext`. |
| `$/Ama.CRDT/Services/CrdtMerger.cs` | Synchronous implementation of `ICrdtMerger` that leverages object tree traversal to compute the state merge mathematically using strategy providers. |
| `$/Ama.CRDT/Services/CrdtMetadataManager.cs` | Manages CRDT metadata initialization, resetting, cloning, merging, and compaction across strategies. |
| `$/Ama.CRDT/Services/CrdtPatcher.cs` | Compares versions of a data model or processes explicit intents to generate CRDT patches, populating `ShardId` and `ShardClock` and updating `ShardVersionVectors`. |
| `$/Ama.CRDT/Services/CrdtScopeFactory.cs` | An implementation of `ICrdtScopeFactory` that uses the root `IServiceProvider` to create a new `IServiceScope` and configure it with a `ReplicaContext` holding the unique replica ID. |
| `$/Ama.CRDT/Services/Decorators/AsyncCrdtApplicatorDecoratorBase.cs` | An abstract base class for `IAsyncCrdtApplicator` decorators. Uses the `DecoratorBehavior` enum in its constructor to strictly control the Template Method execution flow, making bugs around pipeline ordering structurally impossible. |
| `$/Ama.CRDT/Services/Decorators/AsyncCrdtMergerDecoratorBase.cs` | An abstract base class for `IAsyncCrdtMerger` decorators supporting execution flow management via `DecoratorBehavior`. |
| `$/Ama.CRDT/Services/Decorators/AsyncCrdtPatcherDecoratorBase.cs` | An abstract base class for `IAsyncCrdtPatcher` decorators. Uses the `DecoratorBehavior` enum in its constructor to safely structure patch and operation generation overrides. |
| `$/Ama.CRDT/Services/Decorators/CompactingApplicatorDecorator.cs` | A decorator for `IAsyncCrdtApplicator` that automatically runs garbage collection on the document's metadata using the `DecoratorBehavior.After` flow. |
| `$/Ama.CRDT/Services/Decorators/CompactingMergerDecorator.cs` | A decorator for `IAsyncCrdtMerger` that runs metadata compaction after successfully merging document states. |
| `$/Ama.CRDT/Services/Decorators/JournalingApplicatorDecorator.cs` | Decorator for intercepting patch applications to record operations to the journal before the underlying application logic executes. |
| `$/Ama.CRDT/Services/Decorators/JournalingPatcherDecorator.cs` | Decorator for intercepting patch generations to record generated operations to the journal, using `IDocumentIdProvider` via dependency injection. |
| `$/Ama.CRDT/Services/Decorators/LargerThanMemoryApplicatorDecorator.cs` | No description provided. |
| `$/Ama.CRDT/Services/DifferentiateObjectContext.cs` | Defines the context object for the `ICrdtPatcher.DifferentiateObject` method, encapsulating all necessary parameters. |
| `$/Ama.CRDT/Services/GarbageCollection/CompactionCandidate.cs` | Represents the metadata payload (e.g., Timestamp, ReplicaId, Version) of a tombstone or deleted item being evaluated for garbage collection. |
| `$/Ama.CRDT/Services/GarbageCollection/GlobalMinimumVersionPolicy.cs` | Implements a mathematically safe compaction policy based on the Global Minimum Version Vector (GMVV) across a cluster of replicas. |
| `$/Ama.CRDT/Services/GarbageCollection/GlobalMinimumVersionPolicyFactory.cs` | Factory for creating instances of `GlobalMinimumVersionPolicy` dynamically using a provider delegate for the GMVV. |
| `$/Ama.CRDT/Services/GarbageCollection/ICompactionPolicy.cs` | Defines a policy that determines whether a CRDT tombstone or metadata entry is safe to delete, supporting both wall-clock timestamps and causal versions. |
| `$/Ama.CRDT/Services/GarbageCollection/ICompactionPolicyFactory.cs` | Defines a factory that dynamically generates a compaction policy. Useful for long-running scopes that need policies evaluated relative to the current time. |
| `$/Ama.CRDT/Services/GarbageCollection/ThresholdCompactionPolicy.cs` | Implements a heuristic-based compaction policy (TTL) that considers any timestamp or version older than a specified threshold as safe to compact. |
| `$/Ama.CRDT/Services/GarbageCollection/ThresholdCompactionPolicyFactory.cs` | Factory for creating instances of `ThresholdCompactionPolicy` dynamically based on time-to-live (`TimeSpan`) or dynamic threshold providers. |
| `$/Ama.CRDT/Services/Helpers/MetadataPathHelper.cs` | Provides formalized methods for constructing and evaluating metadata state paths, ensuring decorator strategies isolate their state safely without string mangling bugs. |
| `$/Ama.CRDT/Services/Helpers/PocoPathHelper.cs` | Helper class for parsing JSON paths and resolving or manipulating property values on POCOs using AOT contexts. |
| `$/Ama.CRDT/Services/IAsyncCrdtApplicator.cs` | Defines the asynchronous contract for a service that applies a CRDT patch to a document. |
| `$/Ama.CRDT/Services/IAsyncCrdtMerger.cs` | Defines the asynchronous contract for merging two complete CRDT document states. |
| `$/Ama.CRDT/Services/IAsyncCrdtPatcher.cs` | Defines the asynchronous contract for a service that compares two versions of a data model and generates a CRDT patch. |
| `$/Ama.CRDT/Services/ICrdtApplicator.cs` | No description provided. |
| `$/Ama.CRDT/Services/ICrdtMerger.cs` | Defines the synchronous contract for a service that merges two complete CRDT document states based on the CvRDT paradigm. |
| `$/Ama.CRDT/Services/ICrdtMetadataManager.cs` | Defines a service for managing CRDT metadata. Its responsibilities include initializing, resetting, cloning, merging, and compacting metadata state such as LWW timestamps, positional trackers, and version vectors. This service is critical for enabling conflict-free merges by externalizing the state needed for resolution. |
| `$/Ama.CRDT/Services/ICrdtPatcher.cs` | Defines the contract for a service that compares two versions of a data model and generates a CRDT patch, as well as an intent-based method for creating patches directly. |
| `$/Ama.CRDT/Services/ICrdtScopeFactory.cs` | Defines a factory that creates isolated `IServiceScope` instances configured with a `ReplicaContext` and shard version vectors. |
| `$/Ama.CRDT/Services/Journaling/ICrdtOperationJournal.cs` | Defines a contract for an operation journal to append and retrieve partitioned CRDT operations by `shardId` and `shardClock`. |
| `$/Ama.CRDT/Services/Journaling/IJournalManager.cs` | Defines a service for retrieving missing CRDT operations based on synchronization requirements filtered by `shardId`. |
| `$/Ama.CRDT/Services/Journaling/JournalManager.cs` | Implements `IJournalManager` to retrieve missing operations based on `ReplicaSyncRequirement` by querying an underlying `ICrdtOperationJournal`. |
| `$/Ama.CRDT/Services/LargerThanMemory/ChunkedDocumentManager.cs` | Manages virtualized CRDT documents stored across disjoint chunks in stream/block storage via `IChunkStorageService`. |
| `$/Ama.CRDT/Services/LargerThanMemory/IChunkDocumentManager.cs` | No description provided. |
| `$/Ama.CRDT/Services/LargerThanMemory/IChunkStorageService.cs` | Provides a high-level abstraction for saving and loading disjoint chunks of data and metadata for larger-than-memory CRDTs. |
| `$/Ama.CRDT/Services/LargerThanMemory/IChunkableCollectionStrategy.cs` | Extends `IVirtualCollectionStrategy` with the ability to physically split and merge disjoint chunks of data for block/stream storage. |
| `$/Ama.CRDT/Services/LargerThanMemory/IKvDocumentManager.cs` | Defines the contract for managing a CRDT document using a True Key-Value backend. |
| `$/Ama.CRDT/Services/LargerThanMemory/IKvStorageService.cs` | Provides a True Key-Value storage abstraction for CRDT data and metadata, mapping items 1-to-1 with database rows without chunking. |
| `$/Ama.CRDT/Services/LargerThanMemory/IVirtualCollectionStrategy.cs` | Defines a CRDT strategy that supports externalizing its elements into a Key-Value store or segmented storage, exposing key generation logic. |
| `$/Ama.CRDT/Services/LargerThanMemory/IVirtualDocumentCollectionReader.cs` | No description provided. |
| `$/Ama.CRDT/Services/LargerThanMemory/IVirtualDocumentManager.cs` | Defines common operations for managing virtualized CRDT documents regardless of their underlying storage strategy, combining read queries with setup/maintenance operations. |
| `$/Ama.CRDT/Services/LargerThanMemory/IVirtualDocumentPatchHandler.cs` | Defines a contract for handling patch application for virtualized CRDT documents (e.g., Chunked or KV partitioned). Implemented by the respective Virtual Document Managers to intercept and correctly route document patches to external storage. |
| `$/Ama.CRDT/Services/LargerThanMemory/IVirtualDocumentProjector.cs` | Defines a projection hook interface for CQRS systems, allowing users to intercept completely converged POCO updates for custom Read Models immediately after patches apply. |
| `$/Ama.CRDT/Services/LargerThanMemory/KvDocumentManager.cs` | Manages virtualized CRDT documents stored in a key-value backend via `IKvStorageService`. |
| `$/Ama.CRDT/Services/Metrics/LargerThanMemoryManagerCrdtMetrics.cs` | No description provided. |
| `$/Ama.CRDT/Services/Metrics/MetricTimer.cs` | A helper `IDisposable` struct that uses a `Stopwatch` to measure the duration of a code block and records it to a `Histogram` upon disposal. |
| `$/Ama.CRDT/Services/Pooling/CrdtStringPool.cs` | Implements `ICrdtStringPool` using `ConcurrentDictionary` and `AlternateLookup<ReadOnlySpan<char>>` with capacity and length guardrails. |
| `$/Ama.CRDT/Services/Pooling/ICrdtStringPool.cs` | Defines the contract for a bounded, thread-safe string pooling service to deduplicate recurring structural CRDT strings. |
| `$/Ama.CRDT/Services/Providers/CrdtEntityBuilder.cs` | A fluent builder to configure CRDT strategies for a specific entity type, avoiding reflections by directly evaluating Expression trees in an AOT-friendly way. |
| `$/Ama.CRDT/Services/Providers/CrdtModelBuilder.cs` | A fluent builder used to configure CRDT strategies and decorators for C# types mapping to `CrdtPropertyKey`. |
| `$/Ama.CRDT/Services/Providers/CrdtModelRegistry.cs` | A registry that holds the configurations of CRDT strategies, operating on `CrdtPropertyKey`. |
| `$/Ama.CRDT/Services/Providers/CrdtPropertyBuilder.cs` | A fluent builder to configure CRDT strategies and decorators for a specific `CrdtPropertyKey`. |
| `$/Ama.CRDT/Services/Providers/CrdtStrategyProvider.cs` | Resolves CRDT strategies for properties based on AOT compiled metadata mappings and the fluent model registry. |
| `$/Ama.CRDT/Services/Providers/DefaultDocumentIdProvider.cs` | The default implementation of `IDocumentIdProvider` that extracts the document ID by looking for an 'Id' property. |
| `$/Ama.CRDT/Services/Providers/DefaultShardIdProvider.cs` | Provides a default implementation of `IShardIdProvider` that assigns all documents and document IDs to a single "default" shard. |
| `$/Ama.CRDT/Services/Providers/ElementComparerProvider.cs` | No description provided. |
| `$/Ama.CRDT/Services/Providers/EpochTimestampProvider.cs` | The default implementation of `ICrdtTimestampProvider` that generates `CrdtTimestamp` based on Unix milliseconds. |
| `$/Ama.CRDT/Services/Providers/ICrdtModelRegistry.cs` | Interface for retrieving configurations using the AOT-friendly `CrdtPropertyKey`. |
| `$/Ama.CRDT/Services/Providers/ICrdtStrategyProvider.cs` | A contract for a service that resolves CRDT strategies, refactored to consume AOT structs like `CrdtPropertyInfo`. |
| `$/Ama.CRDT/Services/Providers/ICrdtTimestampProvider.cs` | Defines a service for generating `CrdtTimestamp` instances, allowing for custom timestamp implementations without boxing. |
| `$/Ama.CRDT/Services/Providers/IDocumentIdProvider.cs` | Defines a service for extracting and assigning strongly-typed `IComparable` document IDs on models. |
| `$/Ama.CRDT/Services/Providers/IElementComparer.cs` | No description provided. |
| `$/Ama.CRDT/Services/Providers/IElementComparerProvider.cs` | No description provided. |
| `$/Ama.CRDT/Services/Providers/IShardIdProvider.cs` | Defines an interface for calculating the Shard ID for a CRDT document from a materialized object or from a document ID and hosted type. |
| `$/Ama.CRDT/Services/ReplicaContext.cs` | A scoped service that holds the unique identifier for a CRDT replica and tracks causality vectors per shard. |
| `$/Ama.CRDT/Services/Serialization/BrotliJsonCrdtSerializer.cs` | A Native AOT compatible implementation of `ICrdtSerializer` utilizing `System.Text.Json` combined with Brotli compression natively provided by the framework, serializing strings as Base64 encoded compressed payloads. |
| `$/Ama.CRDT/Services/Serialization/ICrdtSerializer.cs` | Defines a format-agnostic abstraction for serializing and deserializing CRDT models and payloads, decoupling the core library from System.Text.Json. |
| `$/Ama.CRDT/Services/Serialization/JsonCrdtSerializer.cs` | The default Native AOT compatible implementation of `ICrdtSerializer` utilizing `System.Text.Json` and pre-configured `JsonSerializerOptions`. |
| `$/Ama.CRDT/Services/Strategies/ApplyOperationContext.cs` | Defines the context for an `ICrdtStrategy.ApplyOperation` call, encapsulating all necessary parameters for applying a single CRDT operation to a document. |
| `$/Ama.CRDT/Services/Strategies/ArrayLcsStrategy.cs` | Implements the Array LCS strategy for ordered sequences, including `MergeState` for state-based CRDT merging. |
| `$/Ama.CRDT/Services/Strategies/AverageRegisterStrategy.cs` | Implements the Average Register strategy, including `MergeState` by choosing maximum timestamps of contributions. |
| `$/Ama.CRDT/Services/Strategies/BoundedCounterStrategy.cs` | Implements the Bounded Counter strategy, clamping values within a defined range and using `CounterState` for tracking PN-Counter contributions. |
| `$/Ama.CRDT/Services/Strategies/CompactionContext.cs` | Defines the context for an `ICrdtStrategy.Compact` call, encapsulating parameters for garbage collection. |
| `$/Ama.CRDT/Services/Strategies/CounterMapStrategy.cs` | Implements the Counter-Map strategy, where each key in a dictionary is treated as an independent PN-Counter. |
| `$/Ama.CRDT/Services/Strategies/CounterStrategy.cs` | Implements the CRDT Counter (PN-Counter) strategy using centralized AOT helpers from `PocoPathHelper`. |
| `$/Ama.CRDT/Services/Strategies/Decorators/ApprovalQuorumStrategy.cs` | A decorator strategy that tracks pending proposals and requires a quorum of replicas to propose the exact same operation before it is applied. |
| `$/Ama.CRDT/Services/Strategies/Decorators/EpochBoundStrategy.cs` | No description provided. |
| `$/Ama.CRDT/Services/Strategies/FixedSizeArrayStrategy.cs` | Implements the Fixed-Size Array strategy with LWW per index and `MergeState` for state-based merging. |
| `$/Ama.CRDT/Services/Strategies/FwwMapStrategy.cs` | Implements the First-Writer-Wins Map strategy, including `MergeState` and virtual collection partitioning support. |
| `$/Ama.CRDT/Services/Strategies/FwwSetStrategy.cs` | Implements the First-Writer-Wins Set strategy, including `MergeState` and virtual collection partitioning support. |
| `$/Ama.CRDT/Services/Strategies/FwwStrategy.cs` | Implements the First-Writer-Wins (FWW) strategy for conflict resolution, resolving concurrent modifications by selecting the value with the lowest timestamp. |
| `$/Ama.CRDT/Services/Strategies/GCounterStrategy.cs` | Implements the G-Counter (Grow-Only Counter) strategy, which only allows for positive increments. |
| `$/Ama.CRDT/Services/Strategies/GSetStrategy.cs` | Implements the G-Set (Grow-Only Set) CRDT strategy using centralized AOT helpers from `PocoPathHelper`. |
| `$/Ama.CRDT/Services/Strategies/GenerateOperationContext.cs` | Defines the context for explicitly generating intent-based operations in strategies. |
| `$/Ama.CRDT/Services/Strategies/GeneratePatchContext.cs` | Defines the context object for the `ICrdtStrategy.GeneratePatch` method, encapsulating all necessary parameters. |
| `$/Ama.CRDT/Services/Strategies/GraphStrategy.cs` | Implements a CRDT strategy for graph data structures, treating vertices and edges as a grow-only set, suitable for modeling relationships and networks. |
| `$/Ama.CRDT/Services/Strategies/ICrdtStrategy.cs` | Defines the core contract for CRDT strategies, including patch generation, intent operation generation, operation application, state merging (`MergeState`), and metadata compaction. |
| `$/Ama.CRDT/Services/Strategies/LseqStrategy.cs` | Implements the LSEQ strategy for ordered sequences using centralized AOT helpers from `PocoPathHelper`. |
| `$/Ama.CRDT/Services/Strategies/LwwMapStrategy.cs` | Implements the LWW-Map (Last-Writer-Wins Map) CRDT strategy using centralized AOT helpers from `PocoPathHelper`. |
| `$/Ama.CRDT/Services/Strategies/LwwSetStrategy.cs` | Implements the LWW-Set (Last-Writer-Wins Set) CRDT strategy using centralized AOT helpers from `PocoPathHelper`. |
| `$/Ama.CRDT/Services/Strategies/LwwStrategy.cs` | Implements the Last-Writer-Wins (LWW) register strategy using centralized AOT helpers from `PocoPathHelper`. |
| `$/Ama.CRDT/Services/Strategies/MaxWinsMapStrategy.cs` | Implements a value-based Max-Wins Map strategy. For each key, conflicts are resolved by choosing the highest value, making the map's keys grow-only. |
| `$/Ama.CRDT/Services/Strategies/MaxWinsStrategy.cs` | Implements the Max-Wins Register strategy using centralized AOT helpers from `PocoPathHelper`. |
| `$/Ama.CRDT/Services/Strategies/MergeStateContext.cs` | Defines the context for an `ICrdtStrategy.MergeState` call, encapsulating parameters like primary/secondary data and fully resolved property paths. |
| `$/Ama.CRDT/Services/Strategies/MinWinsMapStrategy.cs` | Implements a value-based Min-Wins Map strategy. For each key, conflicts are resolved by choosing the lowest value, making the map's keys grow-only. |
| `$/Ama.CRDT/Services/Strategies/MinWinsStrategy.cs` | Implements the Min-Wins Register strategy using centralized AOT helpers from `PocoPathHelper`. |
| `$/Ama.CRDT/Services/Strategies/OrMapStrategy.cs` | Implements the OR-Map (Observed-Remove Map) CRDT strategy. It uses centralized reflection helpers and supports partitioning. |
| `$/Ama.CRDT/Services/Strategies/OrSetStrategy.cs` | Implements the OR-Set (Observed-Remove Set) CRDT strategy using centralized AOT helpers from `PocoPathHelper`. |
| `$/Ama.CRDT/Services/Strategies/PriorityQueueStrategy.cs` | Implements a strategy for collections that behave as a priority queue using centralized AOT helpers from `PocoPathHelper`. |
| `$/Ama.CRDT/Services/Strategies/ReplicatedTreeStrategy.cs` | Implements a CRDT strategy for tree data structures, allowing nodes to be added, removed, and moved concurrently. It uses unique tags (similar to OR-Set) for adds and LWW for moves to ensure convergence. |
| `$/Ama.CRDT/Services/Strategies/RgaStrategy.cs` | Implements the Replicated Growable Array (RGA) strategy. Evaluates insertions as an ordered linked set and applies deletes by emitting tombstones. Also handles intent based operation generation. |
| `$/Ama.CRDT/Services/Strategies/SortedSetStrategy.cs` | Implements a CRDT strategy for sorted sets using LCS and centralized AOT helpers from `PocoPathHelper`. |
| `$/Ama.CRDT/Services/Strategies/StateMachineStrategy.cs` | Implements the State Machine strategy using centralized AOT helpers from `PocoPathHelper`. |
| `$/Ama.CRDT/Services/Strategies/TwoPhaseGraphStrategy.cs` | Implements a 2P-Graph strategy where vertices and edges can be added and removed, but not re-added after removal, ensuring monotonic growth of the tombstone sets. |
| `$/Ama.CRDT/Services/Strategies/TwoPhaseSetStrategy.cs` | Implements the 2P-Set (Two-Phase Set) CRDT strategy using centralized AOT helpers from `PocoPathHelper`. |
| `$/Ama.CRDT/Services/Strategies/VoteCounterStrategy.cs` | Implements the Vote Counter strategy using centralized AOT helpers from `PocoPathHelper`. |
| `$/Ama.CRDT/Services/Versioning/IVersionVectorSyncService.cs` | Defines a service for calculating synchronization gaps and evaluating journal operations on a per-shard basis. |
| `$/Ama.CRDT/Services/Versioning/VersionVectorSyncService.cs` | Implementation of `IVersionVectorSyncService` that negotiates vector requirements per shard and evaluates journal completion using `ShardClock`. |
| `$/CodingStandards.md` | No description provided. |
| `$/FilesDescription.md` | No description provided. |
| `$/LICENSE` | No description provided. |
| `$/README.md` | Provides the main entry point and overview for the CRDT library, including usage examples and references to `Ama.CRDT.LargerThanMemory.Streams`. |
| `$/Specs/add-approval-quorum-strategy.md` | No description provided. |
| `$/Specs/add-leader-election-strategy.md` | No description provided. |
| `$/Specs/add-more-text-specific-strategies.md` | No description provided. |
| `$/Specs/add-string-interning-pooling.md` | Specification detailing the architectural changes required to implement string interning and bounded string pooling for memory efficiency across CRDT metadata, operations, and JSON serialization. |
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
| `$/docs/extensibility.md` | Provides examples and guides for extending Ama.CRDT, including custom strategies, IElementComparer, and ICrdtTimestampProvider returning CrdtTimestamp. |
| `$/docs/journaling.md` | Explains the operation journaling feature, how to configure decorators to automatically record operations, and how to use it for advanced synchronization. |
| `$/docs/metadata-management.md` | Contains best practices for managing CRDT metadata size, detailing techniques like tombstone pruning using CrdtTimestamp, version vector compaction, and optimized serialization. |
| `$/docs/multi-replica-and-serialization.md` | Explains advanced synchronization mechanics for multi-node setups and demonstrates configuring `CrdtPatch` JSON serialization correctly. |
| `$/docs/partitioning.md` | Documents the setup and usage of the library's Larger-Than-Memory partitioning features, `IChunkDocumentManager`, and `IKvDocumentManager`. |
| `$/docs/strategies-reference.md` | Provides a comprehensive table detailing all available CRDT strategies (e.g., LWW, Counters, Sets, Maps) and their ideal use cases. |
| `$/docs/target-crdt-strategies.md` | Contains a comprehensive, categorized catalog of CRDT strategies, broken down into base strategies and composable decorators, providing a long-term roadmap for the library. |
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
