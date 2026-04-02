# Explicit Operations (Intent Builder)

Sometimes you don't want to compare two entire objects to find a small change, or you want to explicitly capture a user's intent (e.g., "increment by 1") instead of setting absolute values. The library provides a strongly-typed intent API that bypasses the diffing process entirely:

```csharp
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;

// ...

// Generate an explicit operation targeting the LoginCount property
var incrementOp = patcher.GenerateOperation(originalDocument, doc => doc.LoginCount, new IncrementIntent(1));

// Generate another explicit operation targeting a collection
var addBadgeOp = patcher.GenerateOperation(originalDocument, doc => doc.Badges, new AddIntent("veteran"));

var patch = new CrdtPatch([incrementOp, addBadgeOp]);

// Apply or distribute the patch as usual
var applyResult = applicator.ApplyPatch(originalDocument, patch);
```

The `.GenerateOperation(...)` syntax scales powerfully across all CRDT strategies and ensures your explicitly defined changes map exactly to `IncrementIntent`, `AddIntent`, `RemoveIntent`, `MoveNodeIntent`, and more, in a completely strongly-typed fashion.