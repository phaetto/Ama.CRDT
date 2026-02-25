namespace Ama.CRDT.UnitTests.Services.Partitioning;

using Ama.CRDT.Models;
using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Services.Partitioning;
using Moq;
using Shouldly;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

public class PartitionStorageServiceContractTests
{
    public class TestData
    {
        public string Id { get; set; } = string.Empty;
    }

    [Fact]
    public async Task CanMockSavePartitionContentAsync()
    {
        // Arrange
        var mockService = new Mock<IPartitionStorageService>();
        var logicalKey = "test-key";
        var propertyName = "TestProperty";
        var partition = new DataPartition(new CompositePartitionKey(logicalKey, "range"), null, 0, 0, 0, 0);
        var data = new TestData { Id = "1" };
        var metadata = new CrdtMetadata();
        var updatedPartition = new DataPartition(new CompositePartitionKey(logicalKey, "range"), null, 10, 20, 30, 40);

        mockService.Setup(x => x.SavePartitionContentAsync(logicalKey, propertyName, partition, data, metadata, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedPartition);

        // Act
        var result = await mockService.Object.SavePartitionContentAsync(logicalKey, propertyName, partition, data, metadata);

        // Assert
        result.ShouldBe(updatedPartition);
        mockService.Verify(x => x.SavePartitionContentAsync(logicalKey, propertyName, partition, data, metadata, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CanMockLoadHeaderPartitionContentAsync()
    {
        // Arrange
        var mockService = new Mock<IPartitionStorageService>();
        var logicalKey = "test-key";
        var partition = new HeaderPartition(new CompositePartitionKey(logicalKey, null), 0, 10, 10, 20);
        var doc = new CrdtDocument<TestData>(new TestData { Id = "1" }, new CrdtMetadata());

        mockService.Setup(x => x.LoadHeaderPartitionContentAsync<TestData>(logicalKey, partition, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);

        // Act
        var result = await mockService.Object.LoadHeaderPartitionContentAsync<TestData>(logicalKey, partition);

        // Assert
        result.Data.ShouldNotBeNull();
        result.Data.Id.ShouldBe("1");
        mockService.Verify(x => x.LoadHeaderPartitionContentAsync<TestData>(logicalKey, partition, It.IsAny<CancellationToken>()), Times.Once);
    }
}