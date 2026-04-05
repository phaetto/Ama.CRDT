# Ama.CRDT Collaborative Editing Showcase

This project is a Windows Forms application that demonstrates real-time collaborative text editing using the `Ama.CRDT` library. It specifically highlights the power of the **RGA (Replicated Growable Array)** strategy for handling ordered sequential data, such as lines of text in a shared document.

## Key Features Demonstrated

1. **Replicated Growable Array (RGA):**
   The shared document model (`SharedDocument.cs`) utilizes `[CrdtRgaStrategy]` on an `IList<string>`. This strategy is the industry standard for sequence-based data (like text editors), resolving insertion conflicts predictably and emitting tombstones for deletions.
   
2. **Explicit Operation Intents:**
   Instead of generating generic diffs by comparing huge strings, the custom `IntentTextBox` calculates exact linear changes and explicitly emits `InsertIntent` and `RemoveIntent` commands to the `IAsyncCrdtPatcher`. This proves how developers can bypass generic patching for highly optimized, domain-specific intent generation.

3. **Multi-Replica Architecture:**
   Using the `NetworkBroker`, the application simulates a real-time, peer-to-peer/broadcast network. You can spawn multiple independent editor windows (replicas) that communicate via `CrdtPatch` messages.

4. **Catch-up Syncing & Operation Journaling:**
   When a new editor replica is spawned, it doesn't just copy the current state. It uses the `IVersionVectorSyncService` to calculate its missing causal history and fetches exactly the operations it missed from the `MemoryJournal` to catch up to the cluster seamlessly.

5. **Safe Garbage Collection (GMVV):**
   Text editing creates many tombstones over time. The showcase uses the `CompactingApplicatorDecorator` and a `GlobalMinimumVersionPolicy` to automatically clean up tombstone metadata *only* when all active replicas have safely observed the operations (Global Minimum Version Vector).

6. **"Monkey Mode" Chaos Testing:**
   Each editor features a "Monkey Mode" that programmatically adds, alters, and deletes random lines at configurable intervals. You can enable this on multiple windows simultaneously to watch the CRDT flawlessly resolve hundreds of concurrent editing conflicts without locking or centralized orchestration.

## How to Run

1. Set `Ama.CRDT.ShowCase.CollaborativeEditing` as your Startup Project in Visual Studio.
2. Run the application. The **CRDT Collaborative Editing Manager** window will appear.
3. Click **"Spawn New Editor Replica"** two or three times to open multiple editor windows.
4. **Manual Editing:** Start typing in any editor. You will see your changes instantly synchronize across all other open editor windows.
5. **Chaos Testing:** Check the **"Monkey Mode"** box on several editors. Watch as they furiously edit the document simultaneously. The text will converge to the exact same state across all windows, demonstrating the mathematical guarantees of CRDTs.

## Technical Architecture Highlights

* **Decorators in Action:** The `Program.cs` uses `AddCrdtApplicatorDecorator` and `AddCrdtPatcherDecorator` to seamlessly inject `Journaling` and `Compacting` capabilities into the pipeline without modifying the core logic.
* **Native AOT Ready:** The application relies entirely on `CollaborativeEditingCrdtAotContext` and `CollaborativeEditingJsonContext`, proving that complex collaborative features can run without reflection. 
* **Custom UI Integration:** The `IntentTextBox` shows how easily standard UI controls can be adapted to speak CRDT intents.