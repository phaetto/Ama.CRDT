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
using Ama.CRDT.Models.Intents;
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
        var timestamp = new CrdtTimestamp(1000, 0, ReplicaId: "replica-mem-test");

        // Warmup JIT and string pools
        var warmupDoc = BuildSimulatedDocument(scope.ServiceProvider, 5, 10, timestamp);
        _ = warmupDoc.Metadata.DeepClone();

        var itemCounts = new[] { 0, 5, 25, 100 };
        var opCounts = new[] { 0, 10, 50 };

        output.WriteLine("| Items / Col | Ops Applied | Data Bytes | Metadata Bytes | Full Doc Bytes | Meta Overhead % | Meta/Data Ratio |");
        output.WriteLine("| :---: | :---: | :---: | :---: | :---: | :---: | :---: |");

        foreach (var count in itemCounts)
        {
            foreach (var ops in opCounts)
            {
                var doc = BuildSimulatedDocument(scope.ServiceProvider, count, ops, timestamp);

                long dataBytes = MeasureAllocatedBytes(() => CloneModel(doc.Data));
                long metaBytes = MeasureAllocatedBytes(() => doc.Metadata.DeepClone());
                long fullDocBytes = dataBytes + metaBytes;

                double overheadPercentage = (double)metaBytes / dataBytes * 100.0;
                double ratio = (double)metaBytes / dataBytes;

                output.WriteLine($"| {count} | {ops} | {dataBytes:N0} B | {metaBytes:N0} B | {fullDocBytes:N0} B | {overheadPercentage:F1}% | {ratio:F2}x |");

                dataBytes.ShouldBeGreaterThan(0);
                metaBytes.ShouldBeGreaterThan(0);
                overheadPercentage.ShouldBeGreaterThan(0);
            }
        }
    }

    [Fact]
    public void MeasurePackedDocumentMemoryOverhead_AcrossConfigurations()
    {
        var brotliServices = new ServiceCollection();
        brotliServices.AddCrdt().AddCrdtSystemTextJson(useBrotliCompression: true);
        brotliServices.AddCrdtAotContext<MemoryOverheadTestCrdtAotContext>();
        using var brotliServiceProvider = brotliServices.BuildServiceProvider();

        using var brotliScope = brotliServiceProvider.GetRequiredService<ICrdtScopeFactory>().CreateScope("replica-mem-test");

        var brotliMetaManager = brotliScope.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();
        var timestamp = new CrdtTimestamp(1000, 0, ReplicaId: "replica-mem-test");

        // Warmup JIT and string pools
        var warmupDoc = BuildSimulatedDocument(brotliScope.ServiceProvider, 5, 10, timestamp);
        _ = brotliMetaManager.Pack(warmupDoc);

        var itemCounts = new[] { 0, 5, 25, 100 };
        var opCounts = new[] { 0, 10, 50 };

        output.WriteLine("=== PackedCrdtDocument (Brotli) At-Rest Payload Size vs In-Memory CrdtMetadata ===");
        output.WriteLine("| Items / Col | Ops Applied | Data Bytes | Unpacked Meta (Heap) | Packed Brotli Bytes | Brotli Meta Overhead % | Brotli Savings % |");
        output.WriteLine("| :---: | :---: | :---: | :---: | :---: | :---: | :---: |");

        foreach (var count in itemCounts)
        {
            foreach (var ops in opCounts)
            {
                var sampleDoc = BuildSimulatedDocument(brotliScope.ServiceProvider, count, ops, timestamp);

                long dataBytes = MeasureAllocatedBytes(() => CloneModel(sampleDoc.Data));
                long unpackedMetaBytes = MeasureAllocatedBytes(() => sampleDoc.Metadata.DeepClone());

                var packedBrotliDoc = brotliMetaManager.Pack(sampleDoc);

                long brotliPayloadBytes = packedBrotliDoc.MetadataBytes.Length;

                double brotliOverheadPct = (double)brotliPayloadBytes / dataBytes * 100.0;
                double brotliSavingsPct = (1.0 - ((double)brotliPayloadBytes / unpackedMetaBytes)) * 100.0;

                output.WriteLine($"| {count} | {ops} | {dataBytes:N0} B | {unpackedMetaBytes:N0} B | {brotliPayloadBytes:N0} B | {brotliOverheadPct:F1}% | {brotliSavingsPct:F1}% |");

                brotliPayloadBytes.ShouldBeGreaterThan(0);
                brotliPayloadBytes.ShouldBeLessThan(unpackedMetaBytes);
            }
        }

        output.WriteLine("");
        output.WriteLine("=== Full Replica Document Cache Memory Footprint (Unpacked vs Packed Brotli) ===");
        output.WriteLine("| Configuration | Unpacked Docs Bytes | Packed (Brotli) Retained Bytes | Brotli Cache Reduction % |");
        output.WriteLine("| :--- | :---: | :---: | :---: |");

        var cacheConfigs = new (string Name, int Documents, int ItemsPerCol, int OpsPerDoc)[]
        {
            ("Small Cache (10 docs, 5 items/col, 0 ops)", 10, 5, 0),
            ("Small Cache (10 docs, 5 items/col, 15 ops)", 10, 5, 15),
            ("Medium Cache (25 docs, 20 items/col, 0 ops)", 25, 20, 0),
            ("Medium Cache (25 docs, 20 items/col, 30 ops)", 25, 20, 30),
            ("Large Cache (50 docs, 50 items/col, 0 ops)", 50, 50, 0),
            ("Large Cache (50 docs, 50 items/col, 50 ops)", 50, 50, 50)
        };

        foreach (var cfg in cacheConfigs)
        {
            var templateDoc = BuildSimulatedDocument(brotliScope.ServiceProvider, cfg.ItemsPerCol, cfg.OpsPerDoc, timestamp);

            long dataOnlyBytes = MeasureAllocatedBytes(() =>
            {
                var models = new TestMemoryModel[cfg.Documents];
                for (int d = 0; d < cfg.Documents; d++)
                {
                    models[d] = CloneModel(templateDoc.Data);
                }
                return models;
            });

            long unpackedDocsBytes = MeasureAllocatedBytes(() =>
            {
                var docs = new CrdtDocument<TestMemoryModel>[cfg.Documents];
                for (int d = 0; d < cfg.Documents; d++)
                {
                    var model = CloneModel(templateDoc.Data);
                    var meta = templateDoc.Metadata.DeepClone();
                    docs[d] = new CrdtDocument<TestMemoryModel>(model, meta);
                }
                return docs;
            });

            long singleBrotliArrayBytes = brotliMetaManager.Pack(templateDoc).MetadataBytes.Length + 24; // byte[] object header + length
            long packedBrotliCacheBytes = dataOnlyBytes + (singleBrotliArrayBytes * cfg.Documents);

            double brotliReductionPct = (1.0 - ((double)packedBrotliCacheBytes / unpackedDocsBytes)) * 100.0;

            output.WriteLine($"| {cfg.Name} | {unpackedDocsBytes:N0} B | {packedBrotliCacheBytes:N0} B | {brotliReductionPct:F1}% |");

            packedBrotliCacheBytes.ShouldBeLessThan(unpackedDocsBytes);
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
        bareScopeBytes.ShouldBeLessThan(600);
        syncScopeBytes.ShouldBeGreaterThan(bareScopeBytes);
        syncScopeBytes.ShouldBeLessThan(1500);
        asyncScopeBytes.ShouldBeGreaterThan(syncScopeBytes);
        asyncScopeBytes.ShouldBeLessThan(2000);

        // 4. Full active replica state across cluster/shard/document/ops configurations
        var configs = new (string Name, int Shards, int PeerReplicas, int Dots, int Documents, int ItemsPerCol, int OpsPerDoc)[]
        {
            ("Minimal (1 shard, 3 peers, 1 doc, 0 items, 0 ops)", 1, 3, 0, 1, 0, 0),
            ("Small (1 shard, 5 peers, 5 docs, 5 items, 10 ops)", 1, 5, 2, 5, 5, 10),
            ("Medium (5 shards, 15 peers, 10 docs, 20 items, 25 ops)", 5, 15, 5, 10, 20, 25),
            ("Large Sharded (20 shards, 50 peers, 25 docs, 50 items, 50 ops)", 20, 50, 10, 25, 50, 50)
        };

        output.WriteLine("");
        output.WriteLine("=== Full Replica Memory Footprint Across Configurations ===");
        output.WriteLine("| Configuration | Scope & Shards Bytes | Documents (Data + Meta) Bytes | Total Replica Bytes | Bytes / Document |");
        output.WriteLine("| :--- | :---: | :---: | :---: | :---: |");

        using var initScope = scopeFactory.CreateScope("replica-mem-test");
        var timestamp = new CrdtTimestamp(1000, 0, ReplicaId: "replica-mem-test");

        foreach (var cfg in configs)
        {
            var shardIds = CreateIds("shard", cfg.Shards);
            var repIds = CreateIds("rep", cfg.PeerReplicas);
            var templateDoc = BuildSimulatedDocument(initScope.ServiceProvider, cfg.ItemsPerCol, cfg.OpsPerDoc, timestamp);

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
                    var model = CloneModel(templateDoc.Data);
                    var meta = templateDoc.Metadata.DeepClone();
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

    private static TestMemoryModel CloneModel(TestMemoryModel source)
    {
        var tags = new List<string>(source.Tags.Count);
        for (int i = 0; i < source.Tags.Count; i++)
        {
            tags.Add(source.Tags[i]);
        }

        var scores = new Dictionary<string, int>(source.Scores.Count);
        foreach (var kvp in source.Scores)
        {
            scores[kvp.Key] = kvp.Value;
        }

        return new TestMemoryModel
        {
            Id = source.Id,
            Name = source.Name,
            Count = source.Count,
            Tags = tags,
            Scores = scores
        };
    }

    private static CrdtDocument<TestMemoryModel> BuildSimulatedDocument(IServiceProvider sp, int initialCollectionSize, int opsCount, CrdtTimestamp baseTimestamp)
    {
        var metadataManager = sp.GetRequiredService<ICrdtMetadataManager>();
        var patcher = sp.GetRequiredService<ICrdtPatcher>();
        var applicator = sp.GetRequiredService<ICrdtApplicator>();

        var model = CreateModel(initialCollectionSize);
        var meta = metadataManager.Initialize(model, baseTimestamp);
        var doc = new CrdtDocument<TestMemoryModel>(model, meta);

        for (int i = 0; i < opsCount; i++)
        {
            var ts = new CrdtTimestamp(baseTimestamp.Ticks + i + 1, 0, ReplicaId: "replica-mem-test");
            CrdtOperation op = (i % 5) switch
            {
                0 => patcher.GenerateOperation(doc, m => m.Count, new IncrementIntent(1), ts),
                1 => patcher.GenerateOperation(doc, m => m.Tags, new AddIntent($"op-tag-{i}"), ts),
                2 => patcher.GenerateOperation(doc, m => m.Tags, new RemoveValueIntent(i >= 5 ? $"op-tag-{i - 4}" : "item"), ts),
                3 => patcher.GenerateOperation(doc, m => m.Scores, new MapSetIntent($"op-k{i}", i * 10), ts),
                _ => patcher.GenerateOperation(doc, m => m.Scores, new MapRemoveIntent(i >= 5 ? $"op-k{i - 1}" : "k0"), ts)
            };

            applicator.ApplyPatch(doc, new CrdtPatch([op]));
        }

        if (opsCount > 0)
        {
            // Simulate causal history and out-of-order exception tracking from peer replicas
            int peerCount = Math.Min(10, (opsCount / 5) + 1);
            for (int r = 1; r <= peerCount; r++)
            {
                doc.Metadata.VersionVector[$"peer-rep-{r:D2}"] = opsCount * 2;
            }

            int exceptionCount = Math.Min(5, (opsCount / 10) + 1);
            for (int e = 0; e < exceptionCount; e++)
            {
                var exTs = new CrdtTimestamp(baseTimestamp.Ticks + 5000 + e, 0, ReplicaId: "peer-rep-01");
                doc.Metadata.SeenExceptions.Add(new CrdtOperation(
                    Guid.NewGuid(),
                    "peer-rep-01",
                    "$.count",
                    OperationType.Increment,
                    1m,
                    exTs,
                    (opsCount * 2) + e + 2));
            }
        }

        return doc;
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