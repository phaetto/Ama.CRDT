namespace Ama.CRDT.UnitTests.Services.Partitioning;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Services;
using Ama.CRDT.Services.GarbageCollection;
using Ama.CRDT.Services.Partitioning;
using Ama.CRDT.Services.Providers;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
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
    private (IPartitionManager<MultiPartitionedModel> Manager, Mock<IPartitionStorageService> StorageMock) CreateManager(bool withPolicy = false)
    {
        var mockStorage = new Mock<IPartitionStorageService>();
        
        var meterFactoryMock = new Mock<IMeterFactory>();
        meterFactoryMock.Setup(f => f.Create(It.IsAny<MeterOptions>())).Returns(new Meter("TestMeter"));

        var services = new ServiceCollection()
            .AddCrdt()
            .AddSingleton(meterFactoryMock.Object)
            .AddSingleton(mockStorage.Object); // Inject the mock instead of StreamPartitionStorageService

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
        
        var manager = scope.ServiceProvider.GetRequiredService<IPartitionManager<MultiPartitionedModel>>();
        return (manager, mockStorage);
    }

    [Fact]
    public async Task InitializeAsync_ShouldCallStorageServiceMethods()
    {
        // Arrange
        var (manager, mockStorage) = CreateManager();
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
    public async Task CompactAsync_ShouldDoNothing_WhenNoPoliciesRegistered()
    {
        // Arrange
        var (manager, mockStorage) = CreateManager(withPolicy: false);

        // Act
        await manager.CompactAsync();

        // Assert
        mockStorage.Verify(x => x.GetAllHeaderPartitionsAsync(It.IsAny<CancellationToken>()), Times.Never);
        mockStorage.Verify(x => x.SavePartitionContentAsync(It.IsAny<IComparable>(), It.IsAny<string>(), It.IsAny<IPartition>(), It.IsAny<MultiPartitionedModel>(), It.IsAny<CrdtMetadata>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CompactAsync_ShouldIterateAndSavePartitions_WhenPoliciesRegistered()
    {
        // Arrange
        var (manager, mockStorage) = CreateManager(withPolicy: true);
        var logicalKey = "tenant-1";

        var headerPartition = new HeaderPartition(new CompositePartitionKey(logicalKey, null), 0, 0, 0, 0);
        var dataPartition = new DataPartition(new CompositePartitionKey(logicalKey, "item1"), null, 0, 0, 0, 0);

        mockStorage.Setup(x => x.GetAllHeaderPartitionsAsync(It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable<IPartition>(headerPartition));

        mockStorage.Setup(x => x.GetHeaderPartitionAsync(logicalKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(headerPartition);

        mockStorage.Setup(x => x.LoadHeaderPartitionContentAsync<MultiPartitionedModel>(logicalKey, headerPartition, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CrdtDocument<MultiPartitionedModel>(new MultiPartitionedModel(), new CrdtMetadata()));

        mockStorage.Setup(x => x.GetPartitionsAsync(logicalKey, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable<IPartition>(dataPartition));

        mockStorage.Setup(x => x.LoadPartitionContentAsync<MultiPartitionedModel>(logicalKey, It.IsAny<string>(), dataPartition, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CrdtDocument<MultiPartitionedModel>(new MultiPartitionedModel(), new CrdtMetadata()));

        mockStorage.Setup(x => x.SaveHeaderPartitionContentAsync(It.IsAny<IComparable>(), It.IsAny<HeaderPartition>(), It.IsAny<MultiPartitionedModel>(), It.IsAny<CrdtMetadata>(), default))
            .ReturnsAsync((IComparable k, HeaderPartition p, MultiPartitionedModel d, CrdtMetadata m, CancellationToken c) => p);

        mockStorage.Setup(x => x.SavePartitionContentAsync(It.IsAny<IComparable>(), It.IsAny<string>(), It.IsAny<IPartition>(), It.IsAny<MultiPartitionedModel>(), It.IsAny<CrdtMetadata>(), default))
            .ReturnsAsync((IComparable k, string prop, IPartition p, MultiPartitionedModel d, CrdtMetadata m, CancellationToken c) => p);

        // Act
        await manager.CompactAsync();

        // Assert
        // Verified Header Compaction
        mockStorage.Verify(x => x.SaveHeaderPartitionContentAsync(logicalKey, headerPartition, It.IsAny<MultiPartitionedModel>(), It.IsAny<CrdtMetadata>(), It.IsAny<CancellationToken>()), Times.Once);
        mockStorage.Verify(x => x.InsertHeaderPartitionAsync(logicalKey, headerPartition, It.IsAny<CancellationToken>()), Times.Once);

        // Verified Data Partition Compaction (Items & Tags = 2 properties)
        mockStorage.Verify(x => x.SavePartitionContentAsync(logicalKey, It.IsAny<string>(), dataPartition, It.IsAny<MultiPartitionedModel>(), It.IsAny<CrdtMetadata>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        mockStorage.Verify(x => x.DeletePropertyPartitionAsync(It.IsAny<string>(), dataPartition, It.IsAny<CancellationToken>()), Times.Exactly(2));
        mockStorage.Verify(x => x.InsertPropertyPartitionAsync(It.IsAny<string>(), dataPartition, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task InitializeAsync_ShouldAttemptPiggybackedCompaction_BeforeSplitting()
    {
        // Arrange
        var (manager, mockStorage) = CreateManager(withPolicy: true);
        var initialObject = new MultiPartitionedModel { TenantId = "tenant-1" };
        
        var oversizePartition = new DataPartition(new CompositePartitionKey("tenant-1", null), null, 0, PartitionManager<MultiPartitionedModel>.MaxPartitionDataSize + 1000, 0, 0);
        var compactedPartition = new DataPartition(new CompositePartitionKey("tenant-1", null), null, 0, PartitionManager<MultiPartitionedModel>.MaxPartitionDataSize - 1000, 0, 0);

        mockStorage.Setup(x => x.SaveHeaderPartitionContentAsync(It.IsAny<IComparable>(), It.IsAny<HeaderPartition>(), It.IsAny<MultiPartitionedModel>(), It.IsAny<CrdtMetadata>(), default))
            .ReturnsAsync((IComparable k, HeaderPartition p, MultiPartitionedModel d, CrdtMetadata m, CancellationToken c) => p);

        // First call to SavePartitionContent returns an oversized partition
        // Second call (inside Piggybacked Compaction) returns a smaller (compacted) partition
        mockStorage.SetupSequence(x => x.SavePartitionContentAsync(It.IsAny<IComparable>(), It.IsAny<string>(), It.IsAny<IPartition>(), It.IsAny<MultiPartitionedModel>(), It.IsAny<CrdtMetadata>(), default))
            .ReturnsAsync(oversizePartition)   // Items
            .ReturnsAsync(compactedPartition)  // Items - Piggybacked
            .ReturnsAsync(oversizePartition)   // Tags
            .ReturnsAsync(compactedPartition); // Tags - Piggybacked

        mockStorage.Setup(x => x.LoadPartitionContentAsync<MultiPartitionedModel>(It.IsAny<IComparable>(), It.IsAny<string>(), It.IsAny<IPartition>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CrdtDocument<MultiPartitionedModel>(new MultiPartitionedModel(), new CrdtMetadata()));

        // Act
        await manager.InitializeAsync(initialObject);

        // Assert
        // Ensure split logic was bypassed and it merely deleted and replaced the shrunken partition.
        // It should be called once during Init, and once during the piggybacked save replacement. (Total 2x per property)
        mockStorage.Verify(x => x.InsertPropertyPartitionAsync(nameof(MultiPartitionedModel.Items), It.IsAny<IPartition>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        mockStorage.Verify(x => x.DeletePropertyPartitionAsync(nameof(MultiPartitionedModel.Items), oversizePartition, It.IsAny<CancellationToken>()), Times.Once);
        
        mockStorage.Verify(x => x.InsertPropertyPartitionAsync(nameof(MultiPartitionedModel.Tags), It.IsAny<IPartition>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        mockStorage.Verify(x => x.DeletePropertyPartitionAsync(nameof(MultiPartitionedModel.Tags), oversizePartition, It.IsAny<CancellationToken>()), Times.Once);
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