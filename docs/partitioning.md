# Larger-Than-Memory Virtual Collections

For very large documents, especially those containing massive collections, loading the entire object into memory for every operation can be inefficient or impossible. The Larger-Than-Memory feature allows you to externalize these collections using two storage paradigms:

1. **Chunked Storage (`IChunkDocumentManager<T>`)**: Stores the document across physical streams or files. Collections are split into disjoint chunks, balancing memory footprint against write-amplification.
2. **True Key-Value Storage (`IKvDocumentManager<T>`)**: Maps individual virtual collection items directly to individual database rows, completely removing write-amplification for massive collections.

You need to select the medium that is used for larger than memory: [See Ama.CRDT.Partitioning.Streams for Streams implementation](../Ama.CRDT.Partitioning.Streams/README.md)

## Setup

1.  **Define a Partition Key**: Your root CRDT model must be decorated with the `[PartitionKey]` attribute, specifying which property acts as the logical identifier for the document (e.g., a tenant ID, a document ID).
2.  **Use a Virtual Collection Strategy**: One or more properties in your model must use a CRDT strategy that supports virtualization (i.e., implements `IVirtualCollectionStrategy` or `IChunkableCollectionStrategy`).
3.  **Register AOT Contexts**: Because of complex generic resolutions in partitions, you must define the type within your `CrdtAotContext` and `JsonSerializerContext`.

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

    // This large dictionary is the target for virtualization.
    // Operations on this dictionary will only load the relevant data chunks or KV rows.
    [CrdtOrMapStrategy]
    public Dictionary<string, UserProfile> UserProfiles { get; set; } = new();
}

public class UserProfile 
{
    public string Name { get; set; }
}
```

**AOT Contexts:**
```csharp
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Attributes;
using System.Text.Json.Serialization;

[CrdtAotType(typeof(LargeTenantData))]
[CrdtAotType(typeof(UserProfile))]
[CrdtAotType(typeof(Dictionary<string, UserProfile>))]
public partial class AppCrdtContext : CrdtAotContext { }

[JsonSerializable(typeof(LargeTenantData))]
[JsonSerializable(typeof(UserProfile))]
[JsonSerializable(typeof(Dictionary<string, UserProfile>))]
public partial class AppJsonContext : JsonSerializerContext { }
```

## Dependency Injection Setup

You must register the `LargerThanMemoryApplicatorDecorator`, choose your document manager (Chunked or KV), and optionally bind a CQRS projector.

```csharp
builder.Services.AddCrdt()
    // 1. Add the complex decorator that intercepts applicator calls
    .AddCrdtApplicatorDecorator<LargerThanMemoryApplicatorDecorator>(DecoratorBehavior.Complex)
    
    // 2. Select a storage engine (e.g., Stream Partitioning)
    .AddCrdtStreamPartitioning<FileSystemPartitionStreamProvider>()
    
    // 3. Register the Document Manager for your specific model
    .AddCrdtChunkedDocument<LargeTenantData>()
    
    // 4. (Optional) Hook a CQRS projector for ultra-fast Read Models!
    .AddCrdtVirtualDocumentProjector<LargeTenantData, TenantSqliteProjector>();
```

## Usage

> **⚠️ Important Note on Reading Data:** The proposed and most efficient way to perform reads—especially for user interfaces, pagination, or complex queries—is to use **CQRS Projections** (explained below) to maintain a local, optimized database purely for reads. The `IVirtualDocumentCollectionReader<T>` interface should **only** be used for background processes that do not care about order or querying power.

Instead of loading the entire document into memory, you interact with `IVirtualDocumentCollectionReader<T>` (for reads) and `IChunkDocumentManager<T>` / `IKvDocumentManager<T>` (for initialization and maintenance). Patch application is seamlessly handled by the `LargerThanMemoryApplicatorDecorator`, which dynamically streams the necessary data automatically.

```csharp
using Ama.CRDT.Services;
using Ama.CRDT.Services.LargerThanMemory;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;

// 1. Get the required services from a replica scope
var manager = scope.ServiceProvider.GetRequiredService<IChunkDocumentManager<LargeTenantData>>();
var reader = scope.ServiceProvider.GetRequiredService<IVirtualDocumentCollectionReader<LargeTenantData>>();
var applicator = scope.ServiceProvider.GetRequiredService<IAsyncCrdtApplicator>();
var patcher = scope.ServiceProvider.GetRequiredService<ICrdtPatcher>();

// 2. Initialize the virtualized document
var initialData = new LargeTenantData { TenantId = "tenant-123", TenantName = "Big Corp" };
initialData.UserProfiles.Add("user-a", new UserProfile { Name = "Alice" });
await manager.InitializeAsync(initialData);

// 3. Load the target document header ONLY
// This keeps the memory footprint tiny by ignoring the massive UserProfiles collection.
var targetDocument = await reader.GetDocumentHeaderAsync("tenant-123");

if (targetDocument != null)
{
    // 4. Generate an operation using explicit intents
    var operation = patcher.GenerateOperation(
        targetDocument.Value, 
        doc => doc.UserProfiles, 
        new MapSetIntent("user-b", new UserProfile { Name = "Bob" })
    );
    
    var patch = new CrdtPatch([operation]);

    // 5. Apply the patch
    // The LargerThanMemoryApplicatorDecorator will use the logical key and the key within the operations
    // to find and load ONLY the necessary chunks/rows from storage, apply the changes, and persist them back.
    await applicator.ApplyPatchAsync(targetDocument.Value, patch);
}
```

## Background Processing

While you should use projections for UI and queries, sometimes you need to process the entire dataset for tasks like backups, migrations, data exports, or batch analytics. The `IVirtualDocumentCollectionReader<T>` is perfect for these background processes because it streams data efficiently without loading massive virtual collections entirely into memory.

```csharp
using Ama.CRDT.Services.LargerThanMemory;
using System.Collections.Generic;

var reader = scope.ServiceProvider.GetRequiredService<IVirtualDocumentCollectionReader<LargeTenantData>>();

// 1. Count elements efficiently
long totalUsers = await reader.GetElementCountAsync("tenant-123", nameof(LargeTenantData.UserProfiles));
Console.WriteLine($"Total users to process: {totalUsers}");

// 2. Stream elements one by one without blowing up memory.
// Note: The order of elements is not guaranteed and querying is not supported.
await foreach (var kvp in reader.GetElementsAsync<KeyValuePair<string, UserProfile>>("tenant-123", nameof(LargeTenantData.UserProfiles)))
{
    Console.WriteLine($"Processing user {kvp.Key}: {kvp.Value.Name}");
}
```

## CQRS Projections (Read Models)

When dealing with massive CRDT documents, querying the event stream, iterating chunks, or using `IVirtualDocumentCollectionReader` is typically too slow and limited for a UI. Ama.CRDT solves this by providing `IVirtualDocumentProjector<T>`, which is the **recommended approach for all application reads**.

By implementing this interface, your application is notified natively the instant a CRDT patch successfully converges into standard POCO data. You can pipe this data directly into an SQLite database, Elasticsearch, or Redis, giving your users real-time, lightning-fast queries entirely separate from the CRDT write logic.

```csharp
public class TenantSqliteProjector : VirtualDocumentProjector<LargeTenantData>
{
    // Invoked automatically when the header is modified
    public override async Task ProjectHeaderAsync(IComparable logicalKey, LargeTenantData header, CancellationToken cancellationToken = default)
    {
        // Execute SQL UPDATE ...
    }

    // Invoked automatically when KV items are upserted or deleted
    public override async Task ProjectItemUpsertAsync(IComparable logicalKey, string propertyName, IComparable itemKey, object item, CancellationToken cancellationToken = default)
    {
        if (item is UserProfile profile)
        {
            // Execute SQL INSERT OR REPLACE ...
        }
    }
}
```