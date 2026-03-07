# Managing Metadata Size

The `CrdtMetadata` object stores the necessary state to resolve conflicts and ensure eventual consistency. Over time, this metadata can grow. The library provides tools to help you keep it compact.

## Pruning Tombstones and Exceptions

When you remove properties, overwrite values, or remove elements from sets/maps, their timestamps or unique tags are kept as "tombstones" to prevent older updates from incorrectly re-introducing them. Similarly, network partitions may cause replicas to buffer out-of-order logs (`SeenExceptions`).

You can periodically prune these safely using the `ICrdtMetadataManager`:

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

// 4. Prune basic LWW property tombstones
metadataManager.PruneLwwTombstones(myMetadata, thresholdTimestamp);

// 5. Prune LWW-Set and Priority Queue tombstones
metadataManager.PruneLwwSetTombstones(myMetadata, thresholdTimestamp);

// 6. Prune OR-Set, OR-Map, and Replicated Tree tombstones
// (No threshold needed; safely removes elements where removes fully cover adds)
metadataManager.PruneOrSetTombstones(myMetadata);

// 7. Prune old out-of-order operations to prevent unbounded growth
metadataManager.PruneSeenExceptions(myMetadata, thresholdTimestamp);
```

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