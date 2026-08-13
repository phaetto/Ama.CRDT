namespace Ama.CRDT.LargerThanMemory.Streams.UnitTests;

using Ama.CRDT.Extensions;
using Ama.CRDT.LargerThanMemory.Streams.Extensions;
using Ama.CRDT.LargerThanMemory.Streams.Services;
using Ama.CRDT.LargerThanMemory.Streams.Services.Serialization;
using Ama.CRDT.Models;
using Ama.CRDT.Models.LargerThanMemory;
using Ama.CRDT.Services;
using Ama.CRDT.Services.LargerThanMemory;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using System;
using System.Diagnostics.Metrics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

public class StreamChunkStorageServiceDataTests
{
    public class TestData { public string Id { get; set; } = "1"; }

    private sealed class DummyPartitionStreamProvider : IChunkStreamProvider
    {
        public Task<Stream> GetPropertyIndexStreamAsync(string propertyName, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Stream> GetPropertyDataStreamAsync(IComparable logicalKey, string propertyName, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Stream> GetHeaderIndexStreamAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Stream> GetHeaderDataStreamAsync(IComparable logicalKey, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    [Fact]
    public async Task SavePartitionContentAsync_ShouldWriteToStreamAndReturnUpdatedPartition()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddCrdt();
        services.AddCrdtStreamChunking<DummyPartitionStreamProvider>();

        var meterFactoryMock = new Mock<IMeterFactory>();
        meterFactoryMock.Setup(f => f.Create(It.IsAny<MeterOptions>())).Returns(new Meter("TestMeter"));
        services.AddSingleton(meterFactoryMock.Object);

        var streamProviderMock = new Mock<IChunkStreamProvider>();
        var mockStream = new MemoryStream();
        streamProviderMock.Setup(x => x.GetPropertyDataStreamAsync(It.IsAny<IComparable>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockStream);

        // Replace the dummy stream provider with the mock
        services.AddScoped(_ => streamProviderMock.Object);

        var serializationMock = new Mock<IChunkSerializationService>();
        serializationMock.Setup(x => x.SerializeObjectAsync(It.IsAny<Stream>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<Stream, object, CancellationToken>((s, o, c) =>
            {
                s.WriteByte(1); // Write dummy data
            })
            .Returns(Task.CompletedTask);

        // Override serialization to use the mock
        services.AddScoped(_ => serializationMock.Object);

        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        var scope = scopeFactory.CreateScope("test-replica");

        var service = (StreamChunkStorageService)scope.ServiceProvider.GetRequiredService<IChunkStorageService>();

        var originalPartition = new CollectionChunk(new CompositeChunkKey("A", "B"), null, 0, 0, 0, 0);
        var data = new TestData();
        var meta = new CrdtMetadata();

        // Act
        var result = (CollectionChunk)await service.SaveChunkContentAsync("A", "prop", originalPartition, data, meta);

        // Assert
        result.DataOffset.ShouldBe(1024);
        result.DataLength.ShouldBe(1);
        result.MetadataOffset.ShouldBe(1025);
        result.MetadataLength.ShouldBe(1);
        mockStream.Length.ShouldBe(1026); // 1024 bytes header, one byte for data, one byte for metadata
    }
}