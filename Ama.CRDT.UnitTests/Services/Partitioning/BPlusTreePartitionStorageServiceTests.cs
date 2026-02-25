namespace Ama.CRDT.UnitTests.Services.Partitioning;

using Ama.CRDT.Models;
using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Services.Metrics;
using Ama.CRDT.Services.Partitioning;
using Ama.CRDT.Services.Partitioning.Serialization;
using Moq;
using Shouldly;
using System;
using System.Diagnostics.Metrics;
using System.IO;
using System.Threading.Tasks;
using Xunit;

public class BPlusTreePartitionStorageServiceTests
{
    public class TestData { public string Id { get; set; } = "1"; }

    [Fact]
    public async Task SavePartitionContentAsync_ShouldWriteToStreamAndReturnUpdatedPartition()
    {
        // Arrange
        var meter = new Meter("TestMeter");
        var meterFactoryMock = new Mock<IMeterFactory>();
        meterFactoryMock.Setup(m => m.Create(It.IsAny<MeterOptions>())).Returns(meter);
        
        var metrics = new PartitionManagerCrdtMetrics(meterFactoryMock.Object);

        var streamProviderMock = new Mock<IPartitionStreamProvider>();
        var strategyMock = new Mock<IPartitioningStrategy>();
        var serializationMock = new Mock<IPartitionSerializationService>();

        var mockStream = new MemoryStream();
        streamProviderMock.Setup(x => x.GetPropertyDataStreamAsync(It.IsAny<IComparable>(), It.IsAny<string>()))
            .ReturnsAsync(mockStream);

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(x => x.GetService(typeof(IPartitionStreamProvider))).Returns(streamProviderMock.Object);

        serializationMock.Setup(x => x.SerializeObjectAsync(It.IsAny<Stream>(), It.IsAny<object>()))
            .Callback<Stream, object>((s, o) =>
            {
                s.WriteByte(1); // Write dummy data
            })
            .Returns(Task.CompletedTask);

        var service = new BPlusTreePartitionStorageService(
            serviceProviderMock.Object,
            strategyMock.Object,
            serializationMock.Object,
            metrics);

        var originalPartition = new DataPartition(new CompositePartitionKey("A", "B"), null, 0, 0, 0, 0);
        var data = new TestData();
        var meta = new CrdtMetadata();

        // Act
        var result = (DataPartition)await service.SavePartitionContentAsync("A", "prop", originalPartition, data, meta);

        // Assert
        result.DataOffset.ShouldBe(0);
        result.DataLength.ShouldBe(1);
        result.MetadataOffset.ShouldBe(1);
        result.MetadataLength.ShouldBe(1);
        mockStream.Length.ShouldBe(2); // One byte for data, one byte for metadata
    }
}