namespace Ama.CRDT.UnitTests.Services.Partitioning;

using Ama.CRDT.Attributes;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Metrics;
using Ama.CRDT.Services.Partitioning;
using Ama.CRDT.Services.Providers;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

[PartitionKey(nameof(TenantId))]
public sealed class MultiPartitionedModel
{
    public string TenantId { get; set; } = "";
    public string HeaderData { get; set; } = "Initial";

    [CrdtOrMapStrategy]
    public Dictionary<string, string> Items { get; set; } = new();
    
    [CrdtOrMapStrategy]
    public Dictionary<string, string> Tags { get; set; } = new();
}

public sealed class PartitionManagerTests
{
    private readonly Mock<IPartitionStorageService> mockStorage;
    private readonly IPartitionManager<MultiPartitionedModel> manager;
    private readonly ICrdtMetadataManager metaManager;
    private readonly ICrdtTimestampProvider timestampProvider;

    public PartitionManagerTests()
    {
        mockStorage = new Mock<IPartitionStorageService>();

        var meterFactoryMock = new Mock<IMeterFactory>();
        meterFactoryMock.Setup(f => f.Create(It.IsAny<MeterOptions>())).Returns(new Meter("TestMeter"));

        var services = new ServiceCollection()
            .AddCrdt()
            .AddSingleton(meterFactoryMock.Object)
            .AddSingleton(mockStorage.Object) // Inject the mock instead of BPlusTreePartitionStorageService
            .BuildServiceProvider();

        var scopeFactory = services.GetRequiredService<ICrdtScopeFactory>();
        var scope = scopeFactory.CreateScope("TestReplica");
        
        manager = scope.ServiceProvider.GetRequiredService<IPartitionManager<MultiPartitionedModel>>();
        metaManager = scope.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();
        timestampProvider = scope.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();
    }

    private async IAsyncEnumerable<T> AsAsyncEnumerable<T>(IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            yield return item;
            await Task.CompletedTask;
        }
    }

    [Fact]
    public async Task InitializeAsync_ShouldCallStorageServiceMethods()
    {
        // Arrange
        var initialObject = new MultiPartitionedModel { TenantId = "tenant-1" };
        
        mockStorage.Setup(x => x.SaveHeaderPartitionContentAsync(It.IsAny<IComparable>(), It.IsAny<HeaderPartition>(), It.IsAny<MultiPartitionedModel>(), It.IsAny<CrdtMetadata>(), default))
            .ReturnsAsync((IComparable k, HeaderPartition p, MultiPartitionedModel d, CrdtMetadata m, CancellationToken c) => p);
        
        mockStorage.Setup(x => x.SavePartitionContentAsync(It.IsAny<IComparable>(), It.IsAny<string>(), It.IsAny<IPartition>(), It.IsAny<MultiPartitionedModel>(), It.IsAny<CrdtMetadata>(), default))
            .ReturnsAsync((IComparable k, string prop, IPartition p, MultiPartitionedModel d, CrdtMetadata m, CancellationToken c) => p);

        // Act
        await manager.InitializeAsync(initialObject);

        // Assert
        mockStorage.Verify(x => x.InitializeHeaderIndexAsync(default), Times.Once);
        mockStorage.Verify(x => x.ClearHeaderDataAsync("tenant-1", default), Times.Once);
        mockStorage.Verify(x => x.InsertHeaderPartitionAsync("tenant-1", It.IsAny<HeaderPartition>(), default), Times.Once);

        mockStorage.Verify(x => x.InitializePropertyIndexAsync(nameof(MultiPartitionedModel.Items), default), Times.Once);
        mockStorage.Verify(x => x.ClearPropertyDataAsync("tenant-1", nameof(MultiPartitionedModel.Items), default), Times.Once);
        mockStorage.Verify(x => x.InsertPropertyPartitionAsync(nameof(MultiPartitionedModel.Items), It.IsAny<IPartition>(), default), Times.Once);
        
        mockStorage.Verify(x => x.InitializePropertyIndexAsync(nameof(MultiPartitionedModel.Tags), default), Times.Once);
        mockStorage.Verify(x => x.ClearPropertyDataAsync("tenant-1", nameof(MultiPartitionedModel.Tags), default), Times.Once);
        mockStorage.Verify(x => x.InsertPropertyPartitionAsync(nameof(MultiPartitionedModel.Tags), It.IsAny<IPartition>(), default), Times.Once);
    }

    [Fact]
    public async Task ApplyPatchAsync_UnderLimit_ShouldSavePartition_ViaStorageService()
    {
        // Arrange
        var logicalKey = "tenant-1";
        var propName = nameof(MultiPartitionedModel.Items);
        var existingPartition = new DataPartition(new CompositePartitionKey(logicalKey, "key1"), null, 0, 1000, 0, 0);
        var headerPartition = new HeaderPartition(new CompositePartitionKey(logicalKey, null), 0, 0, 0, 0);

        var doc = new MultiPartitionedModel { TenantId = logicalKey, Items = { { "key1", "val1" } } };
        var crdtDoc = new CrdtDocument<MultiPartitionedModel>(doc, metaManager.Initialize(doc));

        mockStorage.Setup(x => x.GetPropertyPartitionAsync(It.IsAny<CompositePartitionKey>(), propName, default)).ReturnsAsync(existingPartition);
        mockStorage.Setup(x => x.LoadPartitionContentAsync<MultiPartitionedModel>(logicalKey, propName, existingPartition, default)).ReturnsAsync(crdtDoc);
        mockStorage.Setup(x => x.GetHeaderPartitionAsync(logicalKey, default)).ReturnsAsync(headerPartition);
        mockStorage.Setup(x => x.LoadHeaderPartitionContentAsync<MultiPartitionedModel>(logicalKey, headerPartition, default)).ReturnsAsync(crdtDoc);

        // Returns a normal sized partition when saved
        var updatedPartition = new DataPartition(existingPartition.StartKey, existingPartition.EndKey, 0, 4000, 0, 0); // DataLength = 4000 < 8192
        mockStorage.Setup(x => x.SavePartitionContentAsync(logicalKey, propName, existingPartition, It.IsAny<MultiPartitionedModel>(), It.IsAny<CrdtMetadata>(), default))
            .ReturnsAsync(updatedPartition);

        var patch = new CrdtPatch([new CrdtOperation(Guid.NewGuid(), "A", "$.items", OperationType.Upsert, new OrMapAddItem("key2", "val2", Guid.NewGuid()), timestampProvider.Now())]) { LogicalKey = logicalKey };

        // Act
        await manager.ApplyPatchAsync(patch);

        // Assert
        mockStorage.Verify(x => x.UpdatePropertyPartitionAsync(propName, updatedPartition, default), Times.Once);
        mockStorage.Verify(x => x.DeletePropertyPartitionAsync(It.IsAny<string>(), It.IsAny<IPartition>(), default), Times.Never);
        mockStorage.Verify(x => x.InsertPropertyPartitionAsync(It.IsAny<string>(), It.IsAny<IPartition>(), default), Times.Never);
    }

    [Fact]
    public async Task ApplyPatchAsync_OverLimit_ShouldSplitPartition_ViaStorageService()
    {
        // Arrange
        var logicalKey = "tenant-1";
        var propName = nameof(MultiPartitionedModel.Items);
        var existingPartition = new DataPartition(new CompositePartitionKey(logicalKey, "item1"), null, 0, 0, 0, 0);
        var headerPartition = new HeaderPartition(new CompositePartitionKey(logicalKey, null), 0, 0, 0, 0);

        var doc = new MultiPartitionedModel { TenantId = logicalKey, Items = { { "item1", "val1" }, { "item2", "val2" } } }; // Items to allow split
        var crdtDoc = new CrdtDocument<MultiPartitionedModel>(doc, metaManager.Initialize(doc));

        mockStorage.Setup(x => x.GetPropertyPartitionAsync(It.IsAny<CompositePartitionKey>(), propName, default)).ReturnsAsync(existingPartition);
        mockStorage.Setup(x => x.LoadPartitionContentAsync<MultiPartitionedModel>(logicalKey, propName, existingPartition, default)).ReturnsAsync(crdtDoc);
        mockStorage.Setup(x => x.GetHeaderPartitionAsync(logicalKey, default)).ReturnsAsync(headerPartition);
        mockStorage.Setup(x => x.LoadHeaderPartitionContentAsync<MultiPartitionedModel>(logicalKey, headerPartition, default)).ReturnsAsync(crdtDoc);

        // First save returns a huge partition that breaches 8192
        var largePartition = new DataPartition(existingPartition.StartKey, existingPartition.EndKey, 0, 9000, 0, 0);
        mockStorage.Setup(x => x.SavePartitionContentAsync(logicalKey, propName, existingPartition, It.IsAny<MultiPartitionedModel>(), It.IsAny<CrdtMetadata>(), default))
            .ReturnsAsync(largePartition);

        // Follow up saves during the split return cleanly
        mockStorage.Setup(x => x.SavePartitionContentAsync(logicalKey, propName, It.Is<IPartition>(p => p != null && !p.Equals(existingPartition)), It.IsAny<MultiPartitionedModel>(), It.IsAny<CrdtMetadata>(), default))
            .ReturnsAsync((IComparable k, string pName, IPartition p, MultiPartitionedModel d, CrdtMetadata m, CancellationToken c) => p);

        var patch = new CrdtPatch([new CrdtOperation(Guid.NewGuid(), "A", "$.items", OperationType.Upsert, new OrMapAddItem("item3", "val3", Guid.NewGuid()), timestampProvider.Now())]) { LogicalKey = logicalKey };

        // Act
        await manager.ApplyPatchAsync(patch);

        // Assert
        mockStorage.Verify(x => x.SavePartitionContentAsync(logicalKey, propName, existingPartition, It.IsAny<MultiPartitionedModel>(), It.IsAny<CrdtMetadata>(), default), Times.Once);
        mockStorage.Verify(x => x.DeletePropertyPartitionAsync(propName, largePartition, default), Times.Once);
        mockStorage.Verify(x => x.InsertPropertyPartitionAsync(propName, It.IsAny<IPartition>(), default), Times.Exactly(2));
    }

    [Fact]
    public async Task ApplyPatchAsync_UnderMinLimit_ShouldMergePartition_ViaStorageService()
    {
        // Arrange
        var logicalKey = "tenant-1";
        var propName = nameof(MultiPartitionedModel.Items);
        
        var dp1 = new DataPartition(new CompositePartitionKey(logicalKey, "item1"), new CompositePartitionKey(logicalKey, "item5"), 0, 0, 0, 0);
        var dp2 = new DataPartition(new CompositePartitionKey(logicalKey, "item5"), null, 0, 0, 0, 0);
        var headerPartition = new HeaderPartition(new CompositePartitionKey(logicalKey, null), 0, 0, 0, 0);

        var doc1 = new MultiPartitionedModel { TenantId = logicalKey, Items = { { "item1", "val1" } } };
        var crdtDoc1 = new CrdtDocument<MultiPartitionedModel>(doc1, metaManager.Initialize(doc1));
        var doc2 = new MultiPartitionedModel { TenantId = logicalKey, Items = { { "item5", "val5" } } };
        var crdtDoc2 = new CrdtDocument<MultiPartitionedModel>(doc2, metaManager.Initialize(doc2));

        var smallPartition = new DataPartition(dp2.StartKey, dp2.EndKey, 0, 100, 0, 0);

        mockStorage.Setup(x => x.GetPropertyPartitionAsync(It.IsAny<CompositePartitionKey>(), propName, default))
            .ReturnsAsync((CompositePartitionKey k, string p, CancellationToken c) => 
            {
                if (k.RangeKey as string == "item1") return dp1;
                if (k.RangeKey as string == "item5") return smallPartition;
                return dp2; // default for "item6" during ApplyPatch
            });

        mockStorage.Setup(x => x.LoadPartitionContentAsync<MultiPartitionedModel>(logicalKey, propName, It.IsAny<IPartition>(), default))
            .ReturnsAsync((IComparable k, string p, IPartition part, CancellationToken c) => 
            {
                if (part != null && part.Equals(dp1)) return crdtDoc1;
                return crdtDoc2; // for dp2 and smallPartition
            });

        mockStorage.Setup(x => x.GetHeaderPartitionAsync(logicalKey, default)).ReturnsAsync(headerPartition);
        mockStorage.Setup(x => x.LoadHeaderPartitionContentAsync<MultiPartitionedModel>(logicalKey, headerPartition, default)).ReturnsAsync(crdtDoc1);

        // When applying the patch, simulate the partition dropping to a very small size (< 2048)
        mockStorage.Setup(x => x.SavePartitionContentAsync(logicalKey, propName, dp2, It.IsAny<MultiPartitionedModel>(), It.IsAny<CrdtMetadata>(), default))
            .ReturnsAsync(smallPartition);

        // Simulating the merge logic
        mockStorage.Setup(x => x.GetPropertyPartitionCountAsync(logicalKey, propName, default)).ReturnsAsync(2);
        
        // Return smallPartition as the current state in the index during iteration
        mockStorage.Setup(x => x.GetPartitionsAsync(logicalKey, propName, default))
            .Returns(AsAsyncEnumerable<IPartition>(new IPartition[] { dp1, smallPartition }));

        // The save for the final merged partition
        mockStorage.Setup(x => x.SavePartitionContentAsync(logicalKey, propName, It.Is<IPartition>(p => p != null && p is DataPartition && ((DataPartition)p).StartKey.Equals(dp1.StartKey) && ((DataPartition)p).EndKey == null), It.IsAny<MultiPartitionedModel>(), It.IsAny<CrdtMetadata>(), default))
            .ReturnsAsync((IComparable k, string pName, IPartition p, MultiPartitionedModel d, CrdtMetadata m, CancellationToken c) => p);

        var patch = new CrdtPatch([new CrdtOperation(Guid.NewGuid(), "A", "$.items", OperationType.Remove, new OrMapRemoveItem("item6", new HashSet<Guid>()), timestampProvider.Now())]) { LogicalKey = logicalKey };

        // Act
        await manager.ApplyPatchAsync(patch);

        // Assert
        mockStorage.Verify(x => x.DeletePropertyPartitionAsync(propName, dp1, default), Times.Once);
        mockStorage.Verify(x => x.DeletePropertyPartitionAsync(propName, smallPartition, default), Times.Once);
        mockStorage.Verify(x => x.InsertPropertyPartitionAsync(propName, It.Is<IPartition>(p => p != null && p is DataPartition && ((DataPartition)p).StartKey.Equals(dp1.StartKey)), default), Times.Once);
    }
}