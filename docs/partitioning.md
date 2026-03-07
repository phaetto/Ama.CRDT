# Partitioning (Larger-Than-Memory Data)

For very large documents, especially those containing massive collections, loading the entire object into memory for every operation can be inefficient or impossible. The partitioning feature allows you to store the document across one or more streams (e.g., files on disk), loading only the relevant parts when applying a patch. This is ideal for scenarios like managing multi-tenant data or huge dictionaries where operations typically only affect a small subset of the data.

You need to select the medium that is used for larger than memory: [See Ama.CRDT.Partitioning.Streams for Streams implementation](../Ama.CRDT.Partitioning.Streams/README.md)

## Setup

1.  **Define a Partition Key**: Your root CRDT model must be decorated with the `[PartitionKey]` attribute, specifying which property acts as the logical identifier for the document (e.g., a tenant ID, a document ID).
2.  **Use a Partitionable Strategy**: Exactly one property in your model must use a CRDT strategy that supports partitioning (i.e., implements `IPartitionableCrdtStrategy`). Currently, `[CrdtOrMapStrategy]` and `[CrdtArrayLcsStrategy]` support this.

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

Instead of the standard `ICrdtPatcher`/`ICrdtApplicator` workflow, you interact with the `IPartitionManager<T>`.

```csharp
using Ama.CRDT.Services.Partitioning;
using Ama.CRDT.Models;
// ... other usings

// 1. Get the Partition Manager from a replica scope
// var scope = ...;
var partitionManager = scope.ServiceProvider.GetRequiredService<IPartitionManager<LargeTenantData>>();

// 2. Prepare streams for data and index storage (e.g., FileStream or MemoryStream)
using var dataStream = new MemoryStream();
using var indexStream = new MemoryStream();

// 3. Initialize the partitioned document
var initialData = new LargeTenantData { TenantId = "tenant-123", TenantName = "Big Corp" };
initialData.UserProfiles.Add("user-a", new UserProfile { Name = "Alice" });
await partitionManager.InitializeAsync(dataStream, indexStream, initialData);

// 4. Create a patch that targets the partitioned document
// This patch would be generated from another replica using the standard ICrdtPatcher.
var patch = new CrdtPatch(new List<CrdtOperation>
{
    // An operation to add a new user to the dictionary.
    // The key "user-b" will be used to find the right partition.
    new CrdtOperation(Guid.NewGuid(), "replica-B", "$.userProfiles", OperationType.Upsert, 
        new OrMapAddItem("user-b", new UserProfile { Name = "Bob" }, Guid.NewGuid()), 
        timestampProvider.Now())
})
{
    // The LogicalKey is crucial. It must match the value of the PartitionKey property.
    LogicalKey = "tenant-123"
};

// 5. Apply the patch
// The PartitionManager will use the logical key and the key within the operation
// to find and load only the necessary partition from the streams, apply the change,
// and persist it back. It also handles splitting or merging partitions automatically.
await partitionManager.ApplyPatchAsync(patch);

// The streams (dataStream, indexStream) now contain the updated, partitioned data.
```