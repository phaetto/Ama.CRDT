namespace Ama.CRDT.UnitTests.Services.Decorators;

using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Models.LargerThanMemory;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Decorators;
using Ama.CRDT.Services.LargerThanMemory;
using Ama.CRDT.Services.Metrics;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.UnitTests.Services.LargerThanMemory;
using Ama.CRDT.UnitTests.Services.Partitioning;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

public sealed class PartitioningApplicatorDecoratorTests
{
    private readonly Mock<IPartitionStorageService> mockStorage;
    private readonly Mock<IAsyncCrdtApplicator> mockInnerApplicator;
    private readonly PartitioningApplicatorDecorator decorator;
    private readonly ICrdtMetadataManager metaManager;
    private readonly ICrdtTimestampProvider timestampProvider;

    public PartitioningApplicatorDecoratorTests()
    {
        mockStorage = new Mock<IPartitionStorageService>();
        mockInnerApplicator = new Mock<IAsyncCrdtApplicator>();

        // Setup the inner applicator mock to act as a pass-through
        mockInnerApplicator.Setup(x => x.ApplyPatchAsync(It.IsAny<CrdtDocument<MultiPartitionedModel>>(), It.IsAny<CrdtPatch>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CrdtDocument<MultiPartitionedModel> doc, CrdtPatch p, CancellationToken c) => new ApplyPatchResult<MultiPartitionedModel>(doc, new List<UnappliedOperation>()));

        var meterFactoryMock = new Mock<IMeterFactory>();
        meterFactoryMock.Setup(f => f.Create(It.IsAny<MeterOptions>())).Returns(new Meter("TestMeter"));

        var services = new ServiceCollection()
            .AddCrdt()
            .AddSingleton<CrdtAotContext, DecoratorsTestCrdtAotContext>()
            .AddSingleton<CrdtAotContext, PartitioningTestCrdtAotContext>()
            .AddSingleton(meterFactoryMock.Object)
            .BuildServiceProvider();

        var scopeFactory = services.GetRequiredService<ICrdtScopeFactory>();
        var scope = scopeFactory.CreateScope("TestReplica");
        
        metaManager = scope.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();
        timestampProvider = scope.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();
        
        var strategyProvider = scope.ServiceProvider.GetRequiredService<ICrdtStrategyProvider>();
        var metrics = scope.ServiceProvider.GetRequiredService<LargerThanMemoryManagerCrdtMetrics>();
        var aotContexts = new CrdtAotContext[] { new DecoratorsTestCrdtAotContext(), new PartitioningTestCrdtAotContext() };

        decorator = new PartitioningApplicatorDecorator(
            mockInnerApplicator.Object,
            mockStorage.Object,
            strategyProvider,
            metrics,
            aotContexts,
            DecoratorBehavior.Complex);
    }

    [Fact]
    public async Task ApplyPatchAsync_UnderLimit_ShouldSavePartition_ViaStorageService()
    {
        // Arrange
        var logicalKey = "tenant-1";
        var propName = nameof(MultiPartitionedModel.Items);
        var existingPartition = new CollectionChunk(new CompositeChunkKey(logicalKey, "key1"), null, 0, 1000, 0, 0);
        var headerPartition = new HeaderChunk(new CompositeChunkKey(logicalKey, null), 0, 0, 0, 0);

        var doc = new MultiPartitionedModel { TenantId = logicalKey, Items = { { "key1", "val1" } } };
        var crdtDoc = new CrdtDocument<MultiPartitionedModel>(doc, metaManager.Initialize(doc));

        mockStorage.Setup(x => x.GetPropertyPartitionAsync(It.IsAny<CompositeChunkKey>(), propName, default)).ReturnsAsync(existingPartition);
        mockStorage.Setup(x => x.LoadPartitionContentAsync<MultiPartitionedModel>(logicalKey, propName, It.IsAny<IChunk>(), default)).ReturnsAsync(crdtDoc);
        mockStorage.Setup(x => x.GetHeaderPartitionAsync(logicalKey, default)).ReturnsAsync(headerPartition);
        mockStorage.Setup(x => x.LoadHeaderPartitionContentAsync<MultiPartitionedModel>(logicalKey, headerPartition, default)).ReturnsAsync(crdtDoc);

        // Returns a normal sized partition when saved
        var updatedPartition = new CollectionChunk(existingPartition.StartKey, existingPartition.EndKey, 0, 100, 0, 0);
        mockStorage.Setup(x => x.SavePartitionContentAsync(logicalKey, propName, existingPartition, It.IsAny<MultiPartitionedModel>(), It.IsAny<CrdtMetadata>(), default))
            .ReturnsAsync(updatedPartition);

        var patch = new CrdtPatch([new CrdtOperation(Guid.NewGuid(), "A", "$.items", OperationType.Upsert, new OrMapAddItem("key2", "val2", Guid.NewGuid()), timestampProvider.Now(), 0)]);

        // Act
        await decorator.ApplyPatchAsync(crdtDoc, patch);

        // Assert
        mockInnerApplicator.Verify(x => x.ApplyPatchAsync(It.IsAny<CrdtDocument<MultiPartitionedModel>>(), It.IsAny<CrdtPatch>(), default), Times.Once);
        mockStorage.Verify(x => x.UpdatePropertyPartitionAsync(propName, updatedPartition, default), Times.Once);
        mockStorage.Verify(x => x.DeletePropertyPartitionAsync(It.IsAny<string>(), It.IsAny<IChunk>(), default), Times.Never);
        mockStorage.Verify(x => x.InsertPropertyPartitionAsync(It.IsAny<string>(), It.IsAny<IChunk>(), default), Times.Never);
    }

    [Fact]
    public async Task ApplyPatchAsync_OverLimit_ShouldSplitPartition_ViaStorageService()
    {
        // Arrange
        var logicalKey = "tenant-1";
        var propName = nameof(MultiPartitionedModel.Items);
        var existingPartition = new CollectionChunk(new CompositeChunkKey(logicalKey, "item1"), null, 0, 0, 0, 0);
        var headerPartition = new HeaderChunk(new CompositeChunkKey(logicalKey, null), 0, 0, 0, 0);

        var doc = new MultiPartitionedModel { TenantId = logicalKey };
        // Populate > MaxPartitionItemCount to trigger a split based on item count
        for (int i = 0; i < 105; i++)
        {
            doc.Items.Add($"item{i}", $"val{i}");
        }
        var crdtDoc = new CrdtDocument<MultiPartitionedModel>(doc, metaManager.Initialize(doc));

        mockStorage.Setup(x => x.GetPropertyPartitionAsync(It.IsAny<CompositeChunkKey>(), propName, default)).ReturnsAsync(existingPartition);
        
        // Use It.IsAny<IChunk> so when SplitPartitionAsync loads the largePartition, it doesn't return a default/null struct.
        mockStorage.Setup(x => x.LoadPartitionContentAsync<MultiPartitionedModel>(logicalKey, propName, It.IsAny<IChunk>(), default)).ReturnsAsync(crdtDoc);
        mockStorage.Setup(x => x.GetHeaderPartitionAsync(logicalKey, default)).ReturnsAsync(headerPartition);
        mockStorage.Setup(x => x.LoadHeaderPartitionContentAsync<MultiPartitionedModel>(logicalKey, headerPartition, default)).ReturnsAsync(crdtDoc);

        // First save returns a partition that will be split due to item count (DataLength size parameter is ignored)
        var largePartition = new CollectionChunk(existingPartition.StartKey, existingPartition.EndKey, 0, 0, 0, 0);
        mockStorage.Setup(x => x.SavePartitionContentAsync(logicalKey, propName, existingPartition, It.IsAny<MultiPartitionedModel>(), It.IsAny<CrdtMetadata>(), default))
            .ReturnsAsync(largePartition);

        // Follow up saves during the split return cleanly
        mockStorage.Setup(x => x.SavePartitionContentAsync(logicalKey, propName, It.Is<IChunk>(p => p != null && !p.Equals(existingPartition)), It.IsAny<MultiPartitionedModel>(), It.IsAny<CrdtMetadata>(), default))
            .ReturnsAsync((IComparable k, string pName, IChunk p, MultiPartitionedModel d, CrdtMetadata m, CancellationToken c) => p);

        var patch = new CrdtPatch([new CrdtOperation(Guid.NewGuid(), "A", "$.items", OperationType.Upsert, new OrMapAddItem("item_new", "val_new", Guid.NewGuid()), timestampProvider.Now(), 0)]);

        // Act
        await decorator.ApplyPatchAsync(crdtDoc, patch);

        // Assert
        mockInnerApplicator.Verify(x => x.ApplyPatchAsync(It.IsAny<CrdtDocument<MultiPartitionedModel>>(), It.IsAny<CrdtPatch>(), default), Times.Once);
        mockStorage.Verify(x => x.SavePartitionContentAsync(logicalKey, propName, existingPartition, It.IsAny<MultiPartitionedModel>(), It.IsAny<CrdtMetadata>(), default), Times.Once);
        mockStorage.Verify(x => x.DeletePropertyPartitionAsync(propName, largePartition, default), Times.Once);
        mockStorage.Verify(x => x.InsertPropertyPartitionAsync(propName, It.IsAny<IChunk>(), default), Times.Exactly(2));
    }

    [Fact]
    public async Task ApplyPatchAsync_UnderMinLimit_ShouldMergePartition_ViaStorageService()
    {
        // Arrange
        var logicalKey = "tenant-1";
        var propName = nameof(MultiPartitionedModel.Items);
        
        var dp1 = new CollectionChunk(new CompositeChunkKey(logicalKey, "item1"), new CompositeChunkKey(logicalKey, "item5"), 0, 0, 0, 0);
        var dp2 = new CollectionChunk(new CompositeChunkKey(logicalKey, "item5"), null, 0, 0, 0, 0);
        var headerPartition = new HeaderChunk(new CompositeChunkKey(logicalKey, null), 0, 0, 0, 0);

        var doc1 = new MultiPartitionedModel { TenantId = logicalKey, Items = { { "item1", "val1" } } };
        var crdtDoc1 = new CrdtDocument<MultiPartitionedModel>(doc1, metaManager.Initialize(doc1));
        
        // Use a document that will be evaluated as having < MinPartitionItemCount (1 item < 25)
        var doc2 = new MultiPartitionedModel { TenantId = logicalKey, Items = { { "item5", "val5" } } };
        var crdtDoc2 = new CrdtDocument<MultiPartitionedModel>(doc2, metaManager.Initialize(doc2));

        var smallPartition = new CollectionChunk(dp2.StartKey, dp2.EndKey, 0, 100, 0, 0);

        mockStorage.Setup(x => x.GetPropertyPartitionAsync(It.IsAny<CompositeChunkKey>(), propName, default))
            .ReturnsAsync((CompositeChunkKey k, string p, CancellationToken c) => 
            {
                if (k.RangeKey as string == "item1") return dp1;
                if (k.RangeKey as string == "item5") return smallPartition;
                return dp2; // default for "item6" during ApplyPatch
            });

        mockStorage.Setup(x => x.LoadPartitionContentAsync<MultiPartitionedModel>(logicalKey, propName, It.IsAny<IChunk>(), default))
            .ReturnsAsync((IComparable k, string p, IChunk part, CancellationToken c) => 
            {
                if (part != null && part.Equals(dp1)) return crdtDoc1;
                return crdtDoc2; // for dp2 and smallPartition
            });

        mockStorage.Setup(x => x.GetHeaderPartitionAsync(logicalKey, default)).ReturnsAsync(headerPartition);
        mockStorage.Setup(x => x.LoadHeaderPartitionContentAsync<MultiPartitionedModel>(logicalKey, headerPartition, default)).ReturnsAsync(crdtDoc1);

        // When applying the patch, simulate the partition returning representing fewer than 25 items
        mockStorage.Setup(x => x.SavePartitionContentAsync(logicalKey, propName, dp2, It.IsAny<MultiPartitionedModel>(), It.IsAny<CrdtMetadata>(), default))
            .ReturnsAsync(smallPartition);

        // Simulating the merge logic - Partition count > 1 allows merges
        mockStorage.Setup(x => x.GetPropertyPartitionCountAsync(logicalKey, propName, default)).ReturnsAsync(2);
        
        // Return dp1 as the previous partition via index lookup for the merge resolution
        mockStorage.Setup(x => x.GetPropertyPartitionByIndexAsync(logicalKey, 0, propName, default)).ReturnsAsync(dp1);

        // The save for the final merged partition
        mockStorage.Setup(x => x.SavePartitionContentAsync(logicalKey, propName, It.Is<IChunk>(p => p != null && p is CollectionChunk && ((CollectionChunk)p).StartKey.Equals(dp1.StartKey) && ((CollectionChunk)p).EndKey == null), It.IsAny<MultiPartitionedModel>(), It.IsAny<CrdtMetadata>(), default))
            .ReturnsAsync((IComparable k, string pName, IChunk p, MultiPartitionedModel d, CrdtMetadata m, CancellationToken c) => p);

        var patch = new CrdtPatch([new CrdtOperation(Guid.NewGuid(), "A", "$.items", OperationType.Remove, new OrMapRemoveItem("item6", new HashSet<Guid>()), timestampProvider.Now(), 0)]);

        // Act
        await decorator.ApplyPatchAsync(crdtDoc1, patch);

        // Assert
        mockInnerApplicator.Verify(x => x.ApplyPatchAsync(It.IsAny<CrdtDocument<MultiPartitionedModel>>(), It.IsAny<CrdtPatch>(), default), Times.Once);
        mockStorage.Verify(x => x.DeletePropertyPartitionAsync(propName, dp1, default), Times.Once);
        mockStorage.Verify(x => x.DeletePropertyPartitionAsync(propName, smallPartition, default), Times.Once);
        mockStorage.Verify(x => x.InsertPropertyPartitionAsync(propName, It.Is<IChunk>(p => p != null && p is CollectionChunk && ((CollectionChunk)p).StartKey.Equals(dp1.StartKey)), default), Times.Once);
    }

    [Fact]
    public async Task ApplyPatchAsync_ModifiesHeaderMetadata_ButStripsDataMetadata()
    {
        // Arrange
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

        mockStorage.Setup(x => x.GetHeaderPartitionAsync(logicalKey, default)).ReturnsAsync(headerPartition);
        mockStorage.Setup(x => x.LoadHeaderPartitionContentAsync<MultiPartitionedModel>(logicalKey, headerPartition, default)).ReturnsAsync(headerCrdtDoc);
        
        mockStorage.Setup(x => x.GetPropertyPartitionAsync(It.IsAny<CompositeChunkKey>(), propName, default)).ReturnsAsync(existingPartition);
        mockStorage.Setup(x => x.LoadPartitionContentAsync<MultiPartitionedModel>(logicalKey, propName, It.IsAny<IChunk>(), default)).ReturnsAsync(dataCrdtDoc);

        mockStorage.Setup(x => x.SavePartitionContentAsync(logicalKey, propName, existingPartition, It.IsAny<MultiPartitionedModel>(), It.IsAny<CrdtMetadata>(), default))
            .ReturnsAsync(existingPartition);
            
        mockStorage.Setup(x => x.SaveHeaderPartitionContentAsync(logicalKey, headerPartition, It.IsAny<MultiPartitionedModel>(), It.IsAny<CrdtMetadata>(), default))
            .ReturnsAsync(headerPartition);

        var operationTs = timestampProvider.Now();
        var patch = new CrdtPatch([new CrdtOperation(Guid.NewGuid(), "replica-2", "$.items", OperationType.Upsert, new OrMapAddItem("k1", "v1", Guid.NewGuid()), operationTs, 0)]);

        // Act
        await decorator.ApplyPatchAsync(headerCrdtDoc, patch);

        // Assert
        mockInnerApplicator.Verify(x => x.ApplyPatchAsync(It.IsAny<CrdtDocument<MultiPartitionedModel>>(), It.IsAny<CrdtPatch>(), default), Times.Once);

        // 1. Data partition was saved, and its metadata has NO version vector / seen exceptions
        mockStorage.Verify(x => x.SavePartitionContentAsync(
            logicalKey, 
            propName, 
            existingPartition, 
            It.IsAny<MultiPartitionedModel>(), 
            It.Is<CrdtMetadata>(m => m.VersionVector.Count == 0 && m.SeenExceptions.Count == 0), 
            default), Times.Once);

        // 2. Header partition was saved, and its metadata HAS the retained version vector for replica-1.
        // Even though no header operations applied, it persists the partition back because data ops trigger a save.
        mockStorage.Verify(x => x.SaveHeaderPartitionContentAsync(
            logicalKey, 
            headerPartition, 
            It.IsAny<MultiPartitionedModel>(), 
            It.Is<CrdtMetadata>(m => m.VersionVector.ContainsKey("replica-1") && m.VersionVector["replica-1"] == currentClock), 
            default), Times.Once);
    }
}