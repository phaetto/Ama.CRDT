# Modern.CRDT

A .NET library for achieving eventual consistency in distributed systems using Conflict-free Replicated Data Types (CRDTs). It provides a simple, high-level API to compare, patch, and merge POCOs (Plain Old C# Objects), with merge behavior controlled by attributes.

## Features

- **Attribute-Driven Strategies**: Define conflict resolution logic directly on your POCO properties using attributes like `[LwwStrategy]`, `[CrdtCounter]`, and `[CrdtArrayLcsStrategy]`.
- **POCO-First**: Work directly with your C# objects. The library handles recursive diffing and patching seamlessly.
- **Clean Data/Metadata Separation**: Keeps your data models pure by storing CRDT state (timestamps, version vectors) in a parallel `CrdtMetadata` object.
- **Extensible**: Easily create and register your own custom CRDT strategies.
- **Multi-Replica Support**: Designed for scenarios with multiple concurrent writers, using a factory pattern to create services for each unique replica.
- **Dependency Injection Friendly**: All services are designed to be registered and resolved through a standard DI container.

## Getting Started

### Setup

In your `Program.cs` or service configuration file, register the CRDT services.

```csharp
using Modern.CRDT.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add CRDT services to the DI container
builder.Services.AddJsonCrdt(options =>
{
    // A default replica ID is required, but can be overridden
    // when creating replica-specific services.
    options.ReplicaId = "default-replica";
});

// ... other service registrations

var app = builder.Build();
```

## Controlling Merge Behavior with CRDT Strategies

This library uses attributes on your POCO properties to determine how to merge changes. You can control the conflict resolution logic for each property individually.

-   **`[LwwStrategy]` (Last-Writer-Wins)**: This is the default strategy for simple properties (strings, numbers, booleans, etc.). When a conflict occurs, the value with the most recent timestamp wins. You can omit this attribute, as it is the default.

-   **`[CrdtCounter]`**: Use this for numeric properties (`int`, `long`, etc.) that should behave like a counter. Instead of overwriting values, the library treats changes as increments or decrements. This ensures that concurrent additions (e.g., two users liking a post at the same time) are both counted, resulting in a final value that reflects the sum of all changes.

-   **`[CrdtArrayLcsStrategy]`**: This is the default strategy for collections (`List<T>`, arrays). It uses a Longest Common Subsequence (LCS) algorithm to intelligently handle insertions and deletions. This is more efficient than replacing the entire array, as it only generates operations for the items that were actually added or removed.

### Define Your Model

First, decorate your POCO properties with the desired CRDT strategy attributes. This model will be used in the following examples.

```csharp
using Modern.CRDT.Attributes;

public class UserStats
{
    [LwwStrategy] // Can be omitted as it's the default for simple types
    public string LastSeenLocation { get; set; } = string.Empty;

    [CrdtCounter]
    public long LoginCount { get; set; }

    [CrdtArrayLcsStrategy] // Can be omitted as it's the default for collections
    public List<string> Badges { get; set; } = [];
}
```

## High-Level Usage: `ICrdtService`

For single-replica applications or simple use cases, the `ICrdtService` provides a straightforward facade. It uses the default `ReplicaId` configured during setup.

```csharp
using Modern.CRDT.Models;
using Modern.CRDT.Services;

// 1. Inject services from DI container
// ICrdtService crdtService = ...;
// ICrdtMetadataManager metadataManager = ...;

// 2. Establish an initial state
var originalState = new UserStats { LoginCount = 5, Badges = new List<string> { "newcomer" } };
var originalMetadata = metadataManager.Initialize(originalState);
var originalDocument = new CrdtDocument<UserStats>(originalState, originalMetadata);

// 3. Modify the state
var modifiedState = new UserStats { LoginCount = 6, Badges = new List<string> { "newcomer", "explorer" } };
var modifiedDocument = new CrdtDocument<UserStats>(modifiedState, originalMetadata);

// 4. Create a patch. The service internally updates the metadata in modifiedDocument.
var patch = crdtService.CreatePatch(originalDocument, modifiedDocument);

// 5. Simulate receiving this patch elsewhere and applying it
var stateToMerge = new UserStats { LoginCount = 5, Badges = new List<string> { "newcomer" } };
var metadataToMerge = metadataManager.Initialize(stateToMerge);

crdtService.Merge(stateToMerge, patch, metadataToMerge);

// 6. Assert the new state is correct
// stateToMerge.LoginCount is now 6
// stateToMerge.Badges now contains "newcomer" and "explorer"
```

## Advanced Usage: Multi-Replica Synchronization

For distributed systems with multiple writers, you need a unique `ICrdtPatcher` for each replica. The `ICrdtPatcherFactory` allows you to create these. This example shows two replicas modifying the same object concurrently and converging to a consistent state.

```csharp
using Modern.CRDT.Models;
using Modern.CRDT.Services;

// 1. Inject services from DI container
// ICrdtPatcherFactory patcherFactory = ...;
// ICrdtApplicator applicator = ...;
// ICrdtMetadataManager metadataManager = ...;

// 2. Create replica-specific patchers
var patcherA = patcherFactory.Create("replica-A");
var patcherB = patcherFactory.Create("replica-B");

// 3. Establish a base state
var baseState = new UserStats { LastSeenLocation = "Lobby", LoginCount = 10, Badges = new List<string> { "welcome" } };
var baseMetadata = metadataManager.Initialize(baseState);

// 4. Create two replicas from the base state
var docA = new CrdtDocument<UserStats>(
    new UserStats { LastSeenLocation = "Lobby", LoginCount = 10, Badges = new List<string> { "welcome" } },
    metadataManager.Initialize(baseState)
);

var docB = new CrdtDocument<UserStats>(
    new UserStats { LastSeenLocation = "Lobby", LoginCount = 10, Badges = new List<string> { "welcome" } },
    metadataManager.Initialize(baseState)
);

// 5. Modify both replicas independently
// Replica A: User logs in again and earns a new badge
var modifiedA = new UserStats { LastSeenLocation = "Lobby", LoginCount = 11, Badges = new List<string> { "welcome", "veteran" } };

// Replica B: User changes location and also logs in
var modifiedB = new UserStats { LastSeenLocation = "Marketplace", LoginCount = 11, Badges = new List<string> { "welcome" } };

// 6. Generate patches
// The patcher automatically updates the metadata for the 'to' document with new timestamps
var patchFromA = patcherA.GeneratePatch(docA, new CrdtDocument<UserStats>(modifiedA, docA.Metadata));
var patchFromB = patcherB.GeneratePatch(docB, new CrdtDocument<UserStats>(modifiedB, docB.Metadata));

// 7. Synchronize: Cross-apply patches
// Apply A's patch to B's document
applicator.ApplyPatch(docB.Data, patchFromA, docB.Metadata);

// Apply B's patch to A's document
applicator.ApplyPatch(docA.Data, patchFromB, docA.Metadata);

// 8. Assert Convergence
// Both replicas now have the same converged state.
// docA.Data and docB.Data are now identical.
// - LastSeenLocation: "Marketplace" (LWW from B wins)
// - LoginCount: 12 (Counter incremented by both, 10 + 1 + 1)
// - Badges: ["welcome", "veteran"] (LCS merge adds "veteran")
```

## Managing Metadata Size

The `CrdtMetadata` object stores the necessary state to resolve conflicts and ensure eventual consistency. Over time, especially in frequently updated documents, this metadata can grow. The library provides tools to help you keep it compact.

### Pruning LWW Tombstones

When you remove a property from your object, its corresponding timestamp in the `Lww` metadata dictionary is kept as a "tombstone." This prevents an old version of the document from re-introducing the deleted value. While necessary, these tombstones can accumulate.

You can periodically prune old tombstones using the `ICrdtMetadataManager`. This is a trade-off: you save space, but you increase the risk of an old, offline replica re-introducing a value if it comes back online after the tombstone has been pruned. A common strategy is to prune tombstones older than a reasonable time window (e.g., 30 days).

```csharp
using Modern.CRDT.Services;
using Modern.CRDT.Models;

// 1. Inject the manager and timestamp provider
// ICrdtMetadataManager metadataManager = ...;
// ICrdtTimestampProvider timestampProvider = ...;

// 2. Assume 'myMetadata' is the CrdtMetadata for a document
CrdtMetadata myMetadata = ...; // Load the metadata for a document

// 3. Define a threshold. For example, prune everything older than 30 days.
// (This example assumes the default EpochTimestamp, which is based on milliseconds)
long thirtyDaysInMillis = 30L * 24 * 60 * 60 * 1000;
var nowTimestamp = (EpochTimestamp)timestampProvider.Now();
var thresholdTimestamp = new EpochTimestamp(nowTimestamp.Value - thirtyDaysInMillis);

// 4. Prune the metadata
metadataManager.PruneLwwTombstones(myMetadata, thresholdTimestamp);

// 5. Save the compacted metadata
// ...
```

### Version Vector Compaction

The library uses a version vector (`CrdtMetadata.VersionVector`) to track the latest operation timestamp seen from each replica. This is a highly efficient way to summarize the history of a document without storing every single operation. This process is not automatic and as a dev you have to call `metadataManager.AdvanceVersionVector(metadata, operation)` to ensure that the log of seen operations (`SeenExceptions`) does not grow indefinitely. You can safely do that after every patch on a replica.

## Extensibility: Creating Custom Strategies

You can extend the library with your own conflict resolution logic by creating a custom strategy. This involves three steps:

#### 1. Create a Custom Attribute

Create an attribute that inherits from `CrdtStrategyAttribute` and passes your strategy's type to the base constructor.

```csharp
public sealed class MyCustomStrategyAttribute() : CrdtStrategyAttribute(typeof(MyCustomStrategy));
```

#### 2. Implement `ICrdtStrategy`

Create a class that implements the `ICrdtStrategy` interface.
-   `GeneratePatch`: Contains the logic to compare two versions of a property and create a list of `CrdtOperation`s.
-   `ApplyOperation`: Contains the logic to apply a single operation to the POCO.

```csharp
public sealed class MyCustomStrategy : ICrdtStrategy
{
    public void GeneratePatch(ICrdtPatcher patcher, List<CrdtOperation> operations, string path, PropertyInfo property, object? originalValue, object? modifiedValue, CrdtMetadata originalMeta, CrdtMetadata modifiedMeta)
    {
        // Add custom diffing logic here
    }

    public void ApplyOperation(object root, CrdtOperation operation)
    {
        // Add custom application logic here
    }
}
```

#### 3. Register in the DI Container

In `Program.cs`, register your new strategy as a singleton and also register it as an `ICrdtStrategy` so the `CrdtStrategyManager` can find it.

```csharp
// In Program.cs
// ...
builder.Services.AddJsonCrdt(options => { /* ... */ });

// Register the custom strategy
builder.Services.AddSingleton<MyCustomStrategy>();

// Make it available to the strategy manager
builder.Services.AddSingleton<ICrdtStrategy>(sp => sp.GetRequiredService<MyCustomStrategy>());
```

You can now use `[MyCustomStrategy]` on your POCO properties.

## How It Works

-   **`ICrdtService`**: The high-level facade for simple use cases. It internally uses the `ICrdtPatcher` and `ICrdtApplicator` registered with the default replica ID.
-   **`ICrdtPatcher`**: Takes two `CrdtDocument<T>` objects (`from` and `to`) and generates a `CrdtPatch`. It recursively compares the POCOs, using the `ICrdtStrategyManager` to find the correct strategy for each property. It also updates the `to` document's metadata with new timestamps for any changed values.
-   **`ICrdtApplicator`**: Takes a POCO, a `CrdtPatch`, and the POCO's `CrdtMetadata`. It processes each operation in the patch, first checking the metadata to prevent applying stale or duplicate operations (ensuring idempotency). If an operation is valid, it uses the `ICrdtStrategyManager` to find the correct strategy to modify the POCO.
-   **`ICrdtStrategyManager`**: A service that inspects a property's attributes (e.g., `[CrdtCounter]`) to resolve and return the appropriate `ICrdtStrategy` implementation from the DI container. It provides default strategies (LWW for simple types, LCS for collections) if no attribute is present.
-   **`ICrdtPatcherFactory`**: A factory for creating `ICrdtPatcher` instances, each configured with a unique `ReplicaId`. This is crucial for correctly attributing changes in a multi-replica environment.
-   **`ICrdtMetadataManager`**: A helper service for managing the `CrdtMetadata` object. It can initialize metadata from a POCO, compact it to save space, and perform other state management tasks.

## Building and Testing

To build the project, simply run:

```bash
dotnet build
```

To run the unit tests:

```bash
dotnet test
```

## License
The code is provided as is, use it the way you want.