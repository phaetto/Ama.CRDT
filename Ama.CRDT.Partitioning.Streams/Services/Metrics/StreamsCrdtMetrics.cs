namespace Ama.CRDT.Partitioning.Streams.Services.Metrics;

using System;
using System.Diagnostics.Metrics;

public sealed class StreamsCrdtMetrics
{
    private readonly Meter meter;

    public Histogram<double> InitializationDuration { get; }
    public Histogram<double> FindDuration { get; }
    public Histogram<double> InsertDuration { get; }
    public Histogram<double> UpdateDuration { get; }
    public Histogram<double> DeleteDuration { get; }
    public Histogram<double> GetAllDuration { get; }
    public Histogram<double> GetPartitionCountDuration { get; }
    public Histogram<double> GetDataPartitionByIndexDuration { get; }
    public Counter<long> NodesSplit { get; }
    public Counter<long> NodesMerged { get; }
    public Counter<long> NodesBorrowed { get; }
    public Counter<long> NodeReads { get; }
    public Counter<long> NodeWrites { get; }
    
    // Space reuse metrics
    public Counter<long> BlocksReused { get; }
    public Counter<long> InPlaceOverwrites { get; }
    public Counter<long> BytesSaved { get; }
    public Counter<long> BlocksFreed { get; }

    public StreamsCrdtMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        meter = meterFactory.Create("Ama.CRDT.Partitioning.Streams");

        InitializationDuration = meter.CreateHistogram<double>("crdt.streams.initialization.duration", "ms", "The duration of the B+ Tree index initialization.");
        FindDuration = meter.CreateHistogram<double>("crdt.streams.find.duration", "ms", "The duration of finding a partition in the B+ Tree index.");
        InsertDuration = meter.CreateHistogram<double>("crdt.streams.insert.duration", "ms", "The duration of inserting a partition into the B+ Tree index.");
        UpdateDuration = meter.CreateHistogram<double>("crdt.streams.update.duration", "ms", "The duration of updating a partition in the B+ Tree index.");
        DeleteDuration = meter.CreateHistogram<double>("crdt.streams.delete.duration", "ms", "The duration of deleting a partition from the B+ Tree index.");
        GetAllDuration = meter.CreateHistogram<double>("crdt.streams.get_all.duration", "ms", "The duration of retrieving all partitions from the B+ Tree index.");
        GetPartitionCountDuration = meter.CreateHistogram<double>("crdt.streams.get_partition_count.duration", "ms", "The duration of counting partitions in the B+ Tree index.");
        GetDataPartitionByIndexDuration = meter.CreateHistogram<double>("crdt.streams.get_data_partition_by_index.duration", "ms", "The duration of retrieving a data partition by index from the B+ Tree index.");
        NodesSplit = meter.CreateCounter<long>("crdt.streams.nodes.split.count", "nodes", "The number of B+ Tree nodes split.");
        NodesMerged = meter.CreateCounter<long>("crdt.streams.nodes.merged.count", "nodes", "The number of B+ Tree nodes merged.");
        NodesBorrowed = meter.CreateCounter<long>("crdt.streams.nodes.borrowed.count", "nodes", "The number of times a B+ Tree node borrowed from a sibling.");
        NodeReads = meter.CreateCounter<long>("crdt.streams.node.reads.count", "nodes", "The number of B+ Tree nodes read from the stream.");
        NodeWrites = meter.CreateCounter<long>("crdt.streams.node.writes.count", "nodes", "The number of B+ Tree nodes written to the stream.");
        
        BlocksReused = meter.CreateCounter<long>("crdt.streams.blocks.reused.count", "blocks", "The number of times a free block was successfully reused instead of appending to the stream.");
        InPlaceOverwrites = meter.CreateCounter<long>("crdt.streams.blocks.inplace_overwrites.count", "blocks", "The number of times a node update fit within its existing block size and was overwritten in place.");
        BytesSaved = meter.CreateCounter<long>("crdt.streams.bytes.saved.count", "bytes", "The total number of bytes saved (not appended) by reusing free blocks or overwriting in place.");
        BlocksFreed = meter.CreateCounter<long>("crdt.streams.blocks.freed.count", "blocks", "The number of blocks successfully added to the free block list.");
    }
}