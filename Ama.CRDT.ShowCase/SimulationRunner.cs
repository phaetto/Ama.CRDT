namespace Ama.CRDT.ShowCase;
using System.Linq;
using System.Threading.Channels;
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.ShowCase.Models;
using Ama.CRDT.ShowCase.Services;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Orchestrates a distributed multi-replica simulation using concurrent producers, write replicas, and passive replicas
/// communicating via channels to demonstrate CRDT convergence without locks.
/// </summary>
public sealed class SimulationRunner(
    ICrdtScopeFactory scopeFactory,
    IInMemoryDatabaseService database,
    ICrdtMetadataManager metadataManager)
{
    private const int TotalItems = 200;
    private const int WriteReplicaCount = 5;
    private const int PassiveReplicaCount = 3;
    private static readonly string[] Names = ["Alice", "Bob", "Charlie", "David", "Eve", "Frank", "Grace", "Heidi", "Ivan", "Judy"];

    public async Task RunAsync()
    {
        Console.WriteLine($"Simulating {TotalItems} operations, with each change from {WriteReplicaCount} write replicas broadcast to {PassiveReplicaCount} passive replicas.\n");
        
        var tasksChannel = Channel.CreateUnbounded<User>();
        
        // Create a separate channel for each passive replica to enable broadcasting patches.
        var replicaChannels = Enumerable.Range(0, PassiveReplicaCount)
            .Select(_ => Channel.CreateUnbounded<CrdtPatch>())
            .ToList();
        var replicaWriters = replicaChannels.Select(c => c.Writer).ToList();

        var producerTask = ProduceAsync(tasksChannel.Writer);

        var writeReplicaTasks = new List<Task>();
        for (var i = 0; i < WriteReplicaCount; i++)
        {
            var replicaId = $"write-replica-{i + 1}";
            // Each write replica will broadcast to all passive replicas.
            writeReplicaTasks.Add(WriteReplicaAsync(tasksChannel.Reader, replicaWriters, replicaId));
        }

        var passiveReplicaTasks = new List<Task>();
        for (var i = 0; i < PassiveReplicaCount; i++)
        {
            var replicaId = $"passive-replica-{i + 1}";
            // Each passive replica reads from its own dedicated channel.
            passiveReplicaTasks.Add(PassiveReplicaAsync(replicaChannels[i].Reader, replicaId));
        }

        await producerTask;
        await Task.WhenAll(writeReplicaTasks);
        
        // All write replicas are done, so we can complete all passive replica channels.
        foreach (var writer in replicaWriters)
        {
            writer.Complete();
        }
        
        await Task.WhenAll(passiveReplicaTasks);

        await VerifyConvergence();
    }

    private static async Task ProduceAsync(ChannelWriter<User> writer)
    {
        Console.WriteLine("-> Producer: Started. Generating items...");
        for (var i = 0; i < TotalItems; i++)
        {
            var user = new User(Guid.NewGuid(), Names[Random.Shared.Next(Names.Length)]);
            await writer.WriteAsync(user);
        }
        writer.Complete();
        Console.WriteLine("-> Producer: Finished. All items sent to write replicas.");
    }

    private async Task WriteReplicaAsync(ChannelReader<User> reader, IReadOnlyList<ChannelWriter<CrdtPatch>> writers, string replicaId)
    {
        using var scope = scopeFactory.CreateScope(replicaId);
        var patcher = scope.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        var applicator = scope.ServiceProvider.GetRequiredService<ICrdtApplicator>();

        Console.WriteLine($"  -> Write Replica '{replicaId}': Started.");
        var count = 0;
        await foreach (var user in reader.ReadAllAsync())
        {
#pragma warning disable CS0162 // Unreachable code detected
            // Under some number of items simulate latency
            if (TotalItems < 300)
            {
                await Task.Delay(Random.Shared.Next(1, 10));
            }
            else
            {
                await Task.Yield();
            }
#pragma warning restore CS0162 // Unreachable code detected

            // Each write replica operates on its own, consistent view of the data.
            // It reads its own state, computes a change, generates a patch, and then updates its own state.
            var (from, metadata) = await database.GetStateAsync<UserStats>(replicaId);
            var fromDoc = new CrdtDocument<UserStats>(from, metadata);
    
            // The 'to' state must be a modification of the 'from' state for the diffing logic to work correctly.
            // 1. Create a deep copy of the 'from' state to represent the new state.
            var to = new UserStats
            {
                ProcessedItemsCount = from.ProcessedItemsCount,
                UniqueUserNames = new List<string>(from.UniqueUserNames),
                LastProcessedUserName = from.LastProcessedUserName,
                LastProcessedTimestamp = from.LastProcessedTimestamp
            };
    
            // 2. Apply the intended changes to the 'to' state.
            to.ProcessedItemsCount++;
    
            if (!to.UniqueUserNames.Contains(user.Name))
            {
                to.UniqueUserNames.Add(user.Name);
            }
    
            to.LastProcessedUserName = user.Name;
            to.LastProcessedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    
            var toMeta = metadataManager.Initialize(to);
            var toDoc = new CrdtDocument<UserStats>(to, toMeta);
    
            // 3. Generate a patch based on the difference between the 'from' and 'to' states.
            var patch = patcher.GeneratePatch(fromDoc, toDoc);
    
            // 4. Update the write replica's own local state with the changes it just made.
            var newDocument = applicator.ApplyPatch(new CrdtDocument<UserStats>(from, metadata), patch);
    
            if (patch.Operations.Any())
            {
                metadataManager.AdvanceVersionVector(metadata, patch.Operations.First());
            }
    
            await database.SaveStateAsync(replicaId, newDocument, metadata);
    
            // 5. Broadcast the patch to all passive replicas.
            if (patch.Operations.Any())
            {
                var writeTasks = new List<Task>(writers.Count);
                foreach (var writer in writers)
                {
                    writeTasks.Add(writer.WriteAsync(patch).AsTask());
                }
                await Task.WhenAll(writeTasks);
            }
    
            ++count;
        }
        Console.WriteLine($"  -> Write Replica '{replicaId}': Finished. Items: {count}");
    }

    private async Task PassiveReplicaAsync(ChannelReader<CrdtPatch> reader, string replicaId)
    {
        using var scope = scopeFactory.CreateScope(replicaId);
        var applicator = scope.ServiceProvider.GetRequiredService<ICrdtApplicator>();

        Console.WriteLine($"    -> Passive Replica '{replicaId}': Started. Applying patches...");
        var count = 0;
        await foreach (var patch in reader.ReadAllAsync())
        {
#pragma warning disable CS0162 // Unreachable code detected
            // Under some number of items simulate latency
            if (TotalItems < 300)
            {
                await Task.Delay(Random.Shared.Next(1, 10));
            }
            else
            {
                await Task.Yield();
            }
#pragma warning restore CS0162 // Unreachable code detected


            var (document, metadata) = await database.GetStateAsync<UserStats>(replicaId);
            
            var newDocument = applicator.ApplyPatch(new CrdtDocument<UserStats>(document, metadata), patch);

            if(patch.Operations.Any())
            {
                metadataManager.AdvanceVersionVector(metadata, patch.Operations.First());
            }

            await database.SaveStateAsync(replicaId, newDocument, metadata);

            ++count;
        }
        Console.WriteLine($"    -> Passive Replica '{replicaId}': Finished. Count: {count}");
    }

    private async Task VerifyConvergence()
    {
        Console.WriteLine("\n🏁 --- Verification --- 🏁");
        var finalStates = new Dictionary<string, UserStats>();

        // Fetch the final state for each passive replica
        for (var i = 0; i < PassiveReplicaCount; i++)
        {
            var replicaId = $"passive-replica-{i + 1}";
            var (state, meta) = await database.GetStateAsync<UserStats>(replicaId);
            finalStates.Add(replicaId, state);
        }
        
        // There's nothing to compare if there are fewer than 2 replicas.
        if (finalStates.Count < 2)
        {
            Console.WriteLine("Verification skipped: At least two passive replicas are needed to compare states.");
        }
        else
        {
            var referenceStateEntry = finalStates.First();
            var referenceState = referenceStateEntry.Value;
            var overallConvergence = true;

            // Compare every other state against the first one
            foreach (var (replicaId, currentState) in finalStates.Skip(1))
            {
                var replicaConverged = CompareStates(referenceState, currentState, replicaId);
                if (!replicaConverged)
                {
                    overallConvergence = false;
                }
            }

            if (overallConvergence)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n✅ SUCCESS: All {PassiveReplicaCount} passive replicas reached the same state.");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n❌ FAILURE: Passive replicas have different final states. See details above.");
                Console.ResetColor();
            }
        }
        
        // Print stats from the first replica
        var finalState = finalStates.First().Value;
        var firstReplicaId = finalStates.First().Key;
        Console.WriteLine($"\n--- Final Stats (from '{firstReplicaId}') ---");
        Console.WriteLine($"Total processed items: {finalState.ProcessedItemsCount} (Expected: {TotalItems})");
        Console.WriteLine($"Total unique user names: {finalState.UniqueUserNames.Count}");
        Console.WriteLine($"Last processed user name: '{finalState.LastProcessedUserName}'");
        
        if (finalState.ProcessedItemsCount != TotalItems)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: Processed item count does not match total items.");
            Console.ResetColor();
        }
    }

    private static bool CompareStates(UserStats reference, UserStats current, string replicaId)
    {
        var converged = true;

        if (reference.ProcessedItemsCount != current.ProcessedItemsCount)
        {
            converged = false;
            ReportDivergence(replicaId, nameof(UserStats.ProcessedItemsCount), reference.ProcessedItemsCount, current.ProcessedItemsCount);
        }
        
        if (reference.LastProcessedUserName != current.LastProcessedUserName)
        {
            converged = false;
            ReportDivergence(replicaId, nameof(UserStats.LastProcessedUserName), reference.LastProcessedUserName, current.LastProcessedUserName);
        }
        
        if (reference.LastProcessedTimestamp != current.LastProcessedTimestamp)
        {
            converged = false;
            ReportDivergence(replicaId, nameof(UserStats.LastProcessedTimestamp), reference.LastProcessedTimestamp, current.LastProcessedTimestamp);
        }
        
        var referenceNames = new HashSet<string>(reference.UniqueUserNames);
        var currentNames = new HashSet<string>(current.UniqueUserNames);

        if (!referenceNames.SetEquals(currentNames))
        {
            converged = false;
            ReportDivergence(replicaId, "UniqueUserNames.Count", reference.UniqueUserNames.Count, current.UniqueUserNames.Count);
            
            var missing = referenceNames.Except(currentNames).ToList();
            var extra = currentNames.Except(referenceNames).ToList();
            if(missing.Any()) Console.WriteLine($"  - User names missing from '{replicaId}': {string.Join(", ", missing)}");
            if(extra.Any()) Console.WriteLine($"  - User names found only in '{replicaId}': {string.Join(", ", extra)}");
        }
        else if (!reference.UniqueUserNames.SequenceEqual(current.UniqueUserNames))
        {
            // For LSEQ or other ordered lists, the final order must be identical.
            converged = false;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ DIVERGENCE in '{replicaId}': Property '{nameof(UserStats.UniqueUserNames)}' lists have the same elements but different order.");
            Console.ResetColor();
        }
        
        return converged;
    }

    private static void ReportDivergence<T>(string replicaId, string property, T expected, T actual)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"❌ DIVERGENCE in '{replicaId}': Property '{property}' differs. Expected: '{expected}', Actual: '{actual}'.");
        Console.ResetColor();
    }
}