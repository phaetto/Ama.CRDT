namespace Ama.CRDT.Services.Metrics;

using System.Diagnostics.Metrics;

public sealed class BPlusTreeCrdtMetrics
{
    private readonly Meter meter;

    public Histogram<double> FindDuration { get; }
    public Histogram<double> InsertDuration { get; }
    public Histogram<double> UpdateDuration { get; }
    public Histogram<double> DeleteDuration { get; }
    public Histogram<double> GetAllDuration { get; }
    public Counter<long> NodesSplit { get; }
    public Counter<long> NodesMerged { get; }
    public Counter<long> NodesBorrowed { get; }
    public Counter<long> NodeReads { get; }
    public Counter<long> NodeWrites { get; }

    public BPlusTreeCrdtMetrics(IMeterFactory meterFactory)
    {
        meter = meterFactory.Create("Ama.CRDT.BPlusTree");

        FindDuration = meter.CreateHistogram<double>("crdt.bplus_tree.find.duration", "ms", "The duration of finding a partition in the B+ Tree index.");
        InsertDuration = meter.CreateHistogram<double>("crdt.bplus_tree.insert.duration", "ms", "The duration of inserting a partition into the B+ Tree index.");
        UpdateDuration = meter.CreateHistogram<double>("crdt.bplus_tree.update.duration", "ms", "The duration of updating a partition in the B+ Tree index.");
        DeleteDuration = meter.CreateHistogram<double>("crdt.bplus_tree.delete.duration", "ms", "The duration of deleting a partition from the B+ Tree index.");
        GetAllDuration = meter.CreateHistogram<double>("crdt.bplus_tree.get_all.duration", "ms", "The duration of retrieving all partitions from the B+ Tree index.");
        NodesSplit = meter.CreateCounter<long>("crdt.bplus_tree.nodes.split.count", "nodes", "The number of B+ Tree nodes split.");
        NodesMerged = meter.CreateCounter<long>("crdt.bplus_tree.nodes.merged.count", "nodes", "The number of B+ Tree nodes merged.");
        NodesBorrowed = meter.CreateCounter<long>("crdt.bplus_tree.nodes.borrowed.count", "nodes", "The number of times a B+ Tree node borrowed from a sibling.");
        NodeReads = meter.CreateCounter<long>("crdt.bplus_tree.node.reads.count", "nodes", "The number of B+ Tree nodes read from the stream.");
        NodeWrites = meter.CreateCounter<long>("crdt.bplus_tree.node.writes.count", "nodes", "The number of B+ Tree nodes written to the stream.");
    }
}