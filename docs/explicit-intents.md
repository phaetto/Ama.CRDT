# Explicit Operations (Intent Builder)

Sometimes you don't want to compare two entire objects to find a small change, or you want to explicitly capture a user's intent (e.g., "increment by 1") instead of setting absolute values. The library provides a strongly-typed fluent intent builder that bypasses the diffing process entirely:

```csharp
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;

// ...

// Build an explicit operation targeting the LoginCount property
var incrementOp = patcher.BuildOperation(originalDocument, doc => doc.LoginCount)
                         .Increment(1);

// Build another explicit operation targeting a collection
var addBadgeOp = patcher.BuildOperation(originalDocument, doc => doc.Badges)
                        .Add("veteran");

var patch = new CrdtPatch([incrementOp, addBadgeOp]);

// Apply or distribute the patch as usual
var applyResult = applicator.ApplyPatch(originalDocument, patch);
```

The `.BuildOperation(...)` syntax scales powerfully across all CRDT strategies and ensures your explicitly defined changes map exactly to `.Increment()`, `.Add()`, `.Remove()`, `.MoveNode()`, and more, in a completely strongly-typed fashion.