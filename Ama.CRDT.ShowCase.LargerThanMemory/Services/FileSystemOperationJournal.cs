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
using Ama.CRDT.Services;
using Ama.CRDT.Services.Journaling;
using Microsoft.Extensions.DependencyInjection;

public sealed class FileSystemOperationJournal : ICrdtOperationJournal
{
    private readonly string journalFilePath;
    private readonly Dictionary<string, List<JournaledOperation>> operationsByOrigin = new();
    private readonly object lockObj = new();
    private readonly JsonSerializerOptions jsonOptions;

    public FileSystemOperationJournal(
        ReplicaContext replicaContext, 
        [FromKeyedServices("Ama.CRDT")] JsonSerializerOptions jsonOptions)
    {
        ArgumentNullException.ThrowIfNull(replicaContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(replicaContext.ReplicaId);
        ArgumentNullException.ThrowIfNull(jsonOptions);

        this.jsonOptions = jsonOptions;

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
            var ops = JsonSerializer.Deserialize<List<JournaledOperation>>(json, jsonOptions) ?? new List<JournaledOperation>();
            
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
        var json = JsonSerializer.Serialize(allOps, jsonOptions);
        File.WriteAllText(journalFilePath, json);
    }

    public void Append(string documentId, IReadOnlyList<CrdtOperation> operations)
    {
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
                if (!originOps.Any(o => o.Operation.GlobalClock == op.GlobalClock))
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

    public Task AppendAsync(string documentId, IReadOnlyList<CrdtOperation> operations, CancellationToken cancellationToken = default)
    {
        Append(documentId, operations);
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<JournaledOperation> GetOperationsByRangeAsync(string originReplicaId, long minGlobalClock, long maxGlobalClock, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield(); // Suppress CS1998

        List<JournaledOperation> matchingOps;
        lock (lockObj)
        {
            if (!operationsByOrigin.TryGetValue(originReplicaId, out var originOps))
            {
                yield break;
            }

            matchingOps = originOps.Where(o => o.Operation.GlobalClock > minGlobalClock && o.Operation.GlobalClock <= maxGlobalClock).ToList();
        }

        foreach (var op in matchingOps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return op;
        }
    }

    public async IAsyncEnumerable<JournaledOperation> GetOperationsByDotsAsync(string originReplicaId, IEnumerable<long> globalClocks, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield(); // Suppress CS1998

        var clockSet = new HashSet<long>(globalClocks);
        List<JournaledOperation> matchingOps;

        lock (lockObj)
        {
            if (!operationsByOrigin.TryGetValue(originReplicaId, out var originOps))
            {
                yield break;
            }

            matchingOps = originOps.Where(o => clockSet.Contains(o.Operation.GlobalClock)).ToList();
        }

        foreach (var op in matchingOps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return op;
        }
    }
}