namespace Ama.CRDT.UnitTests.Services.Partitioning;

using Ama.CRDT.Attributes;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Models.Serialization;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Partitioning;
using Ama.CRDT.Services.Providers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

public sealed class PartitionManagerTests
{
    private sealed class TestMapModel
    {
        [CrdtOrMapStrategy]
        public Dictionary<string, string> Items { get; set; } = new();
    }

    private sealed record TestInfrastructure(ICrdtScopeFactory ScopeFactory, IServiceProvider ServiceProvider);

    [Fact]
    public async Task InitializeAsync_ShouldCreateInitialPartitionAndWriteData()
    {
        // Arrange
        await using var dataStream = new MemoryStream();
        await using var indexStream = new MemoryStream();
        var infrastructure = CreateTestInfrastructure();
        using var scope = infrastructure.ScopeFactory.CreateScope("A");
        var manager = scope.ServiceProvider.GetRequiredService<IPartitionManager<TestMapModel>>();
        var initialObject = new TestMapModel { Items = { { "key1", "one" }, { "key2", "two" } } };

        // Act
        await manager.InitializeAsync(dataStream, indexStream, initialObject);

        // Assert
        dataStream.Length.ShouldBeGreaterThan(0);
        indexStream.Length.ShouldBeGreaterThan(0);

        var partitioningStrategy = scope.ServiceProvider.GetRequiredService<IPartitioningStrategy>();
        var partitionNode = await partitioningStrategy.FindPartitionAsync("key1");
        partitionNode.ShouldNotBeNull();
        partitionNode.Value.StartKey.ShouldBe("key1");
        partitionNode.Value.EndKey.ShouldBeNull();

        var doc = await ReadDocumentFromPartition<TestMapModel>(partitionNode.Value, dataStream);
        doc.Items.ShouldBe(new Dictionary<string, string> { { "key1", "one" }, { "key2", "two" } });
    }

    [Fact]
    public async Task ApplyPatchAsync_WithUpsert_ShouldUpdateCorrectPartition()
    {
        // Arrange
        await using var dataStream = new MemoryStream();
        await using var indexStream = new MemoryStream();
        var infrastructure = CreateTestInfrastructure();
        
        using var scope = infrastructure.ScopeFactory.CreateScope("A");
        var manager = scope.ServiceProvider.GetRequiredService<IPartitionManager<TestMapModel>>();
        var patcher = scope.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        var strategy = scope.ServiceProvider.GetRequiredService<IPartitioningStrategy>();
        var timestampProvider = scope.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();

        var initialObject = new TestMapModel { Items = { { "key1", "one" }, { "key2", "two" } } };
        await manager.InitializeAsync(dataStream, indexStream, initialObject);

        var modifiedObject = new TestMapModel { Items = { { "key1", "one" }, { "key2", "two" }, { "key3", "three" } } };
        
        var operations = new List<CrdtOperation>();
        var metadata = scope.ServiceProvider.GetRequiredService<ICrdtMetadataManager>().Initialize(initialObject);
        var context = new DifferentiateObjectContext(
            Path: "$",
            Type: typeof(TestMapModel),
            FromObj: initialObject,
            ToObj: modifiedObject,
            FromRoot: initialObject,
            ToRoot: modifiedObject,
            FromMeta: metadata,
            Operations: operations,
            ChangeTimestamp: timestampProvider.Now()
        );
        patcher.DifferentiateObject(context);
        var patch = new CrdtPatch { Operations = operations };
        
        // Act
        await manager.ApplyPatchAsync(patch);

        // Assert
        var partitionNode = await strategy.FindPartitionAsync("key1");
        partitionNode.ShouldNotBeNull();
        
        var updatedDoc = await ReadDocumentFromPartition<TestMapModel>(partitionNode.Value, dataStream);
        updatedDoc.Items.Count.ShouldBe(3);
        updatedDoc.Items.ShouldContainKey("key3");
        updatedDoc.Items["key3"].ShouldBe("three");
    }

    [Fact]
    public async Task ApplyPatchAsync_WhenPartitionExceedsMaxSize_ShouldSplitPartition()
    {
        // Arrange
        await using var dataStream = new MemoryStream();
        await using var indexStream = new MemoryStream();
        var infrastructure = CreateTestInfrastructure();
        
        using var scope = infrastructure.ScopeFactory.CreateScope("A");
        var manager = scope.ServiceProvider.GetRequiredService<IPartitionManager<TestMapModel>>();
        var patcher = scope.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        var strategy = scope.ServiceProvider.GetRequiredService<IPartitioningStrategy>();
        var timestampProvider = scope.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();

        var initialObject = new TestMapModel { Items = { { "a", "val-a" }, { "b", "val-b" }, { "c", "val-c" } } };
        await manager.InitializeAsync(dataStream, indexStream, initialObject);
        
        var largeString = new string('x', 4500);
        var modifiedObject = new TestMapModel
        {
            Items = new Dictionary<string, string> { { "a", "val-a" }, { "b", "val-b" }, { "c", "val-c" }, { "d", largeString }, { "e", largeString } }
        };

        var operations = new List<CrdtOperation>();
        var metadata = scope.ServiceProvider.GetRequiredService<ICrdtMetadataManager>().Initialize(initialObject);
        var context = new DifferentiateObjectContext(
            Path: "$",
            Type: typeof(TestMapModel),
            FromObj: initialObject,
            ToObj: modifiedObject,
            FromRoot: initialObject,
            ToRoot: modifiedObject,
            FromMeta: metadata,
            Operations: operations,
            ChangeTimestamp: timestampProvider.Now()
        );
        patcher.DifferentiateObject(context);
        var patch = new CrdtPatch { Operations = operations };
        
        // Act
        await manager.ApplyPatchAsync(patch);
        
        // Assert
        // Split should occur at key "c". Partition1: ["a", "b"], Partition2: ["c", "d", "e"]
        var partition1Node = await strategy.FindPartitionAsync("a");
        var partition2Node = await strategy.FindPartitionAsync("d");

        partition1Node.ShouldNotBeNull();
        partition2Node.ShouldNotBeNull();
        partition1Node.Value.StartKey.ShouldNotBe(partition2Node.Value.StartKey);

        var partition1 = partition1Node.Value;
        var partition2 = partition2Node.Value;

        partition1.StartKey.ShouldBe("a");
        partition1.EndKey.ShouldBe("c");

        partition2.StartKey.ShouldBe("c");
        partition2.EndKey.ShouldBeNull();
        
        var doc1 = await ReadDocumentFromPartition<TestMapModel>(partition1, dataStream);
        var doc2 = await ReadDocumentFromPartition<TestMapModel>(partition2, dataStream);

        doc1.Items.Keys.ShouldBe(new[] { "a", "b" });
        doc2.Items.Keys.ShouldBe(new[] { "c", "d", "e" });
    }

    [Fact]
    public async Task ApplyPatchAsync_WhenPartitionIsUnderMinSize_ShouldMergePartitions()
    {
        // Arrange: Start with two partitions
        await using var dataStream = new MemoryStream();
        await using var indexStream = new MemoryStream();
        var infrastructure = CreateTestInfrastructure();

        using var scope = infrastructure.ScopeFactory.CreateScope("A");
        var manager = scope.ServiceProvider.GetRequiredService<IPartitionManager<TestMapModel>>();
        var patcher = scope.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        var strategy = scope.ServiceProvider.GetRequiredService<IPartitioningStrategy>();
        var timestampProvider = scope.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();

        // 1. Create a large object to force a split
        var largeString = new string('x', 8191); // Guarantees a split
        var initialObject = new TestMapModel { Items = { { "a", "val-a" }, { "b", "val-b" } , { "c", largeString } } };
        await manager.InitializeAsync(dataStream, indexStream, initialObject);

        // Verify we have 2 partitions. For ["a", "b", "c"], split key is "b". p1={a}, p2={b,c}
        var p1_pre = await strategy.FindPartitionAsync("a");
        var p2_pre = await strategy.FindPartitionAsync("b");
        p1_pre.ShouldNotBeNull();
        p2_pre.ShouldNotBeNull();
        p1_pre.Value.StartKey.ShouldNotBe(p2_pre.Value.StartKey);

        // 2. Apply a patch to remove the large item, making partition 2 underfull
        var modifiedObject = new TestMapModel { Items = { { "a", "val-a" }, { "b", "val-b" } } };
        var operations = new List<CrdtOperation>();
        // To generate the patch, we need the metadata associated with the 'from' state (initialObject).
        var start_content = await manager.GetPartitionContentAsync("a");
        var metadata = scope.ServiceProvider.GetRequiredService<ICrdtMetadataManager>().Clone(start_content.Value.Metadata);
        var context = new DifferentiateObjectContext(
            Path: "$",
            Type: typeof(TestMapModel),
            FromObj: initialObject,
            ToObj: modifiedObject,
            FromRoot: initialObject,
            ToRoot: modifiedObject,
            FromMeta: metadata,
            Operations: operations,
            ChangeTimestamp: timestampProvider.Now()
        );
        patcher.DifferentiateObject(context);
        var patch = new CrdtPatch { Operations = operations };

        // Act
        await manager.ApplyPatchAsync(patch);

        // Assert: We should be back to a single partition
        var allPartitions = await strategy.GetAllPartitionsAsync();
        allPartitions.Count.ShouldBe(1);

        var final_p1 = await strategy.FindPartitionAsync("a");
        var final_p2 = await strategy.FindPartitionAsync("b"); // This should now resolve to the same partition as p1

        final_p1.ShouldNotBeNull();
        final_p2.ShouldNotBeNull();
        final_p1.Value.StartKey.ShouldBe(final_p2.Value.StartKey); // Both keys point to the same single partition
        final_p1.Value.StartKey.ShouldBe("a");
        final_p1.Value.EndKey.ShouldBeNull();
        
        var doc = await ReadDocumentFromPartition<TestMapModel>(final_p1.Value, dataStream);
        doc.Items.Count.ShouldBe(2);
        doc.Items.Keys.OrderBy(k => k).ShouldBe(new[] { "a", "b" });
    }

    [Fact]
    public async Task GetPartitionAsync_ShouldReturnCorrectPartitionForKey()
    {
        // Arrange
        await using var dataStream = new MemoryStream();
        await using var indexStream = new MemoryStream();
        var infrastructure = CreateTestInfrastructure();
        using var scope = infrastructure.ScopeFactory.CreateScope("A");
        var manager = scope.ServiceProvider.GetRequiredService<IPartitionManager<TestMapModel>>();
        var initialObject = new TestMapModel { Items = { { "key1", "one" }, { "key2", "two" } } };
        await manager.InitializeAsync(dataStream, indexStream, initialObject);

        // Act
        var partition = await manager.GetPartitionAsync("key2");

        // Assert
        partition.ShouldNotBeNull();
        partition.Value.StartKey.ShouldBe("key1");
        partition.Value.EndKey.ShouldBeNull();
    }

    [Fact]
    public async Task GetPartitionContentAsync_ShouldReturnCorrectContentForKey()
    {
        // Arrange
        await using var dataStream = new MemoryStream();
        await using var indexStream = new MemoryStream();
        var infrastructure = CreateTestInfrastructure();
        using var scope = infrastructure.ScopeFactory.CreateScope("A");
        var manager = scope.ServiceProvider.GetRequiredService<IPartitionManager<TestMapModel>>();
        var initialObject = new TestMapModel { Items = { { "key1", "one" }, { "key2", "two" } } };
        await manager.InitializeAsync(dataStream, indexStream, initialObject);

        // Act
        var content = await manager.GetPartitionContentAsync("key2");

        // Assert
        content.ShouldNotBeNull();
        var data = content.Value.Data.ShouldBeOfType<TestMapModel>();
        data.Items.Count.ShouldBe(2);
        data.Items["key1"].ShouldBe("one");
        data.Items["key2"].ShouldBe("two");
        content.Value.Metadata.ShouldNotBeNull();
        content.Value.Metadata.Lww.ShouldNotBeEmpty();
    }
    
    private TestInfrastructure CreateTestInfrastructure()
    {
        var services = new ServiceCollection()
            .AddCrdt(); 

        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        return new TestInfrastructure(scopeFactory, serviceProvider);
    }
    
    private async Task<T> ReadDocumentFromPartition<T>(Partition partition, Stream dataStream) where T : class
    {
        var buffer = new byte[partition.DataLength];
        dataStream.Seek(partition.DataOffset, SeekOrigin.Begin);
        await dataStream.ReadExactlyAsync(buffer);

        // The stream might have other data, so deserialize only the partition's buffer
        using var memStream = new MemoryStream(buffer);
        return (await JsonSerializer.DeserializeAsync<T>(memStream, CrdtJsonContext.DefaultOptions))!;
    }
}