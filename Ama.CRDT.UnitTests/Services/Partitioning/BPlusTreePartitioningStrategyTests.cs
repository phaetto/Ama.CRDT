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
        var p10 = new DataPartition(new CompositePartitionKey("A", 10), null, 1L, 1, 1L, 1);
        var p20 = new DataPartition(new CompositePartitionKey("A", 20), null, 2L, 2, 2L, 2);
        await strategy1.InsertPartitionAsync(p10);
        await strategy1.InsertPartitionAsync(p20);

        // Phase 2: Create a new strategy instance and initialize from the same stream
        var strategy2 = new BPlusTreePartitioningStrategy(serializationHelper, streamProvider);
        await strategy2.InitializeAsync();

        // Assert
        var foundP10 = await strategy2.FindPartitionAsync(new CompositePartitionKey("A", 15));
        foundP10.ShouldNotBeNull();
        foundP10.ShouldBe(p10);

        var foundP20 = await strategy2.FindPartitionAsync(new CompositePartitionKey("A", 25));
        foundP20.ShouldNotBeNull();
        foundP20.ShouldBe(p20);

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
        var partition = new DataPartition(key, null, 100L, 200, 300L, 100);

        // Act
        await strategy.InsertPartitionAsync(partition);

        // Assert
        var foundPartition = await strategy.FindPartitionAsync(new CompositePartitionKey("tenant-a", 15));
        foundPartition.ShouldNotBeNull();
        foundPartition.ShouldBe(partition);

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

        var pA_header_key = new CompositePartitionKey("A", null);
        var pA_header = new HeaderPartition(pA_header_key, 1L, 1, 1L, 1);
        var pA_10 = new DataPartition(new CompositePartitionKey("A", 10), null, 2L, 2, 2L, 2);
        
        var pB_header_key = new CompositePartitionKey("B", null);
        var pB_header = new HeaderPartition(pB_header_key, 3L, 3, 3L, 3);
        var pB_20 = new DataPartition(new CompositePartitionKey("B", 20), null, 4L, 4, 4L, 4);

        // Act
        await strategy.InsertPartitionAsync(pB_20);
        await strategy.InsertPartitionAsync(pA_header);
        await strategy.InsertPartitionAsync(pA_10);
        await strategy.InsertPartitionAsync(pB_header);
        
        // Assert
        (await strategy.FindPartitionAsync(new CompositePartitionKey("A", null)))!.ShouldBe(pA_header);
        (await strategy.FindPartitionAsync(new CompositePartitionKey("A", 5)))!.ShouldBe(pA_header);
        (await strategy.FindPartitionAsync(new CompositePartitionKey("A", 10)))!.ShouldBe(pA_10);
        (await strategy.FindPartitionAsync(new CompositePartitionKey("A", 100)))!.ShouldBe(pA_10);
        
        (await strategy.FindPartitionAsync(new CompositePartitionKey("B", null)))!.ShouldBe(pB_header);
        (await strategy.FindPartitionAsync(new CompositePartitionKey("B", 15)))!.ShouldBe(pB_header);
        (await strategy.FindPartitionAsync(new CompositePartitionKey("B", 20)))!.ShouldBe(pB_20);
        (await strategy.FindPartitionAsync(new CompositePartitionKey("B", 50)))!.ShouldBe(pB_20);
        
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
            .Select(i => new DataPartition(
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
        (await strategy.FindPartitionAsync(new CompositePartitionKey(logicalKey, 5)))!.ShouldBe(partitions[0]);
        (await strategy.FindPartitionAsync(new CompositePartitionKey(logicalKey, 10 * (splitSize - 1) + 5)))!.ShouldBe(partitions[splitSize - 1]);
    }
    
    [Fact]
    public async Task UpdateAndFindAsync_WithCompositeKeys_ShouldReflectChanges()
    {
        // Arrange
        var streamProvider = new InMemoryPartitionStreamProvider();
        var strategy = new BPlusTreePartitioningStrategy(serializationHelper, streamProvider);
        const string logicalKey = "A";
        await strategy.InitializeAsync();
        
        var originalPartition = new DataPartition(new CompositePartitionKey(logicalKey, 10), null, 100L, 10, 110L, 5);
        await strategy.InsertPartitionAsync(originalPartition);

        // Act
        var updatedPartition = originalPartition with { DataLength = 999 };
        await strategy.UpdatePartitionAsync(updatedPartition);

        // Assert
        var foundPartition = await strategy.FindPartitionAsync(new CompositePartitionKey(logicalKey, 15));
        foundPartition.ShouldNotBeNull();
        foundPartition.ShouldBeOfType<DataPartition>().DataLength.ShouldBe(999);
        foundPartition.ShouldBe(updatedPartition);
    }
    
    [Fact]
    public async Task DeleteAndFindAsync_SimpleDelete_ShouldSucceed()
    {
        // Arrange
        var streamProvider = new InMemoryPartitionStreamProvider();
        var strategy = new BPlusTreePartitioningStrategy(serializationHelper, streamProvider);
        const string logicalKey = "A";
        await strategy.InitializeAsync();

        var p10 = new DataPartition(new CompositePartitionKey(logicalKey, 10), null, 1L, 1, 1L, 1);
        var p20 = new DataPartition(new CompositePartitionKey(logicalKey, 20), null, 2L, 2, 2L, 2);
        var p30 = new DataPartition(new CompositePartitionKey(logicalKey, 30), null, 3L, 3, 3L, 3);
        await strategy.InsertPartitionAsync(p10);
        await strategy.InsertPartitionAsync(p20);
        await strategy.InsertPartitionAsync(p30);

        // Act
        await strategy.DeletePartitionAsync(p20);

        // Assert
        (await strategy.FindPartitionAsync(new CompositePartitionKey(logicalKey, 15)))!.ShouldBe(p10);
        (await strategy.FindPartitionAsync(new CompositePartitionKey(logicalKey, 25)))!.ShouldBe(p10);
        (await strategy.FindPartitionAsync(new CompositePartitionKey(logicalKey, 35)))!.ShouldBe(p30);
    }

    [Fact]
    public async Task GetAllPartitionsAsync_ShouldReturnAllInsertedPartitions()
    {
        // Arrange
        var streamProvider = new InMemoryPartitionStreamProvider();
        var strategy = new BPlusTreePartitioningStrategy(serializationHelper, streamProvider);
        await strategy.InitializeAsync();
        
        var header_key = new CompositePartitionKey("A", null);
        var header = new HeaderPartition(header_key, 1L, 1, 1L, 1);
        var data1 = new DataPartition(new CompositePartitionKey("A", "key1"), null, 2L, 2, 2L, 2);
        var data2 = new DataPartition(new CompositePartitionKey("A", "key5"), null, 3L, 3, 3L, 3);
        var otherTenant = new DataPartition(new CompositePartitionKey("B", "key1"), null, 4L, 4, 4L, 4);
        
        await strategy.InsertPartitionAsync(header);
        await strategy.InsertPartitionAsync(data1);
        await strategy.InsertPartitionAsync(data2);
        await strategy.InsertPartitionAsync(otherTenant);

        // Act
        var allPartitions = await strategy.GetAllPartitionsAsync();

        // Assert
        allPartitions.Count.ShouldBe(4);
        allPartitions.ShouldContain(header);
        allPartitions.ShouldContain(data1);
        allPartitions.ShouldContain(data2);
        allPartitions.ShouldContain(otherTenant);
    }
    
    [Fact(Skip = "This is a long running test")]
    public async Task StressTest_AfterManyOperations_ShouldMaintainIntegrity()
    {
        // Arrange
        var streamProvider = new InMemoryPartitionStreamProvider();
        var strategy = new BPlusTreePartitioningStrategy(serializationHelper, streamProvider);
        const string logicalKey = "StressTest";
        await strategy.InitializeAsync();
        
        var random = new Random(42); // Seed for reproducibility
        var expectedPartitions = new Dictionary<CompositePartitionKey, IPartition>();
        const int initialCount = 2000;
        const int deleteCount = 1000;
        const int reinsertCount = 500;

        // Act & Assert Phase 1: Initial Bulk Insertion
        var initialPartitions = Enumerable.Range(0, initialCount)
            .Select(i => new DataPartition(
                new CompositePartitionKey(logicalKey, i),
                null, (long)i, i, (long)i, i))
            .Cast<IPartition>();
        
        foreach (var p in initialPartitions)
        {
            await strategy.InsertPartitionAsync(p);
            expectedPartitions.Add(p.GetPartitionKey(), p);
        }

        var allPartitionsAfterInsert = await strategy.GetAllPartitionsAsync();
        allPartitionsAfterInsert.Count.ShouldBe(initialCount);
        foreach (var p in expectedPartitions.Values)
        {
            var found = await strategy.FindPartitionAsync(p.GetPartitionKey());
            found.ShouldNotBeNull();
            found.ShouldBe(p);
        }

        // Act & Assert Phase 2: Random Deletions
        var partitionsToDelete = expectedPartitions.Values.OrderBy(_ => random.Next()).Take(deleteCount).ToList();
        
        foreach (var p in partitionsToDelete)
        {
            await strategy.DeletePartitionAsync(p);
            expectedPartitions.Remove(p.GetPartitionKey());
        }

        var allPartitionsAfterDelete = await strategy.GetAllPartitionsAsync();
        allPartitionsAfterDelete.Count.ShouldBe(initialCount - deleteCount);
        
        // Verify remaining partitions are findable
        foreach (var p in expectedPartitions.Values)
        {
            var found = await strategy.FindPartitionAsync(p.GetPartitionKey());
            found.ShouldNotBeNull($"Partition with key {p.GetPartitionKey()} should be found but was not.");
            found.ShouldBe(p);
        }
        
        // Verify deleted partitions are not findable via floor search
        foreach (var p in partitionsToDelete)
        {
            var key = p.GetPartitionKey();
            var found = await strategy.FindPartitionAsync(key);

            var previousPartition = expectedPartitions.Values
                .Where(ep => Comparer<object>.Default.Compare(ep.GetPartitionKey(), key) < 0)
                .OrderByDescending(ep => ep.GetPartitionKey())
                .FirstOrDefault();

            if (previousPartition != null)
            {
                found.ShouldBe(previousPartition, $"Searching for deleted key {key} should have returned previous partition {previousPartition.GetPartitionKey()}, but returned {found?.GetPartitionKey()}");
            }
            else
            {
                found.ShouldBeNull($"Searching for deleted key {key} should have returned null as it was the first, but returned {found?.GetPartitionKey()}");
            }
        }

        // Act & Assert Phase 3: Re-insertion and New Insertions
        var partitionsToReinsert = Enumerable.Range(initialCount, reinsertCount)
             .Select(i => new DataPartition(
                new CompositePartitionKey(logicalKey, i),
                null, (long)i, i, (long)i, i))
            .Cast<IPartition>();
            
        foreach (var p in partitionsToReinsert)
        {
            await strategy.InsertPartitionAsync(p);
            expectedPartitions.Add(p.GetPartitionKey(), p);
        }
        
        // Final Verification
        var finalPartitions = (await strategy.GetAllPartitionsAsync()).OrderBy(p => p.GetPartitionKey()).ToList();
        var expectedFinalPartitions = expectedPartitions.Values.OrderBy(p => p.GetPartitionKey()).ToList();
        
        finalPartitions.Count.ShouldBe(expectedFinalPartitions.Count);
        finalPartitions.ShouldBe(expectedFinalPartitions, ignoreOrder: false);
    }
}