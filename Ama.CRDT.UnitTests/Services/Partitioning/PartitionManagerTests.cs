namespace Ama.CRDT.UnitTests.Services.Partitioning;

using Ama.CRDT.Attributes;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Partitioning;
using Ama.CRDT.Services.Providers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.Json;

[PartitionKey(nameof(TenantId))]
public sealed class PartitionedMapModel
{
    public string TenantId { get; set; } = "";
    public string HeaderData { get; set; } = "Initial";
    [CrdtOrMapStrategy]
    public Dictionary<string, string> Items { get; set; } = new();
}

public sealed class PartitionManagerTests
{
    private sealed class InMemoryPartitionStreamProvider : IPartitionStreamProvider
    {
        private readonly MemoryStream indexStream = new();
        private readonly ConcurrentDictionary<object, MemoryStream> dataStreams = new();

        public Task<Stream> GetIndexStreamAsync() => Task.FromResult<Stream>(indexStream);

        public Task<Stream> GetDataStreamAsync(object logicalKey)
        {
            var dataStream = dataStreams.GetOrAdd(logicalKey, _ => new MemoryStream());
            return Task.FromResult<Stream>(dataStream);
        }
    }

    private sealed class TestInfrastructure : IDisposable
    {
        public ICrdtScopeFactory ScopeFactory { get; }
        public IServiceProvider ServiceProvider { get; }
    
        public TestInfrastructure()
        {
            var services = new ServiceCollection()
                .AddCrdt()
                .AddCrdtPartitionStreamProvider<InMemoryPartitionStreamProvider>();

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
        var manager = scope.ServiceProvider.GetRequiredService<IPartitionManager<PartitionedMapModel>>();
        var initialObject = new PartitionedMapModel { TenantId = "tenant-1", Items = { { "key1", "one" } } };

        // Act
        await manager.InitializeAsync(initialObject);

        // Assert
        var strategy = scope.ServiceProvider.GetRequiredService<IPartitioningStrategy>();

        var allPartitions = await strategy.GetAllPartitionsAsync();
        var partitions = allPartitions.Where(p => p.StartKey.LogicalKey.Equals("tenant-1")).ToList();
        partitions.Count.ShouldBe(2);

        var headerKey = new CompositePartitionKey("tenant-1", null);
        var dataKey = new CompositePartitionKey("tenant-1", "key1");
        
        var headerPartition = await strategy.FindPartitionAsync(headerKey);
        headerPartition.ShouldNotBeNull();
        headerPartition.Value.StartKey.RangeKey.ShouldBeNull();
        
        var dataPartition = await strategy.FindPartitionAsync(dataKey);
        dataPartition.ShouldNotBeNull();
        dataPartition.Value.StartKey.RangeKey.ShouldNotBeNull();

        var headerDoc = await manager.GetPartitionContentAsync(headerKey);
        headerDoc.ShouldNotBeNull();
        var headerContent = headerDoc.Value.Data!;
        headerContent.Items.ShouldBeEmpty();
        headerContent.HeaderData.ShouldBe("Initial");

        var dataDoc = await manager.GetPartitionContentAsync(dataKey);
        dataDoc.ShouldNotBeNull();
        var dataContent = dataDoc.Value.Data!;
        dataContent.Items.Count.ShouldBe(1);
        dataContent.Items["key1"].ShouldBe("one");
        dataContent.HeaderData.ShouldBe("Initial"); // Cloned initial state, but this partition only manages Items
    }

    [Fact]
    public async Task ApplyPatchAsync_ShouldEnsureDataIsolation()
    {
        // Arrange
        using var infrastructure = new TestInfrastructure();
        
        // Setup tenant A
        using var scopeA = infrastructure.ScopeFactory.CreateScope("A");
        var managerA = scopeA.ServiceProvider.GetRequiredService<IPartitionManager<PartitionedMapModel>>();
        var initialA = new PartitionedMapModel { TenantId = "tenant-A", Items = { { "a1", "val-a1" } } };
        await managerA.InitializeAsync(initialA);
        
        // Setup tenant B
        using var scopeB = infrastructure.ScopeFactory.CreateScope("B");
        var managerB = scopeB.ServiceProvider.GetRequiredService<IPartitionManager<PartitionedMapModel>>();
        var initialB = new PartitionedMapModel { TenantId = "tenant-B", Items = { { "b1", "val-b1" } } };
        await managerB.InitializeAsync(initialB);
        
        var patch = new CrdtPatch([new CrdtOperation(Guid.NewGuid(), "B", "$.items", OperationType.Upsert, new OrMapAddItem("b2", "val-b2", Guid.NewGuid()), scopeB.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>().Now())])
            { LogicalKey = "tenant-B" };

        // Act
        await managerB.ApplyPatchAsync(patch);

        // Assert
        var docA = await managerA.GetPartitionContentAsync(new CompositePartitionKey("tenant-A", "a1"));
        docA.ShouldNotBeNull();
        var contentA = docA.Value.Data!;
        contentA.Items.Count.ShouldBe(1);
        contentA.Items.ShouldNotContainKey("b2");

        var docB = await managerB.GetPartitionContentAsync(new CompositePartitionKey("tenant-B", "b2"));
        docB.ShouldNotBeNull();
        var contentB = docB.Value.Data!;
        contentB.Items.Count.ShouldBe(2);
        contentB.Items["b2"].ShouldBe("val-b2");
    }

    [Fact]
    public async Task ApplyPatchAsync_ShouldRouteUpdatesToCorrectPartitionType()
    {
        // Arrange
        using var infrastructure = new TestInfrastructure();
        using var scope = infrastructure.ScopeFactory.CreateScope("A");
        var manager = scope.ServiceProvider.GetRequiredService<IPartitionManager<PartitionedMapModel>>();
        var initialObject = new PartitionedMapModel { TenantId = "tenant-1", Items = { { "key1", "one" } } };
        await manager.InitializeAsync(initialObject);

        var headerOp = new CrdtOperation(Guid.NewGuid(), "A", "$.headerData", OperationType.Upsert, "Updated", scope.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>().Now());
        var dataOp = new CrdtOperation(Guid.NewGuid(), "A", "$.items", OperationType.Upsert, new OrMapAddItem("key2", "two", Guid.NewGuid()), scope.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>().Now());
        var patch = new CrdtPatch([headerOp, dataOp]) { LogicalKey = "tenant-1" };

        // Act
        await manager.ApplyPatchAsync(patch);

        // Assert
        var headerKey = new CompositePartitionKey("tenant-1", null);
        var dataKey = new CompositePartitionKey("tenant-1", "key1");

        var headerDoc = await manager.GetPartitionContentAsync(headerKey);
        headerDoc.ShouldNotBeNull();
        var headerContent = headerDoc.Value.Data!;
        headerContent.HeaderData.ShouldBe("Updated");
        headerContent.Items.ShouldBeEmpty();

        var dataDoc = await manager.GetPartitionContentAsync(dataKey);
        dataDoc.ShouldNotBeNull();
        var dataContent = dataDoc.Value.Data!;
        dataContent.HeaderData.ShouldBe("Initial"); // Header data is not in this partition's scope.
        dataContent.Items.Count.ShouldBe(2);
        dataContent.Items["key2"].ShouldBe("two");
    }

    [Fact]
    public async Task ApplyPatchAsync_WhenDataPartitionSplits_HeaderRemainsUnchanged()
    {
        // Arrange
        using var infrastructure = new TestInfrastructure();
        using var scope = infrastructure.ScopeFactory.CreateScope("A");
        var manager = scope.ServiceProvider.GetRequiredService<IPartitionManager<PartitionedMapModel>>();
        var largeString = new string('x', 4500);
        var initialObject = new PartitionedMapModel { TenantId = "tenant-1", Items = { { "a", "val-a" }, { "c", largeString } } };
        await manager.InitializeAsync(initialObject);
        
        var patch = new CrdtPatch([new CrdtOperation(Guid.NewGuid(), "A", "$.items", OperationType.Upsert, new OrMapAddItem("b", largeString, Guid.NewGuid()), scope.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>().Now())])
            { LogicalKey = "tenant-1" };

        // Act: This patch will cause the data partition to split
        await manager.ApplyPatchAsync(patch);

        // Assert
        var strategy = scope.ServiceProvider.GetRequiredService<IPartitioningStrategy>();

        var allPartitions = await strategy.GetAllPartitionsAsync();
        var partitions = allPartitions.Where(p => p.StartKey.LogicalKey.Equals("tenant-1")).ToList();
        partitions.Count.ShouldBe(3); // 1 header, 2 data

        var headerPartition = partitions.Single(p => p.StartKey.RangeKey is null);
        var dataPartitions = partitions.Where(p => p.StartKey.RangeKey is not null).ToList();
        dataPartitions.Count.ShouldBe(2);

        var headerDoc = await manager.GetPartitionContentAsync(headerPartition.StartKey);
        headerDoc.ShouldNotBeNull();
        var headerContent = headerDoc.Value.Data!;
        headerContent.HeaderData.ShouldBe("Initial");
    }
}