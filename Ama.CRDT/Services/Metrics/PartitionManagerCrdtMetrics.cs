namespace Ama.CRDT.Services.Metrics;

using System.Diagnostics.Metrics;

public sealed class PartitionManagerCrdtMetrics
{
    private readonly Meter meter;

    public Counter<long> PatchesApplied { get; }
    public Counter<long> PartitionsSplit { get; }
    public Counter<long> PartitionsMerged { get; }
    public Histogram<double> InitializationDuration { get; }
    public Histogram<double> ApplyPatchDuration { get; }
    public Histogram<double> StreamReadDuration { get; }
    public Histogram<double> StreamWriteDuration { get; }
    public Histogram<double> GroupOperationsDuration { get; }
    public Histogram<double> PersistChangesDuration { get; }
    public Histogram<double> SplitPartitionDuration { get; }
    public Histogram<double> MergePartitionDuration { get; }
    public Histogram<double> ApplicatorApplyPatchDuration { get; }
    public Histogram<double> StrategySplitDuration { get; }
    public Histogram<double> GetPartitionDuration { get; }
    public Histogram<double> GetPartitionContentDuration { get; }
    public Histogram<double> GetAllDataPartitionsDuration { get; }
    public Histogram<double> GetDataPartitionCountDuration { get; }
    public Histogram<double> GetDataPartitionByIndexDuration { get; }
    public Histogram<double> GetAllLogicalKeysDuration { get; }


    public PartitionManagerCrdtMetrics(IMeterFactory meterFactory)
    {
        meter = meterFactory.Create("Ama.CRDT.Partitioning");

        PatchesApplied = meter.CreateCounter<long>("crdt.partition_manager.patches.applied.count", "patches", "The number of CRDT patches applied to the partition manager.");
        PartitionsSplit = meter.CreateCounter<long>("crdt.partition_manager.partitions.split.count", "partitions", "The number of partitions that have been split due to size constraints.");
        PartitionsMerged = meter.CreateCounter<long>("crdt.partition_manager.partitions.merged.count", "partitions", "The number of partitions that have been merged due to size constraints.");

        InitializationDuration = meter.CreateHistogram<double>("crdt.partition_manager.initialization.duration", "ms", "The duration of the partition manager initialization process.");
        ApplyPatchDuration = meter.CreateHistogram<double>("crdt.partition_manager.apply_patch.duration", "ms", "The duration of applying a patch to a partitioned document.");
        StreamReadDuration = meter.CreateHistogram<double>("crdt.partition_manager.stream.read.duration", "ms", "The duration of reading partition content from the underlying stream.");
        StreamWriteDuration = meter.CreateHistogram<double>("crdt.partition_manager.stream.write.duration", "ms", "The duration of writing partition content to the underlying stream.");
        GroupOperationsDuration = meter.CreateHistogram<double>("crdt.partition_manager.group_operations.duration", "ms", "The duration of grouping operations by partition.");
        PersistChangesDuration = meter.CreateHistogram<double>("crdt.partition_manager.persist_changes.duration", "ms", "The duration of persisting partition changes to the underlying stream.");
        SplitPartitionDuration = meter.CreateHistogram<double>("crdt.partition_manager.split_partition.duration", "ms", "The duration of the partition split operation.");
        MergePartitionDuration = meter.CreateHistogram<double>("crdt.partition_manager.merge_partition.duration", "ms", "The duration of the partition merge operation.");

        ApplicatorApplyPatchDuration = meter.CreateHistogram<double>("crdt.partition_manager.applicator.apply_patch.duration", "ms", "The duration of the ICrdtApplicator.ApplyPatch call.");
        StrategySplitDuration = meter.CreateHistogram<double>("crdt.partition_manager.strategy.split.duration", "ms", "The duration of the IPartitionableCrdtStrategy.Split call.");

        GetPartitionDuration = meter.CreateHistogram<double>("crdt.partition_manager.get_partition.duration", "ms", "The duration of retrieving a single partition.");
        GetPartitionContentDuration = meter.CreateHistogram<double>("crdt.partition_manager.get_partition_content.duration", "ms", "The duration of retrieving the content of a single partition.");
        GetAllDataPartitionsDuration = meter.CreateHistogram<double>("crdt.partition_manager.get_all_data_partitions.duration", "ms", "The duration of retrieving all data partitions for a logical key.");
        GetDataPartitionCountDuration = meter.CreateHistogram<double>("crdt.partition_manager.get_data_partition_count.duration", "ms", "The duration of counting data partitions for a logical key.");
        GetDataPartitionByIndexDuration = meter.CreateHistogram<double>("crdt.partition_manager.get_data_partition_by_index.duration", "ms", "The duration of retrieving a data partition by its index.");
        GetAllLogicalKeysDuration = meter.CreateHistogram<double>("crdt.partition_manager.get_all_logical_keys.duration", "ms", "The duration of retrieving all distinct logical keys.");
    }
}