namespace Ama.CRDT.ShowCase.LargerThanMemory.Services;

using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Partitioning;
using Ama.CRDT.ShowCase.LargerThanMemory.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public sealed class DataGeneratorService(
    IPartitionManager<BlogPost> partitionManager,
    IAsyncCrdtApplicator crdtApplicator,
    ICrdtPatcher patcher,
    ICrdtMetadataManager metadataManager)
{
    private const int BlogPostCount = 10;
    private const int MinCommentsPerPost = 500;
    private const int MaxCommentsPerPost = 1000;
    private const int MinTagsPerPost = 20;
    private const int MaxTagsPerPost = 200;
    private const int BatchSize = 10;
    private static readonly DateTimeOffset NewestCommentDate = DateTimeOffset.UtcNow;

    public async Task GenerateDataAsync()
    {
        Console.WriteLine($"Generating {BlogPostCount} blog posts with {MinCommentsPerPost}-{MaxCommentsPerPost} comments each. This may take a while...");

        var random = new Random();
        var faker = new SimpleFaker();

        for (int i = 0; i < BlogPostCount; i++)
        {
            var blogPost = new BlogPost
            {
                Id = Guid.NewGuid(),
                Title = faker.LoremSentence(5),
                Content = faker.LoremParagraphs(3),
                Comments = new Dictionary<DateTimeOffset, Comment>(), // Start with empty comments
                Tags = new List<string>() // Start empty, will be seeded via patch to showcase strategy
            };

            Console.WriteLine($"Generating post '{blogPost.Title}'...");
            await partitionManager.InitializeAsync(blogPost);

            // Get the initial document with its server-side generated metadata. This is cheap as the collection is empty.
            var crdtDocument = await partitionManager.GetHeaderPartitionContentAsync(blogPost.Id);
            var metadata = crdtDocument.Value.Metadata;

            // Generate tags and create a patch to showcase Array LCS strategy
            var tags = faker.LoremWords(random.Next(MinTagsPerPost, MaxTagsPerPost));
            var fromDocForTags = new CrdtDocument<BlogPost>(
                new BlogPost { Id = blogPost.Id, Tags = new List<string>() },
                metadata);
            
            var tagsOperations = new List<CrdtOperation>(tags.Count);
            foreach (var tag in tags)
            {
                var op = patcher.GenerateOperation(fromDocForTags, x => x.Tags, new AddIntent(tag));
                metadataManager.AdvanceVersionVector(metadata!, op);
                tagsOperations.Add(op);
            }

            var tagsPatch = new CrdtPatch(tagsOperations);
            await crdtApplicator.ApplyPatchAsync(crdtDocument.Value, tagsPatch);

            var totalComments = random.Next(MinCommentsPerPost, MaxCommentsPerPost + 1);
            var commentsGenerated = 0;
            var currentCommentDate = NewestCommentDate;

            while (commentsGenerated < totalComments)
            {
                var currentBatchSize = Math.Min(BatchSize, totalComments - commentsGenerated);
                if (currentBatchSize <= 0) break;
                
                var fromDocument = new CrdtDocument<BlogPost>(
                    new BlogPost { Id = blogPost.Id, Comments = new Dictionary<DateTimeOffset, Comment>() },
                    metadata); // Use the single, evolving metadata object

                var operations = new List<CrdtOperation>(currentBatchSize);
                for (var j = 0; j < currentBatchSize; j++)
                {
                    currentCommentDate = currentCommentDate.AddMinutes(-random.Next(1, 60)); // Make each comment older than the previous one
                    
                    var comment = new Comment(
                        Guid.NewGuid(),
                        faker.FullName(),
                        faker.LoremParagraphs(1),
                        currentCommentDate
                    );
                    
                    var op = patcher.GenerateOperation(fromDocument, x => x.Comments, new MapSetIntent(comment.CreatedAt, comment));
                    metadataManager.AdvanceVersionVector(metadata!, op);
                    operations.Add(op);
                }
                
                var patch = new CrdtPatch(operations);
                await crdtApplicator.ApplyPatchAsync(crdtDocument.Value, patch);

                commentsGenerated += currentBatchSize;
                Console.Write($"\r  - Added {commentsGenerated}/{totalComments} comments.");
            }
            Console.WriteLine();
        }

        Console.WriteLine("Data generation complete.");
    }
}