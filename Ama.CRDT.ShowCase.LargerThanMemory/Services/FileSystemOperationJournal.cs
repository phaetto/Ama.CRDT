namespace Ama.CRDT.ShowCase.LargerThanMemory.Services;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Journaling;
using Ama.CRDT.Services.Serialization;

public sealed class FileSystemOperationJournal : ICrdtOperationJournal
{
    private readonly string journalFilePath;
    private readonly Dictionary<string, List<JournaledOperation>> operationsByOrigin = new();
    private readonly object lockObj = new();
    private readonly ICrdtSerializer crdtSerializer;

    public FileSystemOperationJournal(ReplicaContext replicaContext, ICrdtSerializer crdtSerializer)
    {
        ArgumentNullException.ThrowIfNull(replicaContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(replicaContext.ReplicaId);
        ArgumentNullException.ThrowIfNull(crdtSerializer);

        this.crdtSerializer = crdtSerializer;

        var basePath = Path.Combine(Environment.CurrentDirectory, "data", replicaContext.ReplicaId);
        Directory.CreateDirectory(basePath);
        journalFilePath = Path.Combine(basePath, "journal.json");

        LoadJournal();
    }

    private void LoadJournal()
    {
        if (!File.Exists(journalFilePath)) return;

        try
        {
            var bytes = File.ReadAllBytes(journalFilePath);
            var ops = crdtSerializer.DeserializeFromBytes<List<JournaledOperation>>(bytes) ?? new List<JournaledOperation>();
            
            foreach (var jOp in ops)
            {
                if (!operationsByOrigin.TryGetValue(jOp.Operation.ReplicaId, out var originOps))
                {
                    originOps = new List<JournaledOperation>();
                    operationsByOrigin[jOp.Operation.ReplicaId] = originOps;
                }
                originOps.Add(jOp);
            }
        }
        catch
        {
            // Ignore deserialization issues for showcase
        }
    }

    private void SaveJournal()
    {
        var allOps = operationsByOrigin.Values.SelectMany(x => x).ToList();
        var bytes = crdtSerializer.SerializeToBytes(allOps);
        File.WriteAllBytes(journalFilePath, bytes);
    }

    public void Append(IComparable documentId, IReadOnlyList<CrdtOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(documentId);

        lock (lockObj)
        {
            bool added = false;
            foreach (var op in operations)
            {
                if (!operationsByOrigin.TryGetValue(op.ReplicaId, out var originOps))
                {
                    originOps = new List<JournaledOperation>();
                    operationsByOrigin[op.ReplicaId] = originOps;
                }
                
                // Prevent duplicates
                if (!originOps.Any(o => o.Operation.ShardClock == op.ShardClock))
                {
                    originOps.Add(new JournaledOperation(documentId, op));
                    added = true;
                }
            }

            if (added)
            {
                SaveJournal();
            }
        }
    }

    public Task AppendAsync(IComparable documentId, IReadOnlyList<CrdtOperation> operations, CancellationToken cancellationToken = default)
    {
        Append(documentId, operations);
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<JournaledOperation> GetOperationsByRangeAsync(string shardId, string originReplicaId, long minShardClock, long maxShardClock, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield(); // Suppress CS1998

        List<JournaledOperation> matchingOps;
        lock (lockObj)
        {
            if (!operationsByOrigin.TryGetValue(originReplicaId, out var originOps))
            {
                yield break;
            }

            matchingOps = originOps.Where(o => o.Operation.ShardClock > minShardClock && o.Operation.ShardClock <= maxShardClock).ToList();
        }

        foreach (var op in matchingOps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return op;
        }
    }

    public async IAsyncEnumerable<JournaledOperation> GetOperationsByDotsAsync(string shardId, string originReplicaId, IEnumerable<long> shardClocks, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield(); // Suppress CS1998

        var clockSet = new HashSet<long>(shardClocks);
        List<JournaledOperation> matchingOps;

        lock (lockObj)
        {
            if (!operationsByOrigin.TryGetValue(originReplicaId, out var originOps))
            {
                yield break;
            }

            matchingOps = originOps.Where(o => clockSet.Contains(o.Operation.ShardClock)).ToList();
        }

        foreach (var op in matchingOps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return op;
        }
    }
}