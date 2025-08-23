using Ama.CRDT.Benchmarks.Models;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;

namespace Ama.CRDT.Benchmarks.Benchmarks;

[Config(typeof(AntiVirusFriendlyConfig))]
[MemoryDiagnoser]
public class PatcherBenchmarks
{
    private ICrdtPatcher patcher = null!;
    private CrdtDocument<SimplePoco> simplePocoFrom;
    private CrdtDocument<SimplePoco> simplePocoTo;
    private CrdtDocument<ComplexPoco> complexPocoFrom;
    private CrdtDocument<ComplexPoco> complexPocoTo;
    private ICrdtMetadataManager metadataManager = null!;
    
    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddCrdt(options => options.ReplicaId = "benchmark-replica");
        var serviceProvider = services.BuildServiceProvider();

        patcher = serviceProvider.GetRequiredService<ICrdtPatcher>();
        metadataManager = serviceProvider.GetRequiredService<ICrdtMetadataManager>();
        
        // Simple POCO setup
        var simpleFrom = new SimplePoco { Id = Guid.NewGuid(), Name = "Original", Score = 10 };
        var simpleTo = new SimplePoco { Id = simpleFrom.Id, Name = "Updated", Score = 15 };
        
        var simpleFromMetadata = new CrdtMetadata();
        metadataManager.InitializeLwwMetadata(simpleFromMetadata, simpleFrom, new EpochTimestamp(1));
        simplePocoFrom = new CrdtDocument<SimplePoco>(simpleFrom, simpleFromMetadata);
        
        var simpleToMetadata = CloneMetadata(simpleFromMetadata);
        metadataManager.InitializeLwwMetadata(simpleToMetadata, simpleTo, new EpochTimestamp(2));
        simplePocoTo = new CrdtDocument<SimplePoco>(simpleTo, simpleToMetadata);
        
        // Complex POCO setup
        var complexFrom = new ComplexPoco
        {
            Id = Guid.NewGuid(),
            Description = "Initial complex object",
            ViewCount = 100,
            Details = new Details { Author = "Author1", CreatedAt = DateTime.UtcNow, IsActive = true },
            Tags = [new Tag { Id = 1, Value = "TagA" }]
        };

        var complexTo = new ComplexPoco
        {
            Id = complexFrom.Id,
            Description = "Updated complex object",
            ViewCount = 150,
            Details = new Details { Author = "Author2", CreatedAt = DateTime.UtcNow.AddHours(1), IsActive = false },
            Tags = [new Tag { Id = 1, Value = "TagA" }, new Tag { Id = 2, Value = "TagB" }]
        };

        var complexFromMetadata = new CrdtMetadata();
        metadataManager.InitializeLwwMetadata(complexFromMetadata, complexFrom, new EpochTimestamp(3));
        complexPocoFrom = new CrdtDocument<ComplexPoco>(complexFrom, complexFromMetadata);
        
        var complexToMetadata = CloneMetadata(complexFromMetadata);
        metadataManager.InitializeLwwMetadata(complexToMetadata, complexTo, new EpochTimestamp(4));
        complexPocoTo = new CrdtDocument<ComplexPoco>(complexTo, complexToMetadata);
    }

    [Benchmark]
    public CrdtPatch GeneratePatchSimple()
    {
        return patcher.GeneratePatch(simplePocoFrom, simplePocoTo);
    }

    [Benchmark]
    public CrdtPatch GeneratePatchComplex()
    {
        return patcher.GeneratePatch(complexPocoFrom, complexPocoTo);
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