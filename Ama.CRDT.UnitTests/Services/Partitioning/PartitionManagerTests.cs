namespace Ama.CRDT.UnitTests.Services.Partitioning;

using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Partitioning;
using Ama.CRDT.Services.Providers;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
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
            .AddSingleton(mockStorage.Object) // Inject the mock instead of StreamPartitionStorageService
            .BuildServiceProvider();

        var scopeFactory = services.GetRequiredService<ICrdtScopeFactory>();
        var scope = scopeFactory.CreateScope("TestReplica");
        
        manager = scope.ServiceProvider.GetRequiredService<IPartitionManager<MultiPartitionedModel>>();
        metaManager = scope.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();
        timestampProvider = scope.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();
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
}