namespace Ama.CRDT.UnitTests.Services.Partitioning;

using Ama.CRDT.Attributes;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Partitioning;
using Ama.CRDT.Services.Providers;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
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
    private const string ItemsPropertyName = nameof(MultiPartitionedModel.Items);
    private const string TagsPropertyName = nameof(MultiPartitionedModel.Tags);

    private sealed class InMemoryPartitionStreamProvider : IPartitionStreamProvider
    {
        private readonly ConcurrentDictionary<string, MemoryStream> streams = new();

        public Task<Stream> GetPropertyIndexStreamAsync(string propertyName) =>
            Task.FromResult<Stream>(streams.GetOrAdd($"index_{propertyName}", _ => new MemoryStream()));

        public Task<Stream> GetPropertyDataStreamAsync(IComparable logicalKey, string propertyName) =>
            Task.FromResult<Stream>(streams.GetOrAdd($"data_{logicalKey}_{propertyName}", _ => new MemoryStream()));
        
        public Task<Stream> GetHeaderIndexStreamAsync() =>
            Task.FromResult<Stream>(streams.GetOrAdd("index_header", _ => new MemoryStream()));

        public Task<Stream> GetHeaderDataStreamAsync(IComparable logicalKey) =>
            Task.FromResult<Stream>(streams.GetOrAdd($"data_{logicalKey}_header", _ => new MemoryStream()));
    }

    private sealed class TestInfrastructure : IDisposable
    {
        public ICrdtScopeFactory ScopeFactory { get; }
        public IServiceProvider ServiceProvider { get; }
    
        public TestInfrastructure()
        {
            var meterFactoryMock = new Mock<IMeterFactory>();
            var meter = new Meter("TestMeterForPartitionManagerTests");
            meterFactoryMock.Setup(f => f.Create(It.IsAny<MeterOptions>())).Returns(meter);

            var services = new ServiceCollection()
                .AddCrdt()
                .AddSingleton<IPartitionStreamProvider, InMemoryPartitionStreamProvider>()
                .AddSingleton(meterFactoryMock.Object);

            ServiceProvider = services.BuildServiceProvider();
            ScopeFactory = ServiceProvider.GetRequiredService<ICrdtScopeFactory>();
        }
        public void Dispose() => (ServiceProvider as IDisposable)?.Dispose();
    }
    
    [Fact]
    public async Task InitializeAsync_ShouldCreateHeaderAndDataPartitions()
    {
        // Arrange
        using var infrastructure = new TestInfrastructure();
        using var scope = infrastructure.ScopeFactory.CreateScope("A");
        var manager = scope.ServiceProvider.GetRequiredService<IPartitionManager<MultiPartitionedModel>>();
        var initialObject = new MultiPartitionedModel { TenantId = "tenant-1", Items = { { "key1", "one" } } };

        // Act
        await manager.InitializeAsync(initialObject);

        // Assert
        var headerPartition = await manager.GetHeaderPartitionAsync("tenant-1");
        headerPartition.ShouldNotBeNull();

        var dataKey = new CompositePartitionKey("tenant-1", "key1");
        var dataPartition = await manager.GetDataPartitionAsync(dataKey, ItemsPropertyName);
        dataPartition.ShouldNotBeNull();

        var headerDoc = await manager.GetHeaderPartitionContentAsync("tenant-1");
        headerDoc!.Value.Data!.Items.ShouldBeEmpty();
        headerDoc!.Value.Data!.HeaderData.ShouldBe("Initial");

        var dataDoc = await manager.GetDataPartitionContentAsync(dataKey, ItemsPropertyName);
        dataDoc!.Value.Data!.Items.Count.ShouldBe(1);
        dataDoc!.Value.Data!.Items["key1"].ShouldBe("one");
    }

    [Fact]
    public async Task InitializeAsync_ForMultiPropertyType_ShouldCreatePartitionsForEachProperty()
    {
        // Arrange
        using var infrastructure = new TestInfrastructure();
        using var scope = infrastructure.ScopeFactory.CreateScope("A");
        var manager = scope.ServiceProvider.GetRequiredService<IPartitionManager<MultiPartitionedModel>>();
        var initialObject = new MultiPartitionedModel
        {
            TenantId = "tenant-1",
            Items = { { "item1", "val1" } },
            Tags = { { "tag1", "val1" } }
        };

        // Act
        await manager.InitializeAsync(initialObject);

        // Assert
        (await ToListAsync(manager.GetAllDataPartitionsAsync("tenant-1", ItemsPropertyName))).Count.ShouldBe(1);
        (await ToListAsync(manager.GetAllDataPartitionsAsync("tenant-1", TagsPropertyName))).Count.ShouldBe(1);

        var headerDoc = await manager.GetHeaderPartitionContentAsync("tenant-1");
        headerDoc!.Value.Data!.Items.ShouldBeEmpty();
        headerDoc!.Value.Data!.Tags.ShouldBeEmpty();

        var itemDoc = await manager.GetDataPartitionContentAsync(new CompositePartitionKey("tenant-1", "item1"), ItemsPropertyName);
        itemDoc!.Value.Data!.Items.Count.ShouldBe(1);
        itemDoc!.Value.Data!.Tags.ShouldBeEmpty(); // From header

        var tagDoc = await manager.GetDataPartitionContentAsync(new CompositePartitionKey("tenant-1", "tag1"), TagsPropertyName);
        tagDoc!.Value.Data!.Tags.Count.ShouldBe(1);
        tagDoc!.Value.Data!.Items.ShouldBeEmpty(); // From header
    }

    [Fact]
    public async Task ApplyPatchAsync_ForMultiPropertyType_ShouldRouteToCorrectPropertyStreams()
    {
        // Arrange
        using var infrastructure = new TestInfrastructure();
        using var scope = infrastructure.ScopeFactory.CreateScope("A");
        var manager = scope.ServiceProvider.GetRequiredService<IPartitionManager<MultiPartitionedModel>>();
        var timestampProvider = scope.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();
        await manager.InitializeAsync(new MultiPartitionedModel { TenantId = "tenant-1" });

        var itemOp = new CrdtOperation(Guid.NewGuid(), "A", "$.items", OperationType.Upsert, new OrMapAddItem("item1", "val1", Guid.NewGuid()), timestampProvider.Now());
        var tagOp = new CrdtOperation(Guid.NewGuid(), "A", "$.tags", OperationType.Upsert, new OrMapAddItem("tag1", "val1", Guid.NewGuid()), timestampProvider.Now());
        var headerOp = new CrdtOperation(Guid.NewGuid(), "A", "$.headerData", OperationType.Upsert, "Updated", timestampProvider.Now());
        var patch = new CrdtPatch([itemOp, tagOp, headerOp]) { LogicalKey = "tenant-1" };

        // Act
        await manager.ApplyPatchAsync(patch);

        // Assert
        var fullObject = await manager.GetFullObjectAsync("tenant-1");
        fullObject.ShouldNotBeNull();
        fullObject.HeaderData.ShouldBe("Updated");
        fullObject.Items.Count.ShouldBe(1);
        fullObject.Items["item1"].ShouldBe("val1");
        fullObject.Tags.Count.ShouldBe(1);
        fullObject.Tags["tag1"].ShouldBe("val1");
    }

    [Fact]
    public async Task GetFullObjectAsync_ShouldReconstructObjectFromAllPartitions()
    {
        // Arrange
        using var infrastructure = new TestInfrastructure();
        using var scope = infrastructure.ScopeFactory.CreateScope("A");
        var manager = scope.ServiceProvider.GetRequiredService<IPartitionManager<MultiPartitionedModel>>();
        var largeString = new string('x', 4500);
        var initialObject = new MultiPartitionedModel
        {
            TenantId = "tenant-1",
            HeaderData = "Test",
            Items = { { "a", "1" }, { "b", largeString }, { "c", "3" } }, // Will cause split
            Tags = { { "d", "4" }, { "e", largeString }, { "f", "6" } } // Will cause split
        };
        await manager.InitializeAsync(initialObject);
        
        // Ensure splits happened
        (await ToListAsync(manager.GetAllDataPartitionsAsync("tenant-1", ItemsPropertyName))).Count.ShouldBeGreaterThan(1);
        (await ToListAsync(manager.GetAllDataPartitionsAsync("tenant-1", TagsPropertyName))).Count.ShouldBeGreaterThan(1);

        // Act
        var fullObject = await manager.GetFullObjectAsync("tenant-1");

        // Assert
        fullObject.ShouldNotBeNull();
        fullObject.TenantId.ShouldBe("tenant-1");
        fullObject.HeaderData.ShouldBe("Test");
        fullObject.Items.Count.ShouldBe(3);
        fullObject.Items["b"].ShouldBe(largeString);
        fullObject.Tags.Count.ShouldBe(3);
        fullObject.Tags["e"].ShouldBe(largeString);
    }
    
    [Fact]
    public async Task GetAllLogicalKeysAsync_ShouldReturnDistinctKeys()
    {
        // Arrange
        using var infrastructure = new TestInfrastructure();
        
        using (var scopeA = infrastructure.ScopeFactory.CreateScope("A"))
        {
            var managerA = scopeA.ServiceProvider.GetRequiredService<IPartitionManager<MultiPartitionedModel>>();
            await managerA.InitializeAsync(new MultiPartitionedModel { TenantId = "tenant-A" });
        }
        
        using (var scopeB = infrastructure.ScopeFactory.CreateScope("B"))
        {
            var managerB = scopeB.ServiceProvider.GetRequiredService<IPartitionManager<MultiPartitionedModel>>();
            await managerB.InitializeAsync(new MultiPartitionedModel { TenantId = "tenant-B" });
        }
        
        // Act
        using var scopeC = infrastructure.ScopeFactory.CreateScope("C");
        var managerC = scopeC.ServiceProvider.GetRequiredService<IPartitionManager<MultiPartitionedModel>>();
        var logicalKeys = (await managerC.GetAllLogicalKeysAsync()).ToList();

        // Assert
        logicalKeys.Count.ShouldBe(2);
        logicalKeys.ShouldContain("tenant-A");
        logicalKeys.ShouldContain("tenant-B");
    }

    [Fact]
    public async Task ApplyPatchAsync_ShouldEnsureDataIsolationBetweenLogicalKeys()
    {
        // Arrange
        using var infrastructure = new TestInfrastructure();
        
        using var scopeA = infrastructure.ScopeFactory.CreateScope("A");
        var managerA = scopeA.ServiceProvider.GetRequiredService<IPartitionManager<MultiPartitionedModel>>();
        await managerA.InitializeAsync(new MultiPartitionedModel { TenantId = "tenant-A", Items = { { "a1", "val-a1" } } });
        
        using var scopeB = infrastructure.ScopeFactory.CreateScope("B");
        var managerB = scopeB.ServiceProvider.GetRequiredService<IPartitionManager<MultiPartitionedModel>>();
        await managerB.InitializeAsync(new MultiPartitionedModel { TenantId = "tenant-B", Items = { { "b1", "val-b1" } } });
        
        var patch = new CrdtPatch([new CrdtOperation(Guid.NewGuid(), "B", "$.items", OperationType.Upsert, new OrMapAddItem("b2", "val-b2", Guid.NewGuid()), scopeB.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>().Now())])
            { LogicalKey = "tenant-B" };

        // Act
        await managerB.ApplyPatchAsync(patch);

        // Assert
        var fullA = await managerA.GetFullObjectAsync("tenant-A");
        fullA!.Items.Count.ShouldBe(1);
        fullA.Items.ShouldNotContainKey("b2");
        
        var fullB = await managerB.GetFullObjectAsync("tenant-B");
        fullB!.Items.Count.ShouldBe(2);
        fullB.Items["b2"].ShouldBe("val-b2");
    }

    [Fact]
    public async Task GetDataPartitionContentAsync_WithDataKey_ShouldReturnMergedData()
    {
        // Arrange
        using var infrastructure = new TestInfrastructure();
        using var scope = infrastructure.ScopeFactory.CreateScope("A");
        var manager = scope.ServiceProvider.GetRequiredService<IPartitionManager<MultiPartitionedModel>>();
        var initialObject = new MultiPartitionedModel { TenantId = "tenant-1", HeaderData = "My Header", Items = { { "key1", "one" } } };
        await manager.InitializeAsync(initialObject);

        var dataKey = new CompositePartitionKey("tenant-1", "key1");

        // Act
        var result = await manager.GetDataPartitionContentAsync(dataKey, ItemsPropertyName);

        // Assert
        result.ShouldNotBeNull();
        var content = result.Value.Data!;
        content.TenantId.ShouldBe("tenant-1");
        content.HeaderData.ShouldBe("My Header"); 
        content.Items.Count.ShouldBe(1);
        content.Items["key1"].ShouldBe("one");
    }
    
    [Fact]
    public async Task GetDataPartitionByIndexAsync_ShouldReturnCorrectPartition()
    {
        // Arrange
        using var infrastructure = new TestInfrastructure();
        using var scope = infrastructure.ScopeFactory.CreateScope("A");
        var manager = scope.ServiceProvider.GetRequiredService<IPartitionManager<MultiPartitionedModel>>();
        
        var largeString = new string('x', 4500);
        var items = new Dictionary<string, string>
        {
            { "a", "val-a" }, { "b", largeString }, { "c", "val-c" }, { "d", largeString }, { "e", "val-e" },
        };
        await manager.InitializeAsync(new MultiPartitionedModel { TenantId = "tenant-1", Items = items });

        var allDataPartitions = await ToListAsync(manager.GetAllDataPartitionsAsync("tenant-1", ItemsPropertyName));
        allDataPartitions.Count.ShouldBeGreaterThan(1);

        // Act & Assert
        for (var i = 0; i < allDataPartitions.Count; i++)
        {
            var partitionFromIndex = await manager.GetDataPartitionByIndexAsync("tenant-1", i, ItemsPropertyName);
            partitionFromIndex.ShouldNotBeNull();
            partitionFromIndex.GetPartitionKey().ShouldBe(allDataPartitions[i].GetPartitionKey());
        }
        
        (await manager.GetDataPartitionByIndexAsync("tenant-1", allDataPartitions.Count, ItemsPropertyName)).ShouldBeNull();
        (await manager.GetDataPartitionByIndexAsync("tenant-1", -1, ItemsPropertyName)).ShouldBeNull();
        (await manager.GetDataPartitionByIndexAsync("non-existent", 0, ItemsPropertyName)).ShouldBeNull();
    }

    private async Task<List<TItem>> ToListAsync<TItem>(IAsyncEnumerable<TItem> asyncEnumerable)
    {
        var list = new List<TItem>();
        await foreach (var item in asyncEnumerable)
        {
            list.Add(item);
        }
        return list;
    }
}