# Ama.CRDT

A .NET library for achieving eventual consistency in distributed systems using Conflict-free Replicated Data Types (CRDTs). It provides a simple, high-level API to compare, patch, and merge POCOs (Plain Old C# Objects), with merge behavior controlled by attributes.

## Features

- **Attribute-Driven Strategies**: Define conflict resolution logic directly on your POCO properties using a rich set of attributes like `[CrdtLwwStrategy]`, `[CrdtCounterStrategy]`, `[CrdtOrSetStrategy]`, and `[CrdtStateMachineStrategy]`.
- **POCO-First**: Work directly with your C# objects. The library handles recursive diffing and patching seamlessly.
- **Clean Data/Metadata Separation**: Keeps your data models pure by storing CRDT state (timestamps, version vectors) in a parallel `CrdtMetadata` object.
- **Extensible**: Easily create and register your own custom CRDT strategies, element comparers, and timestamp providers.
- **Multi-Replica Support**: Designed for scenarios with multiple concurrent writers, using a factory pattern to create services for each unique replica.
- **Dependency Injection Friendly**: All services are designed to be registered and resolved through a standard DI container.

## Installation

You can install Ama.CRDT via the .NET CLI or the NuGet Package Manager in Visual Studio.

### .NET CLI

```bash
dotnet add package Ama.CRDT
```

### NuGet Package Manager

In Visual Studio, open the NuGet Package Manager Console and run:

```powershell
Install-Package Ama.CRDT
```

Or, you can search for `Ama.CRDT` in the NuGet Package Manager UI and install it from there.

## Getting Started

### 1. Setup

In your `Program.cs` or service configuration file, register the CRDT services.

```csharp
using Ama.CRDT.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add CRDT services to the DI container
builder.Services.AddCrdt(options =>
{
    // A default replica ID is required, but it's best practice to
    // create replica-specific services using the ICrdtPatcherFactory.
    options.ReplicaId = "default-replica";
});

// ... other service registrations

var app = builder.Build();
```

### 2. Define Your Model

Decorate your POCO properties with the desired CRDT strategy attributes. These attributes tell the library how to handle concurrent changes.

```csharp
using Ama.CRDT.Attributes;

public class UserStats
{
    // LwwStrategy is the default, so this attribute is optional.
    // It's shown here for clarity.
    [CrdtLwwStrategy]
    public string LastSeenLocation { get; set; } = string.Empty;

    [CrdtCounterStrategy]
    public long LoginCount { get; set; }

    [CrdtOrSetStrategy] // Use OR-Set to allow badges to be re-added after removal.
    public List<string> Badges { get; set; } = [];
}
```

## Basic Usage

The core workflow involves creating a patch from a change and applying it to another replica.

```csharp
using Ama.CRDT.Models;
using Ama.CRDT.Services;

// 1. Inject services from DI container
// ICrdtPatcherFactory patcherFactory = ...;
// ICrdtApplicator applicator = ...;
// ICrdtMetadataManager metadataManager = ...;

// 2. Create a patcher for your replica
var patcher = patcherFactory.Create("replica-A");

// 3. Establish an initial state
var originalState = new UserStats { LoginCount = 5, Badges = new List<string> { "newcomer" } };
var originalMetadata = metadataManager.Initialize(originalState);
var originalDocument = new CrdtDocument<UserStats>(originalState, originalMetadata);

// 4. Modify the state
var modifiedState = new UserStats { LoginCount = 6, Badges = new List<string> { "newcomer", "explorer" } };
// The metadata must be passed to the new document to track timestamps correctly.
var modifiedDocument = new CrdtDocument<UserStats>(modifiedState, originalMetadata);

// 5. Create a patch. The patcher updates the metadata in modifiedDocument.
var patch = patcher.GeneratePatch(originalDocument, modifiedDocument);

// 6. Simulate receiving this patch elsewhere and applying it
var stateToMerge = new UserStats { LoginCount = 5, Badges = new List<string> { "newcomer" } };
var metadataToMerge = metadataManager.Initialize(stateToMerge);
var documentToMerge = new CrdtDocument<UserStats>(stateToMerge, metadataToMerge);

applicator.ApplyPatch(documentToMerge, patch);

// 7. Assert the new state is correct
// documentToMerge.Data.LoginCount is now 6
// documentToMerge.Data.Badges now contains ["newcomer", "explorer"]
```

## Controlling Merge Behavior with CRDT Strategies

This library uses attributes on your POCO properties to determine how to merge changes. You can control the conflict resolution logic for each property individually.

| Strategy | Description | Best Use Case |
| :--- | :--- | :--- |
| **Numeric & Value Strategies** | | |
| `[CrdtLwwStrategy]` | (Default) Last-Writer-Wins. The value with the latest timestamp overwrites others. | Simple properties like names, statuses, or any field where the last update should be the final state. |
| `[CrdtCounterStrategy]` | A simple counter that supports increments and decrements. | Likes, view counts, scores, or inventory quantities where changes are additive. |
| `[CrdtGCounterStrategy]` | A Grow-Only Counter that only supports positive increments. | Counting events that can only increase, like page visits or successful transactions. |
| `[CrdtBoundedCounterStrategy]` | A counter whose value is clamped within a specified minimum and maximum range. | Health bars in a game (0-100), volume controls, or any numeric value with hard limits. |
| `[CrdtMaxWinsStrategy]` | A register where conflicts are resolved by choosing the highest numeric value. | High scores, auction bids, or tracking the peak value of a metric. |
| `[CrdtMinWinsStrategy]` | A register where conflicts are resolved by choosing the lowest numeric value. | Best lap times, lowest price seen, or finding the earliest event timestamp. |
| `[CrdtAverageRegisterStrategy]` | A register where the final value is the average of contributions from all replicas. | Aggregating sensor readings from multiple devices, user ratings, or calculating an average latency. |
| **Set & Collection Strategies** | | |
| `[CrdtArrayLcsStrategy]` | (Default for collections) Uses Longest Common Subsequence (LCS) to handle insertions and deletions efficiently. Preserves order. | Collaborative text editing, managing ordered lists of tasks, or any sequence where element order matters. |
| `[CrdtSortedSetStrategy]` | Maintains a collection sorted by a natural or specified key. Uses LCS for diffing. | Leaderboards, sorted lists of tags, or displaying items in a consistent, sorted order. |
| `[CrdtGSetStrategy]` | A Grow-Only Set. Elements can be added but never removed. | Storing tags, accumulating unique identifiers, or tracking event participation where removal is not allowed. |
| `[CrdtTwoPhaseSetStrategy]` | A Two-Phase Set. Elements can be added and removed, but an element cannot be re-added once removed. | Managing feature flags or user roles where an item, once revoked, should stay revoked. |
| `[CrdtLwwSetStrategy]` | A Last-Writer-Wins Set. Element membership is determined by the timestamp of its last add or remove operation. | A shopping cart, user preferences, or any set where the most recent decision to add or remove an item should win. |
| `[CrdtOrSetStrategy]` | An Observed-Remove Set. Allows elements to be re-added after removal by tagging each addition uniquely. | Collaborative tagging systems or managing members in a group where users can leave and rejoin. |
| `[CrdtPriorityQueueStrategy]` | Manages a collection as a priority queue, sorted by a specified property on the elements. | Task queues, notification lists, or any scenario where items need to be processed based on priority. |
| `[CrdtFixedSizeArrayStrategy]` | Manages a fixed-size array where each index is an LWW-Register. Useful for representing grids or slots. | Game boards, seating charts, or fixed-size buffers where each position is updated independently. |
| `[CrdtLseqStrategy]` | An ordered list strategy that generates fractional indexes to avoid conflicts during concurrent insertions. | Collaborative text editors and other real-time sequence editing applications requiring high-precision ordering. |
| `[CrdtVoteCounterStrategy]` | Manages a dictionary of options to voter sets, ensuring each voter can only have one active vote at a time. | Polls, surveys, or any system where users vote for one of several options. |
| **Object & Map Strategies** | | |
| `[CrdtLwwMapStrategy]` | A Last-Writer-Wins Map. Each key-value pair is an independent LWW-Register. Conflicts are resolved per-key. | Storing user preferences, feature flags, or any key-value data where the last update for a given key should win. |
| `[CrdtOrMapStrategy]` | An Observed-Remove Map. Key presence is managed with OR-Set logic, allowing keys to be re-added after removal. Value updates use LWW. | Managing complex dictionaries where keys can be concurrently added and removed, such as a map of user permissions or editable metadata. |
| **State & Locking Strategies** | | |
| `[CrdtStateMachineStrategy]` | Enforces valid state transitions using a user-defined validator, with LWW for conflict resolution. | Order processing (Pending -> Shipped -> Delivered), workflows, or any property with a constrained lifecycle. |
| `[CrdtExclusiveLockStrategy]` | An optimistic exclusive lock where the latest lock or unlock operation (based on LWW) wins. | Preventing concurrent edits on a sub-document or resource without a central locking service. |

## Advanced Usage: Multi-Replica Synchronization

For distributed systems with multiple writers, you need a unique `ICrdtPatcher` for each replica. The `ICrdtPatcherFactory` allows you to create these. This example shows two replicas modifying the same object concurrently and converging to a consistent state.

```csharp
using Ama.CRDT.Models;
using Ama.CRDT.Services;

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

// 4. Create two replicas from the base state (deep cloning data and metadata)
var docA = new CrdtDocument<UserStats>(
    new UserStats { LastSeenLocation = "Lobby", LoginCount = 10, Badges = new List<string> { "welcome" } },
    metadataManager.Clone(baseMetadata)
);

var docB = new CrdtDocument<UserStats>(
    new UserStats { LastSeenLocation = "Lobby", LoginCount = 10, Badges = new List<string> { "welcome" } },
    metadataManager.Clone(baseMetadata)
);

// 5. Modify both replicas independently
// Replica A: User logs in again and earns a new badge
var modifiedAState = docA.Data;
modifiedAState.LoginCount++; // 11
modifiedAState.Badges.Add("veteran");

// Replica B: User changes location and also logs in
var modifiedBState = docB.Data;
modifiedBState.LastSeenLocation = "Marketplace";
modifiedBState.LoginCount++; // 11

// 6. Generate patches
var patchFromA = patcherA.GeneratePatch(
    new CrdtDocument<UserStats>(baseState, baseMetadata), // Compare against original base state
    docA
);
var patchFromB = patcherB.GeneratePatch(
    new CrdtDocument<UserStats>(baseState, baseMetadata),
    docB
);

// 7. Synchronize: Cross-apply patches
// Apply A's patch to B's document
applicator.ApplyPatch(docB, patchFromA);

// Apply B's patch to A's document
applicator.ApplyPatch(docA, patchFromB);

// 8. Assert Convergence
// Both replicas now have the same converged state.
// docA.Data and docB.Data are now identical.
// - LastSeenLocation: "Marketplace" (LWW from B wins, assuming later timestamp)
// - LoginCount: 12 (Counter incremented by both, 10 + 1 + 1)
// - Badges: ["veteran", "welcome"] (OR-Set merge adds "veteran")
```

## Managing Metadata Size

The `CrdtMetadata` object stores the necessary state to resolve conflicts and ensure eventual consistency. Over time, especially in frequently updated documents, this metadata can grow. The library provides tools to help you keep it compact.

### Pruning LWW Tombstones

When you remove a property from your object, its corresponding timestamp in the `Lww` metadata dictionary is kept as a "tombstone." This prevents an old version of the document from re-introducing the deleted value. While necessary, these tombstones can accumulate.

You can periodically prune old tombstones using the `ICrdtMetadataManager`. This is a trade-off: you save space, but you increase the risk of an old, offline replica re-introducing a value if it comes back online after the tombstone has been pruned. A common strategy is to prune tombstones older than a reasonable time window (e.g., 30 days).

```csharp
using Ama.CRDT.Services;
using Ama.CRDT.Models;

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
    public void GeneratePatch([DisallowNull] ICrdtPatcher patcher, [DisallowNull] List<CrdtOperation> operations, [DisallowNull] string path, [DisallowNull] PropertyInfo property, object? originalValue, object? modifiedValue, object? originalRoot, object? modifiedRoot, [DisallowNull] CrdtMetadata originalMeta, [DisallowNull] CrdtMetadata modifiedMeta)
    {
        // Add custom diffing logic here
    }

    public void ApplyOperation([DisallowNull] object root, [DisallowNull] CrdtMetadata metadata, CrdtOperation operation)
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
builder.Services.AddCrdt(options => { /* ... */ });

// Register the custom strategy
builder.Services.AddSingleton<MyCustomStrategy>();

// Make it available to the strategy manager
builder.Services.AddSingleton<ICrdtStrategy>(sp => sp.GetRequiredService<MyCustomStrategy>());
```

You can now use `[MyCustomStrategy]` on your POCO properties.

## Advanced Extensibility

Beyond creating custom strategies, you can also customize other core components of the library, such as array element comparison and timestamp generation.

### Customizing Array Element Comparison

The default `[CrdtArrayLcsStrategy]` uses `object.Equals` to compare elements in a collection. This works for simple types like `string` or `int`, but it's often insufficient for complex objects where uniqueness is determined by a specific property, like an `Id`. The `[CrdtSortedSetStrategy]` also benefits from this customization for identifying elements before sorting.

To solve this, you can implement the `IElementComparer` interface to provide type-specific comparison logic. The strategy manager will automatically find and use your custom comparer for the specified type.

#### 1. Implement `IElementComparer`

Create a class that implements `IElementComparer`. `CanCompare` tells the system which object type this comparer handles, and `Equals`/`GetHashCode` define the comparison logic.

**Example:** Imagine a list of `User` objects that should be uniquely identified by their `Id` property.

_Models/User.cs_
```csharp
public class User
{
    public Guid Id { get; set; }
    public string Name { get; set; }
}
```

_Services/UserComparer.cs_
```csharp
using Ama.CRDT.Services.Providers;
using System.Diagnostics.CodeAnalysis;

// Assuming User model is in this namespace
using MyProject.Models; 

public class UserComparer : IElementComparer
{
    public bool CanCompare(Type type)
    {
        // This comparer is responsible for User objects
        return type == typeof(User);
    }

    public bool Equals(object? x, object? y)
    {
        if (x is not User userX || y is not User userY)
        {
            return object.Equals(x, y);
        }
        // Uniquely identify users by their Id
        return userX.Id == userY.Id;
    }

    public int GetHashCode([DisallowNull] object obj)
    {
        if (obj is User user)
        {
            return user.Id.GetHashCode();
        }
        return obj.GetHashCode();
    }
}
```

#### 2. Register the Comparer

Use the `AddCrdtComparer<TComparer>()` extension method in your service configuration to register your implementation.

```csharp
// In Program.cs
builder.Services.AddCrdt(options => { /* ... */ });

// Register the custom comparer
builder.Services.AddCrdtComparer<UserComparer>();
```

Now, whenever `CrdtArrayLcsStrategy` or `CrdtSortedSetStrategy` processes a `List<User>`, it will use `UserComparer` to correctly identify and diff the elements.

### Providing a Custom Timestamp

By default, the library uses a timestamp based on `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()`. While this is suitable for many scenarios, you may need to integrate a different time source or use a logical clock (like a Lamport timestamp or a version vector clock).

You can replace the default timestamping mechanism by implementing `ICrdtTimestampProvider` and registering it.

#### 1. Implement `ICrdtTimestampProvider`

Your custom provider must return an object that implements `ICrdtTimestamp`. The timestamp object itself must be comparable.

**Note**: This is an advanced scenario. A custom timestamp implementation must be carefully designed to be monotonic and, if necessary, globally unique to ensure correctness.

_Services/MyCustomTimestampProvider.cs_
```csharp
using Ama.CRDT.Services;
using Ama.CRDT.Models;

public class MyCustomTimestampProvider : ICrdtTimestampProvider
{
    public ICrdtTimestamp Now()
    {
        // Your logic to generate a custom timestamp
        // For example, from a hybrid logical clock service.
        var customTimestampValue = GetTimestampFromHlcService(); 
        return new CustomTimestamp(customTimestampValue); // Or EpochTimestamp if it fits to long
    }
}
```

#### 2. Register the Provider

Use the `AddCrdtTimestampProvider<TProvider>()` extension method to replace the default provider.

```csharp
// In Program.cs
builder.Services.AddCrdt(options => { /* ... */ });

// Replace the default provider with your custom one
builder.Services.AddCrdtTimestampProvider<MyCustomTimestampProvider>();
```

## How It Works

-   **`ICrdtPatcherFactory`**: A factory for creating `ICrdtPatcher` instances, each configured with a unique `ReplicaId`. This is crucial for correctly attributing changes in a multi-replica environment.
-   **`ICrdtPatcher`**: Takes two `CrdtDocument<T>` objects (`from` and `to`) and generates a `CrdtPatch`. It recursively compares the POCOs, using the `ICrdtStrategyProvider` to find the correct strategy for each property. It also updates the `to` document's metadata with new timestamps for any changed values.
-   **`ICrdtApplicator`**: Takes a `CrdtDocument<T>` and a `CrdtPatch`. It processes each operation in the patch. If an operation is valid, it uses the `ICrdtStrategyProvider` to find the correct strategy to modify the POCO.
  - **`ICrdtStrategyProvider`**: A service that inspects a property's attributes (e.g., `[CrdtCounterStrategy]`) to resolve and return the appropriate `ICrdtStrategy` implementation from the DI container. It provides default strategies (LWW for simple types, LCS for collections) if no attribute is present.
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
The code is licensed under MIT.