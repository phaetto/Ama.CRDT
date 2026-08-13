# Ama.CRDT.ShowCase.LargerThanMemory

This project is a console-based showcase for the **Ama.CRDT** library, specifically designed to demonstrate advanced features such as **Larger-Than-Memory Virtual Collections**, **CQRS Projections with SQLite**, **Operation Journaling**, and **Disconnected Replica Synchronization**.

## Overview

In a typical CRDT application, entire documents are loaded into memory to apply updates. However, for massive documents (e.g., a blog post with hundreds of thousands of comments), this is inefficient or impossible. 

This showcase simulates a distributed blogging platform where:
- **Documents are Virtualized (Chunked Storage)**: Blog posts are split into a "Header" document and multiple disjoint "Property" chunks (like `Comments` and `Tags`) that stream directly from the file system.
- **CQRS Projections (SQLite Read Models)**: Real-time UI reads are powered by an embedded SQLite database. The CRDT library automatically pushes converged updates to `IVirtualDocumentProjector<T>`, proving that you can separate your heavy CRDT writes from your lightning-fast UI reads.
- **Operations are Journaled**: Every change (patch) made locally is saved to a local file system journal.
- **Disconnected Syncing**: Multiple independent replicas (simulated as separate folders on disk) can operate entirely offline. They can be manually synced by exchanging version vectors and missing journal operations to achieve perfect convergence.

## Key Features Demonstrated

1. **`IChunkDocumentManager<T>` and Stream Partitioning**:
   - Uses `FileSystemPartitionStreamProvider` to persist CRDT state directly to local `.dat` and `.bin` files.
   - Leverages `IVirtualDocumentCollectionReader<T>` to fetch only the document header in memory when mutating properties.

2. **CQRS with `IVirtualDocumentProjector<T>`**:
   - Implements `BlogPostSqliteProjector` to intercept CRDT state merges securely and persist POCO equivalents to a read-optimized relational SQLite layout (`BlogPostReadRepository`).

3. **Operation Journaling (`ICrdtOperationJournal`)**:
   - Uses decorators (`JournalingApplicatorDecorator` and `JournalingPatcherDecorator`) to transparently capture explicit intents and generated operations and save them to `FileSystemOperationJournal`.

4. **Causal Synchronization (`IVersionVectorSyncService`)**:
   - Tracks causality using `DottedVersionVector`.
   - Computes `ReplicaSyncRequirement` to precisely fetch only the missing operations from another replica's journal.

5. **Terminal User Interface (`Terminal.Gui`)**:
   - An interactive console UI to view data, create operations, and manually trigger sync protocols between replicas.

## How to Run

Navigate to the project directory and run:

```bash
dotnet run -c Release
```

### First Run Experience
1. **Data Generation**: On the very first run, the system will generate 10 blog posts, each containing between 500 and 1,000 comments using `SimpleFaker` (a lightweight, AOT-safe random data generator).
2. **Replica Bootstrapping**: It creates 3 distinct replicas (`replica-1`, `replica-2`, `replica-3`) by copying the initial generated folder to simulate a starting point for 3 offline devices.
3. **UI Launch**: The Terminal.Gui interface will open.

### Using the UI
- **Navigation**: Use the mouse or keyboard (`Tab`, `Arrow Keys`, `Enter`) to navigate the UI.
- **Switch Replicas**: Use the top menu `Replica` -> `View replica-X` to switch your current local context.
- **Fast Pagination**: Select a blog post. By default, the UI immediately queries the **SQLite Read Repository** for instant paginated rendering of Tags and Comments. Press **`F2`** to seamlessly query the next page.
- **Make Changes**: Use `Actions` -> `Add Comment` or `Add Tag` to mutate the document. These generate CRDT operations applied safely to the disk-based chunk files, which inherently bounce back into the SQLite Read Model via the projector.
- **Sync**: Notice the "Sync Status" at the bottom left. If you switch replicas, they will be out of sync. Use `Actions` -> `Sync Replicas` (or **`F5`**) to perform a peer-to-peer sync of missing operations and watch the CRDTs (and SQLite views) converge flawlessly!

## Project Structure

- **Models/**: Contains the CRDT data shapes (`BlogPost`, `Comment`) decorated with `[PartitionKey]` and `[CrdtArrayLcsStrategy]`.
- **Services/**: Contains the implementations for file system streams (`FileSystemPartitionStreamProvider`), SQLite projectors (`BlogPostSqliteProjector`, `BlogPostReadRepository`), and data generation (`SimpleFaker`).
- **Program.cs**: Wires up the DI container, registering the CRDT library, journaling decorators, and the `LargerThanMemoryApplicatorDecorator`.
- **SimulationRunner.cs**: Orchestrates the initial boot, data generation, and UI launch.
- **UiService.cs**: The comprehensive terminal interface logic, acting as the Presentation layer reading strictly from SQLite and writing via Explicit Intents.