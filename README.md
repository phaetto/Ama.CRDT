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

// Add CRDT services to the DI container.
// This registers all core services, including strategies, providers,
// and the ICrdtScopeFactory for creating replica-specific scopes.
builder.Services.AddCrdt();

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
using Microsoft.Extensions.DependencyInjection;

// Assume 'serviceProvider' is your configured IServiceProvider

// 1. Get the scope factory. This is typically a singleton.
var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();

// 2. Create a scope for a specific replica (e.g., a user session)
using var userScope = scopeFactory.CreateScope("user-session-abc");

// 3. Resolve replica-specific services from the scope.
// These services are configured with the ReplicaId "user-session-abc".
var patcher = userScope.ServiceProvider.GetRequiredService<ICrdtPatcher>();
var applicator = userScope.ServiceProvider.GetRequiredService<ICrdtApplicator>();
var metadataManager = userScope.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();

// 4. Establish an initial state
var originalState = new UserStats { LoginCount = 5, Badges = ["newcomer"] };
var originalMetadata = metadataManager.Initialize(originalState);
var originalDocument = new CrdtDocument<UserStats>(originalState, originalMetadata);

// 5. Modify the state
var modifiedState = new UserStats { LoginCount = 6, Badges = ["newcomer", "explorer"] };

// 6. Create a patch.
var patch = patcher.GeneratePatch(originalDocument, modifiedState);

// 7. On another replica, apply the patch.
// First, create a scope and get services for the other replica.
using var serverScope = scopeFactory.CreateScope("server-node-xyz");
var serverApplicator = serverScope.ServiceProvider.GetRequiredService<ICrdtApplicator>();
var serverMetadataManager = serverScope.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();

var serverState = new UserStats { LoginCount = 5, Badges = ["newcomer"] };
var serverMetadata = serverMetadataManager.Initialize(serverState);
var serverDocument = new CrdtDocument<UserStats>(serverState, serverMetadata);

serverApplicator.ApplyPatch(serverDocument, patch);

// 8. Assert the new state is correct
// serverDocument.Data.LoginCount is now 6
// serverDocument.Data.Badges now contains ["newcomer", "explorer"]
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
| `[CrdtCounterMapStrategy]` | Manages a dictionary where each key is an independent PN-Counter (supporting increments and decrements). | Tracking scores per player, counting votes per option, or managing inventory per item where quantities can go up or down. |
| `[CrdtMaxWinsMapStrategy]` | A grow-only map where conflicts for each key are resolved by choosing the highest value. | Storing high scores per level in a game, tracking the latest version number per component, or recording the peak bid for different auction items. |
| `[CrdtMinWinsMapStrategy]` | A grow-only map where conflicts for each key are resolved by choosing the lowest value. | Recording the best completion time per race track, finding the cheapest price offered per product from various sellers, or tracking the earliest discovery time for different artifacts. |
| **Specialized Data Structure Strategies** | | |
| `[CrdtGraphStrategy]` | An add-only graph. Supports concurrent additions of vertices and edges. | Building social networks, knowledge graphs, or any scenario where relationships are added but not removed. |
| `[CrdtTwoPhaseGraphStrategy]`| A graph where vertices and edges can be added and removed, but not re-added after removal. | Managing network topologies or dependency graphs where components, once removed, are considered permanently decommissioned. |
| `[CrdtReplicatedTreeStrategy]`| Manages a hierarchical tree structure. Uses OR-Set for node existence (allowing re-addition) and LWW for parent-child links (move operations). | Collaborative document outlines, folder structures, or comment threads where items can be concurrently added, removed, and reorganized. |
| **State & Locking Strategies** | | |
| `[CrdtStateMachineStrategy]` | Enforces valid state transitions using a user-defined validator, with LWW for conflict resolution. | Order processing (Pending -> Shipped -> Delivered), workflows, or any property with a constrained lifecycle. |
| `[CrdtExclusiveLockStrategy]` | An optimistic exclusive lock where the latest lock or unlock operation (based on LWW) wins. | Preventing concurrent edits on a sub-document or resource without a central locking service. |

## Advanced Usage: Multi-Replica Synchronization

For distributed systems with multiple writers, you need a unique set of services for each replica. The `ICrdtScopeFactory` is the recommended way to create these. This example shows two replicas modifying the same object concurrently and converging to a consistent state.

```csharp
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Microsoft.Extensions.DependencyInjection;

// 1. Get the scope factory from the root DI container.
var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();

// 2. Create scopes and resolve services for each replica.
// All CRDT services (patcher, applicator, metadata manager) must be
// resolved from a replica-specific scope to be configured correctly.
using var scopeA = scopeFactory.CreateScope("replica-A");
var patcherA = scopeA.ServiceProvider.GetRequiredService<ICrdtPatcher>();
var applicatorA = scopeA.ServiceProvider.GetRequiredService<ICrdtApplicator>();
var metadataManagerA = scopeA.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();

using var scopeB = scopeFactory.CreateScope("replica-B");
var patcherB = scopeB.ServiceProvider.GetRequiredService<ICrdtPatcher>();
var applicatorB = scopeB.ServiceProvider.GetRequiredService<ICrdtApplicator>();
var metadataManagerB = scopeB.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();

// 3. Establish a base state
var baseState = new UserStats { LastSeenLocation = "Lobby", LoginCount = 10, Badges = ["welcome"] };
var baseMetadata = metadataManagerA.Initialize(baseState); // Use any manager for initialization

// 4. Create two replicas from the base state (deep cloning data and metadata)
var docA = new CrdtDocument<UserStats>(
    new UserStats { LastSeenLocation = "Lobby", LoginCount = 10, Badges = ["welcome"] },
    metadataManagerA.Clone(baseMetadata)
);

var docB = new CrdtDocument<UserStats>(
    new UserStats { LastSeenLocation = "Lobby", LoginCount = 10, Badges = ["welcome"] },
    metadataManagerB.Clone(baseMetadata)
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
    modifiedAState
);
var patchFromB = patcherB.GeneratePatch(
    new CrdtDocument<UserStats>(baseState, baseMetadata),
    modifiedBState
);

// 7. Synchronize: Cross-apply patches
// Apply A's patch to B's document
applicatorB.ApplyPatch(docB, patchFromA);

// Apply B's patch to A's document
applicatorA.ApplyPatch(docA, patchFromB);

// 8. Assert Convergence
// Both replicas now have the same converged state.
// docA.Data and docB.Data are now identical.
// - LastSeenLocation: "Marketplace" (LWW from B wins, assuming later timestamp)
// - LoginCount: 12 (Counter incremented by both, 10 + 1 + 1)
// - Badges: ["veteran", "welcome"] (OR-Set merge adds "veteran")
```

## Serializing and Transmitting Patches

Once a `CrdtPatch` is generated, it needs to be sent to other replicas. This is typically done by serializing the patch to JSON.

The library provides pre-configured `JsonSerializerOptions` for this purpose through the `CrdtJsonContext` class. Using `CrdtJsonContext.DefaultOptions` is the recommended way to serialize patches, as it automatically handles:
- Polymorphic `ICrdtTimestamp` types.
- Polymorphic `object` payloads in `CrdtOperation.Value`.
- Dictionaries with non-string keys.

```csharp
using Ama.CRDT.Models;
using Ama.CRDT.Models.Serialization;
using System.Text.Json;

// Assume 'patch' is the CrdtPatch you generated.
CrdtPatch patch = ...; 

// 1. Serialize the patch to a JSON string using the recommended options.
string jsonPayload = JsonSerializer.Serialize(patch, CrdtJsonContext.DefaultOptions);

// This 'jsonPayload' can now be sent across the network.

// --- On the receiving replica ---

// 2. Deserialize the JSON string back into a CrdtPatch object.
CrdtPatch receivedPatch = JsonSerializer.Deserialize<CrdtPatch>(jsonPayload, CrdtJsonContext.DefaultOptions);

// 3. Apply the received patch.
// var applicator = ...;
// var document = ...;
// applicator.ApplyPatch(document, receivedPatch);
```
**Important**: If you have created a custom `ICrdtTimestamp` or a custom operation payload type, you must register it with the serialization system. See the section on "Extensibility" for details.

## Managing Metadata Size

The `CrdtMetadata` object stores the necessary state to resolve conflicts and ensure eventual consistency. Over time, this metadata can grow. The library provides tools to help you keep it compact.

### Pruning LWW Tombstones

When you remove a property, its timestamp in the `Lww` metadata is kept as a "tombstone" to prevent old versions from re-introducing the deleted value. You can periodically prune old tombstones using `ICrdtMetadataManager`.

```csharp
using Ama.CRDT.Services;
using Ama.CRDT.Models;
using Ama.CRDT.Services.Providers;

// 1. Resolve services from a replica scope
// var scope = ...;
// var metadataManager = scope.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();
// var timestampProvider = scope.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();

// 2. Assume 'myMetadata' is the CrdtMetadata for a document
CrdtMetadata myMetadata = ...;

// 3. Define a threshold (e.g., prune everything older than 30 days).
// (This example assumes the default EpochTimestamp, based on milliseconds)
long thirtyDaysInMillis = 30L * 24 * 60 * 60 * 1000;
var nowTimestamp = (EpochTimestamp)timestampProvider.Now();
var thresholdTimestamp = new EpochTimestamp(nowTimestamp.Value - thirtyDaysInMillis);

// 4. Prune the metadata
metadataManager.PruneLwwTombstones(myMetadata, thresholdTimestamp);

// 5. Save the compacted metadata
// ...
```

### Version Vector Compaction

The library uses a version vector (`CrdtMetadata.VersionVector`) and a set of seen exceptions (`SeenExceptions`) to provide idempotency. This process is managed automatically by the `CrdtApplicator` for strategies marked with `[IdempotentWithContinuousTimeAttribute]` when using a continuous timestamp provider (like `SequentialTimestampProvider`), keeping metadata size under control without manual intervention.

### Serializing Metadata Efficiently

To avoid bloated JSON output from empty collections in the metadata object, use the pre-configured `CrdtJsonContext.MetadataCompactOptions`. It automatically omits empty collections and handles all necessary custom converters.

```csharp
using Ama.CRDT.Models;
using Ama.CRDT.Models.Serialization;
using System.Text.Json;

// Assume 'metadata' is your CrdtMetadata object.
CrdtMetadata metadata = ...; 

// 1. Serialize using the compact options.
string jsonPayload = JsonSerializer.Serialize(metadata, CrdtJsonContext.MetadataCompactOptions);

// This 'jsonPayload' is now compact and can be stored or sent.

// --- When reading the metadata back ---

// 2. Deserialize using the same options.
CrdtMetadata deserializedMetadata = JsonSerializer.Deserialize<CrdtMetadata>(jsonPayload, CrdtJsonContext.MetadataCompactOptions);
```

## Extensibility: Creating Custom Strategies

You can extend the library with your own conflict resolution logic by creating a custom strategy.

#### 1. Create a Custom Attribute

Create an attribute inheriting from `CrdtStrategyAttribute`.

```csharp
public sealed class MyCustomStrategyAttribute() : CrdtStrategyAttribute(typeof(MyCustomStrategy));
```

#### 2. Implement `ICrdtStrategy`

Create a class that implements `ICrdtStrategy`, using the context objects for parameters.

```csharp
using Ama.CRDT.Services.Strategies;

public sealed class MyCustomStrategy : ICrdtStrategy
{
    public void GeneratePatch(GeneratePatchContext context)
    {
        // Add custom diffing logic here
        // var (patcher, operations, path, ...) = context;
    }

    public void ApplyOperation(ApplyOperationContext context)
    {
        // Add custom application logic here
        // var (root, metadata, operation) = context;
    }
}
```

#### 3. Register in the DI Container

Register your new strategy with a scoped lifetime.

```csharp
// In Program.cs
// ...
builder.Services.AddCrdt();

// Register the custom strategy with a scoped lifetime
builder.Services.AddScoped<MyCustomStrategy>();

// Make it available to the strategy provider
builder.Services.AddScoped<ICrdtStrategy>(sp => sp.GetRequiredService<MyCustomStrategy>());
```

You can now use `[MyCustomStrategy]` on your POCO properties.

## Advanced Extensibility

### Customizing Array Element Comparison

By default, collection strategies use deep equality. To identify complex objects by a unique property (like an `Id`), implement `IElementComparer`.

#### 1. Implement `IElementComparer`

**Example:** A comparer for `User` objects that uses the `Id` property.

_Services/UserComparer.cs_
```csharp
using Ama.CRDT.Services.Providers;
using System.Diagnostics.CodeAnalysis;
using YourApp.Models; 

public class UserComparer : IElementComparer
{
    public bool CanCompare(Type type) => type == typeof(User);

    public new bool Equals(object? x, object? y)
    {
        if (x is User userX && y is User userY)
        {
            return userX.Id == userY.Id;
        }
        return object.Equals(x, y);
    }

    public int GetHashCode([DisallowNull] object obj)
    {
        return (obj is User user) ? user.Id.GetHashCode() : obj.GetHashCode();
    }
}
```

#### 2. Register the Comparer

Use the `AddCrdtComparer<TComparer>()` extension method.

```csharp
// In Program.cs
builder.Services.AddCrdt();
builder.Services.AddCrdtComparer<UserComparer>();
```

### Providing a Custom Timestamp

You can replace the default timestamping mechanism by implementing `ICrdtTimestampProvider`.

#### 1. Implement `ICrdtTimestampProvider`

The provider must be thread-safe if used concurrently and should be registered with a scoped lifetime.

```csharp
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Models;
using System.Threading;

public sealed class LogicalClockProvider : ICrdtTimestampProvider
{
    private long counter = 0;
    public bool IsContinuous => true;

    public ICrdtTimestamp Now() => new SequentialTimestamp(Interlocked.Increment(ref counter));
    public ICrdtTimestamp Init() => new SequentialTimestamp(0);
    public ICrdtTimestamp Create(long value) => new SequentialTimestamp(value);

    public IEnumerable<ICrdtTimestamp> IterateBetween(ICrdtTimestamp start, ICrdtTimestamp end)
    {
        if (start is not SequentialTimestamp s || end is not SequentialTimestamp e) yield break;
        for (var i = s.Value + 1; i < e.Value; i++) yield return new SequentialTimestamp(i);
    }
}
```

#### 2. Register the Provider

Use `AddCrdtTimestampProvider<TProvider>()` to replace the default.

```csharp
// In Program.cs
builder.Services.AddCrdt();
builder.Services.AddCrdtTimestampProvider<LogicalClockProvider>();
```

#### 3. Register a Custom Timestamp Type for Serialization

If you create your own `ICrdtTimestamp` implementation, you **must** register it for serialization.

**Example**: A custom `VectorClockTimestamp`.

_Models/VectorClockTimestamp.cs_
```csharp
public readonly record struct VectorClockTimestamp(long Ticks) : ICrdtTimestamp
{
    public int CompareTo(ICrdtTimestamp? other)
    {
        if (other is null) return 1;

        if (other is not VectorClockTimestamp otherClock)
        {
            throw new ArgumentException("Cannot compare VectorClockTimestamp to other timestamp types.");
        }
        
        return Ticks.CompareTo(otherClock.Ticks);
    }
}
```

_In Program.cs_
```csharp
builder.Services.AddCrdt();
builder.Services.AddCrdtTimestampProvider<VectorClockProvider>(); 

// Register the custom timestamp type with a unique discriminator string.
builder.Services.AddCrdtTimestampType<VectorClockTimestamp>("vector-clock");
```

Now, when a patch with a `VectorClockTimestamp` is serialized, the JSON will include a `"$type": "vector-clock"` discriminator.

## How It Works

-   **`ICrdtScopeFactory`**: A singleton factory for creating isolated `IServiceScope` instances. Each scope is configured for a specific `ReplicaId` and provides its own set of scoped services. This is the primary entry point for working with multiple replicas.
-   **`ICrdtPatcher`**: A scoped service that generates a `CrdtPatch` by comparing two versions of a document. It is a stateless service that only reads the `from` document's metadata to understand the state before the change. It uses the `ICrdtStrategyProvider` to find the correct strategy for each property.
-   **`ICrdtApplicator`**: A scoped service that applies a `CrdtPatch` to a document. It uses the `ICrdtStrategyProvider` to find the correct strategy to modify the POCO and its metadata. It also handles idempotency checks using version vectors for supported strategies.
-   **`ICrdtStrategyProvider`**: A service that inspects a property's attributes (e.g., `[CrdtCounterStrategy]`) to resolve the appropriate `ICrdtStrategy` from the DI container. It provides default strategies (LWW for simple types, ArrayLcs for collections) if no attribute is present.
-   **`ICrdtMetadataManager`**: A scoped helper service for managing the `CrdtMetadata` object. It can initialize metadata from a POCO, compact it to save space, and perform other state management tasks.

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