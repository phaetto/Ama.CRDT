namespace Ama.CRDT.ShowCase.LargerThanMemory;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Partitioning;
using Ama.CRDT.ShowCase.LargerThanMemory.Models;
using Ama.CRDT.ShowCase.LargerThanMemory.Services;
using Microsoft.Extensions.DependencyInjection;

public sealed class SimulationRunner(IServiceProvider serviceProvider, ICrdtScopeFactory scopeFactory)
{
    private const int ReplicaCount = 3;

    public async Task RunAsync()
    {
        var replicaIds = Enumerable.Range(1, ReplicaCount).Select(i => $"replica-{i}").ToList();

        List<Guid> allBlogPostIds;
        using (var scope = scopeFactory.CreateScope(replicaIds.First()))
        {
            var partitionManager = scope.ServiceProvider.GetRequiredService<IPartitionManager<BlogPost>>();
            var keys = await partitionManager.GetAllLogicalKeysAsync();
            allBlogPostIds = keys.Cast<Guid>().ToList();
        }

        if (!allBlogPostIds.Any())
        {
            Console.WriteLine($"--- No existing data found. Initializing Replica 1 and Generating Data ---");
            using (var scope = scopeFactory.CreateScope(replicaIds.First()))
            {
                var dataGenerator = scope.ServiceProvider.GetRequiredService<DataGeneratorService>();
                await dataGenerator.GenerateDataAsync();
            }
            Console.WriteLine($"--- Data Generation for Replica 1 Complete ---");

            using (var scope = scopeFactory.CreateScope(replicaIds.First()))
            {
                var partitionManager = scope.ServiceProvider.GetRequiredService<IPartitionManager<BlogPost>>();
                var keys = await partitionManager.GetAllLogicalKeysAsync();
                allBlogPostIds = keys.Cast<Guid>().ToList();
            }

            Console.WriteLine($"--- Bootstrapping Other Replicas ---");
            var sourceDir = Path.Combine(Environment.CurrentDirectory, "data", replicaIds.First());
            foreach (var replicaId in replicaIds.Skip(1))
            {
                var destDir = Path.Combine(Environment.CurrentDirectory, "data", replicaId);
                CopyDirectory(sourceDir, destDir, true);
                Console.WriteLine($"Copied data from {replicaIds.First()} to {replicaId}");
            }
        }
        else
        {
            Console.WriteLine($"--- Found {allBlogPostIds.Count} existing blog post(s). Skipping data generation. ---");
        }

        Console.WriteLine($"--- Launching UI ---");
        var ui = new UiService(serviceProvider, replicaIds, allBlogPostIds);
        ui.Run();
    }
    
    private static void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
    {
        var dir = new DirectoryInfo(sourceDir);
        if (!dir.Exists)
            throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");
        
        DirectoryInfo[] dirs = dir.GetDirectories();
        Directory.CreateDirectory(destinationDir);
        
        foreach (FileInfo file in dir.GetFiles())
        {
            string targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath, true);
        }

        if (recursive)
        {
            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir, true);
            }
        }
    }
}