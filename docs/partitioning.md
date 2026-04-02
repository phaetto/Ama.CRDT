# Partitioning (Larger-Than-Memory Data)

For very large documents, especially those containing massive collections, loading the entire object into memory for every operation can be inefficient or impossible. The partitioning feature allows you to store the document across one or more streams (e.g., files on disk), loading only the relevant parts when applying a patch. This is ideal for scenarios like managing multi-tenant data or huge dictionaries where operations typically only affect a small subset of the data.

You need to select the medium that is used for larger than memory: [See Ama.CRDT.Partitioning.Streams for Streams implementation](../Ama.CRDT.Partitioning.Streams/README.md)

## Setup

1.  **Define a Partition Key**: Your root CRDT model must be decorated with the `[PartitionKey]` attribute, specifying which property acts as the logical identifier for the document (e.g., a tenant ID, a document ID).
2.  **Use a Partitionable Strategy**: One or more properties in your model must use a CRDT strategy that supports partitioning (i.e., implements `IPartitionableCrdtStrategy`). Currently, `[CrdtOrMapStrategy]` and `[CrdtArrayLcsStrategy]` support this.

**Example Model:**
```csharp
using Ama.CRDT.Attributes;
using System.Collections.Generic;

[PartitionKey(nameof(TenantId))]
public class LargeTenantData
{
    // This property will be used to logically partition the data.
    public string TenantId { get; set; }

    // Other header-like data can exist here. It will be stored
    // in a separate "header" partition.
    public string TenantName { get; set; }

    // This large dictionary is the target for partitioning.
    // Operations on this dictionary will only load the relevant data partition.
    [CrdtOrMapStrategy]
    public Dictionary<string, UserProfile> UserProfiles { get; set; } = new();
}

public class UserProfile 
{
    public string Name { get; set; }
}
```

## Usage

Instead of loading the entire document into memory, you interact with `IPartitionManager<T>` to manage initialization and querying. Patch application is seamlessly handled by the standard `IAsyncCrdtApplicator`, which intercepts the call via decorators and streams the necessary partitions automatically.

```csharp
using Ama.CRDT.Services;
using Ama.CRDT.Services.Partitioning;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
// ... other usings

// 1. Get the required services from a replica scope
// var scope = ...;
var partitionManager = scope.ServiceProvider.GetRequiredService<IPartitionManager<LargeTenantData>>();
var applicator = scope.ServiceProvider.GetRequiredService<IAsyncCrdtApplicator>();
var patcher = scope.ServiceProvider.GetRequiredService<ICrdtPatcher>();

// 2. Initialize the partitioned document
// The storage service (configured via DI) handles writing the data and indexes to the persistence medium.
var initialData = new LargeTenantData { TenantId = "tenant-123", TenantName = "Big Corp" };
initialData.UserProfiles.Add("user-a", new UserProfile { Name = "Alice" });
await partitionManager.InitializeAsync(initialData);

// 3. Load the target document header
// The PartitioningApplicatorDecorator will use the logical key on this header 
// document to correctly route the operations.
var targetDocument = await partitionManager.GetHeaderPartitionContentAsync("tenant-123");

if (targetDocument != null)
{
    // 4. Generate an operation using explicit intents
    // The patcher automatically translates the intent into the correct CRDT payloads (e.g., OrMapAddItem)
    var operation = patcher.GenerateOperation(
        targetDocument, 
        doc => doc.UserProfiles, 
        new MapSetIntent("user-b", new UserProfile { Name = "Bob" })
    );
    
    var patch = new CrdtPatch([operation]);

    // 5. Apply the patch
    // The decorator will use the logical key and the key within the operations
    // to find and load only the necessary partitions from the configured storage service,
    // apply the changes, and persist them back. It also handles splitting or merging partitions automatically.
    await applicator.ApplyPatchAsync(targetDocument, patch);
}

// The storage medium now contains the updated, partitioned data.
```