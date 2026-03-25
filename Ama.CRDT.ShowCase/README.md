# Ama.CRDT.ShowCase

This console application serves as a practical demonstration of the `Ama.CRDT` library. It simulates a distributed, lock-free environment where multiple independent nodes (replicas) process data concurrently and eventually converge to the exact same state without requiring centralized coordination or database locks.

## What it does

The simulation models a scenario where a system receives a stream of events (e.g., newly processed users) and multiple worker nodes need to update a shared set of statistics (`UserStats`). 

The simulation involves:
1. **Producer**: Generates 200 mock user events.
2. **5 Write Replicas**: 
   - Receive the user events independently (with simulated latency).
   - Maintain their own isolated state in an in-memory simulated database.
   - Apply updates to their local state.
   - Use `ICrdtPatcher.GeneratePatch` to calculate the exact conflict-free operations (the "Patch") made by their change.
   - Broadcast these patches asynchronously to all passive replicas via .NET Channels.
3. **3 Passive Replicas**:
   - Continuously listen for incoming patches from all 5 Write Replicas.
   - Apply these patches to their own local state using `ICrdtApplicator.ApplyPatch`.
   - Naturally experience out-of-order patch delivery due to concurrency.
4. **Convergence Verification**: Once all data is processed, the system verifies that all 3 Passive Replicas reached the exact same final state, proving Strong Eventual Consistency.

## Key Features Demonstrated

- **Attributes-based Strategy Selection**: The `UserStats` model is decorated with strategies like `[CrdtGCounterStrategy]` (Grow-only counter for totals), `[CrdtGSetStrategy]` (Grow-only set for unique names), and `[CrdtLwwStrategy]` (Last-Writer-Wins for the latest processed properties).
- **Custom Element Comparers**: Demonstrates how to register a `CaseInsensitiveStringComparer` via Dependency Injection to control how CRDT sets evaluate equality and uniqueness.
- **Replica Isolation**: Uses `ICrdtScopeFactory` to generate unique processing scopes for each replica, ensuring proper causality and peer identification.
- **Diffing and Application**: Showcases the separation of concerns between generating CRDT operations (`ICrdtPatcher`) based on state differences and applying them (`ICrdtApplicator`).
- **Conflict-Free Convergence**: Demonstrates that despite highly concurrent patches modifying the same sets and counters, the data structure mathematically converges without traditional database row/table locks.

## Running the Showcase

Ensure you are in the root directory or the project directory and run:

    dotnet run --project Ama.CRDT.ShowCase/Ama.CRDT.ShowCase.csproj

### Expected Output

You will see standard output indicating the start of the write replicas, the generation of mock items, and the passive replicas applying patches. At the end, a verification block will print:

    ✅ SUCCESS: All 3 passive replicas reached the same state.
    
    --- Final Stats (from 'passive-replica-1') ---
    Total processed items: 200 (Expected: 200)
    Total unique user names: 10
    Last processed user name: 'Alice'