namespace Ama.CRDT.UnitTests.Extensions;

using System;
using System.Threading.Tasks;
using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services;
using Ama.CRDT.Services.GarbageCollection;
using Ama.CRDT.Services.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

public sealed class PackedCrdtDocumentExtensionsTests
{
    private readonly IServiceProvider serviceProvider;
    private readonly ICrdtScopeFactory scopeFactory;

    public PackedCrdtDocumentExtensionsTests()
    {
        var services = new ServiceCollection();
        services.AddCrdt();
        services.AddCrdtAotContext<PackedDocumentTestCrdtAotContext>();
        serviceProvider = services.BuildServiceProvider();
        scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
    }

    [Fact]
    public void PackAndUnpack_ViaMetadataManager_ShouldRoundTripStateAndCausalHistory()
    {
        using var scope = scopeFactory.CreateScope("replica-A");
        var metadataManager = scope.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();

        var model = new PackedTestModel { Name = "Alice", Count = 10 };
        var packedDoc = metadataManager.InitializePacked(model);

        packedDoc.Data.ShouldBe(model);
        packedDoc.MetadataBytes.IsEmpty.ShouldBeFalse();

        var unpackedDoc = metadataManager.Unpack(packedDoc);
        unpackedDoc.Data.ShouldBe(model);
        unpackedDoc.Metadata.ShouldNotBeNull();
        unpackedDoc.Metadata.States.ShouldContainKey("$.name");

        var repackedDoc = metadataManager.Pack(unpackedDoc);
        repackedDoc.ShouldBe(packedDoc);
    }

    [Fact]
    public void GeneratePatchAndApplyPatch_WithPackedCrdtDocument_ShouldConvergeAndUpdatePackedBytes()
    {
        using var scopeA = scopeFactory.CreateScope("replica-A");
        using var scopeB = scopeFactory.CreateScope("replica-B");

        var metadataManagerA = scopeA.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();
        var patcherA = scopeA.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        var applicatorA = scopeA.ServiceProvider.GetRequiredService<ICrdtApplicator>();
        var serializerA = scopeA.ServiceProvider.GetRequiredService<ICrdtSerializer>();

        var metadataManagerB = scopeB.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();
        var applicatorB = scopeB.ServiceProvider.GetRequiredService<ICrdtApplicator>();
        var serializerB = scopeB.ServiceProvider.GetRequiredService<ICrdtSerializer>();

        var dataA = new PackedTestModel { Name = "Initial", Count = 1 };
        var packedA = metadataManagerA.InitializePacked(dataA);

        var dataB = new PackedTestModel { Name = "Initial", Count = 1 };
        var packedB = metadataManagerB.InitializePacked(dataB);

        var modifiedA = new PackedTestModel { Name = "UpdatedByA", Count = 5 };
        var patch = patcherA.GeneratePatch(packedA, modifiedA, serializerA);

        var resultA = applicatorA.ApplyPatch(packedA, patch, serializerA);
        var resultB = applicatorB.ApplyPatch(packedB, patch, serializerB);

        resultA.UnappliedOperations.ShouldBeEmpty();
        resultB.UnappliedOperations.ShouldBeEmpty();

        resultA.Document.Data!.Name.ShouldBe("UpdatedByA");
        resultB.Document.Data!.Name.ShouldBe("UpdatedByA");

        var unpackedB = resultB.Document.Unpack(serializerB);
        unpackedB.Metadata!.VersionVector.ShouldContainKeyAndValue("replica-A", 2L);
    }

    [Fact]
    public async Task AsyncPipeline_WithPackedCrdtDocument_ShouldGenerateApplyAndMerge()
    {
        using var scopeA = scopeFactory.CreateScope("replica-A");
        using var scopeB = scopeFactory.CreateScope("replica-B");

        var metadataManagerA = scopeA.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();
        var asyncPatcherA = scopeA.ServiceProvider.GetRequiredService<IAsyncCrdtPatcher>();
        var asyncApplicatorA = scopeA.ServiceProvider.GetRequiredService<IAsyncCrdtApplicator>();
        var asyncMergerA = scopeA.ServiceProvider.GetRequiredService<IAsyncCrdtMerger>();
        var serializerA = scopeA.ServiceProvider.GetRequiredService<ICrdtSerializer>();

        var metadataManagerB = scopeB.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();
        var asyncPatcherB = scopeB.ServiceProvider.GetRequiredService<IAsyncCrdtPatcher>();
        var asyncApplicatorB = scopeB.ServiceProvider.GetRequiredService<IAsyncCrdtApplicator>();
        var serializerB = scopeB.ServiceProvider.GetRequiredService<ICrdtSerializer>();

        var packedA = metadataManagerA.InitializePacked(new PackedTestModel { Name = "Init", Count = 0 });
        var packedB = metadataManagerB.InitializePacked(new PackedTestModel { Name = "Init", Count = 0 });

        // Replica A generates explicit operation
        var opA = await asyncPatcherA.GenerateOperationAsync(packedA, x => x.Name, new SetIntent("FromA"), serializerA);
        var applyResA = await asyncApplicatorA.ApplyPatchAsync(packedA, new CrdtPatch([opA]), serializerA);
        packedA = applyResA.Document;

        // Replica B generates patch
        var patchB = await asyncPatcherB.GeneratePatchAsync(packedB, new PackedTestModel { Name = "FromB", Count = 10 }, serializerB);
        var applyResB = await asyncApplicatorB.ApplyPatchAsync(packedB, patchB, serializerB);
        packedB = applyResB.Document;

        // Merge B into A
        packedA = await asyncMergerA.MergeStateAsync(packedA, packedB, serializerA);

        var unpackedA = packedA.Unpack(serializerA);
        unpackedA.Metadata!.VersionVector.ShouldContainKey("replica-A");
        unpackedA.Metadata!.VersionVector.ShouldContainKey("replica-B");
    }

    [Fact]
    public void CompactAndEvictReplica_WithPackedCrdtDocument_ShouldUpdatePackedMetadata()
    {
        using var scope = scopeFactory.CreateScope("replica-A");
        var metadataManager = scope.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();
        var serializer = scope.ServiceProvider.GetRequiredService<ICrdtSerializer>();

        var doc = new CrdtDocument<PackedTestModel>(new PackedTestModel { Name = "Test" }, metadataManager.Initialize(new PackedTestModel { Name = "Test" }));
        metadataManager.AdvanceVersionVector(doc.Metadata!, "replica-old", 5);

        var packed = doc.Pack(serializer);

        packed = metadataManager.EvictReplica(packed, "replica-old");
        packed = metadataManager.Compact(packed, new ThresholdCompactionPolicy(new CrdtTimestamp(long.MaxValue)));

        var unpacked = packed.Unpack(serializer);
        unpacked.Metadata!.VersionVector.ShouldNotContainKey("replica-old");
    }
}

public sealed class PackedTestModel
{
    public string Name { get; set; } = string.Empty;

    [CrdtCounterStrategy]
    public int Count { get; set; }
}

[CrdtAotType(typeof(PackedTestModel))]
internal partial class PackedDocumentTestCrdtAotContext : CrdtAotContext
{
}