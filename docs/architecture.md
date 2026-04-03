# Architecture & How It Works

Ama.CRDT is built on a few core abstractions that separate data from metadata, cleanly handle distributed state synchronization, and run completely reflection-free for Native AOT compatibility.

## Core Components

- **`CrdtAotContext` (AOT Reflection)**: Source-generated contexts that completely replace runtime reflection. The library uses these generated structs to read properties, instantiate types, and resolve CRDT strategies at compile-time, making it incredibly fast and Native AOT compatible.
- **`ICrdtScopeFactory`**: A singleton factory for creating isolated `IServiceScope` instances. Each scope is configured for a specific `ReplicaId` and provides its own set of scoped services. This is the primary entry point for working with multiple replicas.
- **`ICrdtPatcher`**: A scoped service that generates a `CrdtPatch` by comparing two versions of a document. It is a stateless service that only reads the `from` document's metadata to understand the state before the change. It uses the `ICrdtStrategyProvider` to find the correct strategy for each property. It also exposes `.BuildOperation` for explicit intent creation.
- **`ICrdtApplicator`**: A scoped service that applies a `CrdtPatch` to a document. It uses the `ICrdtStrategyProvider` to find the correct strategy to modify the POCO and its metadata. It also handles idempotency checks using version vectors for supported strategies.
- **`ICrdtStrategyProvider`**: A service that maps a property's AOT metadata to resolve the appropriate `ICrdtStrategy` from the DI container. It provides default strategies (LWW for simple types, ArrayLcs for collections) if no explicit configuration is present.
- **`ICrdtMetadataManager`**: A scoped helper service for managing the `CrdtMetadata` object. It can initialize metadata from a POCO, compact it to save space, and perform other state management tasks.
- **`IPartitionManager<T>`**: Manages partitioned CRDT documents, translating operations into specific partition updates.
- **`IPartitionStorageService`**: An abstraction layer used to save and load strongly-typed partitions without dealing with underlying streams, files, or pointers directly. It orchestrates storage providers and serialization.

## Composable Architecture & Deep Object Graphs

The library is designed with a **Composable Architecture**, meaning you can nest objects and collections as deeply as you need, and mix different CRDT strategies seamlessly within the same POCO. 

### Recursive Traversal and Leaf-Node Operations
When the `ICrdtPatcher` compares two versions of a document, it doesn't just look at the root properties. It recursively traverses the object graph using generated AOT accessors. If you change a deeply nested property, the patcher generates a highly targeted operation for that specific leaf node (e.g., `$.level1.level2.message`), rather than replacing the entire parent object. This minimizes conflict surface area.

### Auto-Instantiation on Application
When the `ICrdtApplicator` receives an operation for a deep JSON path, it automatically reconstructs any missing intermediate objects. For example, if a replica receives an update for `$.level1.level2.count = 99`, but its local `level1` or `level2` is currently `null`, the applicator will automatically instantiate the missing objects in the hierarchy to safely apply the value.

### Optimized Pruning
Conversely, if you set a large nested object to `null`, the patcher optimizes the diff. Instead of generating individual `Remove` operations for every nested leaf node, it generates a single `Remove` operation for the parent path, efficiently pruning the entire sub-tree.

### Strategy Composition Example
You can freely mix complex strategies on a single document. The patcher and applicator will delegate to the correct strategy based on the property's attribute or default type mapping. Intents also work natively across deeply nested paths.

```csharp
public class ComplexDocument
{
    // Uses LWW by default
    public string? Title { get; set; }

    // Resolves concurrent dictionary updates by keeping the lowest numeric value
    [CrdtMinWinsMapStrategy]
    public Dictionary<string, int> Metrics { get; set; } = new();

    // A high-precision ordered list for log entries
    [CrdtLseqStrategy]
    public List<string> Log { get; set; } = new();

    // Enforces valid state transitions (e.g., Draft -> Published)
    [CrdtStateMachineStrategy(typeof(DocStatusValidator))]
    public DocStatus Status { get; set; } = DocStatus.Draft;

    // A concurrent add-only graph
    [CrdtGraphStrategy]
    public CrdtGraph Network { get; set; } = new();

    // Nested objects are traversed and their properties resolved automatically
    public NestedConfig? Config { get; set; }
}

public class NestedConfig
{
    public string? SettingA { get; set; }
    
    // Mix and match strategies at any depth
    [CrdtLseqStrategy]
    public List<string> SubLog { get; set; } = new();
}
```

## Strategy Decorators

In addition to base strategies, Ama.CRDT supports **Decorators**. Decorators allow you to stack cross-cutting concerns or complex distributed rules on top of your core CRDT strategies. You can chain multiple decorator attributes on a single property.

### Example: Chaining Decorators

```csharp
public class ChainedDocument
{
    [CrdtEpochBound]
    [CrdtApprovalQuorum(2)]
    [CrdtLwwStrategy]
    public string Value { get; set; } = string.Empty;
}
```

### How Decorators Work

Decorators operate by wrapping the operation payloads in a deterministic order (sorted alphabetically by the decorator's attribute name).

1. **Patch Generation (Nesting Payloads)**: When the `ICrdtPatcher` creates an operation for a decorated property, the decorators take turns wrapping the inner payload. In the example above, the raw `"New Value"` is wrapped inside an `EpochPayload`, which is then wrapped inside a `QuorumPayload`.
2. **Patch Application (Unwrapping and Gating)**: When the `ICrdtApplicator` receives the operation, the decorators evaluate the payload from the outside in.
   - The `ApprovalQuorumStrategy` intercepts the `QuorumPayload`. It tracks the proposal in metadata. The operation is **halted** until `2` distinct replicas have proposed the exact same operation.
   - Once the quorum is met, the `ApprovalQuorumStrategy` unwraps the payload and passes the inner `EpochPayload` to the next decorator.
   - The `EpochBoundStrategy` evaluates the epoch logic. If valid, it unwraps the final value and passes it to the base `LwwStrategy` to actually update the POCO.

This pipeline mechanism allows you to build highly sophisticated, conditional replication flows without altering the simple base strategies.