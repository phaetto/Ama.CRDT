# Ama.CRDT.ShowCase.LargerThanMemory

This project is a console-based showcase for the **Ama.CRDT** library, specifically designed to demonstrate advanced features such as **Larger-Than-Memory Partitioning**, **Operation Journaling**, and **Disconnected Replica Synchronization**.

## Overview

In a typical CRDT application, entire documents are loaded into memory to apply updates. However, for massive documents (e.g., a blog post with hundreds of thousands of comments), this is inefficient or impossible. 

This showcase simulates a distributed blogging platform where:
- **Documents are Partitioned**: Blog posts are split into a "Header" partition and multiple "Property" partitions (like `Comments` and `Tags`). Only the header and the specific partition chunks you are viewing are loaded into memory.
- **Operations are Journaled**: Every change (patch) made locally is saved to a local file system journal.
- **Disconnected Syncing**: Multiple independent replicas (simulated as separate folders on disk) can operate entirely offline. They can be manually synced by exchanging version vectors and missing journal operations to achieve perfect convergence.

## Key Features Demonstrated

1. **`IPartitionManager` and Stream Partitioning**:
   - Uses `FileSystemPartitionStreamProvider` to persist CRDT state directly to local `.dat` and `.bin` files.
   - Demonstrates "On-Demand" data loading. Comments and Tags are loaded chunk-by-chunk in the UI rather than all at once.

2. **Operation Journaling (`ICrdtOperationJournal`)**:
   - Uses decorators (`JournalingApplicatorDecorator` and `JournalingPatcherDecorator`) to transparently capture explicit intents and generated operations and save them to `FileSystemOperationJournal`.

3. **Causal Synchronization (`IVersionVectorSyncService`)**:
   - Tracks causality using `DottedVersionVector`.
   - Computes `ReplicaSyncRequirement` to precisely fetch only the missing operations from another replica's journal.

4. **Terminal User Interface (`Terminal.Gui`)**:
   - An interactive console UI to view partitioned data, create operations, and manually trigger sync protocols between replicas.

## How to Run

Navigate to the project directory and run:

```bash
dotnet run -c Release
```

### First Run Experience
1. **Data Generation**: On the very first run, the system will generate 10 blog posts, each containing between 500 and 1,000 comments using Bogus. This illustrates heavy data generation writing directly to partitions.
2. **Replica Bootstrapping**: It creates 3 distinct replicas (`replica-1`, `replica-2`, `replica-3`) by copying the initial generated folder to simulate a starting point for 3 offline devices.
3. **UI Launch**: The Terminal.Gui interface will open.

### Using the UI
- **Navigation**: Use the mouse or keyboard (`Tab`, `Arrow Keys`, `Enter`) to navigate the UI.
- **Switch Replicas**: Use the top menu `Replica` -> `View replica-X` to switch your current local context.
- **Load More Data**: Select a blog post. By default, only the header is loaded. Press **`F2`** to load the next partition chunk of Tags and Comments from the disk.
- **Make Changes**: Use `Actions` -> `Add Comment` or `Add Tag` to mutate the document on the current replica.
- **Sync**: Notice the "Sync Status" at the bottom left. If you switch replicas, they will be out of sync. Use `Actions` -> `Sync Replicas` (or **`F5`**) to perform a peer-to-peer sync of missing operations and watch the CRDTs converge flawlessly!

## Project Structure

- **Models/**: Contains the CRDT data shapes (`BlogPost`, `Comment`) decorated with strategy and partitioning attributes.
- **Services/**: Contains the implementations for file system streams (`FileSystemPartitionStreamProvider`), journaling (`FileSystemOperationJournal`), and data generation.
- **Program.cs**: Wires up the DI container, registering the CRDT library, journaling decorators, and partitioning decorators.
- **SimulationRunner.cs**: Orchestrates the initial boot, data generation, and UI launch.
- **UiService.cs**: The comprehensive terminal interface logic handling the interactions and manual sync steps.