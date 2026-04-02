namespace Ama.CRDT.Partitioning.Streams.UnitTests;

using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Partitioning.Streams.Services;
using Ama.CRDT.Partitioning.Streams.Services.Metrics;
using Ama.CRDT.Partitioning.Streams.Services.Serialization;
using Ama.CRDT.Services.Metrics;
using Moq;
using Shouldly;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

public sealed class StreamPartitionStorageServiceIndexTests
{
    private readonly DefaultPartitionSerializationService serializationService = new([]);
    private readonly StreamsCrdtMetrics treeMetrics;
    private readonly PartitionManagerCrdtMetrics partitionMetrics;
    private readonly StreamPartitionStorageService strategy;
    private readonly InMemoryPartitionStreamProvider streamProvider;
    private const string PropertyName = "items";
    private const string LogicalKey = "doc1";
    private const int Degree = 3; 
    private const int MaxKeys = 2 * Degree - 1; // 5

    public StreamPartitionStorageServiceIndexTests()
    {
        var meterFactoryMock = new Mock<IMeterFactory>();
        var meter = new Meter("TestMeterForBPlusTree");
        meterFactoryMock.Setup(f => f.Create(It.IsAny<MeterOptions>())).Returns(meter);
        
        treeMetrics = new StreamsCrdtMetrics(meterFactoryMock.Object);
        partitionMetrics = new PartitionManagerCrdtMetrics(meterFactoryMock.Object);
        streamProvider = new InMemoryPartitionStreamProvider();

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(x => x.GetService(typeof(IPartitionStreamProvider))).Returns(streamProvider);

        strategy = new StreamPartitionStorageService(serviceProviderMock.Object, serializationService, partitionMetrics, treeMetrics);
    }

    private sealed class InMemoryPartitionStreamProvider : IPartitionStreamProvider
    {
        private readonly ConcurrentDictionary<string, MemoryStream> streams = new();
        private const string HeaderIdentifier = "__HEADER__";

        public Task<Stream> GetPropertyIndexStreamAsync(string propertyName, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(streams.GetOrAdd($"index_{propertyName}", _ => new MemoryStream()));

        public Task<Stream> GetPropertyDataStreamAsync(IComparable logicalKey, string propertyName, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(streams.GetOrAdd($"data_{logicalKey}_{propertyName}", _ => new MemoryStream()));

        public Task<Stream> GetHeaderIndexStreamAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(streams.GetOrAdd($"index_{HeaderIdentifier}", _ => new MemoryStream()));
        
        public Task<Stream> GetHeaderDataStreamAsync(IComparable logicalKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(streams.GetOrAdd($"data_{logicalKey}_{HeaderIdentifier}", _ => new MemoryStream()));

        public Task<long> GetIndexStreamLengthAsync(string propertyOrHeader)
        {
            streams.TryGetValue(propertyOrHeader == HeaderIdentifier ? $"index_{HeaderIdentifier}" : $"index_{propertyOrHeader}", out var stream);
            return Task.FromResult(stream?.Length ?? 0);
        }

        public byte[]? GetIndexStreamBytes(string propertyOrHeader)
        {
            var key = propertyOrHeader == HeaderIdentifier ? $"index_{HeaderIdentifier}" : $"index_{propertyOrHeader}";
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
        
        var result = await strategy.GetPropertyPartitionAsync(new CompositePartitionKey(LogicalKey, 1), PropertyName);
        result.ShouldBeNull();
    }
    
    [Fact]
    public async Task InitializeAsync_OnExistingStream_ShouldNotOverwrite()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        var p1 = new DataPartition(new CompositePartitionKey(LogicalKey, 1), null, 1, 1, 1, 1);
        await strategy.InsertPropertyPartitionAsync(PropertyName, p1);
        var streamBytesBefore = streamProvider.GetIndexStreamBytes(PropertyName);

        // Act
        await strategy.InitializePropertyIndexAsync(PropertyName); // Should be a no-op

        // Assert
        var streamBytesAfter = streamProvider.GetIndexStreamBytes(PropertyName);
        streamBytesAfter.ShouldBe(streamBytesBefore);

        var found = await strategy.GetPropertyPartitionAsync(new CompositePartitionKey(LogicalKey, 1), PropertyName);
        found.ShouldBe(p1);
    }
    
    [Fact]
    public async Task InsertAndFindAsync_WithSingleItem_ShouldSucceed()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        var partition = new DataPartition(new CompositePartitionKey(LogicalKey, 10), null, 1L, 1, 1L, 1);
        
        // Act
        await strategy.InsertPropertyPartitionAsync(PropertyName, partition);
        var found = await strategy.GetPropertyPartitionAsync(new CompositePartitionKey(LogicalKey, 10), PropertyName);

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
            await strategy.InsertPropertyPartitionAsync(PropertyName, p);
        }

        // Assert
        foreach (var p in partitions)
        {
            var found = await strategy.GetPropertyPartitionAsync(p.GetPartitionKey(), PropertyName);
            found.ShouldBe(p);
        }
        var allPartitions = await ToListAsync(strategy.GetPartitionsAsync(LogicalKey, PropertyName));
        allPartitions.Count.ShouldBe(MaxKeys + 1);
    }

    [Fact]
    public async Task UpdatePartitionAsync_ShouldModifyExistingPartition()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        var originalKey = new CompositePartitionKey(LogicalKey, 10);
        var originalPartition = new DataPartition(originalKey, null, 1L, 1, 1L, 1);
        await strategy.InsertPropertyPartitionAsync(PropertyName, originalPartition);
        
        var updatedPartition = new DataPartition(originalKey, null, 99L, 99, 99L, 99);

        // Act
        await strategy.UpdatePropertyPartitionAsync(PropertyName, updatedPartition);

        // Assert
        var found = await strategy.GetPropertyPartitionAsync(originalKey, PropertyName);
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
        await strategy.InsertPropertyPartitionAsync(PropertyName, p1);

        // Act & Assert
        await Should.ThrowAsync<KeyNotFoundException>(async () => await strategy.UpdatePropertyPartitionAsync(PropertyName, p2));
    }

    [Fact]
    public async Task DeletePartitionAsync_FromLeaf_ShouldSucceed()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        var p1 = new DataPartition(new CompositePartitionKey(LogicalKey, 10), null, 1L, 1, 1L, 1);
        var p2 = new DataPartition(new CompositePartitionKey(LogicalKey, 20), null, 2L, 2, 2L, 2);
        await strategy.InsertPropertyPartitionAsync(PropertyName, p1);
        await strategy.InsertPropertyPartitionAsync(PropertyName, p2);

        // Act
        await strategy.DeletePropertyPartitionAsync(PropertyName, p1);

        // Assert
        (await strategy.GetPropertyPartitionAsync(p1.GetPartitionKey(), PropertyName)).ShouldBeNull();
        (await strategy.GetPropertyPartitionAsync(p2.GetPartitionKey(), PropertyName)).ShouldBe(p2);
        (await strategy.GetPropertyPartitionCountAsync(LogicalKey, PropertyName)).ShouldBe(1);
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
        await strategy.InsertPropertyPartitionAsync(PropertyName, p0);
        await strategy.InsertPropertyPartitionAsync(PropertyName, p1);
        await strategy.InsertPropertyPartitionAsync(PropertyName, p2);
        await strategy.InsertPropertyPartitionAsync(PropertyName, p3);
        await strategy.InsertPropertyPartitionAsync(PropertyName, p4);
        await strategy.InsertPropertyPartitionAsync(PropertyName, p5); // Split occurs here
        // Root: [p2]
        // Leaves: [p0, p1] <-> [p2, p3, p4, p5]
        
        await strategy.DeletePropertyPartitionAsync(PropertyName, p3);
        await strategy.DeletePropertyPartitionAsync(PropertyName, p4);
        await strategy.DeletePropertyPartitionAsync(PropertyName, p5);
        // Right leaf now has [p2]. Keys < t-1=2. Must borrow or merge.
        // Left leaf has [p0, p1]. It cannot lend. They must merge.
        // Resulting tree should have partitions [p0, p1, p2].
        // Then we delete p1. Final result should be [p0, p2].

        // Act
        await strategy.DeletePropertyPartitionAsync(PropertyName, p1);
        
        // Assert
        var remaining = await ToListAsync(strategy.GetPartitionsAsync(LogicalKey, PropertyName));
        var expected = new List<IPartition> { p0, p2 };

        remaining.Count.ShouldBe(expected.Count);
        remaining.ShouldBe(expected, ignoreOrder: true);
        (await strategy.GetPropertyPartitionCountAsync(LogicalKey, PropertyName)).ShouldBe(2);
    }
    
    [Fact]
    public async Task DeletePartitionAsync_LastItem_ShouldLeaveEmptyTree()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        var p1 = new DataPartition(new CompositePartitionKey(LogicalKey, 10), null, 1L, 1, 1L, 1);
        await strategy.InsertPropertyPartitionAsync(PropertyName, p1);

        // Act
        await strategy.DeletePropertyPartitionAsync(PropertyName, p1);
        
        // Assert
        (await strategy.GetPropertyPartitionCountAsync(LogicalKey, PropertyName)).ShouldBe(0);
        var all = await ToListAsync(strategy.GetPartitionsAsync(LogicalKey, PropertyName));
        all.ShouldBeEmpty();
    }
    
    [Fact]
    public async Task DeletePartitionAsync_OnNonExistentPartition_ShouldThrowKeyNotFoundException()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        var p1 = new DataPartition(new CompositePartitionKey(LogicalKey, 10), null, 1L, 1, 1L, 1);

        // Act & Assert
        await Should.ThrowAsync<KeyNotFoundException>(async () => await strategy.DeletePropertyPartitionAsync(PropertyName, p1));
    }
    
    [Fact]
    public async Task DeletePartitionAsync_DistinguishingHeaderAndDataPartitions_ShouldSucceed()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        var key = new CompositePartitionKey(LogicalKey, null);
        var header = new HeaderPartition(key, 1,1,1,1);
        var data = new DataPartition(key, null, 2,2,2,2); 

        // We insert them both into the SAME index just to test the B-Tree logic's polymorphic handling
        await strategy.InsertPropertyPartitionAsync(PropertyName, header);
        await strategy.InsertPropertyPartitionAsync(PropertyName, data);

        // Act
        await strategy.DeletePropertyPartitionAsync(PropertyName, header);

        // Assert
        (await strategy.GetPropertyPartitionCountAsync(LogicalKey, PropertyName)).ShouldBe(1);
        var all = await ToListAsync(strategy.GetPartitionsAsync(LogicalKey, PropertyName));
        all.Count.ShouldBe(1);
        all[0].ShouldBe(data);
    }

    [Fact]
    public async Task GetPartitionsAsync_OnEmptyTree_ShouldReturnEmpty()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);

        // Act
        var partitions = await ToListAsync(strategy.GetPartitionsAsync(LogicalKey, PropertyName));

        // Assert
        partitions.ShouldBeEmpty();
    }
    
    [Fact]
    public async Task GetPartitionCountAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        (await strategy.GetPropertyPartitionCountAsync(LogicalKey, PropertyName)).ShouldBe(0);
        await InsertRangeAsync(LogicalKey, 0, 100);
        await InsertRangeAsync("other_key", 0, 50);

        // Act
        var logicalKeyCount = await strategy.GetPropertyPartitionCountAsync(LogicalKey, PropertyName);
        var otherKeyCount = await strategy.GetPropertyPartitionCountAsync("other_key", PropertyName);

        // Assert
        logicalKeyCount.ShouldBe(100);
        otherKeyCount.ShouldBe(50);
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
            await strategy.InsertPropertyPartitionAsync(PropertyName, p);
        }

        // Act
        var actualPartitions = await ToListAsync(strategy.GetPartitionsAsync(LogicalKey, PropertyName));
        
        // Assert
        actualPartitions.Count.ShouldBe(partitionCount);
        actualPartitions.ShouldBe(expectedPartitions, ignoreOrder: false);
    }

    #endregion

    #region Filtered Retrieval Tests

    [Fact]
    public async Task GetPartitionsAsync_WithNonExistentLogicalKey_ShouldReturnEmpty()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        await InsertRangeAsync(LogicalKey, 0, 10);

        // Act
        var partitions = await ToListAsync(strategy.GetPartitionsAsync("non_existent_key", PropertyName));

        // Assert
        partitions.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetPartitionsAsync_SpanningMultipleLeafNodes_ShouldReturnAllPartitions()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        await InsertRangeAsync("doc1", 0, 3);
        var doc2_partitions = await InsertRangeAsync("doc2", 0, 20);
        await InsertRangeAsync("doc3", 0, 3);

        // Act
        var result = await ToListAsync(strategy.GetPartitionsAsync("doc2", PropertyName));
        
        // Assert
        result.Count.ShouldBe(20);
        result.ShouldBe(doc2_partitions, ignoreOrder: true);
    }

    [Fact]
    public async Task GetPartitionsAsync_TargetKeyIsFirstAlphabetically_ShouldSucceed()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        var docA_partitions = await InsertRangeAsync("A", 0, 5);
        await InsertRangeAsync("B", 0, 5);

        // Act
        var result = await ToListAsync(strategy.GetPartitionsAsync("A", PropertyName));

        // Assert
        result.Count.ShouldBe(5);
        result.ShouldBe(docA_partitions, ignoreOrder: true);
    }

    [Fact]
    public async Task GetPartitionsAsync_TargetKeyIsLastAlphabetically_ShouldSucceed()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        await InsertRangeAsync("A", 0, 5);
        var docB_partitions = await InsertRangeAsync("B", 0, 5);

        // Act
        var result = await ToListAsync(strategy.GetPartitionsAsync("B", PropertyName));

        // Assert
        result.Count.ShouldBe(5);
        result.ShouldBe(docB_partitions, ignoreOrder: true);
    }

    [Fact]
    public async Task GetPartitionsAsync_FilterDoesNotYieldPartitionsFromOtherKeys()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        var partitionsToInsert = new List<IPartition>();
        partitionsToInsert.AddRange(GeneratePartitions("doc2", 0, 5));
        partitionsToInsert.AddRange(GeneratePartitions("doc1", 0, 5));
        partitionsToInsert.AddRange(GeneratePartitions("doc3", 0, 5));
        
        var random = new Random();
        foreach (var p in partitionsToInsert.OrderBy(x => random.Next()))
        {
            await strategy.InsertPropertyPartitionAsync(PropertyName, p);
        }

        // Act
        var doc1_result = await ToListAsync(strategy.GetPartitionsAsync("doc1", PropertyName));
        var doc2_result = await ToListAsync(strategy.GetPartitionsAsync("doc2", PropertyName));
        var doc3_result = await ToListAsync(strategy.GetPartitionsAsync("doc3", PropertyName));

        // Assert
        doc1_result.Count.ShouldBe(5);
        doc1_result.All(p => p.GetPartitionKey().LogicalKey.Equals("doc1")).ShouldBeTrue();
        
        doc2_result.Count.ShouldBe(5);
        doc2_result.All(p => p.GetPartitionKey().LogicalKey.Equals("doc2")).ShouldBeTrue();
        
        doc3_result.Count.ShouldBe(5);
        doc3_result.All(p => p.GetPartitionKey().LogicalKey.Equals("doc3")).ShouldBeTrue();
    }
    
    [Theory]
    [InlineData(5, 50, 0)] 
    [InlineData(5, 50, 2)] 
    [InlineData(5, 50, 4)] 
    [InlineData(3, 100, 1)]
    public async Task GetPartitionsAsync_WithLargeNumberOfPartitions_ShouldReturnCorrectSubset(int totalLogicalKeys, int partitionsPerKey, int targetKeyIndex)
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

        var random = new Random(42);
        foreach (var p in allPartitions.OrderBy(_ => random.Next()))
        {
            await strategy.InsertPropertyPartitionAsync(PropertyName, p);
        }
        
        var targetLogicalKey = logicalKeys[targetKeyIndex];
        var expectedPartitions = allExpectedPartitions[targetLogicalKey];

        // Act
        var result = await ToListAsync(strategy.GetPartitionsAsync(targetLogicalKey, PropertyName));

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
        await InsertRangeAsync(LogicalKey, 0, 10); // keys are 0, 10, 20...90
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
    public async Task GetPropertyPartitionAsync_WithKeyBetweenPartitions_ShouldReturnFloorPartition()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        var p10 = new DataPartition(new CompositePartitionKey(LogicalKey, 10), null, 1, 1, 1, 1);
        var p20 = new DataPartition(new CompositePartitionKey(LogicalKey, 20), null, 2, 2, 2, 2);
        await strategy.InsertPropertyPartitionAsync(PropertyName, p10);
        await strategy.InsertPropertyPartitionAsync(PropertyName, p20);

        // Act
        var found = await strategy.GetPropertyPartitionAsync(new CompositePartitionKey(LogicalKey, 15), PropertyName);
        
        // Assert
        found.ShouldBe(p10);
    }

    [Fact]
    public async Task GetPropertyPartitionAsync_WithKeySmallerThanAll_ShouldReturnNull()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        var header = new HeaderPartition(new CompositePartitionKey(LogicalKey, null), 1,1,1,1);
        var p10 = new DataPartition(new CompositePartitionKey(LogicalKey, 10), null, 1, 1, 1, 1);
        
        await strategy.InsertPropertyPartitionAsync(PropertyName, header);
        await strategy.InsertPropertyPartitionAsync(PropertyName, p10);
        
        var foundHeader = await strategy.GetPropertyPartitionAsync(new CompositePartitionKey(LogicalKey, 5), PropertyName);
        foundHeader.ShouldBe(header);
        
        var foundNull = await strategy.GetPropertyPartitionAsync(new CompositePartitionKey("another_key", 5), PropertyName);
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

        // Phase 1: Initial Bulk Insertion
        var initialPartitions = Enumerable.Range(0, initialCount).Select(i => new DataPartition(new CompositePartitionKey(LogicalKey, i), null, (long)i, i, (long)i, i)).Cast<IPartition>();
        foreach (var p in initialPartitions)
        {
            await strategy.InsertPropertyPartitionAsync(PropertyName, p);
            expectedPartitions.Add(p.GetPartitionKey(), p);
        }
        var allPartitionsAfterInsert = await ToListAsync(strategy.GetPartitionsAsync(LogicalKey, PropertyName));
        allPartitionsAfterInsert.Count.ShouldBe(initialCount);

        // Phase 2: Random Deletions
        var partitionsToDelete = expectedPartitions.Values.OrderBy(_ => random.Next()).Take(deleteCount).ToList();
        foreach (var p in partitionsToDelete)
        {
            await strategy.DeletePropertyPartitionAsync(PropertyName, p);
            expectedPartitions.Remove(p.GetPartitionKey());
        }
        var allPartitionsAfterDelete = await ToListAsync(strategy.GetPartitionsAsync(LogicalKey, PropertyName));
        allPartitionsAfterDelete.Count.ShouldBe(initialCount - deleteCount);
        allPartitionsAfterDelete.ShouldBe(expectedPartitions.Values, ignoreOrder: true);


        // Phase 3: Re-insertion and New Insertions
        var partitionsToReinsert = Enumerable.Range(initialCount, reinsertCount).Select(i => new DataPartition(new CompositePartitionKey(LogicalKey, i), null, (long)i, i, (long)i, i)).Cast<IPartition>();
        foreach (var p in partitionsToReinsert)
        {
            await strategy.InsertPropertyPartitionAsync(PropertyName, p);
            expectedPartitions.Add(p.GetPartitionKey(), p);
        }
        
        // Final Verification
        var finalPartitions = (await ToListAsync(strategy.GetPartitionsAsync(LogicalKey, PropertyName))).OrderBy(p => p.GetPartitionKey()).ToList();
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
        var indexLength = await streamProvider.GetIndexStreamLengthAsync("__HEADER__");
        indexLength.ShouldBeGreaterThan(0);
        
        var result = await strategy.GetHeaderPartitionAsync(LogicalKey);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task Header_InsertAndFindAsync_WithSingleItem_ShouldSucceed()
    {
        // Arrange
        await strategy.InitializeHeaderIndexAsync();
        var partition = new HeaderPartition(new CompositePartitionKey(LogicalKey, null), 1L, 1, 1L, 1);
        
        // Act
        await strategy.InsertHeaderPartitionAsync(LogicalKey, partition);
        var found = await strategy.GetHeaderPartitionAsync(LogicalKey);

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
        await strategy.InsertHeaderPartitionAsync(LogicalKey, originalPartition);
        
        var updatedPartition = new HeaderPartition(originalKey, 99L, 99, 99L, 99);

        // Act
        await strategy.UpdateHeaderPartitionAsync(LogicalKey, updatedPartition);

        // Assert
        var found = await strategy.GetHeaderPartitionAsync(LogicalKey);
        found.ShouldNotBeNull();
        found.ShouldBe(updatedPartition);
        found.ShouldNotBe(originalPartition);
    }

    [Fact]
    public async Task Header_GetAllPartitionsAsync_ShouldReturnAllPartitions()
    {
        // Arrange
        await strategy.InitializeHeaderIndexAsync();
        var p1 = new HeaderPartition(new CompositePartitionKey("doc1", null), 1, 1, 1, 1);
        var p2 = new HeaderPartition(new CompositePartitionKey("doc2", null), 2, 2, 2, 2);
        var p3 = new HeaderPartition(new CompositePartitionKey("doc3", null), 3, 3, 3, 3);

        await strategy.InsertHeaderPartitionAsync("doc1", p1);
        await strategy.InsertHeaderPartitionAsync("doc2", p2);
        await strategy.InsertHeaderPartitionAsync("doc3", p3);
        
        // Act
        var allPartitions = await ToListAsync(strategy.GetAllHeaderPartitionsAsync());

        // Assert
        allPartitions.Count.ShouldBe(3);
        allPartitions.ShouldBe(new IPartition[] { p1, p2, p3 }, ignoreOrder: true);
    }

    private async Task<List<IPartition>> InsertRangeAsync(IComparable logicalKey, int start, int count)
    {
        var partitions = new List<IPartition>();
        for (var i = start; i < start + count; i++)
        {
            var p = new DataPartition(new CompositePartitionKey(logicalKey, i * 10), null, (long)i, i, (long)i, i);
            partitions.Add(p);
            await strategy.InsertPropertyPartitionAsync(PropertyName, p);
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

    private async Task<List<T>> ToListAsync<T>(IAsyncEnumerable<T> asyncEnumerable, CancellationToken cancellationToken = default)
    {
        var list = new List<T>();
        await foreach (var item in asyncEnumerable.WithCancellation(cancellationToken))
        {
            list.Add(item);
        }
        return list;
    }
}