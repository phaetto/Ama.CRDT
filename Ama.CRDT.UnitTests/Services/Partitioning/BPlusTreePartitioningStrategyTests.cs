namespace Ama.CRDT.UnitTests.Services.Partitioning;

using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Services.Partitioning;
using Ama.CRDT.Services.Partitioning.Serialization;
using Ama.CRDT.Services.Partitioning.Strategies;
using Shouldly;
using System.Collections.Concurrent;
using Xunit;

public sealed class BPlusTreePartitioningStrategyTests
{
    private readonly IndexDefaultSerializationHelper serializationHelper = new();

    private sealed class InMemoryPartitionStreamProvider : IPartitionStreamProvider
    {
        private readonly MemoryStream indexStream = new();
        private readonly ConcurrentDictionary<object, MemoryStream> dataStreams = new();

        public Task<Stream> GetIndexStreamAsync() => Task.FromResult<Stream>(indexStream);
    
        public Task<Stream> GetDataStreamAsync(object logicalKey)
        {
            var stream = dataStreams.GetOrAdd(logicalKey, _ => new MemoryStream());
            return Task.FromResult<Stream>(stream);
        }
        
        public Task<long> GetIndexStreamLengthAsync() => Task.FromResult(indexStream.Length);
    }

    [Fact]
    public async Task InitializeAsync_ShouldCreateValidHeaderAndRootNode()
    {
        // Arrange
        var streamProvider = new InMemoryPartitionStreamProvider();
        var strategy = new BPlusTreePartitioningStrategy(serializationHelper, streamProvider);
        var key = new CompositePartitionKey("A", 1);

        // Act
        await strategy.InitializeAsync();

        // Assert
        var indexLength = await streamProvider.GetIndexStreamLengthAsync();
        indexLength.ShouldBeGreaterThan(0);
        
        var result = await strategy.FindPartitionAsync(key);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task InitializeAsync_FromExistingStream_ShouldLoadAndFindCorrectly()
    {
        // Arrange
        var streamProvider = new InMemoryPartitionStreamProvider();
        
        // Phase 1: Create and populate the index
        var strategy1 = new BPlusTreePartitioningStrategy(serializationHelper, streamProvider);
        await strategy1.InitializeAsync();
        var p10 = new Partition(new CompositePartitionKey("A", 10), null, 1L, 1, 1L, 1);
        var p20 = new Partition(new CompositePartitionKey("A", 20), null, 2L, 2, 2L, 2);
        await strategy1.InsertPartitionAsync(p10);
        await strategy1.InsertPartitionAsync(p20);

        // Phase 2: Create a new strategy instance and initialize from the same stream
        var strategy2 = new BPlusTreePartitioningStrategy(serializationHelper, streamProvider);
        await strategy2.InitializeAsync();

        // Assert
        var foundP10 = await strategy2.FindPartitionAsync(new CompositePartitionKey("A", 15));
        foundP10.ShouldNotBeNull();
        foundP10.Value.ShouldBe(p10);

        var foundP20 = await strategy2.FindPartitionAsync(new CompositePartitionKey("A", 25));
        foundP20.ShouldNotBeNull();
        foundP20.Value.ShouldBe(p20);

        var notFound = await strategy2.FindPartitionAsync(new CompositePartitionKey("B", 1));
        notFound.ShouldBeNull();
    }

    [Fact]
    public async Task InsertAndFindAsync_SinglePartition_ShouldSucceed()
    {
        // Arrange
        var streamProvider = new InMemoryPartitionStreamProvider();
        var strategy = new BPlusTreePartitioningStrategy(serializationHelper, streamProvider);
        await strategy.InitializeAsync();
        var key = new CompositePartitionKey("tenant-a", 10);
        var partition = new Partition(key, null, 100L, 200, 300L, 100);

        // Act
        await strategy.InsertPartitionAsync(partition);

        // Assert
        var foundPartition = await strategy.FindPartitionAsync(new CompositePartitionKey("tenant-a", 15));
        foundPartition.ShouldNotBeNull();
        foundPartition.Value.ShouldBe(partition);

        var notFoundPartition = await strategy.FindPartitionAsync(new CompositePartitionKey("tenant-a", 5));
        notFoundPartition.ShouldBeNull();
    }
    
    [Fact]
    public async Task InsertAndFindAsync_WithCompositeKeys_ShouldSucceed()
    {
        // Arrange
        var streamProvider = new InMemoryPartitionStreamProvider();
        var strategy = new BPlusTreePartitioningStrategy(serializationHelper, streamProvider);
        await strategy.InitializeAsync();

        var pA_header = new Partition(new CompositePartitionKey("A", null), null, 1L, 1, 1L, 1);
        var pA_10 = new Partition(new CompositePartitionKey("A", 10), null, 2L, 2, 2L, 2);
        var pB_header = new Partition(new CompositePartitionKey("B", null), null, 3L, 3, 3L, 3);
        var pB_20 = new Partition(new CompositePartitionKey("B", 20), null, 4L, 4, 4L, 4);

        // Act
        await strategy.InsertPartitionAsync(pB_20);
        await strategy.InsertPartitionAsync(pA_header);
        await strategy.InsertPartitionAsync(pA_10);
        await strategy.InsertPartitionAsync(pB_header);
        
        // Assert
        (await strategy.FindPartitionAsync(new CompositePartitionKey("A", null)))!.Value.ShouldBe(pA_header);
        (await strategy.FindPartitionAsync(new CompositePartitionKey("A", 5)))!.Value.ShouldBe(pA_header);
        (await strategy.FindPartitionAsync(new CompositePartitionKey("A", 10)))!.Value.ShouldBe(pA_10);
        (await strategy.FindPartitionAsync(new CompositePartitionKey("A", 100)))!.Value.ShouldBe(pA_10);
        
        (await strategy.FindPartitionAsync(new CompositePartitionKey("B", null)))!.Value.ShouldBe(pB_header);
        (await strategy.FindPartitionAsync(new CompositePartitionKey("B", 15)))!.Value.ShouldBe(pB_header);
        (await strategy.FindPartitionAsync(new CompositePartitionKey("B", 20)))!.Value.ShouldBe(pB_20);
        (await strategy.FindPartitionAsync(new CompositePartitionKey("B", 50)))!.Value.ShouldBe(pB_20);
        
        (await strategy.FindPartitionAsync(new CompositePartitionKey("C", 1))).ShouldBeNull();
    }
    
    [Fact]
    public async Task InsertPartitionAsync_ShouldTriggerLeafSplit_AndFindShouldWork()
    {
        // Arrange
        const int degree = 8;
        const int splitSize = 2 * degree;
        var streamProvider = new InMemoryPartitionStreamProvider();
        var strategy = new BPlusTreePartitioningStrategy(serializationHelper, streamProvider);
        const string logicalKey = "A";
        await strategy.InitializeAsync();
        
        var partitions = Enumerable.Range(0, splitSize)
            .Select(i => new Partition(
                new CompositePartitionKey(logicalKey, i * 10),
                null,
                (long)i * 100, 10, (long)i * 100 + 50, 10))
            .ToList();

        // Act
        foreach (var p in partitions)
        {
            await strategy.InsertPartitionAsync(p);
        }

        // Assert
        (await strategy.FindPartitionAsync(new CompositePartitionKey(logicalKey, -5))).ShouldBeNull();
        (await strategy.FindPartitionAsync(new CompositePartitionKey(logicalKey, 5)))!.Value.ShouldBe(partitions[0]);
        (await strategy.FindPartitionAsync(new CompositePartitionKey(logicalKey, 10 * (splitSize - 1) + 5)))!.Value.ShouldBe(partitions[splitSize - 1]);
    }
    
    [Fact]
    public async Task UpdateAndFindAsync_WithCompositeKeys_ShouldReflectChanges()
    {
        // Arrange
        var streamProvider = new InMemoryPartitionStreamProvider();
        var strategy = new BPlusTreePartitioningStrategy(serializationHelper, streamProvider);
        const string logicalKey = "A";
        await strategy.InitializeAsync();
        
        var originalPartition = new Partition(new CompositePartitionKey(logicalKey, 10), null, 100L, 10, 110L, 5);
        await strategy.InsertPartitionAsync(originalPartition);

        // Act
        var updatedPartition = originalPartition with { DataLength = 999 };
        await strategy.UpdatePartitionAsync(updatedPartition);

        // Assert
        var foundPartition = await strategy.FindPartitionAsync(new CompositePartitionKey(logicalKey, 15));
        foundPartition.ShouldNotBeNull();
        foundPartition.Value.DataLength.ShouldBe(999);
        foundPartition.Value.ShouldBe(updatedPartition);
    }
    
    [Fact]
    public async Task DeleteAndFindAsync_SimpleDelete_ShouldSucceed()
    {
        // Arrange
        var streamProvider = new InMemoryPartitionStreamProvider();
        var strategy = new BPlusTreePartitioningStrategy(serializationHelper, streamProvider);
        const string logicalKey = "A";
        await strategy.InitializeAsync();

        var p10 = new Partition(new CompositePartitionKey(logicalKey, 10), null, 1L, 1, 1L, 1);
        var p20 = new Partition(new CompositePartitionKey(logicalKey, 20), null, 2L, 2, 2L, 2);
        var p30 = new Partition(new CompositePartitionKey(logicalKey, 30), null, 3L, 3, 3L, 3);
        await strategy.InsertPartitionAsync(p10);
        await strategy.InsertPartitionAsync(p20);
        await strategy.InsertPartitionAsync(p30);

        // Act
        await strategy.DeletePartitionAsync(p20);

        // Assert
        (await strategy.FindPartitionAsync(new CompositePartitionKey(logicalKey, 15)))!.Value.ShouldBe(p10);
        (await strategy.FindPartitionAsync(new CompositePartitionKey(logicalKey, 25)))!.Value.ShouldBe(p10);
        (await strategy.FindPartitionAsync(new CompositePartitionKey(logicalKey, 35)))!.Value.ShouldBe(p30);
    }
}