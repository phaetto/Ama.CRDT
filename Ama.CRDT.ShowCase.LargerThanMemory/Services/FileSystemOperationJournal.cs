namespace Ama.CRDT.ShowCase.LargerThanMemory.Services;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Serialization;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Journaling;

public sealed class FileSystemOperationJournal : ICrdtOperationJournal
{
    private readonly string journalFilePath;
    private readonly Dictionary<string, List<CrdtOperation>> operationsByOrigin = new();
    private readonly object lockObj = new();

    public FileSystemOperationJournal(ReplicaContext replicaContext)
    {
        ArgumentNullException.ThrowIfNull(replicaContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(replicaContext.ReplicaId);

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
            var json = File.ReadAllText(journalFilePath);
            var ops = JsonSerializer.Deserialize<List<CrdtOperation>>(json, CrdtJsonContext.DefaultOptions) ?? new List<CrdtOperation>();
            
            foreach (var op in ops)
            {
                if (!operationsByOrigin.TryGetValue(op.ReplicaId, out var originOps))
                {
                    originOps = new List<CrdtOperation>();
                    operationsByOrigin[op.ReplicaId] = originOps;
                }
                originOps.Add(op);
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
        var json = JsonSerializer.Serialize(allOps, CrdtJsonContext.DefaultOptions);
        File.WriteAllText(journalFilePath, json);
    }

    public void Append(IReadOnlyList<CrdtOperation> operations)
    {
        lock (lockObj)
        {
            bool added = false;
            foreach (var op in operations)
            {
                if (!operationsByOrigin.TryGetValue(op.ReplicaId, out var originOps))
                {
                    originOps = new List<CrdtOperation>();
                    operationsByOrigin[op.ReplicaId] = originOps;
                }
                
                // Prevent duplicates
                if (!originOps.Any(o => o.GlobalClock == op.GlobalClock))
                {
                    originOps.Add(op);
                    added = true;
                }
            }

            if (added)
            {
                SaveJournal();
            }
        }
    }

    public Task AppendAsync(IReadOnlyList<CrdtOperation> operations, CancellationToken cancellationToken = default)
    {
        Append(operations);
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<CrdtOperation> GetOperationsByRangeAsync(string originReplicaId, long minGlobalClock, long maxGlobalClock, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield(); // Suppress CS1998

        List<CrdtOperation> matchingOps;
        lock (lockObj)
        {
            if (!operationsByOrigin.TryGetValue(originReplicaId, out var originOps))
            {
                yield break;
            }

            matchingOps = originOps.Where(o => o.GlobalClock > minGlobalClock && o.GlobalClock <= maxGlobalClock).ToList();
        }

        foreach (var op in matchingOps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return op;
        }
    }

    public async IAsyncEnumerable<CrdtOperation> GetOperationsByDotsAsync(string originReplicaId, IEnumerable<long> globalClocks, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield(); // Suppress CS1998

        var clockSet = new HashSet<long>(globalClocks);
        List<CrdtOperation> matchingOps;

        lock (lockObj)
        {
            if (!operationsByOrigin.TryGetValue(originReplicaId, out var originOps))
            {
                yield break;
            }

            matchingOps = originOps.Where(o => clockSet.Contains(o.GlobalClock)).ToList();
        }

        foreach (var op in matchingOps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return op;
        }
    }
}