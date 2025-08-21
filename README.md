# Modern.CRDT

A .NET library for achieving eventual consistency in distributed systems using Conflict-free Replicated Data Types (CRDTs) on JSON documents. It provides a simple, high-level API to compare, patch, and merge JSON states, with Last-Writer-Wins (LWW) as the primary conflict resolution strategy.

## Features

- **JSON Diffing & Patching**: Compares two JSON documents and generates a compact `CrdtPatch` containing the differences.
- **LWW Conflict Resolution**: Automatically resolves conflicts during patch generation and application using timestamps. Last write wins.
- **Clean Data/Metadata Separation**: Keeps your data documents pure by storing LWW timestamps and other metadata in a parallel structure. Your business logic sees clean JSON, while the library manages replication state.
- **POCO Support**: Work directly with your C# objects. The library handles serialization and deserialization seamlessly.
- **Dependency Injection Friendly**: All services are designed to be registered and resolved through a standard DI container with a simple `AddJsonCrdt()` extension method.

## Getting Started

<!---
(Maybe if I publish it)

### Installation

This library is intended to be used as a NuGet package. To add it to your project:

```bash
dotnet add package Modern.CRDT
```
--->

### Setup

In your `Program.cs` or service configuration file, register the CRDT services:

```csharp
using Modern.CRDT.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add CRDT services to the DI container
builder.Services.AddJsonCrdt();

// ... other service registrations

var app = builder.Build();
```

## Usage

The primary entry point is the `IJsonCrdtService`. It orchestrates the underlying patching and merging logic. The library operates on a `CrdtDocument<T>` which encapsulates your data and its associated metadata.

### Core Concept: `CrdtDocument<T>`

A `CrdtDocument<T>` holds two key pieces of information:
- `Data`: Your POCO (e.g., `MyBusinessObject`).
- `Metadata`: A `JsonObject` that mirrors the structure of your data but contains LWW timestamps for each value.

You are responsible for generating and storing this metadata alongside your data.

### End-to-End Example

Let's simulate two replicas of an object that are modified concurrently and then synchronized.

```csharp
using Modern.CRDT.Models;
using Modern.CRDT.Services;
using System.Text.Json.Nodes;

// 1. Inject the service
// IJsonCrdtService jsonCrdtService = ...;

// 2. Define your model
public class UserProfile
{
    public string? Name { get; set; }
    public int Age { get; set; }
    public List<string>? Tags { get; set; }
}

// 3. Establish a base state
var baseUser = new UserProfile { Name = "Alex", Age = 30, Tags = new List<string> { "dev" } };
var baseMeta = JsonNode.Parse("""
{
    "Name": 100,
    "Age": 100,
    "Tags": [ 100 ]
}
""")!.AsObject();
var baseDocument = new CrdtDocument<UserProfile>(baseUser, baseMeta);


// 4. Create two replicas from the base state
var replicaA = baseDocument with { }; // Create a copy
var replicaB = baseDocument with { };


// 5. Modify both replicas independently with different timestamps
// Replica A: User changes their name (newer timestamp)
var userA = new UserProfile { Name = "Alexander", Age = 30, Tags = new List<string> { "dev" } };
var metaA = JsonNode.Parse("""
{
    "Name": 200, // <-- Newer
    "Age": 100,
    "Tags": [ 100 ]
}
""")!.AsObject();
replicaA = new CrdtDocument<UserProfile>(userA, metaA);


// Replica B: User changes their age and adds a tag (also newer timestamps)
var userB = new UserProfile { Name = "Alex", Age = 31, Tags = new List<string> { "dev", "c#" } };
var metaB = JsonNode.Parse("""
{
    "Name": 100,
    "Age": 300,  // <-- Newer
    "Tags": [ 100, 400 ] // <-- New tag is newest
}
""")!.AsObject();
replicaB = new CrdtDocument<UserProfile>(userB, metaB);


// 6. Synchronize the replicas
// Generate a patch from A's changes and apply it to B
var patchAtoB = jsonCrdtService.CreatePatch(baseDocument, replicaA);
var mergedB = jsonCrdtService.Merge(replicaB, patchAtoB);

// Generate a patch from B's changes and apply it to A
var patchBtoA = jsonCrdtService.CreatePatch(baseDocument, replicaB);
var mergedA = jsonCrdtService.Merge(replicaA, patchBtoA);


// 7. Assert Convergence
// Both replicas now have the same state, with all changes merged based on LWW.
Console.WriteLine(JsonSerializer.Serialize(mergedA.Data));
// Output: {"Name":"Alexander","Age":31,"Tags":["dev","c#"]}

Console.WriteLine(JsonSerializer.Serialize(mergedB.Data));
// Output: {"Name":"Alexander","Age":31,"Tags":["dev","c#"]}

// The metadata is also merged correctly
Console.WriteLine(mergedA.Metadata.ToJsonString());
// Output: {"Name":200,"Age":300,"Tags":[100,400]}
```

## How It Works

The library consists of three core services:

-   `IJsonCrdtPatcher`: This service takes two `CrdtDocument` objects (`from` and `to`) and recursively compares them. It generates a `CrdtPatch` containing `Upsert` or `Remove` operations for any differences found, but only if the timestamp in the `to` document's metadata is greater than the `from` document's metadata (Last-Writer-Wins).
-   `IJsonCrdtApplicator`: This service takes a `CrdtDocument` and a `CrdtPatch`. It applies the operations from the patch to the document. Before applying an operation, it checks if the operation's timestamp is newer than the timestamp in the document's existing metadata, again enforcing the LWW rule. It also handles creating nested JSON structures if the path doesn't exist.
-   `IJsonCrdtService`: This is the high-level facade that orchestrates the patcher and applicator, providing a simple, unified API for end-users.

By separating data from metadata, the library ensures that your domain objects remain clean and unaware of the replication mechanism.

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