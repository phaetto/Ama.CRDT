<!---Human--->
# Purpose
<!---
Add the purpose of this user story.
--->
I want to make a showcase for larger than memory partitioning for an example blog service with a comment list that grows a lot.

<!---Human--->
# Requirements
<!---
Add the requirements, technical or not.
--->
 I want to make a showcase for larger than memory partitioning for an example blog service with a comment list that grows a lot.
- I want when it runs to generate data to a maximum value of GB set in a const
	- If there are existing data do not generate more than the amount set
- Multiple replicas, set in const
- Add a filesystem implementation of `IPartitionStreamProvider`
- The app will have UI to allow you to load and browse blog text and comments from the disk.
- Use `Terminal.Gui` to provide a simple multiwindow UI to go through blogs and scroll through comments

<!---Human--->
## Requirements context
<!---
Add files that we will load for the UI to add context for the solution design.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
--->
- `C:\sources\Ama.CRDT\Ama.CRDT\*.cs`
- `C:\sources\Ama.CRDT\Ama.CRDT.ShowCase.LargerThanMemory\Program.cs`

<!---Human--->
# Testing Methodology
<!---
Add the testing methodology (manual, unit, integration, end-to-end tests?)
--->
Manual testing will be the primary method for this showcase application. The process will involve:
1. Running the application and verifying that the data files are generated correctly up to the specified size limit.
2. Interacting with the `Terminal.Gui` interface to ensure that blog posts and comments can be browsed without issues.
3. Observing the file system to confirm that data is read from partitions on-demand (e.g., by checking file access times) rather than being loaded into memory all at once.
4. Simulating changes on one replica and verifying that they are propagated and correctly applied to other replicas, leading to a converged state that can be viewed in the UI.

<!---AI - Stage 1--->
# Proposed Solutions [AI - Stage 1]
<!---
Here you will need to put a number of solutions that would fit for this problem.
Add the solutions that you rejected as well.
--->
### Solution 1: Single-Process, Multi-Replica Simulation (Recommended)
- **Description:** A single console application will simulate multiple replicas. Each replica will be managed within its own DI scope and will store its partitioned data in a separate directory on the local filesystem using a new `FileSystemPartitionStreamProvider`. A central orchestrator within the application will manage data generation and simulate the exchange of patches between these in-memory replicas. The `Terminal.Gui` UI will allow the user to select and view the state of any replica's partitioned data, demonstrating how data is fetched on-demand from disk.
- **Reasoning for Recommendation:** This approach is the most direct and effective for a showcase. It minimizes external complexity (no networking or multi-process coordination) and keeps the focus squarely on the CRDT partitioning feature and its usage. It is easier to build, debug, and demonstrate the concept of convergence from a single control point.

### Solution 2: Multiple Processes with File-Based Communication
- **Description:** Each replica would be a separate instance of the console application. Communication and patch exchange would happen via a shared directory on the filesystem, where each instance would write its patches and watch for new patches from others. Each process would have its own UI displaying only its local state.
- **Reasoning for Rejection:** This solution is significantly more complex to implement and demonstrate. Managing multiple processes, handling file-based synchronization, and avoiding race conditions would introduce substantial overhead. Visually demonstrating convergence would be clumsy, requiring the user to manage and observe several console windows at once.

### Solution 3: Client-Server Architecture
- **Description:** This solution involves creating a lightweight ASP.NET Core Web API to act as the CRDT data store and a `Terminal.Gui` application as a client. The server would manage the partitioned data, and multiple clients would connect to it to simulate replicas, sending and receiving changes over HTTP.
- **Reasoning for Rejection:** This is overkill for the stated purpose. It introduces a significant amount of unrelated technology (HTTP, API design, serialization) that would distract from the core goal of showcasing the CRDT partitioning mechanism. The complexity of building and running a client-server application is not justified for this demonstration.

<!---AI - Stage 1--->
# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->
1.  **Project Setup:**
    *   Create a new Console Application project named `Ama.CRDT.ShowCase.LargerThanMemory`.
    *   Add a project reference from the new showcase project to the `Ama.CRDT` project.
    *   Add the `Terminal.Gui` NuGet package to the new project.

2.  **Model Definition (`$/Ama.CRDT.ShowCase.LargerThanMemory/Models`):**
    *   Create `Blog.cs`: This will be the root object containing a list of `BlogPost`s.
    *   Create `BlogPost.cs`: Will contain properties like `Id`, `Title`, `Content`, and a list of `Comment` objects.
    *   Create `Comment.cs`: Will contain properties like `Id`, `Author`, `Text`, and `Timestamp`. The `Id` or `Timestamp` will be used as the partition key.
    *   Decorate the `Comments` collection property in `BlogPost` with `[CrdtArrayLcsStrategy]` and `[PartitionKey(nameof(Comment.Id))]` to enable CRDT logic and partitioning.

3.  **File System Stream Provider (`$/Ama.CRDT.ShowCase.LargerThanMemory/Services`):**
    *   Create `FileSystemPartitionStreamProvider.cs` implementing the `IPartitionStreamProvider` interface.
    *   Its constructor will accept a base directory path for a specific replica (e.g., `./data/replica-1/`).
    *   It will implement methods to provide `FileStream` objects for the index file (`index.bin`) and data partition files (`partition_{key}.json`).

4.  **Data Generation (`$/Ama.CRDT.ShowCase.LargerThanMemory/Services`):**
    *   Create a `DataGeneratorService.cs`.
    *   This service will check if data already exists in the target directories.
    *   If not, it will programmatically create a large number of blog posts and comments using the `ICrdtPatcher` and `ICrdtApplicator` services, writing them through the `PartitionManager` until the total data size on disk reaches a configured constant (e.g., `MAX_GB`).

5.  **Main Application Logic (`$/Ama.CRDT.ShowCase.LargerThanMemory/Program.cs`):**
    *   Set up the Dependency Injection container.
    *   Register all necessary services from the `Ama.CRDT` library using `AddCrdtServices()`.
    *   Register the new `FileSystemPartitionStreamProvider` and `DataGeneratorService`.
    *   Define constants for the number of replicas and the maximum data size in GB.
    *   Create and run a `SimulationRunner` class to orchestrate the setup.

6.  **Simulation Orchestration (`$/Ama.CRDT.ShowCase.LargerThanMemory/SimulationRunner.cs`):**
    *   This class will be responsible for:
        *   Creating scopes for each replica using `ICrdtScopeFactory`.
        *   For each replica, setting up its data directory and instantiating a `PartitionManager`.
        *   Running the `DataGeneratorService` for the first replica to create the initial dataset.
        *   Simulating patch distribution to other replicas to ensure they converge to the same state.
        *   Initializing and running the `Terminal.Gui` UI.

7.  **UI Implementation (`$/Ama.CRDT.ShowCase.LargerThanMemory/Services/UiService.cs`):**
    *   Design and build the TUI using `Terminal.Gui`.
    *   The UI will feature a multi-pane window:
        *   **Left Pane:** A `ListView` showing blog post titles.
        *   **Right Pane:** A `TextView` or `ListView` showing the comments for the selected post.
    *   Implement logic to load data on-demand. When a user selects a blog post, use the `PartitionManager` to query and retrieve only the necessary comment partitions from disk for display.
    *   Implement "infinite scrolling" or paging in the comments view, fetching subsequent partitions as the user scrolls down.
    *   Include a menu or status bar to switch the view between different replicas' data stores, demonstrating that they all contain the same converged data.

<!---AI - Stage 1--->
# Proposed Files Needed
<!---
Here you need to list the files you need to load in order to get the correct context for your solution to build and test.
Put in this list only the exising files that need to be modified/loaded. Not the new ones that need to be created.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
With each file in one line.
Remember to ask to load any unit tests if they are related to any files you will want to change.
--->
- `$/Ama.CRDT/Services/Partitioning/IPartitionStreamProvider.cs` (To implement the file-based provider.)
- `$/Ama.CRDT/Services/Partitioning/IPartitionManager.cs` (To interact with the partitioned document.)
- `$/Ama.CRDT/Services/Partitioning/PartitionManager.cs` (To understand its dependencies and how to instantiate it.)
- `$/Ama.CRDT/Attributes/PartitionKeyAttribute.cs` (To correctly decorate the data models for partitioning.)
- `$/Ama.CRDT/Extensions/ServiceCollectionExtensions.cs` (To ensure correct registration of CRDT services and the new provider in the DI container.)
- `$/Ama.CRDT/Services/ICrdtPatcher.cs` (Needed for the data generation process.)
- `$/Ama.CRDT/Services/ICrdtApplicator.cs` (Needed for the data generation process.)
- `$/Ama.CRDT/Services/ICrdtScopeFactory.cs` (To create isolated scopes for each simulated replica.)
- `$/Ama.CRDT.sln` (To add the new showcase project to the solution.)

<!---AI - Stage 2--->
# Changes Done
<!---
Here you add detailed information about all the changes actually done.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
Add all the things that you did in a different way than expected.
--->
- `$/Ama.CRDT/Services/Partitioning/IPartitionManager.cs`: Added a new method `GetAllDataPartitionsAsync(object logicalKey)` to allow the UI to discover and iterate through the data partitions for a given document.
- `$/Ama.CRDT/Services/Partitioning/PartitionManager.cs`: Implemented the new `GetAllDataPartitionsAsync` method.
- `$/Ama.CRDT.ShowCase.LargerThanMemory/Ama.CRDT.ShowCase.LargerThanMemory.csproj`: Created the project file for the new showcase application, including dependencies on `Ama.CRDT`, `Terminal.Gui`, `Microsoft.Extensions.Hosting`, and `Bogus`.
- `$/Ama.CRDT.ShowCase.LargerThanMemory/Models/BlogPost.cs`: Created the main data model. I used an `IDictionary<Guid, Comment>` with the `[CrdtOrMapStrategy]` attribute for the comments, as this strategy is partitionable.
- `$/Ama.CRDT.ShowCase.LargerThanMemory/Models/Comment.cs`: Created a simple record for comments.
- `$/Ama.CRDT.ShowCase.LargerThanMemory/Program.cs`: Created the application entry point, configured dependency injection, and launched the `SimulationRunner`.
- `$/Ama.CRDT.ShowCase.LargerThanMemory/Services/FileSystemPartitionStreamProvider.cs`: Implemented `IPartitionStreamProvider` using local file storage. It uses the injected `ReplicaContext` to create a separate data directory for each replica (e.g., `./data/replica-1/`).
- `$/Ama.CRDT.ShowCase.LargerThanMemory/Services/DataGeneratorService.cs`: Implemented the data generation logic. It creates patches with batches of comments and applies them using `IPartitionManager.ApplyPatchAsync` until a size threshold is met.
- `$/Ama.CRDT.ShowCase.LargerThanMemory/Services/UiService.cs`: Implemented the Terminal User Interface using `Terminal.Gui`. It allows switching between replicas and loads comment partitions on-demand using the new `GetAllDataPartitionsAsync` method and a "Load Next Partition" command.
- `$/Ama.CRDT.ShowCase.LargerThanMemory/SimulationRunner.cs`: Created the main orchestrator. It sets up replica data directories, runs the data generator, and launches the UI. For simplicity in the showcase, it bootstraps replicas by copying the generated data files instead of exchanging patches.
- `$/Ama.CRDT.sln`: Updated the solution file to ensure the new `Ama.CRDT.ShowCase.LargerThanMemory` project is included.
- `$/FilesDescription.md`: Added descriptions for all newly created files.

<!---AI - Stage 2--->
# Manual Changes Needed
<!---
Here you add detailed information about all the manual changes that might be needed to be done from a human.
Example types of changes are:
	- Configuration settings
	- Environment variables
	- Deployments/Scripts/Setups external to this app
	- Dependencies to external projects that would need changes (like nuget packages for example)
	- Settings in other systems (for example, enable some flag or permissions in Github)
If there are none, then just write "No manual changes needed to be applied."
--->
No manual changes are needed to be applied. The showcase is self-contained. Just run the `Ama.CRDT.ShowCase.LargerThanMemory` project.

<!---AI - Stage 2--->
## Possible Techical Debt
<!---
Here you add comments about possible technical debt you encountered or implemented but it was too much to change or out of scope.
--->
- The convergence simulation in `SimulationRunner.cs` is simplified by directly copying data files between replicas. A more realistic showcase would generate patches on one replica and apply them to others, which would be slower but more accurate in demonstrating the CRDT process.
- The `UiService.cs` has basic on-demand loading via an explicit "Load Next Partition" action (F2). A more advanced implementation would trigger loading automatically as the user scrolls to the end of the list, creating a true "infinite scroll" experience.
- Error handling in the UI is minimal. A production application would need more robust handling for file I/O errors or issues during data deserialization.

<!---AI - Stage 2--->
## Last notes and implementation details
<!---
Here you add comments about the implementation that didn't fit on the previous section.
--->
- A new method, `GetAllDataPartitionsAsync`, was added to the `IPartitionManager` interface. This was a necessary extension to the core library to enable the UI to discover and browse the partitions of a document without knowing the specific range keys beforehand.
- The `[CrdtOrMapStrategy]` was used for the comments dictionary because it implements `IPartitionableCrdtStrategy` and is suitable for collections where items are identified by a unique key (in this case, the comment's `Guid`).
- The `DataGeneratorService` applies changes in batches. This is more efficient than applying one operation at a time, as it reduces the overhead of loading, modifying, and saving partition data for every single comment.
- The application will create a `data` directory in its execution path to store the generated files for each replica. This directory can be safely deleted between runs to trigger the data generation process again.

# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->