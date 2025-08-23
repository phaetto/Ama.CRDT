namespace Modern.CRDT.ShowCase;
using System.Linq;
using System.Threading.Channels;
using Modern.CRDT.Models;
using Modern.CRDT.Services;
using Modern.CRDT.ShowCase.Models;
using Modern.CRDT.ShowCase.Services;

/// <summary>
/// Orchestrates a distributed map-reduce simulation using concurrent producers, mappers, and convergers
/// communicating via channels to demonstrate CRDT convergence without locks.
/// </summary>
public sealed class SimulationRunner(
    ICrdtPatcherFactory patcherFactory,
    ICrdtApplicator applicator,
    IInMemoryDatabaseService database,
    ICrdtMetadataManager metadataManager)
{
    private const int TotalItems = 200;
    private const int MapperCount = 5;
    private const int ConvergerCount = 3;
    private static readonly string[] Names = ["Alice", "Bob", "Charlie", "David", "Eve", "Frank", "Grace", "Heidi", "Ivan", "Judy"];

    public async Task RunAsync()
    {
        Console.WriteLine($"Simulating {TotalItems} operations, broadcasting each from {MapperCount} mappers to {ConvergerCount} convergers.\n");
        
        var tasksChannel = Channel.CreateUnbounded<User>();
        
        // Create a separate channel for each converger to enable broadcasting patches.
        var convergerChannels = Enumerable.Range(0, ConvergerCount)
            .Select(_ => Channel.CreateUnbounded<CrdtPatch>())
            .ToList();
        var convergerWriters = convergerChannels.Select(c => c.Writer).ToList();

        var producerTask = ProduceAsync(tasksChannel.Writer);

        var mapperTasks = new List<Task>();
        for (var i = 0; i < MapperCount; i++)
        {
            var replicaId = $"mapper-{i + 1}";
            var patcher = patcherFactory.Create(replicaId);
            // Each mapper will broadcast to all convergers.
            mapperTasks.Add(MapAsync(tasksChannel.Reader, convergerWriters, patcher, replicaId));
        }

        var convergerTasks = new List<Task>();
        for (var i = 0; i < ConvergerCount; i++)
        {
            var replicaId = $"converger-{i + 1}";
            // Each converger reads from its own dedicated channel.
            convergerTasks.Add(ConvergeAsync(convergerChannels[i].Reader, replicaId));
        }

        await producerTask;
        await Task.WhenAll(mapperTasks);
        
        // All mappers are done, so we can complete all converger channels.
        foreach (var writer in convergerWriters)
        {
            writer.Complete();
        }
        
        await Task.WhenAll(convergerTasks);

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
        Console.WriteLine("-> Producer: Finished. All items sent to mappers.");
    }

    private async Task MapAsync(ChannelReader<User> reader, IReadOnlyList<ChannelWriter<CrdtPatch>> writers, ICrdtPatcher patcher, string replicaId)
    {
        Console.WriteLine($"  -> Mapper '{replicaId}': Started.");
        var count = 0;
        await foreach (var user in reader.ReadAllAsync())
        {
            await Task.Delay(Random.Shared.Next(1, 10));

            // A mapper generates a patch representing a single conceptual operation.
            // We do this by diffing an empty state against a state with just this one operation applied.
            var from = new UserStats(); 
            var fromDoc = new CrdtDocument<UserStats>(from);

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var to = new UserStats
            {
                ProcessedItemsCount = 1, // This will become an "increment by 1" operation
                UniqueUserNames = [user.Name],    // This becomes an "add user name" operation
                LastProcessedUserName = user.Name,
                LastProcessedTimestamp = now
            };

            // The 'to' document must contain metadata for its LWW properties
            // to allow the patcher to generate correct operations.
            var toMeta = new CrdtMetadata();
            metadataManager.InitializeLwwMetadata(toMeta, to);
            
            var toDoc = new CrdtDocument<UserStats>(to, toMeta);

            var patch = patcher.GeneratePatch(fromDoc, toDoc);
            
            // Broadcast the patch to all converger channels in parallel.
            var writeTasks = new List<Task>(writers.Count);
            foreach (var writer in writers)
            {
                writeTasks.Add(writer.WriteAsync(patch).AsTask());
            }
            await Task.WhenAll(writeTasks);

            ++count;
        }
        Console.WriteLine($"  -> Mapper '{replicaId}': Finished. Items: {count}");
    }

    private async Task ConvergeAsync(ChannelReader<CrdtPatch> reader, string replicaId)
    {
        Console.WriteLine($"    -> Converger '{replicaId}': Started. Applying patches...");
        var count = 0;
        await foreach (var patch in reader.ReadAllAsync())
        {
            // Simulate network latency and out-of-order arrival
            await Task.Delay(Random.Shared.Next(1, 5)); 

            var (document, metadata) = await database.GetStateAsync<UserStats>(replicaId);
            
            var newDocument = applicator.ApplyPatch(document, patch, metadata);

            metadataManager.AdvanceVersionVector(metadata, patch.Operations.First());

            await database.SaveStateAsync(replicaId, newDocument, metadata);

            ++count;
        }
        Console.WriteLine($"    -> Converger '{replicaId}': Finished. Count: {count}");
    }

    private async Task VerifyConvergence()
    {
        Console.WriteLine("\n🏁 --- Verification --- 🏁");
        var finalStates = new Dictionary<string, UserStats>();

        // Fetch the final state for each converger replica
        for (var i = 0; i < ConvergerCount; i++)
        {
            var replicaId = $"converger-{i + 1}";
            var (state, _) = await database.GetStateAsync<UserStats>(replicaId);
            finalStates.Add(replicaId, state);
        }
        
        // There's nothing to compare if there are fewer than 2 convergers.
        if (finalStates.Count < 2)
        {
            Console.WriteLine("Verification skipped: At least two convergers are needed to compare states.");
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
                Console.WriteLine($"\n✅ SUCCESS: All {ConvergerCount} convergers reached the same state.");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n❌ FAILURE: Convergers have different final states. See details above.");
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
        
        if (reference.UniqueUserNames.Count != current.UniqueUserNames.Count)
        {
            converged = false;
            ReportDivergence(replicaId, "UniqueUserNames.Count", reference.UniqueUserNames.Count, current.UniqueUserNames.Count);
        }
        else if (!reference.UniqueUserNames.SequenceEqual(current.UniqueUserNames))
        {
            converged = false;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ DIVERGENCE in '{replicaId}': Property '{nameof(UserStats.UniqueUserNames)}' lists have different content or order.");
            
            var referenceSet = new HashSet<string>(reference.UniqueUserNames);
            var currentSet = new HashSet<string>(current.UniqueUserNames);
            
            if (referenceSet.SetEquals(currentSet))
            {
                Console.WriteLine("  - Note: The sets of user names are identical; the divergence is in the ordering.");
            }
            else
            {
                var missing = referenceSet.Except(currentSet).ToList();
                var extra = currentSet.Except(referenceSet).ToList();
                if(missing.Any()) Console.WriteLine($"  - User names missing from '{replicaId}': {string.Join(", ", missing)}");
                if(extra.Any()) Console.WriteLine($"  - User names found only in '{replicaId}': {string.Join(", ", extra)}");
            }
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