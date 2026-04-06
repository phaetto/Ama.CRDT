# Ama.CRDT.Partitioning.Streams

A .NET library for implementing Conflict-free Replicated Data Types (CRDTs) for larger-than-memory streams using B+ Trees.
This package is an extension to the `Ama.CRDT` core library, decoupling the stream-based partitioning logic into its own reusable module.

## Features

- **B+ Tree Indexing**: Efficiently indexes CRDT partitions to scale documents beyond system memory.
- **Stream-Based Storage**: Supports persisting partitions using any stream provider (e.g., File streams, Azure Blob Storage, AWS S3).
- **Space Allocation**: Includes an internal mechanism to reuse freed blocks and manage in-place stream updates.
- **Polymorphic Serialization**: Leverages robust serialization mechanisms for dynamic CRDT payloads out of the box.

## Installation

Install via NuGet:

```bash
dotnet add package Ama.CRDT.Partitioning.Streams
```

## Getting Started

To use stream-based partitioning, you need to implement the `IPartitionStreamProvider` interface to define where your data and index streams are stored. 

### 1. Implement a Stream Provider

```csharp
using Ama.CRDT.Services.Partitioning.Streams;
using System;
using System.IO;
using System.Threading.Tasks;

public class FileSystemStreamProvider : IPartitionStreamProvider
{
    public Task<Stream> GetPropertyIndexStreamAsync(string propertyName)
    {
        // Implement logic to return your stream (e.g., new FileStream(...))
        throw new NotImplementedException();
    }

    public Task<Stream> GetPropertyDataStreamAsync(IComparable logicalKey, string propertyName)
    {
        // Implement logic to return your stream
        throw new NotImplementedException();
    }

    public Task<Stream> GetHeaderIndexStreamAsync()
    {
        // Implement logic to return your stream
        throw new NotImplementedException();
    }

    public Task<Stream> GetHeaderDataStreamAsync(IComparable logicalKey)
    {
        // Implement logic to return your stream
        throw new NotImplementedException();
    }
}
```

### 2. Configure Dependency Injection

Register the stream partitioning services in your DI container using the provided extension method. This should be chained after the core `.AddCrdt()` registration.

```csharp
using Ama.CRDT.Extensions; // Includes the AddCrdtStreamPartitioning extension
using Ama.CRDT.Models;
using Ama.CRDT.Services.Decorators;

var builder = WebApplication.CreateBuilder(args);

// Add core CRDT services and register the partitioning decorator
builder.Services.AddCrdt()
    .AddCrdtApplicatorDecorator<PartitioningApplicatorDecorator>(DecoratorBehavior.Complex)
    // Add the stream partitioning services with your custom provider
    .AddCrdtStreamPartitioning<FileSystemStreamProvider>();

var app = builder.Build();
```

### 3. Usage

Once registered, the `IPartitionManager<T>` (from the core `Ama.CRDT` library) will automatically resolve the `IPartitionStorageService` provided by this package, allowing you to transparently manage large partitioned documents.

### 4. Performance Counters

To see the performance counters when debugging you can use one of the following in a command prompt:
```bash
dotnet-counters monitor --name Ama.CRDT.ShowCase.LargerThanMemory --counters "Ama.CRDT.BPlusTree" --maxHistograms 30
```

Or use powershell if you prefer:
```
$p = Get-Process -Name "Ama.CRDT.ShowCase.LargerThanMemory" -ErrorAction SilentlyContinue; if ($p) { dotnet-counters monitor --process-id $p[0].Id --counters "Ama.CRDT.Partitioning.Streams" --maxHistograms 30 } else { Write-Warning "Process not found" }
```

## License
The code is licensed under MIT.