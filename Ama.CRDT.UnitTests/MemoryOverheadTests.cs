namespace Ama.CRDT.UnitTests;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Ama.CRDT.Attributes;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Decorators;
using Ama.CRDT.Services.GarbageCollection;
using Ama.CRDT.Services.Journaling;
using Ama.CRDT.Services.Providers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

public sealed class MemoryOverheadTests
{
    private readonly ITestOutputHelper output;
    private readonly IServiceProvider serviceProvider;

    public MemoryOverheadTests(ITestOutputHelper output)
    {
        this.output = output;
        var services = new ServiceCollection();
        services.AddCrdt();
        services.AddCrdtAotContext<MemoryOverheadTestCrdtAotContext>();
        serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public void MeasureDocumentAndMetadataMemoryOverhead_AcrossConfigurations()
    {
        using var scope = serviceProvider.GetRequiredService<ICrdtScopeFactory>().CreateScope("replica-mem-test");
        var metadataManager = scope.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();
        var timestamp = new CrdtTimestamp(1000, 0, ReplicaId: "replica-mem-test");

        // Warmup JIT and string pools
        var warmupModel = CreateModel(5);
        _ = metadataManager.Initialize(warmupModel, timestamp);

        var itemCounts = new[] { 0, 5, 25, 100 };

        output.WriteLine("| Configuration | Data Bytes | Metadata Bytes | Full Doc Bytes | Meta Overhead % | Meta/Data Ratio |");
        output.WriteLine("| :--- | :---: | :---: | :---: | :---: | :---: |");

        foreach (var count in itemCounts)
        {
            long dataBytes = MeasureAllocatedBytes(() => CreateModel(count));
            var sampleModel = CreateModel(count);
            long metaBytes = MeasureAllocatedBytes(() => metadataManager.Initialize(sampleModel, timestamp));
            long fullDocBytes = dataBytes + metaBytes;

            double overheadPercentage = (double)metaBytes / dataBytes * 100.0;
            double ratio = (double)metaBytes / dataBytes;

            output.WriteLine($"| Model ({count} items/col) | {dataBytes:N0} B | {metaBytes:N0} B | {fullDocBytes:N0} B | {overheadPercentage:F1}% | {ratio:F2}x |");

            dataBytes.ShouldBeGreaterThan(0);
            metaBytes.ShouldBeGreaterThan(0);
            overheadPercentage.ShouldBeGreaterThan(0);
        }
    }

    [Fact]
    public void MeasurePackedDocumentMemoryOverhead_AcrossConfigurations()
    {
        var brotliServices = new ServiceCollection();
        brotliServices.AddCrdt().AddCrdtSystemTextJson(useBrotliCompression: true);
        brotliServices.AddCrdtAotContext<MemoryOverheadTestCrdtAotContext>();
        using var brotliServiceProvider = brotliServices.BuildServiceProvider();

        using var jsonScope = serviceProvider.GetRequiredService<ICrdtScopeFactory>().CreateScope("replica-mem-test");
        using var brotliScope = brotliServiceProvider.GetRequiredService<ICrdtScopeFactory>().CreateScope("replica-mem-test");

        var jsonMetaManager = jsonScope.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();
        var brotliMetaManager = brotliScope.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();
        var timestamp = new CrdtTimestamp(1000, 0, ReplicaId: "replica-mem-test");

        // Warmup JIT and string pools
        var warmupModel = CreateModel(5);
        var warmupMeta = jsonMetaManager.Initialize(warmupModel, timestamp);
        _ = jsonMetaManager.Pack(new CrdtDocument<TestMemoryModel>(warmupModel, warmupMeta));
        _ = brotliMetaManager.Pack(new CrdtDocument<TestMemoryModel>(warmupModel, warmupMeta));

        var itemCounts = new[] { 0, 5, 25, 100 };

        output.WriteLine("=== PackedCrdtDocument At-Rest Payload Size vs In-Memory CrdtMetadata ===");
        output.WriteLine("| Configuration | Data Bytes | Unpacked Meta (Heap) | Packed JSON Bytes | JSON Meta Overhead % | JSON Savings % | Packed Brotli Bytes | Brotli Meta Overhead % | Brotli Savings % |");
        output.WriteLine("| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |");

        foreach (var count in itemCounts)
        {
            long dataBytes = MeasureAllocatedBytes(() => CreateModel(count));
            var sampleModel = CreateModel(count);
            long unpackedMetaBytes = MeasureAllocatedBytes(() => jsonMetaManager.Initialize(sampleModel, timestamp));

            var sampleDoc = new CrdtDocument<TestMemoryModel>(sampleModel, jsonMetaManager.Initialize(sampleModel, timestamp));
            var packedJsonDoc = jsonMetaManager.Pack(sampleDoc);
            var packedBrotliDoc = brotliMetaManager.Pack(sampleDoc);

            long jsonPayloadBytes = packedJsonDoc.MetadataBytes.Length;
            long brotliPayloadBytes = packedBrotliDoc.MetadataBytes.Length;

            double jsonOverheadPct = (double)jsonPayloadBytes / dataBytes * 100.0;
            double jsonSavingsPct = (1.0 - ((double)jsonPayloadBytes / unpackedMetaBytes)) * 100.0;

            double brotliOverheadPct = (double)brotliPayloadBytes / dataBytes * 100.0;
            double brotliSavingsPct = (1.0 - ((double)brotliPayloadBytes / unpackedMetaBytes)) * 100.0;

            output.WriteLine($"| Model ({count} items/col) | {dataBytes:N0} B | {unpackedMetaBytes:N0} B | {jsonPayloadBytes:N0} B | {jsonOverheadPct:F1}% | {jsonSavingsPct:F1}% | {brotliPayloadBytes:N0} B | {brotliOverheadPct:F1}% | {brotliSavingsPct:F1}% |");

            jsonPayloadBytes.ShouldBeGreaterThan(0);
            brotliPayloadBytes.ShouldBeGreaterThan(0);
            jsonPayloadBytes.ShouldBeLessThan(unpackedMetaBytes);
            brotliPayloadBytes.ShouldBeLessThan(unpackedMetaBytes);
        }

        output.WriteLine("");
        output.WriteLine("=== Full Replica Document Cache Memory Footprint (Unpacked vs Packed JSON vs Packed Brotli) ===");
        output.WriteLine("| Configuration | Unpacked Docs Bytes | Packed (JSON) Retained Bytes | Packed (Brotli) Retained Bytes | JSON Cache Reduction % | Brotli Cache Reduction % |");
        output.WriteLine("| :--- | :---: | :---: | :---: | :---: | :---: |");

        var cacheConfigs = new (string Name, int Documents, int ItemsPerCol)[]
        {
            ("Small Cache (10 docs, 5 items/col)", 10, 5),
            ("Medium Cache (25 docs, 20 items/col)", 25, 20),
            ("Large Cache (50 docs, 50 items/col)", 50, 50)
        };

        foreach (var cfg in cacheConfigs)
        {
            long dataOnlyBytes = MeasureAllocatedBytes(() =>
            {
                var models = new TestMemoryModel[cfg.Documents];
                for (int d = 0; d < cfg.Documents; d++)
                {
                    models[d] = CreateModel(cfg.ItemsPerCol);
                }
                return models;
            });

            long unpackedDocsBytes = MeasureAllocatedBytes(() =>
            {
                var docs = new CrdtDocument<TestMemoryModel>[cfg.Documents];
                for (int d = 0; d < cfg.Documents; d++)
                {
                    var model = CreateModel(cfg.ItemsPerCol);
                    var meta = jsonMetaManager.Initialize(model, timestamp);
                    docs[d] = new CrdtDocument<TestMemoryModel>(model, meta);
                }
                return docs;
            });

            var sampleModel = CreateModel(cfg.ItemsPerCol);
            var sampleDoc = new CrdtDocument<TestMemoryModel>(sampleModel, jsonMetaManager.Initialize(sampleModel, timestamp));
            long singleJsonArrayBytes = jsonMetaManager.Pack(sampleDoc).MetadataBytes.Length + 24; // byte[] object header + length
            long singleBrotliArrayBytes = brotliMetaManager.Pack(sampleDoc).MetadataBytes.Length + 24;

            long packedJsonCacheBytes = dataOnlyBytes + (singleJsonArrayBytes * cfg.Documents);
            long packedBrotliCacheBytes = dataOnlyBytes + (singleBrotliArrayBytes * cfg.Documents);

            double jsonReductionPct = (1.0 - ((double)packedJsonCacheBytes / unpackedDocsBytes)) * 100.0;
            double brotliReductionPct = (1.0 - ((double)packedBrotliCacheBytes / unpackedDocsBytes)) * 100.0;

            output.WriteLine($"| {cfg.Name} | {unpackedDocsBytes:N0} B | {packedJsonCacheBytes:N0} B | {packedBrotliCacheBytes:N0} B | {jsonReductionPct:F1}% | {brotliReductionPct:F1}% |");

            packedJsonCacheBytes.ShouldBeLessThan(unpackedDocsBytes);
            packedBrotliCacheBytes.ShouldBeLessThan(packedJsonCacheBytes);
        }
    }

    [Fact]
    public void MeasureDvvAndShardVersionVectorMapMemory_AcrossDimensions()
    {
        var replicaCounts = new[] { 1, 5, 25, 100 };
        var dotCounts = new[] { 0, 5, 20 };

        output.WriteLine("=== DottedVersionVector (DVV) Memory Footprint ===");
        output.WriteLine("| Replicas | Dots / Replica | Allocated Bytes | Bytes / Replica |");
        output.WriteLine("| :---: | :---: | :---: | :---: |");

        foreach (var replicas in replicaCounts)
        {
            foreach (var dots in dotCounts)
            {
                var repIds = CreateIds("rep", replicas);
                long bytes = MeasureAllocatedBytes(() => CreateDvv(repIds, dots));
                double bytesPerReplica = (double)bytes / replicas;

                output.WriteLine($"| {replicas} | {dots} | {bytes:N0} B | {bytesPerReplica:F1} B |");
                bytes.ShouldBeGreaterThan(0);
            }
        }

        var shardCounts = new[] { 1, 10, 50 };
        output.WriteLine("");
        output.WriteLine("=== ShardVersionVectorMap Memory Footprint ===");
        output.WriteLine("| Shards | Replicas / Shard | Dots / Replica | Allocated Bytes | Bytes / Shard |");
        output.WriteLine("| :---: | :---: | :---: | :---: | :---: |");

        foreach (var shards in shardCounts)
        {
            foreach (var replicas in new[] { 5, 25 })
            {
                var shardIds = CreateIds("shard", shards);
                var repIds = CreateIds("rep", replicas);

                long bytes = MeasureAllocatedBytes(() =>
                {
                    var map = new ShardVersionVectorMap();
                    for (int s = 0; s < shardIds.Length; s++)
                    {
                        map[shardIds[s]] = CreateDvv(repIds, 5);
                    }
                    return map;
                });

                double bytesPerShard = (double)bytes / shards;
                output.WriteLine($"| {shards} | {replicas} | 5 | {bytes:N0} B | {bytesPerShard:F1} B |");
                bytes.ShouldBeGreaterThan(0);
            }
        }
    }

    [Fact]
    public void MeasureReplicaOverhead_AcrossConfigurations()
    {
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();

        // 1. Bare replica scope (ReplicaContext + empty ShardVersionVectorMap)
        long bareScopeBytes = MeasureAllocatedBytes(() =>
        {
            using var scope = scopeFactory.CreateScope("replica-mem-test");
            return scope.ServiceProvider.GetRequiredService<ReplicaContext>();
        });

        // 2. Replica scope with core synchronous services resolved
        long syncScopeBytes = MeasureAllocatedBytes(() =>
        {
            using var scope = scopeFactory.CreateScope("replica-mem-test");
            var sp = scope.ServiceProvider;
            _ = sp.GetRequiredService<ICrdtPatcher>();
            _ = sp.GetRequiredService<ICrdtApplicator>();
            _ = sp.GetRequiredService<ICrdtMerger>();
            _ = sp.GetRequiredService<ICrdtMetadataManager>();
            return sp.GetRequiredService<ReplicaContext>();
        });

        // 3. Replica scope with async pipeline adapters resolved
        long asyncScopeBytes = MeasureAllocatedBytes(() =>
        {
            using var scope = scopeFactory.CreateScope("replica-mem-test");
            var sp = scope.ServiceProvider;
            _ = sp.GetRequiredService<IAsyncCrdtPatcher>();
            _ = sp.GetRequiredService<IAsyncCrdtApplicator>();
            _ = sp.GetRequiredService<IAsyncCrdtMerger>();
            return sp.GetRequiredService<ReplicaContext>();
        });

        output.WriteLine("=== Replica Scope & DI Resolution Overhead ===");
        output.WriteLine("| Configuration | Allocated Bytes |");
        output.WriteLine("| :--- | :---: |");
        output.WriteLine($"| Bare Scope (`ReplicaContext` only) | {bareScopeBytes:N0} B |");
        output.WriteLine($"| Core Sync Services (`Patcher`, `Applicator`, `Merger`, `MetadataManager`) | {syncScopeBytes:N0} B |");
        output.WriteLine($"| Async Pipeline (`IAsyncCrdtPatcher`, `IAsyncCrdtApplicator`, `IAsyncCrdtMerger`) | {asyncScopeBytes:N0} B |");

        bareScopeBytes.ShouldBeGreaterThan(0);
        syncScopeBytes.ShouldBeGreaterThan(bareScopeBytes);
        asyncScopeBytes.ShouldBeGreaterThan(bareScopeBytes);

        // 4. Full active replica state across cluster/shard/document configurations
        var configs = new (string Name, int Shards, int PeerReplicas, int Dots, int Documents, int ItemsPerCol)[]
        {
            ("Minimal (1 shard, 3 peers, 1 doc, 0 items)", 1, 3, 0, 1, 0),
            ("Small (1 shard, 5 peers, 5 docs, 5 items)", 1, 5, 2, 5, 5),
            ("Medium (5 shards, 15 peers, 10 docs, 20 items)", 5, 15, 5, 10, 20),
            ("Large Sharded (20 shards, 50 peers, 25 docs, 50 items)", 20, 50, 10, 25, 50)
        };

        output.WriteLine("");
        output.WriteLine("=== Full Replica Memory Footprint Across Configurations ===");
        output.WriteLine("| Configuration | Scope & Shards Bytes | Documents (Data + Meta) Bytes | Total Replica Bytes | Bytes / Document |");
        output.WriteLine("| :--- | :---: | :---: | :---: | :---: |");

        using var initScope = scopeFactory.CreateScope("replica-mem-test");
        var metadataManager = initScope.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();
        var timestamp = new CrdtTimestamp(1000, 0, ReplicaId: "replica-mem-test");

        foreach (var cfg in configs)
        {
            var shardIds = CreateIds("shard", cfg.Shards);
            var repIds = CreateIds("rep", cfg.PeerReplicas);

            long scopeAndShardsBytes = MeasureAllocatedBytes(() =>
            {
                var map = new ShardVersionVectorMap();
                for (int s = 0; s < shardIds.Length; s++)
                {
                    map[shardIds[s]] = CreateDvv(repIds, cfg.Dots);
                }

                using var scope = scopeFactory.CreateScope("replica-mem-test", map);
                _ = scope.ServiceProvider.GetRequiredService<ICrdtPatcher>();
                _ = scope.ServiceProvider.GetRequiredService<ICrdtApplicator>();
                return scope.ServiceProvider.GetRequiredService<ReplicaContext>();
            });

            long docsBytes = MeasureAllocatedBytes(() =>
            {
                var docs = new CrdtDocument<TestMemoryModel>[cfg.Documents];
                for (int d = 0; d < cfg.Documents; d++)
                {
                    var model = CreateModel(cfg.ItemsPerCol);
                    var meta = metadataManager.Initialize(model, timestamp);
                    docs[d] = new CrdtDocument<TestMemoryModel>(model, meta);
                }
                return docs;
            });

            long totalReplicaBytes = scopeAndShardsBytes + docsBytes;
            double bytesPerDoc = (double)totalReplicaBytes / cfg.Documents;

            output.WriteLine($"| {cfg.Name} | {scopeAndShardsBytes:N0} B | {docsBytes:N0} B | {totalReplicaBytes:N0} B | {bytesPerDoc:F1} B |");

            scopeAndShardsBytes.ShouldBeGreaterThan(0);
            docsBytes.ShouldBeGreaterThan(0);
            totalReplicaBytes.ShouldBeGreaterThan(0);
        }
    }

    [Fact]
    public void MeasureReplicaServiceConfigurationsOverhead_AcrossPipelines()
    {
        var configurations = new (string Name, Action<IServiceCollection> Configure)[]
        {
            ("1. Default Minimal (`AddCrdt` only)", services =>
            {
                services.AddCrdt();
                services.AddCrdtAotContext<MemoryOverheadTestCrdtAotContext>();
            }),
            ("2. With Brotli Compression (`useBrotliCompression: true`)", services =>
            {
                services.AddCrdt()
                        .AddCrdtSystemTextJson(useBrotliCompression: true);
                services.AddCrdtAotContext<MemoryOverheadTestCrdtAotContext>();
            }),
            ("3. With Custom Providers (Timestamp, DocId, ShardId, Comparer)", services =>
            {
                services.AddCrdt()
                        .AddCrdtAotContext<MemoryOverheadTestCrdtAotContext>()
                        .AddCrdtTimestampProvider<DummyTimestampProvider>()
                        .AddCrdtDocumentIdProvider<DummyDocumentIdProvider>()
                        .AddCrdtShardIdProvider<DummyShardIdProvider>()
                        .AddCrdtComparer<DummyElementComparer>();
            }),
            ("4. With Journaling Pipeline (Patcher & Applicator Decorators)", services =>
            {
                services.AddCrdt()
                        .AddCrdtAotContext<MemoryOverheadTestCrdtAotContext>()
                        .AddCrdtJournaling<NoOpOperationJournal>()
                        .AddCrdtApplicatorDecorator<JournalingApplicatorDecorator>(DecoratorBehavior.Before)
                        .AddCrdtPatcherDecorator<JournalingPatcherDecorator>(DecoratorBehavior.After);
            }),
            ("5. With Compaction Pipeline (Threshold + GMVV + Applicator/Merger Decorators)", services =>
            {
                services.AddCrdt()
                        .AddCrdtAotContext<MemoryOverheadTestCrdtAotContext>()
                        .AddCrdtCompactionPolicyFactory(sp => new ThresholdCompactionPolicyFactory(TimeSpan.FromDays(7), sp.GetRequiredService<ICrdtTimestampProvider>()))
                        .AddCrdtCompactionPolicyFactory(_ => new GlobalMinimumVersionPolicyFactory(() => new Dictionary<string, long>()))
                        .AddCrdtApplicatorDecorator<CompactingApplicatorDecorator>(DecoratorBehavior.After)
                        .AddCrdtMergerDecorator<CompactingMergerDecorator>(DecoratorBehavior.After);
            }),
            ("6. Full Enterprise Pipeline (Journaling + Compaction + Custom Providers + Brotli)", services =>
            {
                services.AddCrdt()
                        .AddCrdtSystemTextJson(useBrotliCompression: true)
                        .AddCrdtAotContext<MemoryOverheadTestCrdtAotContext>()
                        .AddCrdtTimestampProvider<DummyTimestampProvider>()
                        .AddCrdtDocumentIdProvider<DummyDocumentIdProvider>()
                        .AddCrdtShardIdProvider<DummyShardIdProvider>()
                        .AddCrdtComparer<DummyElementComparer>()
                        .AddCrdtJournaling<NoOpOperationJournal>()
                        .AddCrdtCompactionPolicyFactory(sp => new ThresholdCompactionPolicyFactory(TimeSpan.FromDays(7), sp.GetRequiredService<ICrdtTimestampProvider>()))
                        .AddCrdtCompactionPolicyFactory(_ => new GlobalMinimumVersionPolicyFactory(() => new Dictionary<string, long>()))
                        .AddCrdtApplicatorDecorator<JournalingApplicatorDecorator>(DecoratorBehavior.Before)
                        .AddCrdtApplicatorDecorator<CompactingApplicatorDecorator>(DecoratorBehavior.After)
                        .AddCrdtPatcherDecorator<JournalingPatcherDecorator>(DecoratorBehavior.After)
                        .AddCrdtMergerDecorator<CompactingMergerDecorator>(DecoratorBehavior.After);
            })
        };

        output.WriteLine("=== Replica DI & Pipeline Configuration Memory Overhead ===");
        output.WriteLine("| DI Configuration | Bare Scope | Sync Services | Async Pipeline | Active Replica (5 Shards, 10 Docs) |");
        output.WriteLine("| :--- | :---: | :---: | :---: | :---: |");

        var shardIds = CreateIds("shard", 5);
        var repIds = CreateIds("rep", 10);
        var timestamp = new CrdtTimestamp(1000, 0, ReplicaId: "replica-cfg-test");

        foreach (var (name, configure) in configurations)
        {
            var services = new ServiceCollection();
            configure(services);
            using var sp = services.BuildServiceProvider();
            var scopeFactory = sp.GetRequiredService<ICrdtScopeFactory>();

            long bareBytes = MeasureAllocatedBytes(() =>
            {
                using var scope = scopeFactory.CreateScope("replica-cfg-test");
                return scope.ServiceProvider.GetRequiredService<ReplicaContext>();
            });

            long syncBytes = MeasureAllocatedBytes(() =>
            {
                using var scope = scopeFactory.CreateScope("replica-cfg-test");
                var provider = scope.ServiceProvider;
                _ = provider.GetRequiredService<ICrdtPatcher>();
                _ = provider.GetRequiredService<ICrdtApplicator>();
                _ = provider.GetRequiredService<ICrdtMerger>();
                _ = provider.GetRequiredService<ICrdtMetadataManager>();
                return provider.GetRequiredService<ReplicaContext>();
            });

            long asyncBytes = MeasureAllocatedBytes(() =>
            {
                using var scope = scopeFactory.CreateScope("replica-cfg-test");
                var provider = scope.ServiceProvider;
                _ = provider.GetRequiredService<IAsyncCrdtPatcher>();
                _ = provider.GetRequiredService<IAsyncCrdtApplicator>();
                _ = provider.GetRequiredService<IAsyncCrdtMerger>();
                return provider.GetRequiredService<ReplicaContext>();
            });

            long activeReplicaBytes = MeasureAllocatedBytes(() =>
            {
                var map = new ShardVersionVectorMap();
                for (int s = 0; s < shardIds.Length; s++)
                {
                    map[shardIds[s]] = CreateDvv(repIds, 3);
                }

                using var scope = scopeFactory.CreateScope("replica-cfg-test", map);
                var provider = scope.ServiceProvider;
                _ = provider.GetRequiredService<IAsyncCrdtPatcher>();
                _ = provider.GetRequiredService<IAsyncCrdtApplicator>();
                _ = provider.GetRequiredService<IAsyncCrdtMerger>();
                var metaManager = provider.GetRequiredService<ICrdtMetadataManager>();

                var docs = new CrdtDocument<TestMemoryModel>[10];
                for (int d = 0; d < 10; d++)
                {
                    var model = CreateModel(10);
                    var meta = metaManager.Initialize(model, timestamp);
                    docs[d] = new CrdtDocument<TestMemoryModel>(model, meta);
                }

                return docs;
            });

            output.WriteLine($"| {name} | {bareBytes:N0} B | {syncBytes:N0} B | {asyncBytes:N0} B | {activeReplicaBytes:N0} B |");

            bareBytes.ShouldBeGreaterThan(0);
            syncBytes.ShouldBeGreaterThan(bareBytes);
            asyncBytes.ShouldBeGreaterThan(bareBytes);
            activeReplicaBytes.ShouldBeGreaterThan(asyncBytes);
        }
    }

    private static TestMemoryModel CreateModel(int collectionSize)
    {
        var tags = new List<string>(collectionSize);
        var scores = new Dictionary<string, int>(collectionSize);

        for (int i = 0; i < collectionSize; i++)
        {
            tags.Add("item");
            scores[$"k{i}"] = i;
        }

        return new TestMemoryModel
        {
            Id = "doc-1",
            Name = "Sample Document Title",
            Count = 42,
            Tags = tags,
            Scores = scores
        };
    }

    private static DottedVersionVector CreateDvv(string[] replicaIds, int dotsPerReplica)
    {
        var dvv = new DottedVersionVector();
        for (int r = 0; r < replicaIds.Length; r++)
        {
            var repId = replicaIds[r];
            dvv.Versions[repId] = 50;
            for (int d = 0; d < dotsPerReplica; d++)
            {
                dvv.Add(repId, 52 + (d * 2));
            }
        }
        return dvv;
    }

    private static string[] CreateIds(string prefix, int count)
    {
        var arr = new string[count];
        for (int i = 0; i < count; i++)
        {
            arr[i] = $"{prefix}-{i:D3}";
        }
        return arr;
    }

    private static long MeasureAllocatedBytes<T>(Func<T> action)
    {
        const int iterations = 50;
        _ = action(); // Warmup

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < iterations; i++)
        {
            _ = action();
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        return (after - before) / iterations;
    }

    private sealed class NoOpOperationJournal : ICrdtOperationJournal
    {
        public void Append(IComparable documentId, IReadOnlyList<CrdtOperation> operations)
        {
        }

        public Task AppendAsync(IComparable documentId, IReadOnlyList<CrdtOperation> operations, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public async IAsyncEnumerable<JournaledOperation> GetOperationsByRangeAsync(string shardId, string originReplicaId, long minShardClock, long maxShardClock, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            yield break;
        }

        public async IAsyncEnumerable<JournaledOperation> GetOperationsByDotsAsync(string shardId, string originReplicaId, IEnumerable<long> shardClocks, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            yield break;
        }
    }

    private sealed class DummyTimestampProvider : ICrdtTimestampProvider
    {
        private readonly ReplicaContext replicaContext;

        public DummyTimestampProvider(ReplicaContext replicaContext)
        {
            this.replicaContext = replicaContext;
        }

        public CrdtTimestamp Now() => new(1000, 0, ReplicaId: replicaContext.ReplicaId);

        public CrdtTimestamp Create(long value) => new(value, 0, ReplicaId: replicaContext.ReplicaId);
    }

    private sealed class DummyDocumentIdProvider : IDocumentIdProvider
    {
        public IComparable GetDocumentId<T>(T? obj) => "doc-1";

        public void SetDocumentId<T>(T obj, IComparable id)
        {
        }

        public T CreateDocumentWithId<T>(IComparable id) => default!;
    }

    private sealed class DummyShardIdProvider : IShardIdProvider
    {
        public string GetShardId<T>(CrdtDocument<T> document) where T : class => "shard-default";

        public string GetShardId<T>(IComparable documentId) where T : class => "shard-default";

        public string GetShardId(IComparable documentId, Type documentType) => "shard-default";
    }

    private sealed class DummyElementComparer : IElementComparer
    {
        public bool CanCompare(Type type) => type == typeof(string);

        public new bool Equals(object? x, object? y) => string.Equals(x as string, y as string, StringComparison.Ordinal);

        public int GetHashCode(object obj) => obj?.GetHashCode() ?? 0;
    }
}

public sealed class TestMemoryModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    [CrdtCounterStrategy]
    public int Count { get; set; }

    [CrdtOrSetStrategy]
    public List<string> Tags { get; set; } = new();

    [CrdtLwwMapStrategy]
    public Dictionary<string, int> Scores { get; set; } = new();
}

[CrdtAotType(typeof(TestMemoryModel))]
[CrdtAotType(typeof(List<string>))]
[CrdtAotType(typeof(Dictionary<string, int>))]
internal partial class MemoryOverheadTestCrdtAotContext : CrdtAotContext
{
}