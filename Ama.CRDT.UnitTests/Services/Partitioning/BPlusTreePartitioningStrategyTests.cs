namespace Ama.CRDT.UnitTests.Services.Partitioning;

using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Services.Metrics;
using Ama.CRDT.Services.Partitioning;
using Ama.CRDT.Services.Partitioning.Serialization;
using Ama.CRDT.Services.Partitioning.Strategies;
using Moq;
using Shouldly;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

public sealed class BPlusTreePartitioningStrategyTests
{
    private readonly DefaultPartitionSerializationService serializationService = new();
    private readonly BPlusTreeCrdtMetrics metrics;
    private readonly BPlusTreePartitioningStrategy strategy;
    private readonly InMemoryPartitionStreamProvider streamProvider;
    private const string PropertyName = "items";
    private const string LogicalKey = "doc1";
    // Default B-Tree degree is 3. A node splits when it gets its 6th key.
    // A node needs rebalancing (merge/borrow) if it has 1 key.
    private const int Degree = 3; 
    private const int MaxKeys = 2 * Degree - 1; // 5

    public BPlusTreePartitioningStrategyTests()
    {
        var meterFactoryMock = new Mock<IMeterFactory>();
        var meter = new Meter("TestMeterForBPlusTree");
        meterFactoryMock.Setup(f => f.Create(It.IsAny<MeterOptions>())).Returns(meter);
        metrics = new BPlusTreeCrdtMetrics(meterFactoryMock.Object);
        streamProvider = new InMemoryPartitionStreamProvider();
        strategy = new BPlusTreePartitioningStrategy(serializationService, streamProvider, metrics);
    }

    private sealed class InMemoryPartitionStreamProvider : IPartitionStreamProvider
    {
        private readonly ConcurrentDictionary<string, MemoryStream> streams = new();
        private const string HeaderIdentifier = "@header";

        public Task<Stream> GetPropertyIndexStreamAsync(string propertyName) =>
            Task.FromResult<Stream>(streams.GetOrAdd($"index_{propertyName}", _ => new MemoryStream()));

        public Task<Stream> GetPropertyDataStreamAsync(IComparable logicalKey, string propertyName) =>
            Task.FromResult<Stream>(streams.GetOrAdd($"data_{logicalKey}_{propertyName}", _ => new MemoryStream()));

        public Task<Stream> GetHeaderIndexStreamAsync() =>
            Task.FromResult<Stream>(streams.GetOrAdd("index_header", _ => new MemoryStream()));
        
        public Task<Stream> GetHeaderDataStreamAsync(IComparable logicalKey) =>
            Task.FromResult<Stream>(streams.GetOrAdd($"data_{logicalKey}_header", _ => new MemoryStream()));

        public Task<long> GetIndexStreamLengthAsync(string propertyOrHeader)
        {
            streams.TryGetValue(propertyOrHeader == HeaderIdentifier ? "index_header" : $"index_{propertyOrHeader}", out var stream);
            return Task.FromResult(stream?.Length ?? 0);
        }

        public byte[]? GetIndexStreamBytes(string propertyOrHeader)
        {
            var key = propertyOrHeader == HeaderIdentifier ? "index_header" : $"index_{propertyOrHeader}";
            return streams.TryGetValue(key, out var stream) ? stream.ToArray() : null;
        }
    }

    [Fact]
    public async Task InitializeAsync_ShouldCreateValidHeaderAndRootNode()
    {
        // Act
        await strategy.InitializePropertyIndexAsync(PropertyName);

        // Assert
        var indexLength = await streamProvider.GetIndexStreamLengthAsync(PropertyName);
        indexLength.ShouldBeGreaterThan(0);
        
        var result = await strategy.FindPropertyPartitionAsync(new CompositePartitionKey(LogicalKey, 1), PropertyName);
        result.ShouldBeNull();
    }
    
    [Fact]
    public async Task InitializeAsync_OnExistingStream_ShouldNotOverwrite()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        var p1 = new DataPartition(new CompositePartitionKey(LogicalKey, 1), null, 1, 1, 1, 1);
        await strategy.InsertPropertyPartitionAsync(p1, PropertyName);
        var streamBytesBefore = streamProvider.GetIndexStreamBytes(PropertyName);

        // Act
        await strategy.InitializePropertyIndexAsync(PropertyName); // Should be a no-op

        // Assert
        var streamBytesAfter = streamProvider.GetIndexStreamBytes(PropertyName);
        streamBytesAfter.ShouldBe(streamBytesBefore);

        var found = await strategy.FindPropertyPartitionAsync(new CompositePartitionKey(LogicalKey, 1), PropertyName);
        found.ShouldBe(p1);
    }
    
    [Fact]
    public async Task InsertAndFindAsync_WithSingleItem_ShouldSucceed()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        var partition = new DataPartition(new CompositePartitionKey(LogicalKey, 10), null, 1L, 1, 1L, 1);
        
        // Act
        await strategy.InsertPropertyPartitionAsync(partition, PropertyName);
        var found = await strategy.FindPropertyPartitionAsync(new CompositePartitionKey(LogicalKey, 10), PropertyName);

        // Assert
        found.ShouldNotBeNull();
        found.ShouldBe(partition);
    }

    [Fact]
    public async Task InsertAndFindAsync_CausingRootSplit_ShouldMaintainCorrectness()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        var partitions = new List<IPartition>();
        for (var i = 1; i <= MaxKeys + 1; i++)
        {
            partitions.Add(new DataPartition(new CompositePartitionKey(LogicalKey, i * 10), null, (long)i, i, (long)i, i));
        }

        // Act
        foreach (var p in partitions)
        {
            await strategy.InsertPropertyPartitionAsync(p, PropertyName);
        }

        // Assert
        foreach (var p in partitions)
        {
            var found = await strategy.FindPropertyPartitionAsync(p.GetPartitionKey(), PropertyName);
            found.ShouldBe(p);
        }
        var allPartitions = await ToListAsync(strategy.GetAllPropertyPartitionsAsync(PropertyName));
        allPartitions.Count.ShouldBe(MaxKeys + 1);
    }

    [Fact]
    public async Task UpdatePartitionAsync_ShouldModifyExistingPartition()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        var originalKey = new CompositePartitionKey(LogicalKey, 10);
        var originalPartition = new DataPartition(originalKey, null, 1L, 1, 1L, 1);
        await strategy.InsertPropertyPartitionAsync(originalPartition, PropertyName);
        
        var updatedPartition = new DataPartition(originalKey, null, 99L, 99, 99L, 99);

        // Act
        await strategy.UpdatePropertyPartitionAsync(updatedPartition, PropertyName);

        // Assert
        var found = await strategy.FindPropertyPartitionAsync(originalKey, PropertyName);
        found.ShouldNotBeNull();
        found.ShouldBe(updatedPartition);
        found.ShouldNotBe(originalPartition);
    }

    [Fact]
    public async Task UpdatePartitionAsync_OnNonExistentPartition_ShouldThrowKeyNotFoundException()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        var p1 = new DataPartition(new CompositePartitionKey(LogicalKey, 10), null, 1L, 1, 1L, 1);
        var p2 = new DataPartition(new CompositePartitionKey(LogicalKey, 20), null, 2L, 2, 2L, 2);
        await strategy.InsertPropertyPartitionAsync(p1, PropertyName);

        // Act & Assert
        await Should.ThrowAsync<KeyNotFoundException>(async () => await strategy.UpdatePropertyPartitionAsync(p2, PropertyName));
    }

    [Fact]
    public async Task DeletePartitionAsync_FromLeaf_ShouldSucceed()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        var p1 = new DataPartition(new CompositePartitionKey(LogicalKey, 10), null, 1L, 1, 1L, 1);
        var p2 = new DataPartition(new CompositePartitionKey(LogicalKey, 20), null, 2L, 2, 2L, 2);
        await strategy.InsertPropertyPartitionAsync(p1, PropertyName);
        await strategy.InsertPropertyPartitionAsync(p2, PropertyName);

        // Act
        await strategy.DeletePropertyPartitionAsync(p1, PropertyName);

        // Assert
        (await strategy.FindPropertyPartitionAsync(p1.GetPartitionKey(), PropertyName)).ShouldBeNull();
        (await strategy.FindPropertyPartitionAsync(p2.GetPartitionKey(), PropertyName)).ShouldBe(p2);
        (await strategy.GetPropertyPartitionCountAsync(PropertyName)).ShouldBe(1);
    }

    [Fact]
    public async Task DeletePartitionAsync_CausingMerge_ShouldMaintainCorrectness()
    {
        // Arrange: Insert just enough to have two leaf nodes after a split, then delete to cause a merge
        await strategy.InitializePropertyIndexAsync(PropertyName);
        
        var p0 = new DataPartition(new CompositePartitionKey(LogicalKey, 0), null, 0,0,0,0);
        var p1 = new DataPartition(new CompositePartitionKey(LogicalKey, 1), null, 1,1,1,1);
        var p2 = new DataPartition(new CompositePartitionKey(LogicalKey, 2), null, 2,2,2,2);
        var p3 = new DataPartition(new CompositePartitionKey(LogicalKey, 3), null, 3,3,3,3);
        var p4 = new DataPartition(new CompositePartitionKey(LogicalKey, 4), null, 4,4,4,4);
        var p5 = new DataPartition(new CompositePartitionKey(LogicalKey, 5), null, 5,5,5,5);
        await strategy.InsertPropertyPartitionAsync(p0, PropertyName);
        await strategy.InsertPropertyPartitionAsync(p1, PropertyName);
        await strategy.InsertPropertyPartitionAsync(p2, PropertyName);
        await strategy.InsertPropertyPartitionAsync(p3, PropertyName);
        await strategy.InsertPropertyPartitionAsync(p4, PropertyName);
        await strategy.InsertPropertyPartitionAsync(p5, PropertyName); // Split occurs here
        // Root: [p2]
        // Leaves: [p0, p1] <-> [p2, p3, p4, p5]
        
        await strategy.DeletePropertyPartitionAsync(p3, PropertyName);
        await strategy.DeletePropertyPartitionAsync(p4, PropertyName);
        await strategy.DeletePropertyPartitionAsync(p5, PropertyName);
        // Right leaf now has [p2]. Keys < t-1=2. Must borrow or merge.
        // Left leaf has [p0, p1]. It cannot lend. They must merge.
        // Resulting tree should have partitions [p0, p1, p2].
        // Then we delete p1. Final result should be [p0, p2].

        // Act
        await strategy.DeletePropertyPartitionAsync(p1, PropertyName);
        
        // Assert
        var remaining = await ToListAsync(strategy.GetAllPropertyPartitionsAsync(PropertyName));
        var expected = new List<IPartition> { p0, p2 };

        remaining.Count.ShouldBe(expected.Count);
        remaining.ShouldBe(expected, ignoreOrder: true);
        (await strategy.GetPropertyPartitionCountAsync(PropertyName)).ShouldBe(2);
    }
    
    [Fact]
    public async Task DeletePartitionAsync_LastItem_ShouldLeaveEmptyTree()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        var p1 = new DataPartition(new CompositePartitionKey(LogicalKey, 10), null, 1L, 1, 1L, 1);
        await strategy.InsertPropertyPartitionAsync(p1, PropertyName);

        // Act
        await strategy.DeletePropertyPartitionAsync(p1, PropertyName);
        
        // Assert
        (await strategy.GetPropertyPartitionCountAsync(PropertyName)).ShouldBe(0);
        var all = await ToListAsync(strategy.GetAllPropertyPartitionsAsync(PropertyName));
        all.ShouldBeEmpty();
    }
    
    [Fact]
    public async Task DeletePartitionAsync_OnNonExistentPartition_ShouldThrowKeyNotFoundException()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        var p1 = new DataPartition(new CompositePartitionKey(LogicalKey, 10), null, 1L, 1, 1L, 1);

        // Act & Assert
        await Should.ThrowAsync<KeyNotFoundException>(async () => await strategy.DeletePropertyPartitionAsync(p1, PropertyName));
    }
    
    [Fact]
    public async Task DeletePartitionAsync_DistinguishingHeaderAndDataPartitions_ShouldSucceed()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        var key = new CompositePartitionKey(LogicalKey, null);
        var header = new HeaderPartition(key, 1,1,1,1);
        var data = new DataPartition(key, null, 2,2,2,2); // Note: DataPartition should not have null range key, but for testing...

        await strategy.InsertPropertyPartitionAsync(header, PropertyName);
        await strategy.InsertPropertyPartitionAsync(data, PropertyName);

        // Act
        await strategy.DeletePropertyPartitionAsync(header, PropertyName);

        // Assert
        (await strategy.GetPropertyPartitionCountAsync(PropertyName)).ShouldBe(1);
        var found = await strategy.FindPropertyPartitionAsync(key, PropertyName);
        // Find is ambiguous here. Let's check GetAll.
        var all = await ToListAsync(strategy.GetAllPropertyPartitionsAsync(PropertyName));
        all.Count.ShouldBe(1);
        all[0].ShouldBe(data);
    }

    [Fact]
    public async Task GetAllPartitionsAsync_OnEmptyTree_ShouldReturnEmpty()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);

        // Act
        var partitions = await ToListAsync(strategy.GetAllPropertyPartitionsAsync(PropertyName));

        // Assert
        partitions.ShouldBeEmpty();
    }
    
    [Fact]
    public async Task GetAllPartitionsAsync_WithLogicalKeyFilter_ShouldReturnFilteredPartitions()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        var p1_1 = new DataPartition(new CompositePartitionKey("doc1", 10), null, 1, 1, 1, 1);
        var p1_2 = new DataPartition(new CompositePartitionKey("doc1", 20), null, 2, 2, 2, 2);
        var p2_1 = new DataPartition(new CompositePartitionKey("doc2", 15), null, 3, 3, 3, 3);
        var p3_1 = new DataPartition(new CompositePartitionKey("doc3", 5), null, 4, 4, 4, 4);
        await strategy.InsertPropertyPartitionAsync(p1_1, PropertyName);
        await strategy.InsertPropertyPartitionAsync(p2_1, PropertyName);
        await strategy.InsertPropertyPartitionAsync(p1_2, PropertyName);
        await strategy.InsertPropertyPartitionAsync(p3_1, PropertyName);
        
        // Act
        var doc1Partitions = await ToListAsync(strategy.GetAllPropertyPartitionsAsync(PropertyName, "doc1"));
        var doc2Partitions = await ToListAsync(strategy.GetAllPropertyPartitionsAsync(PropertyName, "doc2"));
        var allPartitions = await ToListAsync(strategy.GetAllPropertyPartitionsAsync(PropertyName));

        // Assert
        doc1Partitions.ShouldBe([p1_1, p1_2], ignoreOrder: true);
        doc2Partitions.ShouldBe([p2_1]);
        allPartitions.Count.ShouldBe(4);
    }

    [Fact]
    public async Task GetPartitionCountAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        (await strategy.GetPropertyPartitionCountAsync(PropertyName)).ShouldBe(0);
        await InsertRangeAsync(LogicalKey, 0, 100);
        await InsertRangeAsync("other_key", 0, 50);
        await InsertRangeAsync("other_other_key", 0, 150);

        // Act
        var totalCount = await strategy.GetPropertyPartitionCountAsync(PropertyName);
        var logicalKeyCount = await strategy.GetPropertyPartitionCountAsync(PropertyName, LogicalKey);
        var otherKeyCount = await strategy.GetPropertyPartitionCountAsync(PropertyName, "other_key");
        var otherOtherKeyCount = await strategy.GetPropertyPartitionCountAsync(PropertyName, "other_other_key");

        // Assert
        totalCount.ShouldBe(300);
        logicalKeyCount.ShouldBe(100);
        otherKeyCount.ShouldBe(50);
        otherOtherKeyCount.ShouldBe(150);
    }

    #region Insert and Traversal Integrity Tests

    [Fact]
    public async Task Insert_AfterManySplits_ShouldAllowFullTraversal()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        const int partitionCount = 500;
        var expectedPartitions = GeneratePartitions(LogicalKey, 0, partitionCount);

        foreach (var p in expectedPartitions)
        {
            await strategy.InsertPropertyPartitionAsync(p, PropertyName);
        }

        // Act
        var actualPartitions = await ToListAsync(strategy.GetAllPropertyPartitionsAsync(PropertyName));
        
        // Assert
        actualPartitions.Count.ShouldBe(partitionCount);
        actualPartitions.ShouldBe(expectedPartitions, ignoreOrder: false);
    }

    #endregion

    #region GetAllPartitionsInternalAsync Tests

    [Fact]
    public async Task GetAllPartitionsInternalAsync_WithNonExistentLogicalKey_ShouldReturnEmpty()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        await InsertRangeAsync(LogicalKey, 0, 10);

        // Act
        var streamGetter = () => streamProvider.GetPropertyIndexStreamAsync(PropertyName);
        var partitions = await ToListAsync(strategy.GetAllPartitionsInternalAsync(PropertyName, streamGetter, "non_existent_key"));

        // Assert
        partitions.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAllPartitionsInternalAsync_SpanningMultipleLeafNodes_ShouldReturnAllPartitions()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        // Insert enough partitions for 'doc2' to cause multiple splits and span leaf nodes.
        await InsertRangeAsync("doc1", 0, 3);
        var doc2_partitions = await InsertRangeAsync("doc2", 0, 20);
        await InsertRangeAsync("doc3", 0, 3);

        // Act
        var streamGetter = () => streamProvider.GetPropertyIndexStreamAsync(PropertyName);
        var result = await ToListAsync(strategy.GetAllPartitionsInternalAsync(PropertyName, streamGetter, "doc2"));
        
        // Assert
        result.Count.ShouldBe(20);
        result.ShouldBe(doc2_partitions, ignoreOrder: true);
    }

    [Fact]
    public async Task GetAllPartitionsInternalAsync_TargetKeyIsFirstAlphabetically_ShouldSucceed()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        var docA_partitions = await InsertRangeAsync("A", 0, 5);
        await InsertRangeAsync("B", 0, 5);

        // Act
        var streamGetter = () => streamProvider.GetPropertyIndexStreamAsync(PropertyName);
        var result = await ToListAsync(strategy.GetAllPartitionsInternalAsync(PropertyName, streamGetter, "A"));

        // Assert
        result.Count.ShouldBe(5);
        result.ShouldBe(docA_partitions, ignoreOrder: true);
    }

    [Fact]
    public async Task GetAllPartitionsInternalAsync_TargetKeyIsLastAlphabetically_ShouldSucceed()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        await InsertRangeAsync("A", 0, 5);
        var docB_partitions = await InsertRangeAsync("B", 0, 5);

        // Act
        var streamGetter = () => streamProvider.GetPropertyIndexStreamAsync(PropertyName);
        var result = await ToListAsync(strategy.GetAllPartitionsInternalAsync(PropertyName, streamGetter, "B"));

        // Assert
        result.Count.ShouldBe(5);
        result.ShouldBe(docB_partitions, ignoreOrder: true);
    }

    [Fact]
    public async Task GetAllPartitionsInternalAsync_FilterDoesNotYieldPartitionsFromOtherKeys()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        var partitionsToInsert = new List<IPartition>();
        partitionsToInsert.AddRange(GeneratePartitions("doc2", 0, 5));
        partitionsToInsert.AddRange(GeneratePartitions("doc1", 0, 5));
        partitionsToInsert.AddRange(GeneratePartitions("doc3", 0, 5));
        
        // Insert in shuffled order to test B-Tree's sorting
        var random = new Random();
        foreach (var p in partitionsToInsert.OrderBy(x => random.Next()))
        {
            await strategy.InsertPropertyPartitionAsync(p, PropertyName);
        }

        // Act
        var streamGetter = () => streamProvider.GetPropertyIndexStreamAsync(PropertyName);
        var doc1_result = await ToListAsync(strategy.GetAllPartitionsInternalAsync(PropertyName, streamGetter, "doc1"));
        var doc2_result = await ToListAsync(strategy.GetAllPartitionsInternalAsync(PropertyName, streamGetter, "doc2"));
        var doc3_result = await ToListAsync(strategy.GetAllPartitionsInternalAsync(PropertyName, streamGetter, "doc3"));
        var all_result = await ToListAsync(strategy.GetAllPartitionsInternalAsync(PropertyName, streamGetter, null));

        // Assert
        doc1_result.Count.ShouldBe(5);
        doc1_result.All(p => p.GetPartitionKey().LogicalKey.Equals("doc1")).ShouldBeTrue();
        
        doc2_result.Count.ShouldBe(5);
        doc2_result.All(p => p.GetPartitionKey().LogicalKey.Equals("doc2")).ShouldBeTrue();
        
        doc3_result.Count.ShouldBe(5);
        doc3_result.All(p => p.GetPartitionKey().LogicalKey.Equals("doc3")).ShouldBeTrue();
        
        all_result.Count.ShouldBe(15);
    }
    
    [Theory]
    [InlineData(5, 50, 0)]  // Test first key in a set of 5
    [InlineData(5, 50, 2)]  // Test middle key in a set of 5
    [InlineData(5, 50, 4)]  // Test last key in a set of 5
    [InlineData(3, 100, 1)] // Test with even more partitions per key
    public async Task GetAllPartitionsInternalAsync_WithLargeNumberOfPartitions_ShouldReturnCorrectSubset(int totalLogicalKeys, int partitionsPerKey, int targetKeyIndex)
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);

        var logicalKeys = Enumerable.Range(0, totalLogicalKeys).Select(i => $"doc-{i:D3}").ToList();
        var allPartitions = new List<IPartition>();
        var allExpectedPartitions = new Dictionary<string, List<IPartition>>();
        
        foreach (var key in logicalKeys)
        {
            var keyPartitions = GeneratePartitions(key, 0, partitionsPerKey);
            allPartitions.AddRange(keyPartitions);
            allExpectedPartitions[key] = keyPartitions;
        }

        // Insert in a random order to ensure the B-Tree is built robustly
        var random = new Random(42);
        foreach (var p in allPartitions.OrderBy(_ => random.Next()))
        {
            await strategy.InsertPropertyPartitionAsync(p, PropertyName);
        }
        
        var targetLogicalKey = logicalKeys[targetKeyIndex];
        var expectedPartitions = allExpectedPartitions[targetLogicalKey];

        // Act
        var streamGetter = () => streamProvider.GetPropertyIndexStreamAsync(PropertyName);
        var result = await ToListAsync(strategy.GetAllPartitionsInternalAsync(PropertyName, streamGetter, targetLogicalKey));

        // Assert
        result.Count.ShouldBe(partitionsPerKey);
        result.ShouldBe(expectedPartitions, ignoreOrder: true);
    }
    
    #endregion

    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, 50)]
    [InlineData(9, 90)]
    public async Task GetDataPartitionByIndexAsync_ShouldReturnCorrectPartition(long index, int expectedRangeKey)
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        var partitions = await InsertRangeAsync(LogicalKey, 0, 10); // keys are 0, 10, 20...90
        await InsertRangeAsync("other_key", 0, 5); // noise

        // Act
        var found = await strategy.GetPropertyPartitionByIndexAsync(LogicalKey, index, PropertyName);

        // Assert
        found.ShouldNotBeNull();
        found.GetPartitionKey().RangeKey.ShouldBe(expectedRangeKey);
    }
    
    [Theory]
    [InlineData(-1)]
    [InlineData(10)]
    public async Task GetDataPartitionByIndexAsync_WithInvalidIndex_ShouldReturnNull(long index)
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        await InsertRangeAsync(LogicalKey, 0, 10);
        
        // Act
        var found = await strategy.GetPropertyPartitionByIndexAsync(LogicalKey, index, PropertyName);

        // Assert
        found.ShouldBeNull();
    }

    [Fact]
    public async Task FindPartitionAsync_WithKeyBetweenPartitions_ShouldReturnFloorPartition()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        var p10 = new DataPartition(new CompositePartitionKey(LogicalKey, 10), null, 1, 1, 1, 1);
        var p20 = new DataPartition(new CompositePartitionKey(LogicalKey, 20), null, 2, 2, 2, 2);
        await strategy.InsertPropertyPartitionAsync(p10, PropertyName);
        await strategy.InsertPropertyPartitionAsync(p20, PropertyName);

        // Act
        var found = await strategy.FindPropertyPartitionAsync(new CompositePartitionKey(LogicalKey, 15), PropertyName);
        
        // Assert
        found.ShouldBe(p10);
    }

    [Fact]
    public async Task FindPartitionAsync_WithKeySmallerThanAll_ShouldReturnNull()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        var header = new HeaderPartition(new CompositePartitionKey(LogicalKey, null), 1,1,1,1);
        var p10 = new DataPartition(new CompositePartitionKey(LogicalKey, 10), null, 1, 1, 1, 1);
        await strategy.InsertPropertyPartitionAsync(p10, PropertyName);
        
        // FindPartition logic finds the GREATEST key SMALLER OR EQUAL to the search key.
        // A search for key=5 should find the partition that starts at key=null (the header).
        await strategy.InsertPropertyPartitionAsync(header, PropertyName);
        var foundHeader = await strategy.FindPropertyPartitionAsync(new CompositePartitionKey(LogicalKey, 5), PropertyName);
        foundHeader.ShouldBe(header);
        
        // If there's no such partition (e.g., different logical key), it should be null.
        var foundNull = await strategy.FindPropertyPartitionAsync(new CompositePartitionKey("another_key", 5), PropertyName);
        foundNull.ShouldBeNull();
    }
    
    [Fact]
    public async Task StressTest_AfterManyOperations_ShouldMaintainIntegrity()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        
        var random = new Random(42);
        var expectedPartitions = new Dictionary<CompositePartitionKey, IPartition>();
        const int initialCount = 1000;
        const int deleteCount = 500;
        const int reinsertCount = 250;

        // Act & Assert Phase 1: Initial Bulk Insertion
        var initialPartitions = Enumerable.Range(0, initialCount).Select(i => new DataPartition(new CompositePartitionKey(LogicalKey, i), null, (long)i, i, (long)i, i)).Cast<IPartition>();
        foreach (var p in initialPartitions)
        {
            await strategy.InsertPropertyPartitionAsync(p, PropertyName);
            expectedPartitions.Add(p.GetPartitionKey(), p);
        }
        var allPartitionsAfterInsert = await ToListAsync(strategy.GetAllPropertyPartitionsAsync(PropertyName));
        allPartitionsAfterInsert.Count.ShouldBe(initialCount);

        // Act & Assert Phase 2: Random Deletions
        var partitionsToDelete = expectedPartitions.Values.OrderBy(_ => random.Next()).Take(deleteCount).ToList();
        foreach (var p in partitionsToDelete)
        {
            await strategy.DeletePropertyPartitionAsync(p, PropertyName);
            expectedPartitions.Remove(p.GetPartitionKey());
        }
        var allPartitionsAfterDelete = await ToListAsync(strategy.GetAllPropertyPartitionsAsync(PropertyName));
        allPartitionsAfterDelete.Count.ShouldBe(initialCount - deleteCount);
        allPartitionsAfterDelete.ShouldBe(expectedPartitions.Values, ignoreOrder: true);


        // Act & Assert Phase 3: Re-insertion and New Insertions
        var partitionsToReinsert = Enumerable.Range(initialCount, reinsertCount).Select(i => new DataPartition(new CompositePartitionKey(LogicalKey, i), null, (long)i, i, (long)i, i)).Cast<IPartition>();
        foreach (var p in partitionsToReinsert)
        {
            await strategy.InsertPropertyPartitionAsync(p, PropertyName);
            expectedPartitions.Add(p.GetPartitionKey(), p);
        }
        
        // Final Verification
        var finalPartitions = (await ToListAsync(strategy.GetAllPropertyPartitionsAsync(PropertyName))).OrderBy(p => p.GetPartitionKey()).ToList();
        var expectedFinalPartitions = expectedPartitions.Values.OrderBy(p => p.GetPartitionKey()).ToList();
        finalPartitions.Count.ShouldBe(expectedFinalPartitions.Count);
        finalPartitions.ShouldBe(expectedFinalPartitions, ignoreOrder: false);
    }
    
    [Fact]
    public async Task Header_InitializeAsync_ShouldCreateValidHeaderAndRootNode()
    {
        // Act
        await strategy.InitializeHeaderIndexAsync();

        // Assert
        var indexLength = await streamProvider.GetIndexStreamLengthAsync("@header");
        indexLength.ShouldBeGreaterThan(0);
        
        var result = await strategy.FindHeaderPartitionAsync(new CompositePartitionKey(LogicalKey, null));
        result.ShouldBeNull();
    }

    [Fact]
    public async Task Header_InsertAndFindAsync_WithSingleItem_ShouldSucceed()
    {
        // Arrange
        await strategy.InitializeHeaderIndexAsync();
        var partition = new HeaderPartition(new CompositePartitionKey(LogicalKey, null), 1L, 1, 1L, 1);
        
        // Act
        await strategy.InsertHeaderPartitionAsync(partition);
        var found = await strategy.FindHeaderPartitionAsync(new CompositePartitionKey(LogicalKey, null));

        // Assert
        found.ShouldNotBeNull();
        found.ShouldBe(partition);
    }

    [Fact]
    public async Task Header_UpdatePartitionAsync_ShouldModifyExistingPartition()
    {
        // Arrange
        await strategy.InitializeHeaderIndexAsync();
        var originalKey = new CompositePartitionKey(LogicalKey, null);
        var originalPartition = new HeaderPartition(originalKey, 1L, 1, 1L, 1);
        await strategy.InsertHeaderPartitionAsync(originalPartition);
        
        var updatedPartition = new HeaderPartition(originalKey, 99L, 99, 99L, 99);

        // Act
        await strategy.UpdateHeaderPartitionAsync(updatedPartition);

        // Assert
        var found = await strategy.FindHeaderPartitionAsync(originalKey);
        found.ShouldNotBeNull();
        found.ShouldBe(updatedPartition);
        found.ShouldNotBe(originalPartition);
    }

    [Fact]
    public async Task Header_DeletePartitionAsync_ShouldSucceed()
    {
        // Arrange
        await strategy.InitializeHeaderIndexAsync();
        var p1 = new HeaderPartition(new CompositePartitionKey(LogicalKey, null), 1L, 1, 1L, 1);
        var p2 = new HeaderPartition(new CompositePartitionKey("doc2", null), 2L, 2, 2L, 2);
        await strategy.InsertHeaderPartitionAsync(p1);
        await strategy.InsertHeaderPartitionAsync(p2);

        // Act
        await strategy.DeleteHeaderPartitionAsync(p1);

        // Assert
        (await strategy.FindHeaderPartitionAsync(p1.GetPartitionKey())).ShouldBeNull();
        (await strategy.FindHeaderPartitionAsync(p2.GetPartitionKey())).ShouldBe(p2);
        (await strategy.GetHeaderPartitionCountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Header_GetAllPartitionsAsync_WithLogicalKeyFilter_ShouldReturnFilteredPartitions()
    {
        // Arrange
        await strategy.InitializeHeaderIndexAsync();
        var p1 = new HeaderPartition(new CompositePartitionKey("doc1", null), 1, 1, 1, 1);
        var p2 = new HeaderPartition(new CompositePartitionKey("doc2", null), 2, 2, 2, 2);
        var p3 = new HeaderPartition(new CompositePartitionKey("doc3", null), 3, 3, 3, 3);

        await strategy.InsertHeaderPartitionAsync(p1);
        await strategy.InsertHeaderPartitionAsync(p2);
        await strategy.InsertHeaderPartitionAsync(p3);
        
        // Act
        var doc1Partitions = await ToListAsync(strategy.GetAllHeaderPartitionsAsync("doc1"));
        var allPartitions = await ToListAsync(strategy.GetAllHeaderPartitionsAsync());

        // Assert
        doc1Partitions.ShouldBe([p1]);
        allPartitions.Count.ShouldBe(3);
    }

    [Fact]
    public async Task Header_GetPartitionCountAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        await strategy.InitializeHeaderIndexAsync();
        (await strategy.GetHeaderPartitionCountAsync()).ShouldBe(0);
        await strategy.InsertHeaderPartitionAsync(new HeaderPartition(new CompositePartitionKey(LogicalKey, null), 1,1,1,1));
        await strategy.InsertHeaderPartitionAsync(new HeaderPartition(new CompositePartitionKey("other_key", null), 2,2,2,2));

        // Act
        var totalCount = await strategy.GetHeaderPartitionCountAsync();
        var logicalKeyCount = await strategy.GetHeaderPartitionCountAsync(LogicalKey);
        var otherKeyCount = await strategy.GetHeaderPartitionCountAsync("other_key");

        // Assert
        totalCount.ShouldBe(2);
        logicalKeyCount.ShouldBe(1);
        otherKeyCount.ShouldBe(1);
    }
    
    private async Task<List<IPartition>> InsertRangeAsync(IComparable logicalKey, int start, int count)
    {
        var partitions = new List<IPartition>();
        for (var i = start; i < start + count; i++)
        {
            var p = new DataPartition(new CompositePartitionKey(logicalKey, i * 10), null, (long)i, i, (long)i, i);
            partitions.Add(p);
            await strategy.InsertPropertyPartitionAsync(p, PropertyName);
        }
        return partitions;
    }
    
    private List<IPartition> GeneratePartitions(IComparable logicalKey, int start, int count)
    {
        var partitions = new List<IPartition>();
        for (var i = start; i < start + count; i++)
        {
            var p = new DataPartition(new CompositePartitionKey(logicalKey, i * 10), null, (long)i, i, (long)i, i);
            partitions.Add(p);
        }
        return partitions;
    }

    private async Task<List<T>> ToListAsync<T>(IAsyncEnumerable<T> asyncEnumerable)
    {
        var list = new List<T>();
        await foreach (var item in asyncEnumerable)
        {
            list.Add(item);
        }
        return list;
    }
}