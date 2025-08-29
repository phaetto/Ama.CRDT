namespace Ama.CRDT.Benchmarks.Benchmarks;

using Ama.CRDT.Benchmarks.Models;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;

[Config(typeof(AntiVirusFriendlyConfig))]
[MemoryDiagnoser]
public class ApplicatorBenchmarks
{
    private IServiceScope scope;
    private ICrdtApplicator applicator = null!;
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
        services.AddCrdt();
        var serviceProvider = services.BuildServiceProvider();
        var serviceScopeFactory = serviceProvider.GetService<ICrdtScopeFactory>();
        scope = serviceScopeFactory.CreateScope("replica-id");

        applicator = scope.ServiceProvider.GetRequiredService<ICrdtApplicator>();
        var patcher = scope.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        var metadataManager = scope.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();
        
        // Simple POCO setup
        simplePocoBase = new SimplePoco { Id = Guid.NewGuid(), Name = "Original", Score = 10 };
        var simpleTo = new SimplePoco { Id = simplePocoBase.Id, Name = "Updated", Score = 15 };
        
        var simpleFromMetadata = new CrdtMetadata();
        metadataManager.Initialize(new CrdtDocument<SimplePoco>(simplePocoBase, simpleFromMetadata), new SequentialTimestamp(1));
        var simplePocoFromDoc = new CrdtDocument<SimplePoco>(simplePocoBase, simpleFromMetadata);

        var simpleToMetadata = CloneMetadata(simpleFromMetadata);
        metadataManager.Initialize(new CrdtDocument<SimplePoco>(simpleTo, simpleToMetadata), new SequentialTimestamp(2));
        
        simplePocoPatch = patcher.GeneratePatch(simplePocoFromDoc, simpleTo);
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
        metadataManager.Initialize(new CrdtDocument<ComplexPoco>(complexPocoBase, complexFromMetadata), new SequentialTimestamp(3));
        var complexPocoFromDoc = new CrdtDocument<ComplexPoco>(complexPocoBase, complexFromMetadata);

        var complexToMetadata = CloneMetadata(complexFromMetadata);
        metadataManager.Initialize(new CrdtDocument<ComplexPoco>(complexTo, complexToMetadata), new SequentialTimestamp(4));
        
        complexPocoPatch = patcher.GeneratePatch(complexPocoFromDoc, complexTo);
        complexMetadata = new CrdtMetadata();
    }
    
    private SimplePoco CreateSimplePocoClone() => new() { Id = simplePocoBase.Id, Name = simplePocoBase.Name, Score = simplePocoBase.Score };
    private ComplexPoco CreateComplexPocoClone() => new()
    {
        Id = complexPocoBase.Id,
        Description = complexPocoBase.Description,
        ViewCount = complexPocoBase.ViewCount,
        Details = new Details
        {
            Author = complexPocoBase.Details.Author,
            CreatedAt = complexPocoBase.Details.CreatedAt,
            IsActive = complexPocoBase.Details.IsActive,
        },
        Tags = new List<Tag>(complexPocoBase.Tags.Select(t => new Tag { Id = t.Id, Value = t.Value })),
    };


    [Benchmark]
    public SimplePoco ApplyPatchSimple()
    {
        // Applicator now modifies in place, so we need to clone for a fair benchmark.
        return applicator.ApplyPatch(new CrdtDocument<SimplePoco>(CreateSimplePocoClone(), simpleMetadata), simplePocoPatch);
    }

    [Benchmark]
    public ComplexPoco ApplyPatchComplex()
    {
        return applicator.ApplyPatch(new CrdtDocument<ComplexPoco>(CreateComplexPocoClone(), complexMetadata), complexPocoPatch);
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