# Multi-Replica Synchronization & Native AOT Serialization

For distributed systems with multiple writers, you need a unique set of services for each replica. The `ICrdtScopeFactory` is the recommended way to create these. This example shows two replicas modifying the same object concurrently and converging to a consistent state.

## Simulating Multi-Replica Synchronization

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
var applyResultB = applicatorB.ApplyPatch(docB, patchFromA);

// Apply B's patch to A's document
var applyResultA = applicatorA.ApplyPatch(docA, patchFromB);

// 8. Assert Convergence
// Both replicas now have the same converged state.
// docA.Data and docB.Data are now identical.
// - LastSeenLocation: "Marketplace" (LWW from B wins, assuming later timestamp)
// - LoginCount: 12 (Counter incremented by both, 10 + 1 + 1)
// - Badges: ["veteran", "welcome"] (OR-Set merge adds "veteran")
```

## Native AOT & Serialization Contexts

Ama.CRDT is fully Native AOT compatible. To achieve this, it strictly avoids `System.Reflection` at runtime and instead uses Roslyn Source Generators to pre-calculate metadata and JSON serialization logic.

Whenever you define a new model, you must register it in two source generators: 
1. **`CrdtContext`**: For property access and tracking changes without reflection.
2. **`JsonSerializerContext`**: For generating Native AOT `System.Text.Json` rules.

### 1. Define your AOT Contexts

```csharp
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Models;
using Ama.CRDT.Attributes;
using System.Text.Json.Serialization;

// AOT Reflection Context
[CrdtSerializable(typeof(UserStats))]
public partial class AppCrdtContext : CrdtContext
{
}

// AOT JSON Context
[JsonSerializable(typeof(UserStats))]
[JsonSerializable(typeof(CrdtDocument<UserStats>))] // If you plan to serialize the whole document
public partial class AppJsonContext : JsonSerializerContext
{
}
```

### 2. Register contexts in Dependency Injection

By registering these contexts in DI, the library merges them with its internal contexts to natively support deep polymorphism and optimal network serialization.

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCrdt()
    .AddCrdtAotContext<AppCrdtContext>()
    .AddCrdtJsonTypeInfoResolver(AppJsonContext.Default);
```

### 3. Serializing and Transmitting Patches

Once a `CrdtPatch` is generated, it needs to be sent to other replicas. This is typically done by serializing the patch to JSON.

Because Ama.CRDT supports complex polymorphism (like various `ICrdtTimestamp` types or dynamic `object` payloads), you should **always** use the unified `JsonSerializerOptions` that the library generates and registers in the DI container as a Keyed Service (`"Ama.CRDT"`).

```csharp
using Ama.CRDT.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

public class NetworkService
{
    private readonly JsonSerializerOptions crdtJsonOptions;

    public NetworkService([FromKeyedServices("Ama.CRDT")] JsonSerializerOptions options)
    {
        crdtJsonOptions = options;
    }

    public string SerializePatch(CrdtPatch patch)
    {
        // 1. Serialize the patch using the AOT-safe options provided by DI
        return JsonSerializer.Serialize(patch, crdtJsonOptions);
    }

    public CrdtPatch DeserializePatch(string jsonPayload)
    {
        // 2. Deserialize the JSON string back into a CrdtPatch object.
        return JsonSerializer.Deserialize<CrdtPatch>(jsonPayload, crdtJsonOptions);
    }
}
```

**Important**: If you have created a custom `ICrdtTimestamp` or a custom operation payload type, you must register it with the serialization system using `AddCrdtTimestampType<T>()` or `AddCrdtSerializableType<T>()`. See [Extensibility & Customization](extensibility.md) for details.