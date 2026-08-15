namespace Ama.CRDT.UnitTests.Services.LargerThanMemory;

using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Models.LargerThanMemory;
using Ama.CRDT.Services;
using Ama.CRDT.Services.GarbageCollection;
using Ama.CRDT.Services.LargerThanMemory;
using Ama.CRDT.Services.Metrics;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.UnitTests.Services.Partitioning;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

public sealed class MultiPartitionedModel
{
    public string TenantId { get; set; } = "";
    public string HeaderData { get; set; } = "Initial";

    [CrdtOrMapStrategy]
    public Dictionary<string, string> Items { get; set; } = new();
    
    [CrdtOrMapStrategy]
    public Dictionary<string, string> Tags { get; set; } = new();
}

public sealed class MultiPartitionedModelIdProvider : IDocumentIdProvider
{
    public IComparable GetDocumentId<T>(T? obj)
    {
        ArgumentNullException.ThrowIfNull(obj);
        if (obj is MultiPartitionedModel model) return model.TenantId;
        throw new NotSupportedException();
    }

    public void SetDocumentId<T>(T obj, IComparable id)
    {
        ArgumentNullException.ThrowIfNull(obj);
        if (obj is MultiPartitionedModel model && id is string strId) model.TenantId = strId;
        else throw new NotSupportedException();
    }

    public T CreateDocumentWithId<T>(IComparable id)
    {
        if (typeof(T) == typeof(MultiPartitionedModel) && id is string strId) return (T)(object)new MultiPartitionedModel { TenantId = strId };
        throw new NotSupportedException();
    }
}

public sealed class ChunkedDocumentManagerTests
{
    private (ChunkedDocumentManager<MultiPartitionedModel> Manager, Mock<IChunkStorageService> StorageMock, IServiceProvider Provider) CreateManager(bool withPolicy = false)
    {
        var mockStorage = new Mock<IChunkStorageService>();
        
        var meterFactoryMock = new Mock<IMeterFactory>();
        meterFactoryMock.Setup(f => f.Create(It.IsAny<MeterOptions>())).Returns(new Meter("TestMeter"));

        var services = new ServiceCollection()
            .AddCrdt()
            .AddSingleton<CrdtAotContext, LargerThanMemoryTestCrdtAotContext>()
            .AddSingleton(meterFactoryMock.Object)
            .AddSingleton<IDocumentIdProvider, MultiPartitionedModelIdProvider>()
            .AddSingleton(mockStorage.Object);

        if (withPolicy)
        {
            var mockPolicy = new Mock<ICompactionPolicy>();
            var mockFactory = new Mock<ICompactionPolicyFactory>();
            mockFactory.Setup(f => f.CreatePolicy()).Returns(mockPolicy.Object);
            services.AddSingleton(mockFactory.Object);
        }

        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<ICrdtScopeFactory>();
        var scope = scopeFactory.CreateScope("TestReplica");
        
        var metadataManager = scope.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();
        var strategyProvider = scope.ServiceProvider.GetRequiredService<ICrdtStrategyProvider>();
        var replicaContext = scope.ServiceProvider.GetRequiredService<ReplicaContext>();
        var metrics = scope.ServiceProvider.GetRequiredService<LargerThanMemoryManagerCrdtMetrics>();
        var aotContexts = scope.ServiceProvider.GetServices<CrdtAotContext>();
        var policies = scope.ServiceProvider.GetServices<ICompactionPolicyFactory>();
        var documentIdProvider = scope.ServiceProvider.GetRequiredService<IDocumentIdProvider>();
        var projectors = scope.ServiceProvider.GetServices<IVirtualDocumentProjector<MultiPartitionedModel>>();

        var manager = new ChunkedDocumentManager<MultiPartitionedModel>(
            mockStorage.Object,
            metadataManager,
            strategyProvider,
            replicaContext,
            metrics,
            policies,
            aotContexts,
            documentIdProvider,
            projectors
        );

        return (manager, mockStorage, scope.ServiceProvider);
    }

    [Fact]
    public async Task InitializeAsync_ShouldCallStorageServiceMethods()
    {
        // Arrange
        var (manager, mockStorage, _) = CreateManager();
        var initialObject = new MultiPartitionedModel { TenantId = "tenant-1" };
        
        mockStorage.Setup(x => x.SaveHeaderChunkContentAsync(It.IsAny<IComparable>(), It.IsAny<HeaderChunk>(), It.IsAny<MultiPartitionedModel>(), It.IsAny<CrdtMetadata>(), default))
            .ReturnsAsync((IComparable k, HeaderChunk p, MultiPartitionedModel d, CrdtMetadata m, CancellationToken c) => p);
        
        mockStorage.Setup(x => x.SaveChunkContentAsync(It.IsAny<IComparable>(), It.IsAny<string>(), It.IsAny<IChunk>(), It.IsAny<MultiPartitionedModel>(), It.IsAny<CrdtMetadata>(), default))
            .ReturnsAsync((IComparable k, string prop, IChunk p, MultiPartitionedModel d, CrdtMetadata m, CancellationToken c) => p);

        // Act
        await manager.InitializeAsync(initialObject);

        // Assert
        mockStorage.Verify(x => x.InitializeHeaderIndexAsync(default), Times.Once);
        mockStorage.Verify(x => x.ClearHeaderDataAsync("tenant-1", default), Times.Once);
        mockStorage.Verify(x => x.InsertHeaderChunkAsync("tenant-1", It.IsAny<HeaderChunk>(), default), Times.Once);

        mockStorage.Verify(x => x.InitializePropertyIndexAsync(nameof(MultiPartitionedModel.Items), default), Times.Once);
        mockStorage.Verify(x => x.ClearPropertyDataAsync("tenant-1", nameof(MultiPartitionedModel.Items), default), Times.Once);
        mockStorage.Verify(x => x.InsertPropertyChunkAsync(nameof(MultiPartitionedModel.Items), It.IsAny<IChunk>(), default), Times.Once);
        
        mockStorage.Verify(x => x.InitializePropertyIndexAsync(nameof(MultiPartitionedModel.Tags), default), Times.Once);
        mockStorage.Verify(x => x.ClearPropertyDataAsync("tenant-1", nameof(MultiPartitionedModel.Tags), default), Times.Once);
        mockStorage.Verify(x => x.InsertPropertyChunkAsync(nameof(MultiPartitionedModel.Tags), It.IsAny<IChunk>(), default), Times.Once);
    }

    [Fact]
    public async Task CompactAsync_ShouldDoNothing_WhenNoPoliciesRegistered()
    {
        // Arrange
        var (manager, mockStorage, _) = CreateManager(withPolicy: false);

        // Act
        await manager.CompactAsync();

        // Assert
        mockStorage.Verify(x => x.GetAllHeaderChunksAsync(It.IsAny<CancellationToken>()), Times.Never);
        mockStorage.Verify(x => x.SaveChunkContentAsync(It.IsAny<IComparable>(), It.IsAny<string>(), It.IsAny<IChunk>(), It.IsAny<MultiPartitionedModel>(), It.IsAny<CrdtMetadata>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CompactAsync_ShouldIterateAndSavePartitions_WhenPoliciesRegistered()
    {
        // Arrange
        var (manager, mockStorage, _) = CreateManager(withPolicy: true);
        var logicalKey = "tenant-1";

        var headerChunk = new HeaderChunk(new CompositeChunkKey(logicalKey, null), 0, 0, 0, 0);
        var dataChunk = new CollectionChunk(new CompositeChunkKey(logicalKey, "item1"), null, 0, 0, 0, 0);

        mockStorage.Setup(x => x.GetAllHeaderChunksAsync(It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable<IChunk>(headerChunk));

        mockStorage.Setup(x => x.GetHeaderChunkAsync(logicalKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(headerChunk);

        mockStorage.Setup(x => x.LoadHeaderChunkContentAsync<MultiPartitionedModel>(logicalKey, headerChunk, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CrdtDocument<MultiPartitionedModel>(new MultiPartitionedModel(), new CrdtMetadata()));

        mockStorage.Setup(x => x.GetChunksAsync(logicalKey, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable<IChunk>(dataChunk));

        mockStorage.Setup(x => x.LoadChunkContentAsync<MultiPartitionedModel>(logicalKey, It.IsAny<string>(), dataChunk, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CrdtDocument<MultiPartitionedModel>(new MultiPartitionedModel(), new CrdtMetadata()));

        mockStorage.Setup(x => x.SaveHeaderChunkContentAsync(It.IsAny<IComparable>(), It.IsAny<HeaderChunk>(), It.IsAny<MultiPartitionedModel>(), It.IsAny<CrdtMetadata>(), default))
            .ReturnsAsync((IComparable k, HeaderChunk p, MultiPartitionedModel d, CrdtMetadata m, CancellationToken c) => p);

        mockStorage.Setup(x => x.SaveChunkContentAsync(It.IsAny<IComparable>(), It.IsAny<string>(), It.IsAny<IChunk>(), It.IsAny<MultiPartitionedModel>(), It.IsAny<CrdtMetadata>(), default))
            .ReturnsAsync((IComparable k, string prop, IChunk p, MultiPartitionedModel d, CrdtMetadata m, CancellationToken c) => p);

        // Act
        await manager.CompactAsync();

        // Assert
        // Verified Header Compaction
        mockStorage.Verify(x => x.SaveHeaderChunkContentAsync(logicalKey, headerChunk, It.IsAny<MultiPartitionedModel>(), It.IsAny<CrdtMetadata>(), It.IsAny<CancellationToken>()), Times.Once);
        mockStorage.Verify(x => x.InsertHeaderChunkAsync(logicalKey, headerChunk, It.IsAny<CancellationToken>()), Times.Once);

        // Verified Data Partition Compaction (Items & Tags = 2 properties)
        mockStorage.Verify(x => x.SaveChunkContentAsync(logicalKey, It.IsAny<string>(), dataChunk, It.IsAny<MultiPartitionedModel>(), It.IsAny<CrdtMetadata>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        mockStorage.Verify(x => x.DeletePropertyChunkAsync(It.IsAny<string>(), dataChunk, It.IsAny<CancellationToken>()), Times.Exactly(2));
        mockStorage.Verify(x => x.InsertPropertyChunkAsync(It.IsAny<string>(), dataChunk, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task InitializeAsync_ShouldAttemptPiggybackedCompaction_BeforeSplitting()
    {
        // Arrange
        var (manager, mockStorage, _) = CreateManager(withPolicy: true);
        var initialObject = new MultiPartitionedModel { TenantId = "tenant-1" };
        
        // Add enough items to trigger a split based on item count
        for (int i = 0; i < ChunkedDocumentManager<MultiPartitionedModel>.MaxChunkItemCount + 1; i++)
        {
            initialObject.Items.Add($"k{i}", $"v{i}");
            initialObject.Tags.Add($"k{i}", $"v{i}");
        }
        
        var oversizePartition = new CollectionChunk(new CompositeChunkKey("tenant-1", null), null, 0, 0, 0, 0);
        var compactedPartition = new CollectionChunk(new CompositeChunkKey("tenant-1", null), null, 0, 0, 0, 0);

        mockStorage.Setup(x => x.SaveHeaderChunkContentAsync(It.IsAny<IComparable>(), It.IsAny<HeaderChunk>(), It.IsAny<MultiPartitionedModel>(), It.IsAny<CrdtMetadata>(), default))
            .ReturnsAsync((IComparable k, HeaderChunk p, MultiPartitionedModel d, CrdtMetadata m, CancellationToken c) => p);

        // First call to SaveChunkContent returns an oversized partition
        // Second call (inside Piggybacked Compaction) returns a smaller (compacted) partition
        mockStorage.SetupSequence(x => x.SaveChunkContentAsync(It.IsAny<IComparable>(), It.IsAny<string>(), It.IsAny<IChunk>(), It.IsAny<MultiPartitionedModel>(), It.IsAny<CrdtMetadata>(), default))
            .ReturnsAsync(oversizePartition)   // Items
            .ReturnsAsync(compactedPartition)  // Items - Piggybacked
            .ReturnsAsync(oversizePartition)   // Tags
            .ReturnsAsync(compactedPartition); // Tags - Piggybacked

        // Returning a document with 0 items simulates that compaction successfully reduced the partition's data size
        mockStorage.Setup(x => x.LoadChunkContentAsync<MultiPartitionedModel>(It.IsAny<IComparable>(), It.IsAny<string>(), It.IsAny<IChunk>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CrdtDocument<MultiPartitionedModel>(new MultiPartitionedModel(), new CrdtMetadata()));

        // Act
        await manager.InitializeAsync(initialObject);

        // Assert
        // Ensure split logic was bypassed and it merely deleted and replaced the shrunken partition.
        // It should be called once during Init, and once during the piggybacked save replacement. (Total 2x per property)
        mockStorage.Verify(x => x.InsertPropertyChunkAsync(nameof(MultiPartitionedModel.Items), It.IsAny<IChunk>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        mockStorage.Verify(x => x.DeletePropertyChunkAsync(nameof(MultiPartitionedModel.Items), oversizePartition, It.IsAny<CancellationToken>()), Times.Once);
        
        mockStorage.Verify(x => x.InsertPropertyChunkAsync(nameof(MultiPartitionedModel.Tags), It.IsAny<IChunk>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        mockStorage.Verify(x => x.DeletePropertyChunkAsync(nameof(MultiPartitionedModel.Tags), oversizePartition, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TryApplyPatchAsync_UnderLimit_ShouldSavePartition_ViaStorageService()
    {
        // Arrange
        var (manager, mockStorage, provider) = CreateManager();
        var timestampProvider = provider.GetRequiredService<ICrdtTimestampProvider>();
        var metaManager = provider.GetRequiredService<ICrdtMetadataManager>();
        
        var logicalKey = "tenant-1";
        var propName = nameof(MultiPartitionedModel.Items);
        var existingPartition = new CollectionChunk(new CompositeChunkKey(logicalKey, "key1"), null, 0, 1000, 0, 0);
        var headerPartition = new HeaderChunk(new CompositeChunkKey(logicalKey, null), 0, 0, 0, 0);

        var doc = new MultiPartitionedModel { TenantId = logicalKey, Items = { { "key1", "val1" } } };
        var crdtDoc = new CrdtDocument<MultiPartitionedModel>(doc, metaManager.Initialize(doc));

        mockStorage.Setup(x => x.GetPropertyChunkAsync(It.IsAny<CompositeChunkKey>(), propName, default)).ReturnsAsync(existingPartition);
        mockStorage.Setup(x => x.LoadChunkContentAsync<MultiPartitionedModel>(logicalKey, propName, It.IsAny<IChunk>(), default)).ReturnsAsync(crdtDoc);
        mockStorage.Setup(x => x.GetHeaderChunkAsync(logicalKey, default)).ReturnsAsync(headerPartition);
        mockStorage.Setup(x => x.LoadHeaderChunkContentAsync<MultiPartitionedModel>(logicalKey, headerPartition, default)).ReturnsAsync(crdtDoc);

        // Returns a normal sized partition when saved
        var updatedPartition = new CollectionChunk(existingPartition.StartKey, existingPartition.EndKey, 0, 100, 0, 0);
        mockStorage.Setup(x => x.SaveChunkContentAsync(logicalKey, propName, existingPartition, It.IsAny<MultiPartitionedModel>(), It.IsAny<CrdtMetadata>(), default))
            .ReturnsAsync(updatedPartition);

        var patch = new CrdtPatch([new CrdtOperation(Guid.NewGuid(), "A", "$.items", OperationType.Upsert, new OrMapAddItem("key2", "val2", Guid.NewGuid()), timestampProvider.Now(), 0)]);

        var mockInnerApplicator = new Mock<IAsyncCrdtApplicator>();
        mockInnerApplicator.Setup(x => x.ApplyPatchAsync(It.IsAny<CrdtDocument<MultiPartitionedModel>>(), It.IsAny<CrdtPatch>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CrdtDocument<MultiPartitionedModel> d, CrdtPatch p, CancellationToken c) => new ApplyPatchResult<MultiPartitionedModel>(d, new List<UnappliedOperation>()));

        // Act
        await manager.TryApplyPatchAsync(mockInnerApplicator.Object, crdtDoc, patch);

        // Assert
        // ApplyPatchAsync called once for the items operation, and zero times for the header.
        mockInnerApplicator.Verify(x => x.ApplyPatchAsync(It.IsAny<CrdtDocument<MultiPartitionedModel>>(), It.IsAny<CrdtPatch>(), default), Times.Once);
        mockStorage.Verify(x => x.UpdatePropertyChunkAsync(propName, updatedPartition, default), Times.Once);
        mockStorage.Verify(x => x.DeletePropertyChunkAsync(It.IsAny<string>(), It.IsAny<IChunk>(), default), Times.Never);
        mockStorage.Verify(x => x.InsertPropertyChunkAsync(It.IsAny<string>(), It.IsAny<IChunk>(), default), Times.Never);
    }

    [Fact]
    public async Task TryApplyPatchAsync_OverLimit_ShouldSplitPartition_ViaStorageService()
    {
        // Arrange
        var (manager, mockStorage, provider) = CreateManager();
        var timestampProvider = provider.GetRequiredService<ICrdtTimestampProvider>();
        var metaManager = provider.GetRequiredService<ICrdtMetadataManager>();
        
        var logicalKey = "tenant-1";
        var propName = nameof(MultiPartitionedModel.Items);
        var existingPartition = new CollectionChunk(new CompositeChunkKey(logicalKey, "item1"), null, 0, 0, 0, 0);
        var headerPartition = new HeaderChunk(new CompositeChunkKey(logicalKey, null), 0, 0, 0, 0);

        var doc = new MultiPartitionedModel { TenantId = logicalKey };
        // Populate > MaxChunkItemCount to trigger a split based on item count
        for (int i = 0; i < 105; i++)
        {
            doc.Items.Add($"item{i}", $"val{i}");
        }
        var crdtDoc = new CrdtDocument<MultiPartitionedModel>(doc, metaManager.Initialize(doc));

        mockStorage.Setup(x => x.GetPropertyChunkAsync(It.IsAny<CompositeChunkKey>(), propName, default)).ReturnsAsync(existingPartition);
        
        mockStorage.Setup(x => x.LoadChunkContentAsync<MultiPartitionedModel>(logicalKey, propName, It.IsAny<IChunk>(), default)).ReturnsAsync(crdtDoc);
        mockStorage.Setup(x => x.GetHeaderChunkAsync(logicalKey, default)).ReturnsAsync(headerPartition);
        mockStorage.Setup(x => x.LoadHeaderChunkContentAsync<MultiPartitionedModel>(logicalKey, headerPartition, default)).ReturnsAsync(crdtDoc);

        // First save returns a partition that will be split due to item count (DataLength size parameter is ignored)
        var largePartition = new CollectionChunk(existingPartition.StartKey, existingPartition.EndKey, 0, 0, 0, 0);
        mockStorage.Setup(x => x.SaveChunkContentAsync(logicalKey, propName, existingPartition, It.IsAny<MultiPartitionedModel>(), It.IsAny<CrdtMetadata>(), default))
            .ReturnsAsync(largePartition);

        // Follow up saves during the split return cleanly
        mockStorage.Setup(x => x.SaveChunkContentAsync(logicalKey, propName, It.Is<IChunk>(p => p != null && !p.Equals(existingPartition)), It.IsAny<MultiPartitionedModel>(), It.IsAny<CrdtMetadata>(), default))
            .ReturnsAsync((IComparable k, string pName, IChunk p, MultiPartitionedModel d, CrdtMetadata m, CancellationToken c) => p);

        var patch = new CrdtPatch([new CrdtOperation(Guid.NewGuid(), "A", "$.items", OperationType.Upsert, new OrMapAddItem("item_new", "val_new", Guid.NewGuid()), timestampProvider.Now(), 0)]);

        var mockInnerApplicator = new Mock<IAsyncCrdtApplicator>();
        mockInnerApplicator.Setup(x => x.ApplyPatchAsync(It.IsAny<CrdtDocument<MultiPartitionedModel>>(), It.IsAny<CrdtPatch>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CrdtDocument<MultiPartitionedModel> d, CrdtPatch p, CancellationToken c) => new ApplyPatchResult<MultiPartitionedModel>(d, new List<UnappliedOperation>()));

        // Act
        await manager.TryApplyPatchAsync(mockInnerApplicator.Object, crdtDoc, patch);

        // Assert
        mockInnerApplicator.Verify(x => x.ApplyPatchAsync(It.IsAny<CrdtDocument<MultiPartitionedModel>>(), It.IsAny<CrdtPatch>(), default), Times.Once);
        mockStorage.Verify(x => x.SaveChunkContentAsync(logicalKey, propName, existingPartition, It.IsAny<MultiPartitionedModel>(), It.IsAny<CrdtMetadata>(), default), Times.Once);
        mockStorage.Verify(x => x.DeletePropertyChunkAsync(propName, largePartition, default), Times.Once);
        mockStorage.Verify(x => x.InsertPropertyChunkAsync(propName, It.IsAny<IChunk>(), default), Times.Exactly(2));
    }

    [Fact]
    public async Task TryApplyPatchAsync_UnderMinLimit_ShouldMergePartition_ViaStorageService()
    {
        // Arrange
        var (manager, mockStorage, provider) = CreateManager();
        var timestampProvider = provider.GetRequiredService<ICrdtTimestampProvider>();
        var metaManager = provider.GetRequiredService<ICrdtMetadataManager>();
        
        var logicalKey = "tenant-1";
        var propName = nameof(MultiPartitionedModel.Items);
        
        var dp1 = new CollectionChunk(new CompositeChunkKey(logicalKey, "item1"), new CompositeChunkKey(logicalKey, "item5"), 0, 0, 0, 0);
        var dp2 = new CollectionChunk(new CompositeChunkKey(logicalKey, "item5"), null, 0, 0, 0, 0);
        var headerPartition = new HeaderChunk(new CompositeChunkKey(logicalKey, null), 0, 0, 0, 0);

        var doc1 = new MultiPartitionedModel { TenantId = logicalKey, Items = { { "item1", "val1" } } };
        var crdtDoc1 = new CrdtDocument<MultiPartitionedModel>(doc1, metaManager.Initialize(doc1));
        
        // Use a document that will be evaluated as having < MinChunkItemCount
        var doc2 = new MultiPartitionedModel { TenantId = logicalKey, Items = { { "item5", "val5" } } };
        var crdtDoc2 = new CrdtDocument<MultiPartitionedModel>(doc2, metaManager.Initialize(doc2));

        var smallPartition = new CollectionChunk(dp2.StartKey, dp2.EndKey, 0, 100, 0, 0);

        mockStorage.Setup(x => x.GetPropertyChunkAsync(It.IsAny<CompositeChunkKey>(), propName, default))
            .ReturnsAsync((CompositeChunkKey k, string p, CancellationToken c) => 
            {
                if (k.RangeKey as string == "item1") return dp1;
                if (k.RangeKey as string == "item5") return smallPartition;
                return dp2; // default for "item6" during ApplyPatch
            });

        mockStorage.Setup(x => x.LoadChunkContentAsync<MultiPartitionedModel>(logicalKey, propName, It.IsAny<IChunk>(), default))
            .ReturnsAsync((IComparable k, string p, IChunk part, CancellationToken c) => 
            {
                if (part != null && part.Equals(dp1)) return crdtDoc1;
                return crdtDoc2; // for dp2 and smallPartition
            });

        mockStorage.Setup(x => x.GetHeaderChunkAsync(logicalKey, default)).ReturnsAsync(headerPartition);
        mockStorage.Setup(x => x.LoadHeaderChunkContentAsync<MultiPartitionedModel>(logicalKey, headerPartition, default)).ReturnsAsync(crdtDoc1);

        // When applying the patch, simulate the partition returning representing fewer than 25 items
        mockStorage.Setup(x => x.SaveChunkContentAsync(logicalKey, propName, dp2, It.IsAny<MultiPartitionedModel>(), It.IsAny<CrdtMetadata>(), default))
            .ReturnsAsync(smallPartition);

        // Simulating the merge logic - Partition count > 1 allows merges
        mockStorage.Setup(x => x.GetPropertyChunkCountAsync(logicalKey, propName, default)).ReturnsAsync(2);
        
        // Return dp1 as the previous partition via index lookup for the merge resolution
        mockStorage.Setup(x => x.GetPropertyChunkByIndexAsync(logicalKey, 0, propName, default)).ReturnsAsync(dp1);

        // The save for the final merged partition
        mockStorage.Setup(x => x.SaveChunkContentAsync(logicalKey, propName, It.Is<IChunk>(p => p != null && p is CollectionChunk && ((CollectionChunk)p).StartKey.Equals(dp1.StartKey) && ((CollectionChunk)p).EndKey == null), It.IsAny<MultiPartitionedModel>(), It.IsAny<CrdtMetadata>(), default))
            .ReturnsAsync((IComparable k, string pName, IChunk p, MultiPartitionedModel d, CrdtMetadata m, CancellationToken c) => p);

        var patch = new CrdtPatch([new CrdtOperation(Guid.NewGuid(), "A", "$.items", OperationType.Remove, new OrMapRemoveItem("item6", new HashSet<Guid>()), timestampProvider.Now(), 0)]);

        var mockInnerApplicator = new Mock<IAsyncCrdtApplicator>();
        mockInnerApplicator.Setup(x => x.ApplyPatchAsync(It.IsAny<CrdtDocument<MultiPartitionedModel>>(), It.IsAny<CrdtPatch>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CrdtDocument<MultiPartitionedModel> d, CrdtPatch p, CancellationToken c) => new ApplyPatchResult<MultiPartitionedModel>(d, new List<UnappliedOperation>()));

        // Act
        await manager.TryApplyPatchAsync(mockInnerApplicator.Object, crdtDoc1, patch);

        // Assert
        mockInnerApplicator.Verify(x => x.ApplyPatchAsync(It.IsAny<CrdtDocument<MultiPartitionedModel>>(), It.IsAny<CrdtPatch>(), default), Times.Once);
        mockStorage.Verify(x => x.DeletePropertyChunkAsync(propName, dp1, default), Times.Once);
        mockStorage.Verify(x => x.DeletePropertyChunkAsync(propName, smallPartition, default), Times.Once);
        mockStorage.Verify(x => x.InsertPropertyChunkAsync(propName, It.Is<IChunk>(p => p != null && p is CollectionChunk && ((CollectionChunk)p).StartKey.Equals(dp1.StartKey)), default), Times.Once);
    }

    [Fact]
    public async Task TryApplyPatchAsync_ModifiesHeaderMetadata_ButStripsDataMetadata()
    {
        // Arrange
        var (manager, mockStorage, provider) = CreateManager();
        var timestampProvider = provider.GetRequiredService<ICrdtTimestampProvider>();
        var metaManager = provider.GetRequiredService<ICrdtMetadataManager>();
        
        var logicalKey = "tenant-metadata-test";
        var propName = nameof(MultiPartitionedModel.Items);
        var existingPartition = new CollectionChunk(new CompositeChunkKey(logicalKey, "k1"), null, 0, 1000, 0, 0);
        var headerPartition = new HeaderChunk(new CompositeChunkKey(logicalKey, null), 0, 0, 0, 0);

        var doc = new MultiPartitionedModel { TenantId = logicalKey };
        
        var headerCrdtDoc = new CrdtDocument<MultiPartitionedModel>(doc, metaManager.Initialize(doc));
        // Inject dummy global state
        long currentClock = 100L;
        headerCrdtDoc.Metadata!.VersionVector["replica-1"] = currentClock;

        var dataCrdtDoc = new CrdtDocument<MultiPartitionedModel>(doc, metaManager.Initialize(doc));
        // Clear global state from data doc (as it would be loaded from store)
        dataCrdtDoc.Metadata!.VersionVector.Clear();
        dataCrdtDoc.Metadata.SeenExceptions.Clear();

        mockStorage.Setup(x => x.GetHeaderChunkAsync(logicalKey, default)).ReturnsAsync(headerPartition);
        mockStorage.Setup(x => x.LoadHeaderChunkContentAsync<MultiPartitionedModel>(logicalKey, headerPartition, default)).ReturnsAsync(headerCrdtDoc);
        
        mockStorage.Setup(x => x.GetPropertyChunkAsync(It.IsAny<CompositeChunkKey>(), propName, default)).ReturnsAsync(existingPartition);
        mockStorage.Setup(x => x.LoadChunkContentAsync<MultiPartitionedModel>(logicalKey, propName, It.IsAny<IChunk>(), default)).ReturnsAsync(dataCrdtDoc);

        mockStorage.Setup(x => x.SaveChunkContentAsync(logicalKey, propName, existingPartition, It.IsAny<MultiPartitionedModel>(), It.IsAny<CrdtMetadata>(), default))
            .ReturnsAsync(existingPartition);
            
        mockStorage.Setup(x => x.SaveHeaderChunkContentAsync(logicalKey, headerPartition, It.IsAny<MultiPartitionedModel>(), It.IsAny<CrdtMetadata>(), default))
            .ReturnsAsync(headerPartition);

        var operationTs = timestampProvider.Now();
        var patch = new CrdtPatch([new CrdtOperation(Guid.NewGuid(), "replica-2", "$.items", OperationType.Upsert, new OrMapAddItem("k1", "v1", Guid.NewGuid()), operationTs, 0)]);

        var mockInnerApplicator = new Mock<IAsyncCrdtApplicator>();
        mockInnerApplicator.Setup(x => x.ApplyPatchAsync(It.IsAny<CrdtDocument<MultiPartitionedModel>>(), It.IsAny<CrdtPatch>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CrdtDocument<MultiPartitionedModel> d, CrdtPatch p, CancellationToken c) => new ApplyPatchResult<MultiPartitionedModel>(d, new List<UnappliedOperation>()));

        // Act
        await manager.TryApplyPatchAsync(mockInnerApplicator.Object, headerCrdtDoc, patch);

        // Assert
        mockInnerApplicator.Verify(x => x.ApplyPatchAsync(It.IsAny<CrdtDocument<MultiPartitionedModel>>(), It.IsAny<CrdtPatch>(), default), Times.Once);

        // 1. Data partition was saved, and its metadata has NO version vector / seen exceptions
        mockStorage.Verify(x => x.SaveChunkContentAsync(
            logicalKey, 
            propName, 
            existingPartition, 
            It.IsAny<MultiPartitionedModel>(), 
            It.Is<CrdtMetadata>(m => m.VersionVector.Count == 0 && m.SeenExceptions.Count == 0), 
            default), Times.Once);

        // 2. Header partition was saved, and its metadata HAS the retained version vector for replica-1.
        // Even though no header operations applied, it persists the partition back because data ops trigger a save.
        mockStorage.Verify(x => x.SaveHeaderChunkContentAsync(
            logicalKey, 
            headerPartition, 
            It.IsAny<MultiPartitionedModel>(), 
            It.Is<CrdtMetadata>(m => m.VersionVector.ContainsKey("replica-1") && m.VersionVector["replica-1"] == currentClock), 
            default), Times.Once);
    }

    private static async IAsyncEnumerable<T> CreateAsyncEnumerable<T>(params T[] items)
    {
        foreach (var item in items)
        {
            yield return item;
        }
        await Task.CompletedTask;
    }
}