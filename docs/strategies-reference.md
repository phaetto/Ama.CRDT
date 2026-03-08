# CRDT Strategies Reference

This library uses attributes on your POCO properties to determine how to merge changes. You can control the conflict resolution logic for each property individually.

| Strategy | Description | Best Use Case |
| :--- | :--- | :--- |
| **Numeric & Value Strategies** | | |
| `[CrdtLwwStrategy]` | (Default) Last-Writer-Wins. The value with the latest timestamp overwrites others. | Simple properties like names, statuses, or any field where the last update should be the final state. |
| `[CrdtCounterStrategy]` | A simple counter that supports increments and decrements. | Likes, view counts, scores, or inventory quantities where changes are additive. |
| `[CrdtGCounterStrategy]` | A Grow-Only Counter that only supports positive increments. | Counting events that can only increase, like page visits or successful transactions. |
| `[CrdtBoundedCounterStrategy]` | A counter whose value is clamped within a specified minimum and maximum range. | Health bars in a game (0-100), volume controls, or any numeric value with hard limits. |
| `[CrdtMaxWinsStrategy]` | A register where conflicts are resolved by choosing the highest numeric value. | High scores, auction bids, or tracking the peak value of a metric. |
| `[CrdtMinWinsStrategy]` | A register where conflicts are resolved by choosing the lowest numeric value. | Best lap times, lowest price seen, or finding the earliest event timestamp. |
| `[CrdtFwwStrategy]` | A First-Writer-Wins register. The value with the earliest timestamp is retained. | Scenarios where the original or earliest value should be preserved, like claiming a unique username. |
| `[CrdtAverageRegisterStrategy]` | A register where the final value is the average of contributions from all replicas. | Aggregating sensor readings from multiple devices, user ratings, or calculating an average latency. |
| **Set & Collection Strategies** | | |
| `[CrdtArrayLcsStrategy]` | (Default for collections) Uses Longest Common Subsequence (LCS) to handle insertions and deletions efficiently. Preserves order. | Collaborative text editing, managing ordered lists of tasks, or any sequence where element order matters. |
| `[CrdtSortedSetStrategy]` | Maintains a collection sorted by a natural or specified key. Uses LCS for diffing. | Leaderboards, sorted lists of tags, or displaying items in a consistent, sorted order. |
| `[CrdtGSetStrategy]` | A Grow-Only Set. Elements can be added but never removed. | Storing tags, accumulating unique identifiers, or tracking event participation where removal is not allowed. |
| `[CrdtTwoPhaseSetStrategy]` | A Two-Phase Set. Elements can be added and removed, but an element cannot be re-added once removed. | Managing feature flags or user roles where an item, once revoked, should stay revoked. |
| `[CrdtLwwSetStrategy]` | A Last-Writer-Wins Set. Element membership is determined by the timestamp of its last add or remove operation. | A shopping cart, user preferences, or any set where the most recent decision to add or remove an item should win. |
| `[CrdtFwwSetStrategy]` | A First-Writer-Wins Set. Element membership is determined by the timestamp of its earliest add or remove operation. | Retaining the earliest decision to add or remove an item, or first-come-first-serve membership. |
| `[CrdtOrSetStrategy]` | An Observed-Remove Set. Allows elements to be re-added after removal by tagging each addition uniquely. | Collaborative tagging systems or managing members in a group where users can leave and rejoin. |
| `[CrdtPriorityQueueStrategy]` | Manages a collection as a priority queue, sorted by a specified property on the elements. | Task queues, notification lists, or any scenario where items need to be processed based on priority. |
| `[CrdtFixedSizeArrayStrategy]` | Manages a fixed-size array where each index is an LWW-Register. Useful for representing grids or slots. | Game boards, seating charts, or fixed-size buffers where each position is updated independently. |
| `[CrdtLseqStrategy]` | An ordered list strategy that generates fractional indexes to avoid conflicts during concurrent insertions. | Collaborative text editors and other real-time sequence editing applications requiring high-precision ordering. |
| `[CrdtRgaStrategy]` | An ordered list strategy (Replicated Growable Array) that links elements to predecessors and uses tombstones for deletions. | Collaborative text editing, rich text sequences, or lists requiring stable, precise element ordering under concurrent edits. |
| `[CrdtVoteCounterStrategy]` | Manages a dictionary of options to voter sets, ensuring each voter can only have one active vote at a time. | Polls, surveys, or any system where users vote for one of several options. |
| **Object & Map Strategies** | | |
| `[CrdtLwwMapStrategy]` | A Last-Writer-Wins Map. Each key-value pair is an independent LWW-Register. Conflicts are resolved per-key. | Storing user preferences, feature flags, or any key-value data where the last update for a given key should win. |
| `[CrdtFwwMapStrategy]` | A First-Writer-Wins Map. Each key-value pair is an independent FWW-Register. Conflicts are resolved per-key using the earliest timestamp. | Initial configurations, first-come-first-serve slot allocations, or retaining original key-value pairs. |
| `[CrdtOrMapStrategy]` | An Observed-Remove Map. Key presence is managed with OR-Set logic, allowing keys to be re-added after removal. Value updates use LWW. | Managing complex dictionaries where keys can be concurrently added and removed, such as a map of user permissions or editable metadata. |
| `[CrdtCounterMapStrategy]` | Manages a dictionary where each key is an independent PN-Counter (supporting increments and decrements). | Tracking scores per player, counting votes per option, or managing inventory per item where quantities can go up or down. |
| `[CrdtMaxWinsMapStrategy]` | A grow-only map where conflicts for each key are resolved by choosing the highest value. | Storing high scores per level in a game, tracking the latest version number per component, or recording the peak bid for different auction items. |
| `[CrdtMinWinsMapStrategy]` | A grow-only map where conflicts for each key are resolved by choosing the lowest value. | Recording the best completion time per race track, finding the cheapest price offered per product from various sellers, or tracking the earliest discovery time for different artifacts. |
| **Specialized Data Structure Strategies** | | |
| `[CrdtGraphStrategy]` | An add-only graph. Supports concurrent additions of vertices and edges. | Building social networks, knowledge graphs, or any scenario where relationships are added but not removed. |
| `[CrdtTwoPhaseGraphStrategy]`| A graph where vertices and edges can be added and removed, but not re-added after removal. | Managing network topologies or dependency graphs where components, once removed, are considered permanently decommissioned. |
| `[CrdtReplicatedTreeStrategy]`| Manages a hierarchical tree structure. Uses OR-Set for node existence (allowing re-addition) and LWW for parent-child links (move operations). | Collaborative document outlines, folder structures, or comment threads where items can be concurrently added, removed, and reorganized. |
| **State & Locking Strategies** | | |
| `[CrdtStateMachineStrategy]` | Enforces valid state transitions using a user-defined validator, with LWW for conflict resolution. | Order processing (Pending -> Shipped -> Delivered), workflows, or any property with a constrained lifecycle. |
| **Decorators** | | |
| `[CrdtApprovalQuorumAttribute]` | A decorator that requires a specified number of approvals from different replicas before applying the underlying CRDT operation. | Distributed consensus, multi-signature actions, or administrative overrides requiring quorum. |
| `[CrdtEpochBoundAttribute]` | A decorator that allows explicitly clearing the underlying state by advancing an epoch, effectively resetting the CRDT for that path. | Resetting a game round, clearing a cart, or gracefully resetting shared state across replicas. |