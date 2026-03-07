# Ama.CRDT

A .NET library for achieving eventual consistency in distributed systems using Conflict-free Replicated Data Types (CRDTs). It provides a simple, high-level API to compare, patch, and merge POCOs (Plain Old C# Objects), with merge behavior controlled by attributes.

## Features

- **Attribute-Driven Strategies**: Define conflict resolution logic directly on your POCO properties using a rich set of attributes like `[CrdtLwwStrategy]`, `[CrdtCounterStrategy]`, `[CrdtOrSetStrategy]`, and `[CrdtStateMachineStrategy]`.
- **POCO-First**: Work directly with your C# objects. The library handles recursive diffing and patching seamlessly.
- **Explicit Intent Builder**: Create precise patches by declaring specific intents (e.g., Increment, Add, Move) instead of diffing entire document states.
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

## Quick Start

### 1. Setup

In your `Program.cs` or service configuration file, register the CRDT services.

```csharp
using Ama.CRDT.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add CRDT services to the DI container.
builder.Services.AddCrdt();

var app = builder.Build();
```

### 2. Define Your Model

Decorate your POCO properties with the desired CRDT strategy attributes.

```csharp
using Ama.CRDT.Attributes;

public class UserStats
{
    // LwwStrategy is the default. The value with the latest timestamp wins.
    [CrdtLwwStrategy]
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

The core workflow involves creating a patch from a change and applying it to another replica.

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

- [**CRDT Strategies Reference**](docs/strategies-reference.md) - A full list of supported CRDT strategies like LWW, Sets, Counters, Graphs, and Maps.
- [**Explicit Intents Builder**](docs/explicit-intents.md) - Learn how to build precise, strongly-typed operations directly instead of generating diffs.
- [**Multi-Replica Synchronization & Serialization**](docs/multi-replica-and-serialization.md) - Learn how to set up multi-node environments and safely serialize patches over the wire.
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

## License
The code is licensed under MIT.