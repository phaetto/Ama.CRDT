using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Modern.CRDT.Benchmarks.Models;
using Modern.CRDT.Extensions;
using Modern.CRDT.Models;
using Modern.CRDT.Services;

namespace Modern.CRDT.Benchmarks.Benchmarks;

[Config(typeof(AntiVirusFriendlyConfig))]
[MemoryDiagnoser]
public class ApplicatorBenchmarks
{
    private IJsonCrdtApplicator applicator = null!;
    private SimplePoco simplePocoBase = null!;
    private CrdtPatch simplePocoPatch;
    private CrdtMetadata simpleMetadata = null!;
    
    private ComplexPoco complexPocoBase = null!;
    private CrdtPatch complexPocoPatch;
    private CrdtMetadata complexMetadata = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddJsonCrdt(options => options.ReplicaId = "benchmark-replica");
        var serviceProvider = services.BuildServiceProvider();

        applicator = serviceProvider.GetRequiredService<IJsonCrdtApplicator>();
        var patcher = serviceProvider.GetRequiredService<IJsonCrdtPatcher>();
        var metadataManager = serviceProvider.GetRequiredService<ICrdtMetadataManager>();
        
        // Simple POCO setup
        simplePocoBase = new SimplePoco { Id = Guid.NewGuid(), Name = "Original", Score = 10 };
        var simpleTo = new SimplePoco { Id = simplePocoBase.Id, Name = "Updated", Score = 15 };
        
        var simpleFromMetadata = new CrdtMetadata();
        metadataManager.InitializeLwwMetadata(simpleFromMetadata, simplePocoBase, new EpochTimestamp(1));
        var simplePocoFromDoc = new CrdtDocument<SimplePoco>(simplePocoBase, simpleFromMetadata);

        var simpleToMetadata = CloneMetadata(simpleFromMetadata);
        metadataManager.InitializeLwwMetadata(simpleToMetadata, simpleTo, new EpochTimestamp(2));
        var simplePocoToDoc = new CrdtDocument<SimplePoco>(simpleTo, simpleToMetadata);
        
        simplePocoPatch = patcher.GeneratePatch(simplePocoFromDoc, simplePocoToDoc);
        simpleMetadata = new CrdtMetadata();
        
        // Complex POCO setup
        complexPocoBase = new ComplexPoco
        {
            Id = Guid.NewGuid(),
            Description = "Initial complex object",
            ViewCount = 100,
            Details = new Details { Author = "Author1", CreatedAt = DateTime.UtcNow, IsActive = true },
            Tags = [new Tag { Id = 1, Value = "TagA" }]
        };

        var complexTo = new ComplexPoco
        {
            Id = complexPocoBase.Id,
            Description = "Updated complex object",
            ViewCount = 150,
            Details = new Details { Author = "Author2", CreatedAt = DateTime.UtcNow.AddHours(1), IsActive = false },
            Tags = [new Tag { Id = 1, Value = "TagA" }, new Tag { Id = 2, Value = "TagB" }]
        };

        var complexFromMetadata = new CrdtMetadata();
        metadataManager.InitializeLwwMetadata(complexFromMetadata, complexPocoBase, new EpochTimestamp(3));
        var complexPocoFromDoc = new CrdtDocument<ComplexPoco>(complexPocoBase, complexFromMetadata);

        var complexToMetadata = CloneMetadata(complexFromMetadata);
        metadataManager.InitializeLwwMetadata(complexToMetadata, complexTo, new EpochTimestamp(4));
        var complexPocoToDoc = new CrdtDocument<ComplexPoco>(complexTo, complexToMetadata);
        
        complexPocoPatch = patcher.GeneratePatch(complexPocoFromDoc, complexPocoToDoc);
        complexMetadata = new CrdtMetadata();
    }

    [Benchmark]
    public SimplePoco ApplyPatchSimple()
    {
        return applicator.ApplyPatch(simplePocoBase, simplePocoPatch, simpleMetadata);
    }

    [Benchmark]
    public ComplexPoco ApplyPatchComplex()
    {
        return applicator.ApplyPatch(complexPocoBase, complexPocoPatch, complexMetadata);
    }
    
    private CrdtMetadata CloneMetadata(CrdtMetadata original)
    {
        var clone = new CrdtMetadata();
        
        foreach (var entry in original.Lww)
        {
            clone.Lww[entry.Key] = entry.Value;
        }
        
        foreach (var entry in original.VersionVector)
        {
            clone.VersionVector[entry.Key] = entry.Value;
        }
        
        foreach (var entry in original.SeenExceptions)
        {
            clone.SeenExceptions.Add(entry);
        }
        
        return clone;
    }
}