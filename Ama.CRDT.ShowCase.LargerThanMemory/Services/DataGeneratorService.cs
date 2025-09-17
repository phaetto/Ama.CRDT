namespace Ama.CRDT.ShowCase.LargerThanMemory.Services;

using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Partitioning;
using Ama.CRDT.ShowCase.LargerThanMemory.Models;
using Bogus;

public sealed class DataGeneratorService(
    IPartitionManager<BlogPost> partitionManager,
    ReplicaContext replicaContext,
    FileSystemPartitionStreamProvider streamProvider,
    ICrdtPatcher patcher,
    ICrdtMetadataManager metadataManager)
{
    private const double MaxSizeGb = 0.05;
    private const long MaxSizeBytes = (long)(MaxSizeGb * 1024 * 1024 * 1024);

    public async Task GenerateDataAsync(Guid blogPostId)
    {
        var replicaPath = streamProvider.GetReplicaBasePath();
        if (Directory.Exists(replicaPath) && GetDirectorySize(replicaPath) > MaxSizeBytes)
        {
            Console.WriteLine($"Data for replica {replicaContext.ReplicaId} already exists and exceeds the size limit. Skipping generation.");
            return;
        }
        
        Console.WriteLine($"Generating data for replica {replicaContext.ReplicaId} up to {MaxSizeGb} GB. This may take a while...");

        var commentFaker = new Faker<Comment>()
            .CustomInstantiator(f => new Comment(
                Guid.NewGuid(),
                f.Name.FullName(),
                f.Lorem.Paragraphs(1),
                f.Date.PastOffset(1)
            ));

        long currentSize = 0;
        int batchCount = 0;

        var fromState = new BlogPost { Id = blogPostId };
        var fromDocument = new CrdtDocument<BlogPost>(fromState, metadataManager.Initialize(fromState));

        while (currentSize < MaxSizeBytes)
        {
            var toState = new BlogPost { Id = blogPostId };
            for (int i = 0; i < 100; i++) // Create batches of 100
            {
                var comment = commentFaker.Generate();
                toState.Comments.Add(comment.Id, comment);
            }

            var patch = patcher.GeneratePatch(fromDocument, toState);
            patch = patch with { LogicalKey = blogPostId };
            await partitionManager.ApplyPatchAsync(patch);
            
            batchCount++;

            if (batchCount % 10 == 0) // Check size every 10 batches
            {
                currentSize = GetDirectorySize(replicaPath);
                Console.WriteLine($"Generated {batchCount * 100} comments. Current size: {(double)currentSize / (1024 * 1024):F2} MB");
            }
        }

        Console.WriteLine("Data generation complete.");
    }

    private static long GetDirectorySize(string path)
    {
        if (!Directory.Exists(path))
        {
            return 0;
        }
        return new DirectoryInfo(path).GetFiles("*.*", SearchOption.AllDirectories).Sum(fi => fi.Length);
    }
}