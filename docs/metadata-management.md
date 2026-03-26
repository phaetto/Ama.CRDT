# Managing Metadata Size

The `CrdtMetadata` object stores the necessary state to resolve conflicts and ensure eventual consistency. Over time, this metadata can grow. The library provides tools to help you keep it compact.

## Compacting Tombstones and Exceptions

When you remove properties, overwrite values, or remove elements from sets/maps, their timestamps or unique tags are kept as "tombstones" to prevent older updates from incorrectly re-introducing them. Similarly, network partitions may cause replicas to buffer out-of-order logs (`SeenExceptions`).

You can periodically clean these safely using the `ICrdtMetadataManager` and an `ICompactionPolicy`:

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
// Example A: Time-To-Live (TTL) threshold (e.g., compact everything older than 30 days)
long thirtyDaysInMillis = 30L * 24 * 60 * 60 * 1000;
var nowTimestamp = (EpochTimestamp)timestampProvider.Now();
var thresholdTimestamp = new EpochTimestamp(nowTimestamp.Value - thirtyDaysInMillis);
var ttlPolicy = new ThresholdCompactionPolicy(thresholdTimestamp);

// Example B: Global Minimum Version Vector (GMVV) policy
// Used when replicas share their version vectors, allowing mathematically safe compaction
// var clusterGmvv = new Dictionary<string, long> { { "replica-A", 100 }, { "replica-B", 95 } };
// var gmvvPolicy = new GlobalMinimumVersionPolicy(clusterGmvv);

// 4. Compact the document and its metadata
metadataManager.Compact(myDocument, ttlPolicy);
```

The `Compact` method will recursively traverse your document, delegating to the corresponding strategies (like LWW, OR-Set, Maps, etc.) to evaluate and safely remove tombstones based on the provided policy. It will also prune old out-of-order operations (`SeenExceptions`) that meet the criteria.

## Version Vector Compaction

The library uses a version vector (`CrdtMetadata.VersionVector`) and a set of seen exceptions (`SeenExceptions`) to provide idempotency. This tracking is managed automatically by the `CrdtApplicator` for all strategies, advancing correctly when causal order is established.

## Serializing Metadata Efficiently

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