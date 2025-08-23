<!---Human--->
# Purpose
<!---
Add the purpose of this user story.
--->
Create an example console application that showcases the lock-free, convergent nature of Conflict-free Replicated Data Types (CRDTs). The application will simulate a distributed map-reduce process to demonstrate how concurrent, independent changes can be generated as patches and later merged into a consistent final state, highlighting the library's core functionality.

<!---Human--->
# Requirements
<!---
Add the requirements, technical or not.
--->
- Console project name: `Modern.CRDT.ShowCase`
- Generate a const amount of items, then distribute them to queues (not concurrent queues).
- Items should be like `{ name: "<User name>" }` with a sample of 10 names (so we have duplicates)
- First Map them and push them to a secondary queue
- The Reduce them to a single array that keeps only the unique Names
- Use CRDTs in between to show the convergence.
- Create a sample DB service, that is just a memory store to simulate a DB for POCO and metadata
- Try and use an example for each of the strategies
- Create a Json node provider to optimize the array checks using `id`s or another unique property
- Make multiple convergers with different replica IDs to simulate that many clients would get the same results.
- Use message queues in between processes for real time simulation and risk of getting out of order and duplicates.

<!---Human--->
## Requirements context
<!---
Add files that we will load for the UI to add context for the solution design.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
--->
- `C:\sources\Modern.CRDT\Modern.CRDT\Services\Strategies\ArrayLcsStrategy.cs`
- `C:\sources\Modern.CRDT\Modern.CRDT\Services\Strategies\CounterStrategy.cs`
- `C:\sources\Modern.CRDT\Modern.CRDT\Services\Strategies\LwwStrategy.cs`
- `C:\sources\Modern.CRDT\Modern.CRDT\Services\Strategies\IJsonNodeComparer.cs`
- `C:\sources\Modern.CRDT\Modern.CRDT\Services\Strategies\JsonNodeComparerProvider.cs`
- `C:\sources\Modern.CRDT\Modern.CRDT\Extensions\ServiceCollectionExtensions.cs`

<!---Human--->
# Testing Methodology
<!---
Add the testing methodology (manual, unit, integration, end-to-end tests?)
--->
Manual testing will be performed by running the console application and observing the output. The application will be designed to be self-verifying, printing the initial state, the operations performed, and the final converged state. The final output will be manually checked to ensure:
1. The final counter value equals the total number of processed items.
2. The final list of names contains all unique names without duplicates.
3. The Last-Writer-Wins properties reflect a value from one of the last-applied patches.

<!---AI - Stage 1--->
# Proposed Solutions [AI - Stage 1]
<!---
Here you will need to put a number of solutions that would fit for this problem.
Add the solutions that you rejected as well.
--->
### Solution 1: Distributed Actor Simulation (Recommended)
This approach models a distributed system using concurrent actors communicating via in-memory message queues (`System.Threading.Channels`).

- **Actors:**
    - **Producer:** A single task that generates raw data (e.g., `User` objects) and places them into a `tasksChannel`.
    - **Mappers:** A pool of concurrent tasks that read from `tasksChannel`. Each mapper processes one data item, generates a `CrdtPatch` representing the change (e.g., increment counter, add user to list), and writes the patch to a `patchesChannel`.
    - **Convergers:** A pool of concurrent tasks, each with a unique `ReplicaId` and a local copy of the state (document and metadata). They read patches from `patchesChannel` and apply them. The simulation can introduce random delays to ensure out-of-order application.
- **Verification:** After all tasks complete, the final state of each converger is compared to demonstrate that they all reached an identical state, regardless of the order in which patches were applied.
- **Reasoning:** This solution best fulfills the requirements by creating a realistic simulation of a concurrent, distributed environment. It directly showcases the lock-free, convergent, and order-independent nature of CRDTs, which is the core purpose of this user story.

### Solution 2: Batched Parallel Processing
This approach simplifies the simulation by processing data in batches rather than as a continuous stream of operations.

- **Process:**
    1. All raw data is generated upfront and split into several batches.
    2. Multiple mapper tasks are started in parallel, each assigned one batch.
    3. Each mapper processes its entire batch and generates a single, consolidated `CrdtPatch` representing the sum of all its changes.
    4. A final merger process collects all the patches.
    5. To prove convergence, the merger applies the patches in different permutations to multiple clean state objects and verifies that all final states are identical.
- **Reasoning for Rejection:** While this approach demonstrates commutativity, it is less representative of a real-time distributed system where small, independent operations arrive continuously and out of order. It doesn't model the message queue and individual operation processing aspect of the requirements as effectively as Solution 1.

### Solution 3: Sequential Two-Party Sync
This is the simplest approach, focusing on the merge logic between two states rather than simulating a multi-node system.

- **Process:**
    1. Two identical copies of a document (State A and State B) are created.
    2. A series of changes are made to State A, and a different series of changes are made to State B.
    3. A patch is generated to sync B with A's changes (`patch_A`).
    4. A patch is generated to sync A with B's changes (`patch_B`).
    5. `patch_A` is applied to B, and `patch_B` is applied to A.
    6. The final states are compared to show they have converged.
- **Reasoning for Rejection:** This solution fails to demonstrate the key requirement of concurrent, multi-node processing. It is a simple two-way merge, not a simulation of a distributed system, and does not showcase the library's ability to handle many concurrent operations from different sources.

<!---AI - Stage 1--->
# Proposed Techical Steps
<!---
Here you should append the tasks that you probably need to do.
An example would be like what files you need to create and what functionality those files would have.
--->
1.  **Project Setup:**
    - Create a new .NET console application project named `Modern.CRDT.ShowCase`.
    - Add a project reference to `Modern.CRDT`.
    - Add necessary NuGet packages (`Microsoft.Extensions.DependencyInjection`).

2.  **Model Definition (`$/Modern.CRDT.ShowCase/Models`):**
    - Create `User.cs`: A `readonly record struct User(Guid Id, string Name)`. The `Id` will be used for uniqueness checks in the custom comparer.
    - Create `UserStats.cs`: A POCO class to hold the shared state.
        - `[CrdtCounter] public long ProcessedItemsCount { get; set; }`
        - `[CrdtArrayLcsStrategy] public List<User> UniqueUsers { get; set; } = [];`
        - `[LwwStrategy] public string LastProcessedUserName { get; set; }`
        - `[LwwStrategy] public long LastProcessedTimestamp { get; set; }`

3.  **Custom Array Comparer (`$/Modern.CRDT.ShowCase/Services`):**
    - Create `UserByIdComparer.cs`: Implement `IJsonNodeComparer<User>`.
    - The `Equals` and `GetHashCode` methods will be implemented to compare `User` objects based on their `Id` property, ensuring that `UniqueUsers` correctly identifies unique users even if names are duplicated.

4.  **In-Memory Database (`$/Modern.CRDT.ShowCase/Services`):**
    - Create `IInMemoryDatabaseService.cs`: Define an interface for a simple key-value store.
    - Create `InMemoryDatabaseService.cs`: Implement the interface using a `ConcurrentDictionary` to store documents and their associated `CrdtMetadata`. This simulates a persistent store for each replica's state.

5.  **Dependency Injection Setup (`$/Modern.CRDT.ShowCase/Program.cs`):**
    - Configure a `ServiceCollection`.
    - Register CRDT services using the `AddCrdt()` extension method.
    - Register the custom `UserByIdComparer` using `services.AddCrdtComparer<User, UserByIdComparer>();`.
    - Register the `InMemoryDatabaseService` as a singleton.

6.  **Simulation Orchestration (`$/Modern.CRDT.ShowCase/Simulation.cs`):**
    - Create a `Simulation` class to encapsulate the main logic.
    - **Initialization:** Set up `System.Threading.Channels` for tasks (`Channel<User>`) and patches (`Channel<CrdtPatch>`).
    - **Producer Task:** Implement a method that generates `User` objects (with some duplicate names but unique IDs) and writes them to the tasks channel.
    - **Mapper Tasks:** Implement a method that creates and runs multiple concurrent mapper tasks. Each mapper reads a `User`, generates a patch reflecting the necessary changes to `UserStats`, and writes the patch to the patches channel.
    - **Converger Tasks:** Implement a method that creates and runs multiple concurrent converger tasks. Each converger will have a unique `ReplicaId`, read patches from the patches channel, and apply them to its local state (managed via the `InMemoryDatabaseService`).
    - **Execution and Verification:** The `Main` method in `Program.cs` will instantiate and run the simulation, wait for all tasks to complete, and then retrieve the final state from each converger to print and compare them, proving convergence.

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
- `$/Modern.CRDT/Extensions/ServiceCollectionExtensions.cs` (To understand how to correctly register library services and custom comparers in the DI container.)
- `$/Modern.CRDT/Services/IJsonCrdtPatcher.cs` (To use the service for generating patches in the mapper tasks.)
- `$/Modern.CRDT/Services/IJsonCrdtApplicator.cs` (To use the service for applying patches in the converger tasks.)
- `$/Modern.CRDT/Services/Strategies/IJsonNodeComparer.cs` (To have the contract for implementing the custom `UserByIdComparer`.)
- `$/Modern.CRDT/Models/CrdtPatch.cs` (To understand the structure of the object being passed through the message queue.)
- `$/Modern.CRDT/Models/CrdtMetadata.cs` (To understand the structure of the metadata that needs to be stored and managed by each converger.)
- `$/Modern.CRDT/Attributes/CrdtCounterAttribute.cs` (To confirm the usage for the counter strategy property.)
- `$/Modern.CRDT/Attributes/LwwStrategyAttribute.cs` (To confirm the usage for the LWW strategy properties.)
- `$/Modern.CRDT/Attributes/CrdtArrayLcsStrategyAttribute.cs` (To confirm the usage for the array strategy property.)

<!---AI - Stage 2--->
# Changes Done
<!---
Here you add detailed information about all the changes actually done.
Format this list in the following way:
	- `$/<Full file path from solution root>` (Reason to be used/loaded)
Add all the things that you did in a different way than expected.
--->
- Created a new console project `Modern.CRDT.ShowCase`.
- Added a `User` model and a `UserStats` model decorated with attributes for all three CRDT strategies (LWW, Counter, ArrayLCS).
- Implemented `UserByIdComparer` to allow the array strategy to uniquely identify users by their `Id` property.
- Created a simulated `InMemoryDatabaseService` to hold the state and metadata for each concurrent "converger" replica.
- Built a `SimulationRunner` class that orchestrates a distributed actor simulation:
    - A **Producer** generates raw `User` data.
    - Multiple concurrent **Mappers**, each with a unique replica ID, process the data, generate CRDT patches, and send them to a shared channel.
    - Multiple concurrent **Convergers**, also with unique replica IDs, consume patches from the channel in a potentially out-of-order fashion due to simulated latency. Each converger applies patches to its own local copy of the state stored in the in-memory database.
- The application's entry point (`Program.cs`) sets up DI, including registering the CRDT services and the custom `UserByIdComparer`, and then runs the simulation.
- At the end, the simulation verifies that all convergers have arrived at the exact same final state, proving convergence, and prints the result to the console.

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
- The new `Modern.CRDT.ShowCase` project needs to be added to the main solution file (`Modern.CRDT.sln`).

<!---AI - Stage 2--->
## Possible Techical Debt
<!---
Here you add comments about possible technical debt you encountered or implemented but it was too much to change or out of scope.
--->
No technical debt was introduced.

<!---AI - Stage 2--->
## Last notes and implementation details
<!---
Here you add comments about the implementation that didn't fit on the previous section.
--->
- The simulation uses `System.Threading.Channels` for high-performance, in-memory messaging between the producer, mapper, and converger tasks, effectively simulating a distributed message queue system.
- To ensure each mapper generates patches with a unique `ReplicaId`, a new `IJsonCrdtPatcher` instance is manually created for each mapper task, injecting a specific `CrdtOptions` instance. This bypasses the complexity of reconfiguring a singleton service from the main DI container.
- The mappers are designed to be stateless. They generate a patch by comparing a conceptual "empty" state with a state representing a single operation (e.g., one user processed). This creates a self-contained patch that can be applied to any existing state.
- The convergers introduce a small, random delay before processing each patch to simulate network latency and demonstrate that the CRDT merge logic is not dependent on the order of operations.
- The final verification step serializes the final state of each converger to a JSON string and compares them, providing a definitive proof of convergence.

# Code Revisions
<!---
Usually stuff are not working as we expect. This section is for the extra info that we make after this implementation.
This section is reserved for AI and human, but add only when you are instructed to.
--->