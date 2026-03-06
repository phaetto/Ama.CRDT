INPUT: strategyName, Enter the strategy name
INPUT: description, Enter the algorithm to implement
TEMPLATE
$\Ama.CRDT\Extensions\IntentBuilderExtensions.cs
$\Ama.CRDT\Models\CrdtMetadata.cs
$\Ama.CRDT\Models\CrdtOperation.cs
$\Ama.CRDT\Services\Helpers\PocoPathHelper.cs
$\Ama.CRDT\Services\CrdtMetadataManager.cs
$\Ama.CRDT\Attributes\Decorators\CrdtEpochBoundAttribute.cs

Add a new decorator {strategyName}.
The strategy implementation should be working like this: {description}.

Add unit tests as well.

Tell me if you need any more files.

Example decorator:
$\Ama.CRDT\Services\Strategies\Decorators\EpochBoundStrategy.cs
$\Ama.CRDT.UnitTests\Services\Strategies\Decorators\EpochBoundStrategyTests.cs