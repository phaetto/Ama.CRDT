# Managing Metadata Size (Garbage Collection)

The `CrdtMetadata` object stores the necessary state to resolve conflicts and ensure eventual consistency. When you remove properties, overwrite values, or remove elements from sets/maps, their timestamps or unique tags are kept as "tombstones" to prevent older updates from incorrectly re-introducing them. Similarly, network partitions may cause replicas to buffer out-of-order logs (`SeenExceptions`).

Over time, this metadata can grow. The library provides a robust Garbage Collection system through `ICompactionPolicy` and `ICompactionPolicyFactory` to help you safely compact and discard obsolete tombstones.

## 1. Automatic Garbage Collection (Recommended)

The easiest way to keep your metadata compact is to run garbage collection automatically every time a patch is applied. Ama.CRDT provides a `CompactingApplicatorDecorator` that seamlessly intercepts the `ApplyPatchAsync` flow, evaluates your registered compaction policies, and prunes the document.

You configure this entirely in your Dependency Injection setup:

```csharp
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Services.Decorators;
using Ama.CRDT.Services.GarbageCollection;
using Ama.CRDT.Services.Providers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCrdt()
    // 1. Register a compaction policy factory.
    // Example: A Time-To-Live (TTL) threshold that compacts tombstones older than 7 days.
    .AddCrdtCompactionPolicyFactory(sp => new ThresholdCompactionPolicyFactory(
        TimeSpan.FromDays(7), 
        sp.GetRequiredService<ICrdtTimestampProvider>()
    ))
    
    // 2. Decorate the applicator to automatically trigger compaction after patches are applied.
    .AddCrdtApplicatorDecorator<CompactingApplicatorDecorator>(DecoratorBehavior.After);
```

With this configured, you never have to manually call compaction logic. The decorator handles it in the background immediately after a patch succeeds.

## 2. Available Compaction Policies

The library ships with two major types of compaction policies depending on your consistency needs:

### Time-To-Live / Threshold Policy
The `ThresholdCompactionPolicy` uses heuristics. It considers any timestamp or logical version older than a specified threshold as safe to compact. This is ideal for most scenarios where you can assume that operations older than a certain window (e.g., 30 days) have naturally propagated to all active replicas.

```csharp
// TTL based on wall-clock time
var ttlFactory = new ThresholdCompactionPolicyFactory(
    TimeSpan.FromDays(30), 
    timestampProvider
);
```

### Global Minimum Version Vector (GMVV) Policy
The `GlobalMinimumVersionPolicy` provides mathematical safety. It ensures a causal operation is only compacted if *every known replica* in the cluster has acknowledged it. This is typically used in tightly coupled, server-to-server clusters where nodes gossip their version vectors.

```csharp
// A delegate that dynamically fetches the lowest confirmed version for all replicas across the cluster
var clusterGmvvFactory = new GlobalMinimumVersionPolicyFactory(() => 
{
    return GetClusterGlobalMinimumVersionsFromDatabase(); 
});
```

## 3. Manual Compaction

If you prefer to run garbage collection on a scheduled background job rather than on every patch application, you can invoke the `ICrdtMetadataManager` directly.

```csharp
using Ama.CRDT.Services;
using Ama.CRDT.Models;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.GarbageCollection;

// 1. Resolve services from a replica scope
// var scope = ...;
// var metadataManager = scope.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();
// var timestampProvider = scope.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();

// 2. Assume 'myDocument' is a CrdtDocument<T>
CrdtDocument<MyModel> myDocument = ...;

// 3. Define a compaction policy.
long thirtyDaysInMillis = 30L * 24 * 60 * 60 * 1000;
var nowTimestamp = (EpochTimestamp)timestampProvider.Now();
var thresholdTimestamp = new EpochTimestamp(nowTimestamp.Value - thirtyDaysInMillis);
var ttlPolicy = new ThresholdCompactionPolicy(thresholdTimestamp);

// 4. Compact the document and its metadata
metadataManager.Compact(myDocument, ttlPolicy);
```

The `Compact` method recursively traverses your document, delegating to the corresponding strategies (like LWW, OR-Set, Maps, etc.) to evaluate and safely remove tombstones based on the provided policy. It will also prune old out-of-order operations (`SeenExceptions`) that meet the criteria.

## Version Vector Compaction

The library uses a continuous version vector (`CrdtMetadata.VersionVector`) and a set of discrete out-of-order dots (`SeenExceptions`) to provide idempotency and DVV tracking.

This causality tracking is managed completely automatically by the `CrdtApplicator`. When causal order is established (i.e., when all gaps are filled), the `ICrdtMetadataManager.AdvanceVersionVector` logic intelligently collapses the `SeenExceptions` back into the main `VersionVector`, ensuring this metadata remains inherently compressed without the need for manual GC.

## Serializing Metadata Efficiently

To avoid bloated JSON output from empty collections in the metadata object (e.g., cleared sets, empty out-of-order logs), use the DI-injected `JsonSerializerOptions` (from the `"Ama.CRDT"` Keyed Service). The library registers modifiers to omit empty collections automatically, making Native AOT execution fast and the network payload compact.

```csharp
using Ama.CRDT.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

// Assume 'metadata' is your CrdtMetadata object.
CrdtMetadata metadata = ...; 

// Retrieve options from DI (which already includes CrdtMetadataJsonResolver)
var options = serviceProvider.GetRequiredKeyedService<JsonSerializerOptions>("Ama.CRDT");

// 1. Serialize
string jsonPayload = JsonSerializer.Serialize(metadata, options);

// This 'jsonPayload' is now compact and can be stored or sent.

// --- When reading the metadata back ---

// 2. Deserialize using the same options.
CrdtMetadata deserializedMetadata = JsonSerializer.Deserialize<CrdtMetadata>(jsonPayload, options);
```