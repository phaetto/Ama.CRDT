| File Path | Description |
| --- | --- |
| `$/.editorconfig` | No description provided. |
| `$/.github/workflows/ci.yml` | GitHub Actions workflow for building and testing the project on pushes to non-master branches and on pull requests to master. This ensures code quality before merging. |
| `$/.github/workflows/publish-nuget-manual.yml` | GitHub Actions workflow for manually building, testing, and publishing a stable, non-prerelease version of the `Ama.CRDT` NuGet package. It includes a check to ensure no unshipped public APIs are present in a stable release. |
| `$/.github/workflows/publish-nuget.yml` | GitHub Actions workflow for building, testing, and publishing the `Ama.CRDT` NuGet package on pushes to the `master` branch. |
| `$/.gitignore` | No description provided. |
| `$/Ama.CRDT.Benchmarks/Ama.CRDT.Benchmarks.csproj` | The project file for the benchmarks application. |
| `$/Ama.CRDT.Benchmarks/AntiVirusFriendlyConfig.cs` | No description provided. |
| `$/Ama.CRDT.Benchmarks/Benchmarks/ApplicatorBenchmarks.cs` | Contains benchmarks for the `JsonCrdtApplicator` service. |
| `$/Ama.CRDT.Benchmarks/Benchmarks/PatcherBenchmarks.cs` | Contains benchmarks for the `JsonCrdtPatcher` service. |
| `$/Ama.CRDT.Benchmarks/Benchmarks/StrategyBenchmarks.cs` | Contains benchmarks for `GeneratePatch` and `ApplyPatch` operations for every individual CRDT strategy. |
| `$/Ama.CRDT.Benchmarks/Models/ComplexPoco.cs` | A complex data model with nested objects and arrays for benchmarking recursive and collection-based scenarios. |
| `$/Ama.CRDT.Benchmarks/Models/SimplePoco.cs` | A simple data model for benchmarking basic scenarios. |
| `$/Ama.CRDT.Benchmarks/Models/StrategyPoco.cs` | A data model containing properties decorated with attributes for each supported CRDT strategy, used for isolated strategy benchmarking. |
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
| `$/Ama.CRDT.UnitTests/Services/Helpers/Models.cs` | Contains simple data models for unit testing path conversion and resolution helpers. |
| `$/Ama.CRDT.UnitTests/Services/Helpers/PocoPathHelperTests.cs` | Contains unit tests for `PocoPathHelper`, verifying JSON path parsing and resolution against POCOs. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/ArrayLcsStrategyTests.cs` | Contains unit tests for `ArrayLcsStrategy`, focusing on convergence properties under concurrent operations. This file includes a test that specifically reproduces a known bug related to the non-commutative application of array insertion patches. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/AverageRegisterStrategyTests.cs` | Contains unit tests for the `AverageRegisterStrategy`, verifying convergence, idempotence, and commutativity. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/BoundedCounterStrategyTests.cs` | Contains unit tests for the `BoundedCounterStrategy`, verifying that values are correctly clamped within their defined bounds. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/CounterStrategyTests.cs` | Contains unit tests for the `CounterStrategy` implementation, verifying both patch generation and its simplified, unconditional data application logic. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/ExclusiveLockStrategyTests.cs` | Contains unit tests for the `ExclusiveLockStrategy`, verifying convergence, LWW-based conflict resolution, and rejection of changes when a lock is held by another party. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/FixedSizeArrayStrategyTests.cs` | Contains unit tests for the `FixedSizeArrayStrategy`, verifying convergence and idempotence for concurrent updates. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/GCounterStrategyTests.cs` | Contains unit tests for the `GCounterStrategy`, ensuring it only generates and applies positive increments. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/GSetStrategyTests.cs` | Contains unit tests for the `GSetStrategy`, verifying its add-only behavior, idempotence, and commutativity. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/LseqStrategyTests.cs` | Contains unit tests for the `LseqStrategy`, verifying convergence and idempotence under concurrent operations. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/LwwSetStrategyTests.cs` | Contains unit tests for the `LwwSetStrategy`, verifying that conflicts are resolved based on the last-write-wins rule, allowing elements to be re-added after removal. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/LwwStrategyTests.cs` | Contains unit tests for the `LwwStrategy` implementation, verifying both patch generation and its simplified, unconditional data application logic. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/MaxWinsStrategyTests.cs` | Contains unit tests for the `MaxWinsStrategy`, verifying that conflicts are resolved by choosing the highest value. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/MinWinsStrategyTests.cs` | Contains unit tests for the `MinWinsStrategy`, verifying that conflicts are resolved by choosing the lowest value. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/OrSetStrategyTests.cs` | Contains unit tests for the `OrSetStrategy`, verifying that it correctly handles concurrent additions and removals without anomalies, allowing for proper re-addition of elements. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/PriorityQueueStrategyTests.cs` | Contains unit tests for the `PriorityQueueStrategy`, verifying that concurrent updates converge and the list remains sorted by priority. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/SortedSetStrategyTests.cs` | No description provided. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/StateMachineStrategyTests.cs` | Contains unit tests for `StateMachineStrategy`, verifying valid/invalid transitions and LWW-based conflict resolution. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/TwoPhaseSetStrategyTests.cs` | Contains unit tests for the `TwoPhaseSetStrategy`, verifying that elements can be added and removed, but not re-added after removal. |
| `$/Ama.CRDT.UnitTests/Services/Strategies/VoteCounterStrategyTests.cs` | Contains unit tests for the `VoteCounterStrategy`, verifying convergence, idempotence, and LWW-based conflict resolution for concurrent voting scenarios. |
| `$/Ama.CRDT/Ama.CRDT.csproj` | The main project file for the CRDT library, configured for NuGet packaging. |
| `$/Ama.CRDT/Attributes/CrdtArrayLcsStrategyAttribute.cs` | An attribute to explicitly mark a collection property to use the Array LCS strategy, which leverages positional identifiers for stable, causally-correct ordering of elements. |
| `$/Ama.CRDT/Attributes/CrdtAverageRegisterStrategyAttribute.cs` | An attribute to mark a property as an Average Register, where its value converges to the average of all replica contributions. |
| `$/Ama.CRDT/Attributes/CrdtBoundedCounterStrategyAttribute.cs` | An attribute to mark a numeric property as a Bounded Counter, which clamps its value within a specified min/max range. |
| `$/Ama.CRDT/Attributes/CrdtCounterStrategyAttribute.cs` | No description provided. |
| `$/Ama.CRDT/Attributes/CrdtExclusiveLockStrategyAttribute.cs` | An attribute to mark a property to be managed by the Exclusive Lock strategy. It uses LWW for conflict resolution and requires a path to a property on the root object that holds the lock owner's ID. |
| `$/Ama.CRDT/Attributes/CrdtFixedSizeArrayStrategyAttribute.cs` | An attribute to mark a collection property as a fixed-size array, where each index is an LWW-Register. |
| `$/Ama.CRDT/Attributes/CrdtGCounterStrategyAttribute.cs` | An attribute to mark a numeric property as a G-Counter (Grow-Only Counter), which only permits positive increments. |
| `$/Ama.CRDT/Attributes/CrdtGSetStrategyAttribute.cs` | An attribute to mark a collection property to be managed by the G-Set (Grow-Only Set) strategy. |
| `$/Ama.CRDT/Attributes/CrdtLseqStrategyAttribute.cs` | An attribute to explicitly mark a collection property to use the LSEQ strategy for managing ordered sequences with dense identifiers. |
| `$/Ama.CRDT/Attributes/CrdtLwwSetStrategyAttribute.cs` | An attribute to mark a collection property to be managed by the LWW-Set (Last-Writer-Wins Set) strategy. |
| `$/Ama.CRDT/Attributes/CrdtLwwStrategyAttribute.cs` | No description provided. |
| `$/Ama.CRDT/Attributes/CrdtMaxWinsStrategyAttribute.cs` | An attribute to mark a property as a Max-Wins Register, where conflicts are resolved by choosing the highest value. |
| `$/Ama.CRDT/Attributes/CrdtMinWinsStrategyAttribute.cs` | An attribute to mark a property as a Min-Wins Register, where conflicts are resolved by choosing the lowest value. |
| `$/Ama.CRDT/Attributes/CrdtOrSetStrategyAttribute.cs` | An attribute to mark a collection property to be managed by the OR-Set (Observed-Remove Set) strategy. |
| `$/Ama.CRDT/Attributes/CrdtPriorityQueueStrategyAttribute.cs` | An attribute to mark a collection as a priority queue, which is managed as an LWW-Set and kept sorted by a specified property. |
| `$/Ama.CRDT/Attributes/CrdtSortedSetStrategyAttribute.cs` | An attribute to explicitly mark a collection property to use the Sorted Set strategy. It uses LCS for diffing and maintains a sorted order. |
| `$/Ama.CRDT/Attributes/CrdtStateMachineStrategyAttribute.cs` | An attribute to mark a property to be managed by the State Machine strategy, which enforces valid state transitions. |
| `$/Ama.CRDT/Attributes/CrdtStrategyAttribute.cs` | The base abstract attribute for marking properties with a specific CRDT merge strategy. Contains the strategy type. |
| `$/Ama.CRDT/Attributes/CrdtTwoPhaseSetStrategyAttribute.cs` | An attribute to mark a collection property to be managed by the 2P-Set (Two-Phase Set) strategy. |
| `$/Ama.CRDT/Attributes/CrdtVoteCounterStrategyAttribute.cs` | An attribute to mark a dictionary property to be managed by the Vote Counter strategy. This strategy ensures each voter has only one active vote, with changes resolved by Last-Writer-Wins. |
| `$/Ama.CRDT/Attributes/Strategies/AssociativeAttribute.cs` | Marks a CRDT strategy as having the associative property, meaning the order of operation grouping does not affect the outcome. |
| `$/Ama.CRDT/Attributes/Strategies/CommutativeAttribute.cs` | Marks a CRDT strategy as having the commutative property, meaning the order of operations does not affect the outcome. |
| `$/Ama.CRDT/Attributes/Strategies/IdempotentAttribute.cs` | Marks a CRDT strategy as having the idempotent property, meaning applying the same operation multiple times has the same effect as applying it once. |
| `$/Ama.CRDT/Attributes/Strategies/IdempotentShortLivedAttribute.cs` | No description provided. |
| `$/Ama.CRDT/Attributes/Strategies/MergeableAttribute.cs` | Marks a CRDT strategy as having a mergeable state, suitable for parallel reduction. |
| `$/Ama.CRDT/Attributes/Strategies/SequentialOperationsAttribute.cs` | Marks a CRDT strategy as requiring sequential operation application, making it unsuitable for parallel reduction. |
| `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs` | Provides DI extension methods for easy library setup, including registration of the patch builder, strategies, the strategy manager, custom array element comparers, a customizable timestamp provider, and a factory for creating replica-specific patchers. |
| `$/Ama.CRDT/Models/AverageRegisterValue.cs` | A data structure that holds a replica's contribution (value and timestamp) for the Average Register strategy. |
| `$/Ama.CRDT/Models/CrdtDocumentOfT.cs` | A generic version of `CrdtDocument` that holds a POCO and its associated `CrdtMetadata`, unifying the API for patch generation and application. |
| `$/Ama.CRDT/Models/CrdtMetadata.cs` | Encapsulates the state required for conflict resolution (LWW timestamps, seen operation IDs), externalizing it from the data model. |
| `$/Ama.CRDT/Models/CrdtOperation.cs` | Represents a single CRDT operation in a patch, including the target JSON Path, type, value, timestamp, and the ID of the replica that generated it. |
| `$/Ama.CRDT/Models/CrdtOptions.cs` | Provides configuration options for the CRDT library, such as the `ReplicaId`. |
| `$/Ama.CRDT/Models/CrdtPatch.cs` | Encapsulates a list of CRDT operations that represent the difference between two JSON documents. |
| `$/Ama.CRDT/Models/EpochTimestamp.cs` | A default, backward-compatible implementation of `ICrdtTimestamp` that wraps a `long` value representing Unix milliseconds. |
| `$/Ama.CRDT/Models/ExclusiveLockPayload.cs` | A data structure for the payload of an Exclusive Lock operation, containing the property value and the lock holder's ID. |
| `$/Ama.CRDT/Models/ICrdtTimestamp.cs` | Represents a logical point in time for a CRDT operation, allowing for different timestamping mechanisms. |
| `$/Ama.CRDT/Models/LseqIdentifier.cs` | A record struct for the dense, ordered identifier used in LSEQ, composed of a path of positions and replica IDs. |
| `$/Ama.CRDT/Models/LseqItem.cs` | A record struct that pairs an LseqIdentifier with its corresponding value in the LSEQ metadata. |
| `$/Ama.CRDT/Models/MovePayload.cs` | A data structure for the payload of a `Move` operation, containing the stable identifier of the element and its new parent. |
| `$/Ama.CRDT/Models/OperationType.cs` | Defines the types of operations (Upsert, Remove, Increment, Move) for a CRDT patch. |
| `$/Ama.CRDT/Models/OrSetItem.cs` | Contains payload record structs (`OrSetAddItem`, `OrSetRemoveItem`) for OR-Set (Observed-Remove Set) operations, bundling values with unique tags. |
| `$/Ama.CRDT/Models/PositionalIdentifier.cs` | No description provided. |
| `$/Ama.CRDT/Models/PositionalItem.cs` | A data structure used in operation payloads for positional array updates, bundling a stable position with the actual value. |
| `$/Ama.CRDT/Models/Serialization/CrdtMetadataJsonResolver.cs` | Provides a custom `IJsonTypeInfoResolver` for `CrdtMetadata` to enable efficient serialization by omitting empty collections from the JSON output. |
| `$/Ama.CRDT/Models/VotePayload.cs` | A data structure for the payload of a vote operation, containing the voter's identifier and their chosen option. |
| `$/Ama.CRDT/PublicAPI.Shipped.txt` | Tracks the shipped public API surface of the library to detect breaking changes. This file should be updated when new APIs are officially released in a stable version. |
| `$/Ama.CRDT/PublicAPI.Unshipped.txt` | Tracks new public APIs that have not yet been included in a stable release. This file must be empty before a manual, stable publish. Build will fail if new public APIs are added without being added to this file first. |
| `$/Ama.CRDT/Services/CrdtApplicator.cs` | No description provided. |
| `$/Ama.CRDT/Services/CrdtMetadataManager.cs` | Implements the logic for managing and compacting CRDT metadata. It provides helper methods like Initialize(document) to create a metadata object from a POCO by reflecting on its properties, and Reset(metadata, document) to clear and re-initialize an existing metadata object. The initialization logic correctly traverses nested objects and collections. |
| `$/Ama.CRDT/Services/CrdtPatcher.cs` | Implements the logic to recursively compare two objects and generate a CRDT patch by delegating to property-specific strategies. |
| `$/Ama.CRDT/Services/CrdtPatcherFactory.cs` | No description provided. |
| `$/Ama.CRDT/Services/EpochTimestampProvider.cs` | The default implementation of `ICrdtTimestampProvider` that generates `EpochTimestamp` based on Unix milliseconds. |
| `$/Ama.CRDT/Services/Helpers/PocoPathHelper.cs` | A utility class containing helper methods for parsing JSON paths and resolving them against POCOs using reflection. |
| `$/Ama.CRDT/Services/ICrdtApplicator.cs` | No description provided. |
| `$/Ama.CRDT/Services/ICrdtMetadataManager.cs` | Defines a service for managing CRDT metadata. Responsibilities include initializing or resetting metadata by traversing a document to create LWW timestamps and array positional trackers, pruning old tombstones to control state growth, and advancing version vectors. |
| `$/Ama.CRDT/Services/ICrdtPatcher.cs` | Defines the contract for a service that compares two versions of a data model and generates a CRDT patch. |
| `$/Ama.CRDT/Services/ICrdtPatcherFactory.cs` | No description provided. |
| `$/Ama.CRDT/Services/ICrdtTimestampProvider.cs` | Defines a service for generating CRDT timestamps, allowing for custom timestamp implementations. |
| `$/Ama.CRDT/Services/Strategies/ArrayLcsStrategy.cs` | Implements a CRDT strategy for arrays using LCS, with support for type-specific element comparers. `GeneratePatch` creates diffs, and `ApplyOperation` unconditionally manipulates the data array. |
| `$/Ama.CRDT/Services/Strategies/AverageRegisterStrategy.cs` | Implements the Average Register strategy, where each replica contributes a value and the final state is the average of all contributions. |
| `$/Ama.CRDT/Services/Strategies/BoundedCounterStrategy.cs` | Implements a counter that is clamped within a specified minimum and maximum value, configured via an attribute. |
| `$/Ama.CRDT/Services/Strategies/CounterStrategy.cs` | Implements the CRDT Counter strategy. `GeneratePatch` creates `Increment` operations, and the simplified `ApplyOperation` unconditionally applies the numeric delta. |
| `$/Ama.CRDT/Services/Strategies/CrdtStrategyManager.cs` | Implements the strategy resolution logic, finding the correct strategy for a property via reflection or returning a default (LWW or ArrayLcs). |
| `$/Ama.CRDT/Services/Strategies/ElementComparerProvider.cs` | Implements the provider logic to select a registered IElementComparer or a default, for use by ArrayLcsStrategy. |
| `$/Ama.CRDT/Services/Strategies/ExclusiveLockStrategy.cs` | Implements an optimistic exclusive lock strategy. Changes are only generated or applied if the lock is not held by a conflicting party. Lock state is resolved using LWW. |
| `$/Ama.CRDT/Services/Strategies/FixedSizeArrayStrategy.cs` | Implements a strategy for fixed-size arrays where each index is an LWW-Register. |
| `$/Ama.CRDT/Services/Strategies/GCounterStrategy.cs` | Implements the G-Counter (Grow-Only Counter) strategy, which only allows for positive increments. |
| `$/Ama.CRDT/Services/Strategies/GSetStrategy.cs` | Implements the G-Set (Grow-Only Set) CRDT strategy, where elements can only be added. |
| `$/Ama.CRDT/Services/Strategies/ICrdtStrategy.cs` | Defines the contract for a strategy, including `GeneratePatch` for creating operations and a simplified `ApplyOperation` for unconditional data manipulation. |
| `$/Ama.CRDT/Services/Strategies/ICrdtStrategyManager.cs` | Defines the contract for a service that resolves the appropriate CRDT strategy for a property. |
| `$/Ama.CRDT/Services/Strategies/IElementComparer.cs` | Defines the contract for a type-specific equality comparer for collection elements, used by ArrayLcsStrategy. |
| `$/Ama.CRDT/Services/Strategies/IElementComparerProvider.cs` | Defines the contract for a service that provides the correct IEqualityComparer<object> for a given array element type. |
| `$/Ama.CRDT/Services/Strategies/IStateMachine.cs` | Defines the contract for a state machine validator, which can be implemented by users to define custom transition logic. |
| `$/Ama.CRDT/Services/Strategies/LseqStrategy.cs` | Implements the LSEQ strategy, which manages ordered sequences using dense, sortable identifiers to ensure convergence. |
| `$/Ama.CRDT/Services/Strategies/LwwSetStrategy.cs` | Implements the LWW-Set (Last-Writer-Wins Set) CRDT strategy, where element membership is determined by the latest timestamp. |
| `$/Ama.CRDT/Services/Strategies/LwwStrategy.cs` | Implements the LWW strategy. `GeneratePatch` creates operations based on timestamps, and the simplified `ApplyOperation` unconditionally applies changes to nodes. |
| `$/Ama.CRDT/Services/Strategies/MaxWinsStrategy.cs` | Implements the Max-Wins Register strategy, where conflicts are resolved by always choosing the highest value. |
| `$/Ama.CRDT/Services/Strategies/MinWinsStrategy.cs` | Implements the Min-Wins Register strategy, where conflicts are resolved by always choosing the lowest value. |
| `$/Ama.CRDT/Services/Strategies/OrSetStrategy.cs` | Implements the OR-Set (Observed-Remove Set) CRDT strategy, which uses unique tags to correctly handle concurrent additions and removals. |
| `$/Ama.CRDT/Services/Strategies/PriorityQueueStrategy.cs` | Implements a strategy for collections that behave as a priority queue, sorted by a specified property. |
| `$/Ama.CRDT/Services/Strategies/SortedSetStrategy.cs` | Implements a CRDT strategy for collections that are treated as sorted sets. It uses LCS for diffing and ensures the collection remains sorted after operations. |
| `$/Ama.CRDT/Services/Strategies/StateMachineStrategy.cs` | Implements the State Machine strategy. It uses a user-provided validator to check state transitions and LWW for conflict resolution. |
| `$/Ama.CRDT/Services/Strategies/TwoPhaseSetStrategy.cs` | Implements the 2P-Set (Two-Phase Set) CRDT strategy, where an element, once removed, cannot be re-added. |
| `$/Ama.CRDT/Services/Strategies/VoteCounterStrategy.cs` | Implements the Vote Counter strategy. It manages a dictionary of options to voter sets, ensuring each voter can only have one active vote at a time, with conflicts resolved by Last-Writer-Wins. |
| `$/CodingStandards.md` | Contains the coding standards for the project, including versioning and publishing guidelines. |
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
| `$/Specs/add-approval-quorum-strategy.md` | Specification file for implementing the Approval Quorum strategy. |
| `$/Specs/add-checksum-strategy.md` | Specification file for implementing the Checksum strategy. |
| `$/Specs/add-immutable-strategy.md` | Specification file for implementing the Immutable strategy. |
| `$/Specs/add-leader-election-strategy.md` | Specification file for implementing the Leader Election strategy. |
| `$/Specs/add-more-meta-and-hybrid-strategies.md` | No description provided. |
| `$/Specs/add-more-object-and-map-strategies.md` | No description provided. |
| `$/Specs/add-more-specialized-data-structure-strategies.md` | No description provided. |
| `$/Specs/add-more-text-specific-strategies.md` | No description provided. |
| `$/Specs/add-ownership-strategy.md` | Specification file for implementing the Ownership strategy. |
| `$/Specs/add-validated-string-strategy.md` | Specification file for implementing the Validated String strategy. |
| `$/Specs/done/add-exclusive-lock-strategy.md` | No description provided. |
| `$/Specs/done/add-more-list-and-sequence-strategies.md` | No description provided. |
| `$/Specs/done/add-more-numeric-and-value-based-strategies.md` | No description provided. |
| `$/Specs/done/add-more-set-strategies.md` | No description provided. |
| `$/Specs/done/add-state-machine-strategy.md` | No description provided. |
| `$/Specs/done/add-vote-counter-strategy.md` | No description provided. |
| `$/Specs/done/create-example-console-app-that-show-cases-the-crdts-with-out-locks.md` | No description provided. |
| `$/Specs/done/implement-correctly-lcs-list-strategy.md` | No description provided. |
| `$/Specs/done/make-package-dev-friendly.md` | No description provided. |
| `$/Specs/done/make-the-api-surface-better.md` | No description provided. |
| `$/Specs/done/publish-as-a-nuget-package.md` | No description provided. |
| `$/Specs/done/readme-update-2025-08-24.md` | No description provided. |
| `$/Specs/make-crdt-strategies-composable.md` | No description provided. |
