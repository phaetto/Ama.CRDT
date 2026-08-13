namespace Ama.CRDT.LargerThanMemory.Streams.UnitTests;

using Ama.CRDT.Extensions;
using Ama.CRDT.LargerThanMemory.Streams.Extensions;
using Ama.CRDT.LargerThanMemory.Streams.Services;
using Ama.CRDT.Models.LargerThanMemory;
using Ama.CRDT.Services;
using Ama.CRDT.Services.LargerThanMemory;
using Microsoft.Extensions.DependencyInjection;
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

public sealed class StreamChunkStorageServiceIndexTests
{
    private readonly IChunkStorageService strategy;
    private readonly InMemoryPartitionStreamProvider streamProvider;
    private const string PropertyName = "items";
    private const string LogicalKey = "doc1";
    private const int Degree = 3; 
    private const int MaxKeys = 2 * Degree - 1; // 5

    public StreamChunkStorageServiceIndexTests()
    {
        var services = new ServiceCollection();
        services.AddCrdt();
        services.AddCrdtStreamChunking<InMemoryPartitionStreamProvider>();

        var meterFactoryMock = new Mock<IMeterFactory>();
        meterFactoryMock.Setup(f => f.Create(It.IsAny<MeterOptions>())).Returns(new Meter("TestMeter"));
        services.AddSingleton(meterFactoryMock.Object);

        // Need a scope context to resolve validated CRDT services
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        var scope = scopeFactory.CreateScope("test-replica");

        strategy = scope.ServiceProvider.GetRequiredService<IChunkStorageService>();
        streamProvider = (InMemoryPartitionStreamProvider)scope.ServiceProvider.GetRequiredService<IChunkStreamProvider>();
    }

    private sealed class InMemoryPartitionStreamProvider : IChunkStreamProvider
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
        
        var result = await strategy.GetPropertyChunkAsync(new CompositeChunkKey(LogicalKey, 1), PropertyName);
        result.ShouldBeNull();
    }
    
    [Fact]
    public async Task InitializeAsync_OnExistingStream_ShouldNotOverwrite()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        var p1 = new CollectionChunk(new CompositeChunkKey(LogicalKey, 1), null, 1, 1, 1, 1);
        await strategy.InsertPropertyChunkAsync(PropertyName, p1);
        var streamBytesBefore = streamProvider.GetIndexStreamBytes(PropertyName);

        // Act
        await strategy.InitializePropertyIndexAsync(PropertyName); // Should be a no-op

        // Assert
        var streamBytesAfter = streamProvider.GetIndexStreamBytes(PropertyName);
        streamBytesAfter.ShouldBe(streamBytesBefore);

        var found = await strategy.GetPropertyChunkAsync(new CompositeChunkKey(LogicalKey, 1), PropertyName);
        found.ShouldBe(p1);
    }
    
    [Fact]
    public async Task InsertAndFindAsync_WithSingleItem_ShouldSucceed()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        var chunk = new CollectionChunk(new CompositeChunkKey(LogicalKey, 10), null, 1L, 1, 1L, 1);
        
        // Act
        await strategy.InsertPropertyChunkAsync(PropertyName, chunk);
        var found = await strategy.GetPropertyChunkAsync(new CompositeChunkKey(LogicalKey, 10), PropertyName);

        // Assert
        found.ShouldNotBeNull();
        found.ShouldBe(chunk);
    }

    [Fact]
    public async Task InsertAndFindAsync_CausingRootSplit_ShouldMaintainCorrectness()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        var chunks = new List<IChunk>();
        for (var i = 1; i <= MaxKeys + 1; i++)
        {
            chunks.Add(new CollectionChunk(new CompositeChunkKey(LogicalKey, i * 10), null, (long)i, i, (long)i, i));
        }

        // Act
        foreach (var p in chunks)
        {
            await strategy.InsertPropertyChunkAsync(PropertyName, p);
        }

        // Assert
        foreach (var p in chunks)
        {
            var found = await strategy.GetPropertyChunkAsync(p.GetPartitionKey(), PropertyName);
            found.ShouldBe(p);
        }
        var allChunks = await ToListAsync(strategy.GetChunksAsync(LogicalKey, PropertyName));
        allChunks.Count.ShouldBe(MaxKeys + 1);
    }

    [Fact]
    public async Task UpdateChunkAsync_ShouldModifyExistingChunk()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        var originalKey = new CompositeChunkKey(LogicalKey, 10);
        var originalChunk = new CollectionChunk(originalKey, null, 1L, 1, 1L, 1);
        await strategy.InsertPropertyChunkAsync(PropertyName, originalChunk);
        
        var updatedChunk = new CollectionChunk(originalKey, null, 99L, 99, 99L, 99);

        // Act
        await strategy.UpdatePropertyChunkAsync(PropertyName, updatedChunk);

        // Assert
        var found = await strategy.GetPropertyChunkAsync(originalKey, PropertyName);
        found.ShouldNotBeNull();
        found.ShouldBe(updatedChunk);
        found.ShouldNotBe(originalChunk);
    }

    [Fact]
    public async Task UpdateChunkAsync_OnNonExistentChunk_ShouldThrowKeyNotFoundException()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        var p1 = new CollectionChunk(new CompositeChunkKey(LogicalKey, 10), null, 1L, 1, 1L, 1);
        var p2 = new CollectionChunk(new CompositeChunkKey(LogicalKey, 20), null, 2L, 2, 2L, 2);
        await strategy.InsertPropertyChunkAsync(PropertyName, p1);

        // Act & Assert
        await Should.ThrowAsync<KeyNotFoundException>(async () => await strategy.UpdatePropertyChunkAsync(PropertyName, p2));
    }

    [Fact]
    public async Task DeleteChunkAsync_FromLeaf_ShouldSucceed()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        var p1 = new CollectionChunk(new CompositeChunkKey(LogicalKey, 10), null, 1L, 1, 1L, 1);
        var p2 = new CollectionChunk(new CompositeChunkKey(LogicalKey, 20), null, 2L, 2, 2L, 2);
        await strategy.InsertPropertyChunkAsync(PropertyName, p1);
        await strategy.InsertPropertyChunkAsync(PropertyName, p2);

        // Act
        await strategy.DeletePropertyChunkAsync(PropertyName, p1);

        // Assert
        (await strategy.GetPropertyChunkAsync(p1.GetPartitionKey(), PropertyName)).ShouldBeNull();
        (await strategy.GetPropertyChunkAsync(p2.GetPartitionKey(), PropertyName)).ShouldBe(p2);
        (await strategy.GetPropertyChunkCountAsync(LogicalKey, PropertyName)).ShouldBe(1);
    }

    [Fact]
    public async Task DeleteChunkAsync_CausingMerge_ShouldMaintainCorrectness()
    {
        // Arrange: Insert just enough to have two leaf nodes after a split, then delete to cause a merge
        await strategy.InitializePropertyIndexAsync(PropertyName);
        
        var p0 = new CollectionChunk(new CompositeChunkKey(LogicalKey, 0), null, 0,0,0,0);
        var p1 = new CollectionChunk(new CompositeChunkKey(LogicalKey, 1), null, 1,1,1,1);
        var p2 = new CollectionChunk(new CompositeChunkKey(LogicalKey, 2), null, 2,2,2,2);
        var p3 = new CollectionChunk(new CompositeChunkKey(LogicalKey, 3), null, 3,3,3,3);
        var p4 = new CollectionChunk(new CompositeChunkKey(LogicalKey, 4), null, 4,4,4,4);
        var p5 = new CollectionChunk(new CompositeChunkKey(LogicalKey, 5), null, 5,5,5,5);
        await strategy.InsertPropertyChunkAsync(PropertyName, p0);
        await strategy.InsertPropertyChunkAsync(PropertyName, p1);
        await strategy.InsertPropertyChunkAsync(PropertyName, p2);
        await strategy.InsertPropertyChunkAsync(PropertyName, p3);
        await strategy.InsertPropertyChunkAsync(PropertyName, p4);
        await strategy.InsertPropertyChunkAsync(PropertyName, p5); // SplitToDisjoint occurs here
        // Root: [p2]
        // Leaves: [p0, p1] <-> [p2, p3, p4, p5]
        
        await strategy.DeletePropertyChunkAsync(PropertyName, p3);
        await strategy.DeletePropertyChunkAsync(PropertyName, p4);
        await strategy.DeletePropertyChunkAsync(PropertyName, p5);
        // Right leaf now has [p2]. Keys < t-1=2. Must borrow or merge.
        // Left leaf has [p0, p1]. It cannot lend. They must merge.
        // Resulting tree should have chunks [p0, p1, p2].
        // Then we delete p1. Final result should be [p0, p2].

        // Act
        await strategy.DeletePropertyChunkAsync(PropertyName, p1);
        
        // Assert
        var remaining = await ToListAsync(strategy.GetChunksAsync(LogicalKey, PropertyName));
        var expected = new List<IChunk> { p0, p2 };

        remaining.Count.ShouldBe(expected.Count);
        remaining.ShouldBe(expected, ignoreOrder: true);
        (await strategy.GetPropertyChunkCountAsync(LogicalKey, PropertyName)).ShouldBe(2);
    }
    
    [Fact]
    public async Task DeleteChunkAsync_LastItem_ShouldLeaveEmptyTree()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        var p1 = new CollectionChunk(new CompositeChunkKey(LogicalKey, 10), null, 1L, 1, 1L, 1);
        await strategy.InsertPropertyChunkAsync(PropertyName, p1);

        // Act
        await strategy.DeletePropertyChunkAsync(PropertyName, p1);
        
        // Assert
        (await strategy.GetPropertyChunkCountAsync(LogicalKey, PropertyName)).ShouldBe(0);
        var all = await ToListAsync(strategy.GetChunksAsync(LogicalKey, PropertyName));
        all.ShouldBeEmpty();
    }
    
    [Fact]
    public async Task DeleteChunkAsync_OnNonExistentChunk_ShouldThrowKeyNotFoundException()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        var p1 = new CollectionChunk(new CompositeChunkKey(LogicalKey, 10), null, 1L, 1, 1L, 1);

        // Act & Assert
        await Should.ThrowAsync<KeyNotFoundException>(async () => await strategy.DeletePropertyChunkAsync(PropertyName, p1));
    }
    
    [Fact]
    public async Task DeleteChunkAsync_DistinguishingHeaderAndDataChunks_ShouldSucceed()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        var key = new CompositeChunkKey(LogicalKey, null);
        var header = new HeaderChunk(key, 1,1,1,1);
        var data = new CollectionChunk(key, null, 2,2,2,2); 

        // We insert them both into the SAME index just to test the B-Tree logic's polymorphic handling
        await strategy.InsertPropertyChunkAsync(PropertyName, header);
        await strategy.InsertPropertyChunkAsync(PropertyName, data);

        // Act
        await strategy.DeletePropertyChunkAsync(PropertyName, header);

        // Assert
        (await strategy.GetPropertyChunkCountAsync(LogicalKey, PropertyName)).ShouldBe(1);
        var all = await ToListAsync(strategy.GetChunksAsync(LogicalKey, PropertyName));
        all.Count.ShouldBe(1);
        all[0].ShouldBe(data);
    }

    [Fact]
    public async Task GetChunksAsync_OnEmptyTree_ShouldReturnEmpty()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);

        // Act
        var chunks = await ToListAsync(strategy.GetChunksAsync(LogicalKey, PropertyName));

        // Assert
        chunks.ShouldBeEmpty();
    }
    
    [Fact]
    public async Task GetChunkCountAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        (await strategy.GetPropertyChunkCountAsync(LogicalKey, PropertyName)).ShouldBe(0);
        await InsertRangeAsync(LogicalKey, 0, 100);
        await InsertRangeAsync("other_key", 0, 50);

        // Act
        var logicalKeyCount = await strategy.GetPropertyChunkCountAsync(LogicalKey, PropertyName);
        var otherKeyCount = await strategy.GetPropertyChunkCountAsync("other_key", PropertyName);

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
        const int chunkCount = 500;
        var expectedChunks = GenerateChunks(LogicalKey, 0, chunkCount);

        foreach (var p in expectedChunks)
        {
            await strategy.InsertPropertyChunkAsync(PropertyName, p);
        }

        // Act
        var actualChunks = await ToListAsync(strategy.GetChunksAsync(LogicalKey, PropertyName));
        
        // Assert
        actualChunks.Count.ShouldBe(chunkCount);
        actualChunks.ShouldBe(expectedChunks, ignoreOrder: false);
    }

    #endregion

    #region Filtered Retrieval Tests

    [Fact]
    public async Task GetChunksAsync_WithNonExistentLogicalKey_ShouldReturnEmpty()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        await InsertRangeAsync(LogicalKey, 0, 10);

        // Act
        var chunks = await ToListAsync(strategy.GetChunksAsync("non_existent_key", PropertyName));

        // Assert
        chunks.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetChunksAsync_SpanningMultipleLeafNodes_ShouldReturnAllChunks()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        await InsertRangeAsync("doc1", 0, 3);
        var doc2_chunks = await InsertRangeAsync("doc2", 0, 20);
        await InsertRangeAsync("doc3", 0, 3);

        // Act
        var result = await ToListAsync(strategy.GetChunksAsync("doc2", PropertyName));
        
        // Assert
        result.Count.ShouldBe(20);
        result.ShouldBe(doc2_chunks, ignoreOrder: true);
    }

    [Fact]
    public async Task GetChunksAsync_TargetKeyIsFirstAlphabetically_ShouldSucceed()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        var docA_chunks = await InsertRangeAsync("A", 0, 5);
        await InsertRangeAsync("B", 0, 5);

        // Act
        var result = await ToListAsync(strategy.GetChunksAsync("A", PropertyName));

        // Assert
        result.Count.ShouldBe(5);
        result.ShouldBe(docA_chunks, ignoreOrder: true);
    }

    [Fact]
    public async Task GetChunksAsync_TargetKeyIsLastAlphabetically_ShouldSucceed()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        await InsertRangeAsync("A", 0, 5);
        var docB_chunks = await InsertRangeAsync("B", 0, 5);

        // Act
        var result = await ToListAsync(strategy.GetChunksAsync("B", PropertyName));

        // Assert
        result.Count.ShouldBe(5);
        result.ShouldBe(docB_chunks, ignoreOrder: true);
    }

    [Fact]
    public async Task GetChunksAsync_FilterDoesNotYieldChunksFromOtherKeys()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        var chunksToInsert = new List<IChunk>();
        chunksToInsert.AddRange(GenerateChunks("doc2", 0, 5));
        chunksToInsert.AddRange(GenerateChunks("doc1", 0, 5));
        chunksToInsert.AddRange(GenerateChunks("doc3", 0, 5));
        
        var random = new Random();
        foreach (var p in chunksToInsert.OrderBy(x => random.Next()))
        {
            await strategy.InsertPropertyChunkAsync(PropertyName, p);
        }

        // Act
        var doc1_result = await ToListAsync(strategy.GetChunksAsync("doc1", PropertyName));
        var doc2_result = await ToListAsync(strategy.GetChunksAsync("doc2", PropertyName));
        var doc3_result = await ToListAsync(strategy.GetChunksAsync("doc3", PropertyName));

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
    public async Task GetChunksAsync_WithLargeNumberOfChunks_ShouldReturnCorrectSubset(int totalLogicalKeys, int chunksPerKey, int targetKeyIndex)
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);

        var logicalKeys = Enumerable.Range(0, totalLogicalKeys).Select(i => $"doc-{i:D3}").ToList();
        var allChunks = new List<IChunk>();
        var allExpectedChunks = new Dictionary<string, List<IChunk>>();
        
        foreach (var key in logicalKeys)
        {
            var keyChunks = GenerateChunks(key, 0, chunksPerKey);
            allChunks.AddRange(keyChunks);
            allExpectedChunks[key] = keyChunks;
        }

        var random = new Random(42);
        foreach (var p in allChunks.OrderBy(_ => random.Next()))
        {
            await strategy.InsertPropertyChunkAsync(PropertyName, p);
        }
        
        var targetLogicalKey = logicalKeys[targetKeyIndex];
        var expectedChunks = allExpectedChunks[targetLogicalKey];

        // Act
        var result = await ToListAsync(strategy.GetChunksAsync(targetLogicalKey, PropertyName));

        // Assert
        result.Count.ShouldBe(chunksPerKey);
        result.ShouldBe(expectedChunks, ignoreOrder: true);
    }
    
    #endregion

    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, 50)]
    [InlineData(9, 90)]
    public async Task GetPropertyChunkByIndexAsync_ShouldReturnCorrectChunk(long index, int expectedRangeKey)
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        await InsertRangeAsync(LogicalKey, 0, 10); // keys are 0, 10, 20...90
        await InsertRangeAsync("other_key", 0, 5); // noise

        // Act
        var found = await strategy.GetPropertyChunkByIndexAsync(LogicalKey, index, PropertyName);

        // Assert
        found.ShouldNotBeNull();
        found.GetPartitionKey().RangeKey.ShouldBe(expectedRangeKey);
    }
    
    [Theory]
    [InlineData(-1)]
    [InlineData(10)]
    public async Task GetPropertyChunkByIndexAsync_WithInvalidIndex_ShouldReturnNull(long index)
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        await InsertRangeAsync(LogicalKey, 0, 10);
        
        // Act
        var found = await strategy.GetPropertyChunkByIndexAsync(LogicalKey, index, PropertyName);

        // Assert
        found.ShouldBeNull();
    }

    [Fact]
    public async Task GetPropertyChunkAsync_WithKeyBetweenChunks_ShouldReturnFloorChunk()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        var p10 = new CollectionChunk(new CompositeChunkKey(LogicalKey, 10), null, 1, 1, 1, 1);
        var p20 = new CollectionChunk(new CompositeChunkKey(LogicalKey, 20), null, 2, 2, 2, 2);
        await strategy.InsertPropertyChunkAsync(PropertyName, p10);
        await strategy.InsertPropertyChunkAsync(PropertyName, p20);

        // Act
        var found = await strategy.GetPropertyChunkAsync(new CompositeChunkKey(LogicalKey, 15), PropertyName);
        
        // Assert
        found.ShouldBe(p10);
    }

    [Fact]
    public async Task GetPropertyChunkAsync_WithKeySmallerThanAll_ShouldReturnNull()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        var header = new HeaderChunk(new CompositeChunkKey(LogicalKey, null), 1,1,1,1);
        var p10 = new CollectionChunk(new CompositeChunkKey(LogicalKey, 10), null, 1, 1, 1, 1);
        
        await strategy.InsertPropertyChunkAsync(PropertyName, header);
        await strategy.InsertPropertyChunkAsync(PropertyName, p10);
        
        var foundHeader = await strategy.GetPropertyChunkAsync(new CompositeChunkKey(LogicalKey, 5), PropertyName);
        foundHeader.ShouldBe(header);
        
        var foundNull = await strategy.GetPropertyChunkAsync(new CompositeChunkKey("another_key", 5), PropertyName);
        foundNull.ShouldBeNull();
    }
    
    [Fact]
    public async Task StressTest_AfterManyOperations_ShouldMaintainIntegrity()
    {
        // Arrange
        await strategy.InitializePropertyIndexAsync(PropertyName);
        
        var random = new Random(42);
        var expectedChunks = new Dictionary<CompositeChunkKey, IChunk>();
        const int initialCount = 1000;
        const int deleteCount = 500;
        const int reinsertCount = 250;

        // Phase 1: Initial Bulk Insertion
        var initialChunks = Enumerable.Range(0, initialCount).Select(i => new CollectionChunk(new CompositeChunkKey(LogicalKey, i), null, (long)i, i, (long)i, i)).Cast<IChunk>();
        foreach (var p in initialChunks)
        {
            await strategy.InsertPropertyChunkAsync(PropertyName, p);
            expectedChunks.Add(p.GetPartitionKey(), p);
        }
        var allChunksAfterInsert = await ToListAsync(strategy.GetChunksAsync(LogicalKey, PropertyName));
        allChunksAfterInsert.Count.ShouldBe(initialCount);

        // Phase 2: Random Deletions
        var chunksToDelete = expectedChunks.Values.OrderBy(_ => random.Next()).Take(deleteCount).ToList();
        foreach (var p in chunksToDelete)
        {
            await strategy.DeletePropertyChunkAsync(PropertyName, p);
            expectedChunks.Remove(p.GetPartitionKey());
        }
        var allChunksAfterDelete = await ToListAsync(strategy.GetChunksAsync(LogicalKey, PropertyName));
        allChunksAfterDelete.Count.ShouldBe(initialCount - deleteCount);
        allChunksAfterDelete.ShouldBe(expectedChunks.Values, ignoreOrder: true);


        // Phase 3: Re-insertion and New Insertions
        var chunksToReinsert = Enumerable.Range(initialCount, reinsertCount).Select(i => new CollectionChunk(new CompositeChunkKey(LogicalKey, i), null, (long)i, i, (long)i, i)).Cast<IChunk>();
        foreach (var p in chunksToReinsert)
        {
            await strategy.InsertPropertyChunkAsync(PropertyName, p);
            expectedChunks.Add(p.GetPartitionKey(), p);
        }
        
        // Final Verification
        var finalChunks = (await ToListAsync(strategy.GetChunksAsync(LogicalKey, PropertyName))).OrderBy(p => p.GetPartitionKey()).ToList();
        var expectedFinalChunks = expectedChunks.Values.OrderBy(p => p.GetPartitionKey()).ToList();
        finalChunks.Count.ShouldBe(expectedFinalChunks.Count);
        finalChunks.ShouldBe(expectedFinalChunks, ignoreOrder: false);
    }
    
    [Fact]
    public async Task Header_InitializeAsync_ShouldCreateValidHeaderAndRootNode()
    {
        // Act
        await strategy.InitializeHeaderIndexAsync();

        // Assert
        var indexLength = await streamProvider.GetIndexStreamLengthAsync("__HEADER__");
        indexLength.ShouldBeGreaterThan(0);
        
        var result = await strategy.GetHeaderChunkAsync(LogicalKey);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task Header_InsertAndFindAsync_WithSingleItem_ShouldSucceed()
    {
        // Arrange
        await strategy.InitializeHeaderIndexAsync();
        var chunk = new HeaderChunk(new CompositeChunkKey(LogicalKey, null), 1L, 1, 1L, 1);
        
        // Act
        await strategy.InsertHeaderChunkAsync(LogicalKey, chunk);
        var found = await strategy.GetHeaderChunkAsync(LogicalKey);

        // Assert
        found.ShouldNotBeNull();
        found.ShouldBe(chunk);
    }

    [Fact]
    public async Task Header_UpdateChunkAsync_ShouldModifyExistingChunk()
    {
        // Arrange
        await strategy.InitializeHeaderIndexAsync();
        var originalKey = new CompositeChunkKey(LogicalKey, null);
        var originalChunk = new HeaderChunk(originalKey, 1L, 1, 1L, 1);
        await strategy.InsertHeaderChunkAsync(LogicalKey, originalChunk);
        
        var updatedChunk = new HeaderChunk(originalKey, 99L, 99, 99L, 99);

        // Act
        await strategy.UpdateHeaderChunkAsync(LogicalKey, updatedChunk);

        // Assert
        var found = await strategy.GetHeaderChunkAsync(LogicalKey);
        found.ShouldNotBeNull();
        found.ShouldBe(updatedChunk);
        found.ShouldNotBe(originalChunk);
    }

    [Fact]
    public async Task Header_GetAllChunksAsync_ShouldReturnAllChunks()
    {
        // Arrange
        await strategy.InitializeHeaderIndexAsync();
        var p1 = new HeaderChunk(new CompositeChunkKey("doc1", null), 1, 1, 1, 1);
        var p2 = new HeaderChunk(new CompositeChunkKey("doc2", null), 2, 2, 2, 2);
        var p3 = new HeaderChunk(new CompositeChunkKey("doc3", null), 3, 3, 3, 3);

        await strategy.InsertHeaderChunkAsync("doc1", p1);
        await strategy.InsertHeaderChunkAsync("doc2", p2);
        await strategy.InsertHeaderChunkAsync("doc3", p3);
        
        // Act
        var allChunks = await ToListAsync(strategy.GetAllHeaderChunksAsync());

        // Assert
        allChunks.Count.ShouldBe(3);
        allChunks.ShouldBe(new IChunk[] { p1, p2, p3 }, ignoreOrder: true);
    }

    private async Task<List<IChunk>> InsertRangeAsync(IComparable logicalKey, int start, int count)
    {
        var chunks = new List<IChunk>();
        for (var i = start; i < start + count; i++)
        {
            var p = new CollectionChunk(new CompositeChunkKey(logicalKey, i * 10), null, (long)i, i, (long)i, i);
            chunks.Add(p);
            await strategy.InsertPropertyChunkAsync(PropertyName, p);
        }
        return chunks;
    }
    
    private List<IChunk> GenerateChunks(IComparable logicalKey, int start, int count)
    {
        var chunks = new List<IChunk>();
        for (var i = start; i < start + count; i++)
        {
            var p = new CollectionChunk(new CompositeChunkKey(logicalKey, i * 10), null, (long)i, i, (long)i, i);
            chunks.Add(p);
        }
        return chunks;
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