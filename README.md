# Ama.CRDT

A .NET library for achieving eventual consistency in distributed systems using Conflict-free Replicated Data Types (CRDTs). It provides a simple, high-level API to compare, patch, and merge POCOs (Plain Old C# Objects), with merge behavior controlled by attributes or a fluent configuration API.

## Features

- **Native AOT Ready**: Zero-reflection architecture powered by C# Source Generators and `System.Text.Json` polymorphic resolvers, making the library incredibly fast, memory-efficient, and trim-safe.
- **Attribute & Fluent Strategies**: Define conflict resolution logic on your properties using attributes like `[CrdtLwwStrategy]`, `[CrdtOrSetStrategy]`, or use the **Fluent API** to keep your POCOs pure.
- **Strategy Decorators**: Stack complex distributed rules on top of base strategies. Chain decorators like `[CrdtEpochBound]` (for Clear-Wins/Reset semantics) and `[CrdtApprovalQuorum]` natively in the pipeline.
- **POCO-First & Composable**: Work directly with your C# objects. Mix and match strategies at any depth. The library handles recursive diffing, patching, and missing object auto-instantiation seamlessly.
- **Explicit Intent Builder**: Create precise patches by declaring specific intents (e.g., Increment, Add, Move) instead of diffing entire document states.
- **Larger-Than-Memory Partitioning**: Scale your collections beyond RAM. Use the bundled Stream-based B+Tree storage (`Ama.CRDT.Partitioning.Streams`) to automatically split, merge, and stream partitions on demand.
- **Advanced Synchronization & Journaling**: Built-in Dotted Version Vectors (DVV) and operation journaling (`ICrdtOperationJournal`) to track causal history, sync disconnected replicas, and request missing data accurately.
- **Automatic Garbage Collection**: Seamlessly compact tombstones and metadata using time-to-live (TTL) thresholds or mathematically safe Global Minimum Version Vectors (GMVV) natively within the DI pipeline via the `CompactingApplicatorDecorator`.
- **Clean Data/Metadata Separation**: Keeps your data models pure by storing CRDT state (timestamps, tombstones, version vectors) in a parallel, highly-compactible `CrdtMetadata` object.
- **Mathematically Proven**: Validated using generative property testing (FsCheck) to guarantee strict convergence, commutativity, and idempotence across all strategies.
- **Developer Experience**: Ships with built-in **Roslyn Analyzers** to catch configuration errors at compile-time, and integrates natively with `System.Diagnostics.Metrics` for robust observability.

## Installation

You can install Ama.CRDT via the .NET CLI or the NuGet Package Manager in Visual Studio.

### .NET CLI

```bash
dotnet add package Ama.CRDT
```

If you need the stream-based larger-than-memory partitioning, also install:
```bash
dotnet add package Ama.CRDT.Partitioning.Streams
```

### NuGet Package Manager

In Visual Studio, open the NuGet Package Manager Console and run:

```powershell
Install-Package Ama.CRDT
```

## Quick Start

### 1. Setup AOT Contexts & DI

To ensure Native AOT compatibility, define a `CrdtAotContext` and a `JsonSerializerContext` for your POCOs. Then register the CRDT services in your `Program.cs`.

```csharp
using Ama.CRDT.Extensions;
using Ama.CRDT.Models.Aot;
using System.Text.Json.Serialization;

// 1. Define an AOT reflection context for your models
[CrdtAotType(typeof(UserStats))]
public partial class MyCrdtAotContext : CrdtAotContext { }

// 2. Define an AOT JSON context for network serialization
[JsonSerializable(typeof(UserStats))]
[JsonSerializable(typeof(CrdtDocument<UserStats>))]
public partial class MyJsonContext : JsonSerializerContext { }

// 3. Register in DI
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCrdt(options => 
{
    // Optionally configure strategies fluently instead of using attributes
    options.Entity<UserStats>()
           .Property(x => x.LoginCount).HasStrategy<CounterStrategy>()
           .Property(x => x.Badges).HasStrategy<OrSetStrategy>();
})
.AddCrdtAotContext<MyCrdtAotContext>() // Register AOT reflection
.AddCrdtJsonTypeInfoResolver(MyJsonContext.Default); // Register AOT JSON

var app = builder.Build();
```

### 2. Define Your Model

Work with plain C# objects. If you didn't use the Fluent API above, you can decorate properties directly.

```csharp
using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Decorators;

public class UserStats
{
    // LwwStrategy is the default. The value with the latest timestamp wins.
    // The EpochBound decorator allows this property to be explicitly "reset" across replicas.
    [CrdtLwwStrategy]
    [CrdtEpochBound]
    public string LastSeenLocation { get; set; } = string.Empty;

    // Changes to this value are additive and safely mergeable.
    [CrdtCounterStrategy]
    public long LoginCount { get; set; }

    // Use OR-Set to allow badges to be concurrently added/removed and re-added.
    [CrdtOrSetStrategy]
    public List<string> Badges { get; set; } = [];
}
```

### 3. Basic Usage

The core workflow involves creating a patch from a change (or via explicit intents) and applying it to another replica.

```csharp
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Microsoft.Extensions.DependencyInjection;

// 1. Get the scope factory.
var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();

// 2. Create a scope for a specific replica (e.g., a user session)
using var userScope = scopeFactory.CreateScope("user-session-abc");
var patcher = userScope.ServiceProvider.GetRequiredService<ICrdtPatcher>();
var applicator = userScope.ServiceProvider.GetRequiredService<ICrdtApplicator>();
var metadataManager = userScope.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();

// 3. Establish an initial state
var originalState = new UserStats { LoginCount = 5, Badges = ["newcomer"] };
var originalMetadata = metadataManager.Initialize(originalState);
var originalDocument = new CrdtDocument<UserStats>(originalState, originalMetadata);

// 4. Modify the state locally
var modifiedState = new UserStats { LoginCount = 6, Badges = ["newcomer", "explorer"] };

// 5. Generate a patch to capture the differences
var patch = patcher.GeneratePatch(originalDocument, modifiedState);

// 6. On another replica, apply the patch.
using var serverScope = scopeFactory.CreateScope("server-node-xyz");
var serverApplicator = serverScope.ServiceProvider.GetRequiredService<ICrdtApplicator>();
var serverMetadataManager = serverScope.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();

// Initialize the server's version of the document
var serverState = new UserStats { LoginCount = 5, Badges = ["newcomer"] };
var serverMetadata = serverMetadataManager.Initialize(serverState);
var serverDocument = new CrdtDocument<UserStats>(serverState, serverMetadata);

// Apply the incoming patch safely
var applyResult = serverApplicator.ApplyPatch(serverDocument, patch);

// Result: serverDocument is now fully synchronized with the user's changes
```

## Documentation Index

Explore the detailed features of the library by checking out the advanced topics in the `/docs` folder:

- [**CRDT Strategies Reference**](docs/strategies-reference.md) - A full list of supported CRDT strategies like LWW, Counters, Sets, Maps, and Graphs.
- [**Explicit Intents Builder**](docs/explicit-intents.md) - Learn how to build precise, strongly-typed operations directly instead of generating diffs.
- [**Multi-Replica Synchronization & Serialization**](docs/multi-replica-and-serialization.md) - Learn how to set up multi-node environments and safely serialize patches over the wire for Native AOT.
- [**Operation Journaling**](docs/journaling.md) - Learn how to automatically record operations to an external datastore for advanced offline-first synchronization.
- [**Managing Metadata Size**](docs/metadata-management.md) - Strategies for compacting state and pruning tombstones efficiently.
- [**Larger-Than-Memory Partitioning**](docs/partitioning.md) - Handle massive datasets efficiently by breaking documents into on-demand streams.
- [**Extensibility & Customization**](docs/extensibility.md) - Build your own CRDT strategies, timestamps, and comparers.
- [**Architecture & How It Works**](docs/architecture.md) - A high-level overview of the library's internal abstractions.

## Building and Testing

To build the project, simply run:

```bash
dotnet build
```

To run the unit tests:

```bash
dotnet test
```

To see the performance counters when debugging you can use one of the following in a command prompt:
```bash
dotnet-counters monitor --name Ama.CRDT.ShowCase.LargerThanMemory --counters "Ama.CRDT.Partitioning" --maxHistograms 30
```

Or use powershell if you prefer:
```powershell
$p = Get-Process -Name "Ama.CRDT.ShowCase.LargerThanMemory" -ErrorAction SilentlyContinue; if ($p) { dotnet-counters monitor --process-id $p[0].Id --counters "Ama.CRDT.Partitioning" --maxHistograms 30 } else { Write-Warning "Process not found" }
```

## AI Coding Assistance

To maintain full transparency, please note that AI coding assistants and Large Language Models (LLMs) were actively used in the design, development, testing, and documentation of this repository. While AI tools significantly accelerated the generation of code and ideas, all output was rigorously reviewed, steered, tested, and refined by human developers (me). I believe in leveraging these tools to enhance productivity while taking complete responsibility for the library's architecture, security, and mathematical correctness.

## License
The code is licensed under MIT.