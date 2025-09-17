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
using Xunit;

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
                // Register the stream provider as a Singleton for tests to ensure
                // all scopes share the same underlying index and data streams.
                .AddSingleton<IPartitionStreamProvider, InMemoryPartitionStreamProvider>();

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
        var partitions = allPartitions.Where(p => p.GetPartitionKey().LogicalKey.Equals("tenant-1")).ToList();
        partitions.Count.ShouldBe(2);

        var headerKey = new CompositePartitionKey("tenant-1", null);
        var dataKey = new CompositePartitionKey("tenant-1", "key1");
        
        var headerPartition = await strategy.FindPartitionAsync(headerKey);
        headerPartition.ShouldNotBeNull();
        headerPartition.ShouldBeOfType<HeaderPartition>();
        (headerPartition as HeaderPartition?)!.Value.Key.RangeKey.ShouldBeNull();
        
        var dataPartition = await strategy.FindPartitionAsync(dataKey);
        dataPartition.ShouldNotBeNull();
        dataPartition.ShouldBeOfType<DataPartition>();
        var dataPartitionValue = (dataPartition as DataPartition?)!.Value;
        dataPartitionValue.StartKey.RangeKey.ShouldNotBeNull();
        dataPartitionValue.EndKey.ShouldBeNull();

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
    public async Task InitializeAsync_WithEmptyCollection_ShouldCreateHeaderAndSingleEmptyDataPartition()
    {
        // Arrange
        using var infrastructure = new TestInfrastructure();
        using var scope = infrastructure.ScopeFactory.CreateScope("A");
        var manager = scope.ServiceProvider.GetRequiredService<IPartitionManager<PartitionedMapModel>>();
        var initialObject = new PartitionedMapModel { TenantId = "tenant-1" };

        // Act
        await manager.InitializeAsync(initialObject);

        // Assert
        var dataPartitions = await manager.GetAllDataPartitionsAsync("tenant-1");
        dataPartitions.Count.ShouldBe(1);

        var headerKey = new CompositePartitionKey("tenant-1", null);
        var headerDoc = await manager.GetPartitionContentAsync(headerKey);
        headerDoc.ShouldNotBeNull();
        headerDoc.Value.Data!.Items.ShouldBeEmpty();

        var dataDoc = await manager.GetPartitionContentAsync(dataPartitions.First().GetPartitionKey());
        dataDoc.ShouldNotBeNull();
        dataDoc.Value.Data!.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task ApplyPatchAsync_WhenStartingWithEmptyCollection_ShouldCorrectlyAddItems()
    {
        // Arrange
        using var infrastructure = new TestInfrastructure();
        using var scope = infrastructure.ScopeFactory.CreateScope("A");
        var manager = scope.ServiceProvider.GetRequiredService<IPartitionManager<PartitionedMapModel>>();
        var timestampProvider = scope.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();
        var initialObject = new PartitionedMapModel { TenantId = "tenant-1" }; // Start with empty Items
        await manager.InitializeAsync(initialObject);

        // Assert initial state
        var initialDataPartitions = await manager.GetAllDataPartitionsAsync("tenant-1");
        initialDataPartitions.Count.ShouldBe(1);
        var initialDataContent = (await manager.GetPartitionContentAsync(initialDataPartitions.Single().GetPartitionKey()))!.Value.Data!;
        initialDataContent.Items.ShouldBeEmpty();

        // Act 1: Add first item
        var patch1 = new CrdtPatch([new CrdtOperation(Guid.NewGuid(), "A", "$.items", OperationType.Upsert, new OrMapAddItem("key1", "one", Guid.NewGuid()), timestampProvider.Now())])
            { LogicalKey = "tenant-1" };
        await manager.ApplyPatchAsync(patch1);

        // Assert 1: First item is present
        var midDataPartitions = await manager.GetAllDataPartitionsAsync("tenant-1");
        midDataPartitions.Count.ShouldBe(1);
        var midDataContent = (await manager.GetPartitionContentAsync(midDataPartitions.Single().GetPartitionKey()))!.Value.Data!;
        midDataContent.Items.Count.ShouldBe(1);
        midDataContent.Items["key1"].ShouldBe("one");

        // Act 2: Add second item
        var patch2 = new CrdtPatch([new CrdtOperation(Guid.NewGuid(), "A", "$.items", OperationType.Upsert, new OrMapAddItem("key2", "two", Guid.NewGuid()), timestampProvider.Now())])
            { LogicalKey = "tenant-1" };
        await manager.ApplyPatchAsync(patch2);

        // Assert 2: Both items are present in the same partition
        var finalDataPartitions = await manager.GetAllDataPartitionsAsync("tenant-1");
        finalDataPartitions.Count.ShouldBe(1);

        var finalDataContent = (await manager.GetPartitionContentAsync(finalDataPartitions.Single().GetPartitionKey()))!.Value.Data!;
        finalDataContent.Items.Count.ShouldBe(2);
        finalDataContent.Items["key1"].ShouldBe("one");
        finalDataContent.Items["key2"].ShouldBe("two");

        // Also check header remains clean
        var headerKey = new CompositePartitionKey("tenant-1", null);
        var headerContent = (await manager.GetPartitionContentAsync(headerKey))!.Value.Data!;
        headerContent.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAllDataPartitionsAsync_ShouldExcludeHeaderPartition()
    {
        // Arrange
        using var infrastructure = new TestInfrastructure();
        using var scope = infrastructure.ScopeFactory.CreateScope("A");
        var manager = scope.ServiceProvider.GetRequiredService<IPartitionManager<PartitionedMapModel>>();
        var initialObject = new PartitionedMapModel { TenantId = "tenant-1", Items = { { "key1", "one" } } };
        await manager.InitializeAsync(initialObject);

        // Act
        var dataPartitions = await manager.GetAllDataPartitionsAsync("tenant-1");

        // Assert
        dataPartitions.Count.ShouldBe(1);
        dataPartitions.Single().ShouldBeOfType<DataPartition>();
    }
    
    [Fact]
    public async Task GetAllLogicalKeysAsync_ShouldReturnDistinctKeysForMultipleDocuments()
    {
        // Arrange
        using var infrastructure = new TestInfrastructure();
        
        using (var scopeA = infrastructure.ScopeFactory.CreateScope("A"))
        {
            var managerA = scopeA.ServiceProvider.GetRequiredService<IPartitionManager<PartitionedMapModel>>();
            await managerA.InitializeAsync(new PartitionedMapModel { TenantId = "tenant-A" });
        }
        
        using (var scopeB = infrastructure.ScopeFactory.CreateScope("B"))
        {
            var managerB = scopeB.ServiceProvider.GetRequiredService<IPartitionManager<PartitionedMapModel>>();
            await managerB.InitializeAsync(new PartitionedMapModel { TenantId = "tenant-B" });
        }
        
        // Act
        using var scopeC = infrastructure.ScopeFactory.CreateScope("C");
        var managerC = scopeC.ServiceProvider.GetRequiredService<IPartitionManager<PartitionedMapModel>>();
        var logicalKeys = (await managerC.GetAllLogicalKeysAsync()).ToList();

        // Assert
        logicalKeys.Count.ShouldBe(2);
        logicalKeys.ShouldContain("tenant-A");
        logicalKeys.ShouldContain("tenant-B");
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
        var docA = (await managerA.GetAllDataPartitionsAsync("tenant-A")).Single();
        var contentA = (await managerA.GetPartitionContentAsync(docA.GetPartitionKey()))!.Value.Data!;
        contentA.Items.Count.ShouldBe(1);
        contentA.Items.ShouldNotContainKey("b2");
        
        var allPartitionsB = await managerB.GetAllDataPartitionsAsync("tenant-B");
        var allItemsB = new Dictionary<string, string>();
        foreach (var p in allPartitionsB)
        {
            var content = (await managerB.GetPartitionContentAsync(p.GetPartitionKey()))!.Value.Data!;
            foreach (var item in content.Items) allItemsB.Add(item.Key, item.Value);
        }
        allItemsB.Count.ShouldBe(2);
        allItemsB["b2"].ShouldBe("val-b2");
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
        var dataPartitions = await manager.GetAllDataPartitionsAsync("tenant-1");

        var headerDoc = await manager.GetPartitionContentAsync(headerKey);
        headerDoc.ShouldNotBeNull();
        var headerContent = headerDoc.Value.Data!;
        headerContent.HeaderData.ShouldBe("Updated");
        headerContent.Items.ShouldBeEmpty();

        var dataContent = (await manager.GetPartitionContentAsync(dataPartitions.First().GetPartitionKey()))!.Value.Data;
        dataContent.HeaderData.ShouldBe("Updated");
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
        var largeString = new string('x', 4500); // Exceeds half of MaxPartitionDataSize
        var initialObject = new PartitionedMapModel { TenantId = "tenant-1", Items = { { "a", "val-a" }, { "c", largeString } } };
        await manager.InitializeAsync(initialObject);
        
        (await manager.GetAllDataPartitionsAsync("tenant-1")).Count.ShouldBe(1);
        
        var patch = new CrdtPatch([new CrdtOperation(Guid.NewGuid(), "A", "$.items", OperationType.Upsert, new OrMapAddItem("b", largeString, Guid.NewGuid()), scope.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>().Now())])
            { LogicalKey = "tenant-1" };

        // Act: This patch will cause the data partition to split
        await manager.ApplyPatchAsync(patch);

        // Assert
        var dataPartitions = await manager.GetAllDataPartitionsAsync("tenant-1");
        dataPartitions.Count.ShouldBe(2);

        var headerKey = new CompositePartitionKey("tenant-1", null);
        var headerDoc = await manager.GetPartitionContentAsync(headerKey);
        headerDoc.ShouldNotBeNull();
        var headerContent = headerDoc.Value.Data!;
        headerContent.HeaderData.ShouldBe("Initial");
        headerContent.Items.ShouldBeEmpty();
    }
    
    [Fact]
    public async Task GetPartitionContentAsync_WithHeaderKey_ShouldReturnCorrectHeaderContent()
    {
        // Arrange
        using var infrastructure = new TestInfrastructure();
        using var scope = infrastructure.ScopeFactory.CreateScope("A");
        var manager = scope.ServiceProvider.GetRequiredService<IPartitionManager<PartitionedMapModel>>();
        var initialObject = new PartitionedMapModel { TenantId = "tenant-1", HeaderData = "My Custom Header", Items = { { "key1", "one" } } };
        await manager.InitializeAsync(initialObject);

        var headerKey = new CompositePartitionKey("tenant-1", null);

        // Act
        var result = await manager.GetPartitionContentAsync(headerKey);

        // Assert
        result.ShouldNotBeNull();
        var content = result.Value.Data!;
        content.TenantId.ShouldBe("tenant-1");
        content.HeaderData.ShouldBe("My Custom Header"); // Header partition has the value from initialObject
        content.Items.ShouldBeEmpty();
    }
    
    [Fact]
    public async Task GetPartitionContentAsync_WithDataKey_ShouldReturnCorrectDataContent()
    {
        // Arrange
        using var infrastructure = new TestInfrastructure();
        using var scope = infrastructure.ScopeFactory.CreateScope("A");
        var manager = scope.ServiceProvider.GetRequiredService<IPartitionManager<PartitionedMapModel>>();
        var initialObject = new PartitionedMapModel { TenantId = "tenant-1", HeaderData = "My Custom Header", Items = { { "key1", "one" } } };
        await manager.InitializeAsync(initialObject);

        var dataKey = new CompositePartitionKey("tenant-1", "key1");

        // Act
        var result = await manager.GetPartitionContentAsync(dataKey);

        // Assert
        result.ShouldNotBeNull();
        var content = result.Value.Data!;
        content.TenantId.ShouldBe("tenant-1");
        // The HeaderData should be assembled from the header partition.
        content.HeaderData.ShouldBe("My Custom Header"); 
        content.Items.Count.ShouldBe(1);
        content.Items["key1"].ShouldBe("one");
    }
    
    [Fact]
    public async Task GetPartitionContentAsync_WithNonExistentKey_ShouldReturnNull()
    {
        // Arrange
        using var infrastructure = new TestInfrastructure();
        using var scope = infrastructure.ScopeFactory.CreateScope("A");
        var manager = scope.ServiceProvider.GetRequiredService<IPartitionManager<PartitionedMapModel>>();
        var initialObject = new PartitionedMapModel { TenantId = "tenant-1" };
        await manager.InitializeAsync(initialObject);

        var nonExistentKey = new CompositePartitionKey("tenant-does-not-exist", null);

        // Act
        var result = await manager.GetPartitionContentAsync(nonExistentKey);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetPartitionContentAsync_WithInvalidKeyType_ShouldThrowArgumentException()
    {
        // Arrange
        using var infrastructure = new TestInfrastructure();
        using var scope = infrastructure.ScopeFactory.CreateScope("A");
        var manager = scope.ServiceProvider.GetRequiredService<IPartitionManager<PartitionedMapModel>>();
        var invalidKey = "not-a-composite-key";

        // Act & Assert
        var exception = await Should.ThrowAsync<ArgumentException>(async () =>
        {
            await manager.GetPartitionContentAsync(invalidKey);
        });
        exception.Message.ShouldStartWith($"Key must be of type {nameof(CompositePartitionKey)} for partitioned documents.");
    }
}