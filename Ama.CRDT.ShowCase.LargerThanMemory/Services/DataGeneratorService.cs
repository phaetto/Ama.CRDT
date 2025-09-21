namespace Ama.CRDT.ShowCase.LargerThanMemory.Services;

using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Partitioning;
using Ama.CRDT.ShowCase.LargerThanMemory.Models;
using Bogus;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public sealed class DataGeneratorService(
    IPartitionManager<BlogPost> partitionManager,
    ICrdtPatcher patcher,
    ICrdtMetadataManager metadataManager)
{
    private const int BlogPostCount = 1;
    private const int MinCommentsPerPost = 50000;
    private const int MaxCommentsPerPost = 100000;
    private const int BatchSize = 10;
    private static readonly DateTimeOffset NewestCommentDate = DateTimeOffset.UtcNow;

    public async Task GenerateDataAsync()
    {
        Console.WriteLine($"Generating {BlogPostCount} blog posts with {MinCommentsPerPost}-{MaxCommentsPerPost} comments each. This may take a while...");

        var blogPostFaker = new Faker<BlogPost>()
            .RuleFor(bp => bp.Id, f => Guid.NewGuid())
            .RuleFor(bp => bp.Title, f => f.Lorem.Sentence(5, 5))
            .RuleFor(bp => bp.Content, f => f.Lorem.Paragraphs(3));

        var commentFaker = new Faker<Comment>()
            .CustomInstantiator(f => new Comment(
                Guid.NewGuid(),
                f.Name.FullName(),
                f.Lorem.Paragraphs(1),
                default // Will be set sequentially to be older
            ));
        
        var random = new Random();

        for (int i = 0; i < BlogPostCount; i++)
        {
            var blogPost = blogPostFaker.Generate();
            blogPost.Comments = new Dictionary<DateTimeOffset, Comment>(); // Start with empty comments

            Console.WriteLine($"Generating post '{blogPost.Title}'...");
            await partitionManager.InitializeAsync(blogPost);

            // Get the initial document with its server-side generated metadata. This is cheap as the collection is empty.
            var crdtDocument = await partitionManager.GetHeaderPartitionContentAsync(blogPost.Id);
            var metadata = crdtDocument.Value.Metadata;

            var totalComments = random.Next(MinCommentsPerPost, MaxCommentsPerPost + 1);
            var commentsGenerated = 0;
            var currentCommentDate = NewestCommentDate;

            while (commentsGenerated < totalComments)
            {
                var currentBatchSize = Math.Min(BatchSize, totalComments - commentsGenerated);
                if (currentBatchSize <= 0) break;
                
                var commentsBatch = new Dictionary<DateTimeOffset, Comment>(currentBatchSize);
                for (var j = 0; j < currentBatchSize; j++)
                {
                    var comment = commentFaker.Generate();
                    currentCommentDate = currentCommentDate.AddMinutes(-random.Next(1, 60)); // Make each comment older than the previous one
                    commentsBatch.Add(currentCommentDate, comment with { CreatedAt = currentCommentDate });
                }

                // To generate a patch for additions, we can compare an empty dictionary with the new batch.
                // It is crucial to use the LATEST metadata for the patch generation.
                var fromDocument = new CrdtDocument<BlogPost>(
                    new BlogPost { Id = blogPost.Id, Comments = new Dictionary<DateTimeOffset, Comment>() },
                    metadata); // Use the single, evolving metadata object

                var toState = new BlogPost { Id = blogPost.Id, Comments = commentsBatch };
                
                var patch = patcher.GeneratePatch(fromDocument, toState);
                patch = patch with { LogicalKey = blogPost.Id };
                await partitionManager.ApplyPatchAsync(patch);

                // Manually advance the version vector in our local metadata copy to keep it in sync.
                foreach (var op in patch.Operations)
                {
                    metadataManager.AdvanceVersionVector(metadata, op);
                }

                commentsGenerated += currentBatchSize;
                Console.Write($"\r  - Added {commentsGenerated}/{totalComments} comments.");
            }
            Console.WriteLine();
        }

        Console.WriteLine("Data generation complete.");
    }
}