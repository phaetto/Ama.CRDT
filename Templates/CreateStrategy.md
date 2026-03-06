INPUT: strategyName, Enter the strategy name
INPUT: algorithmDescription, Enter the algorithm to implement
TEMPLATE
$\Ama.CRDT\Extensions\IntentBuilderExtensions.cs
$\Ama.CRDT\Models\CrdtMetadata.cs
$\Ama.CRDT\Models\CrdtOperation.cs
$\Ama.CRDT\Services\Helpers\PocoPathHelper.cs
$\Ama.CRDT\Services\Strategies\ICrdtStrategy.cs
$\Ama.CRDT\Services\Strategies\ApplyOperationContext.cs
$\Ama.CRDT\Services\Strategies\GenerateOperationContext.cs
$\Ama.CRDT\Services\Strategies\GeneratePatchContext.cs

Implement a new strategy {strategyName}.
The strategy implementation should implement this algorithm: {algorithmDescription}

Tell me if you need any more files.

Example strategy:
$\Ama.CRDT\Attributes\CrdtLwwStrategyAttribute.cs
$\Ama.CRDT\Services\Strategies\LwwStrategy.cs