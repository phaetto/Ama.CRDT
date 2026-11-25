namespace Ama.CRDT.Benchmarks.Benchmarks;

using System;
using System.Collections.Generic;
using Ama.CRDT.Benchmarks.Models;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.DependencyInjection;

[Config(typeof(MemoryOverheadConfig))]
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[Orderer(SummaryOrderPolicy.Declared)]
public class MemoryOverheadBenchmarks
{
    private IServiceScope scope = null!;
    private IServiceScope brotliScope = null!;
    private ICrdtMetadataManager metadataManager = null!;
    private ICrdtMetadataManager brotliMetadataManager = null!;
    private CrdtTimestamp timestamp;

    // Pre-built template instances for cloning / measurement
    private SimplePoco simpleTemplate = null!;
    private WidePoco wideTemplate = null!;
    private ComplexPoco complexSmallTemplate = null!;
    private ComplexPoco complexLargeTemplate = null!;
    private CollectionHeavyPoco collSmallTemplate = null!;
    private CollectionHeavyPoco collMediumTemplate = null!;
    private CollectionHeavyPoco collLargeTemplate = null!;

    private CrdtDocument<SimplePoco> simpleDocTemplate;
    private CrdtDocument<WidePoco> wideDocTemplate;
    private CrdtDocument<ComplexPoco> complexSmallDocTemplate;
    private CrdtDocument<ComplexPoco> complexLargeDocTemplate;
    private CrdtDocument<CollectionHeavyPoco> collSmallDocTemplate;
    private CrdtDocument<CollectionHeavyPoco> collMediumDocTemplate;
    private CrdtDocument<CollectionHeavyPoco> collLargeDocTemplate;

    private ICrdtScopeFactory scopeFactory = null!;
    private string[] replicaIds = null!;
    private string[] shardIds = null!;
    private string[] tagPool = null!;
    private string[] keyPool = null!;
    private string[] valPool = null!;

    public sealed class MemoryOverheadConfig : AntiVirusFriendlyConfig
    {
        public MemoryOverheadConfig()
        {
            AddColumn(new MetadataOverheadPercentColumn());
            AddColumn(new MetadataToDataRatioColumn());
        }
    }

    public sealed class MetadataOverheadPercentColumn : IColumn
    {
        public string Id => nameof(MetadataOverheadPercentColumn);
        public string ColumnName => "Meta Overhead %";
        public bool AlwaysShow => true;
        public ColumnCategory Category => ColumnCategory.Custom;
        public int PriorityInCategory => 0;
        public bool IsNumeric => true;
        public UnitType UnitType => UnitType.Dimensionless;
        public string Legend => "Percentage of CRDT Metadata allocated bytes relative to Data Model allocated bytes ((FullDoc - DataOnly) / DataOnly * 100)";

        public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;
        public bool IsAvailable(Summary summary) => true;

        public string GetValue(Summary summary, BenchmarkCase benchmarkCase) => GetValue(summary, benchmarkCase, SummaryStyle.Default);

        public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
        {
            var categories = benchmarkCase.Descriptor.Categories;
            if (categories == null || categories.Length == 0) return "-";

            var category = categories[0];
            if (!category.StartsWith("Doc:", StringComparison.Ordinal)) return "-";

            BenchmarkCase? baselineCase = null;
            foreach (var bc in summary.BenchmarksCases)
            {
                if (bc.Descriptor.Categories.Length > 0 &&
                    bc.Descriptor.Categories[0] == category &&
                    bc.Descriptor.Baseline)
                {
                    baselineCase = bc;
                    break;
                }
            }

            if (baselineCase == null) return "-";
            if (benchmarkCase == baselineCase) return "0.0% (Base)";

            var currentReport = summary[benchmarkCase];
            var baselineReport = summary[baselineCase];

            var currentBytes = currentReport?.GcStats.GetBytesAllocatedPerOperation(benchmarkCase);
            var baseBytes = baselineReport?.GcStats.GetBytesAllocatedPerOperation(baselineCase);

            if (currentBytes is null || baseBytes is null || baseBytes.Value <= 0) return "-";

            var methodName = benchmarkCase.Descriptor.WorkloadMethod.Name;
            double metaBytes = (methodName.Contains("FullDoc", StringComparison.Ordinal) || methodName.Contains("Packed", StringComparison.Ordinal))
                ? currentBytes.Value - baseBytes.Value
                : currentBytes.Value;

            double overheadPct = (metaBytes / baseBytes.Value) * 100.0;
            return $"{overheadPct:F1}%";
        }
    }

    public sealed class MetadataToDataRatioColumn : IColumn
    {
        public string Id => nameof(MetadataToDataRatioColumn);
        public string ColumnName => "Meta/Data Ratio";
        public bool AlwaysShow => true;
        public ColumnCategory Category => ColumnCategory.Custom;
        public int PriorityInCategory => 1;
        public bool IsNumeric => true;
        public UnitType UnitType => UnitType.Dimensionless;
        public string Legend => "Ratio of CRDT Metadata bytes to Data Model bytes";

        public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;
        public bool IsAvailable(Summary summary) => true;

        public string GetValue(Summary summary, BenchmarkCase benchmarkCase) => GetValue(summary, benchmarkCase, SummaryStyle.Default);

        public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
        {
            var categories = benchmarkCase.Descriptor.Categories;
            if (categories == null || categories.Length == 0) return "-";

            var category = categories[0];
            if (!category.StartsWith("Doc:", StringComparison.Ordinal)) return "-";

            BenchmarkCase? baselineCase = null;
            foreach (var bc in summary.BenchmarksCases)
            {
                if (bc.Descriptor.Categories.Length > 0 &&
                    bc.Descriptor.Categories[0] == category &&
                    bc.Descriptor.Baseline)
                {
                    baselineCase = bc;
                    break;
                }
            }

            if (baselineCase == null) return "-";
            if (benchmarkCase == baselineCase) return "1.00x (Data)";

            var currentReport = summary[benchmarkCase];
            var baselineReport = summary[baselineCase];

            var currentBytes = currentReport?.GcStats.GetBytesAllocatedPerOperation(benchmarkCase);
            var baseBytes = baselineReport?.GcStats.GetBytesAllocatedPerOperation(baselineCase);

            if (currentBytes is null || baseBytes is null || baseBytes.Value <= 0) return "-";

            var methodName = benchmarkCase.Descriptor.WorkloadMethod.Name;
            double metaBytes = (methodName.Contains("FullDoc", StringComparison.Ordinal) || methodName.Contains("Packed", StringComparison.Ordinal))
                ? currentBytes.Value - baseBytes.Value
                : currentBytes.Value;

            double ratio = metaBytes / baseBytes.Value;
            return $"{ratio:F2}x";
        }
    }

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddCrdt();
        services.AddCrdtAotContext<BenchmarkCrdtAotContext>();
        var serviceProvider = services.BuildServiceProvider();
        scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        scope = scopeFactory.CreateScope("replica-bench");

        var brotliServices = new ServiceCollection();
        brotliServices.AddCrdt().AddCrdtSystemTextJson(useBrotliCompression: true);
        brotliServices.AddCrdtAotContext<BenchmarkCrdtAotContext>();
        var brotliServiceProvider = brotliServices.BuildServiceProvider();
        brotliScope = brotliServiceProvider.GetRequiredService<ICrdtScopeFactory>().CreateScope("replica-bench");

        metadataManager = scope.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();
        brotliMetadataManager = brotliScope.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();
        timestamp = new CrdtTimestamp(1000, 0, ReplicaId:"replica-bench");

        // Pre-allocate string pools so string allocations don't skew structural overhead measurements
        replicaIds = new string[200];
        for (int i = 0; i < replicaIds.Length; i++) replicaIds[i] = $"replica-{i:D3}";

        shardIds = new string[200];
        for (int i = 0; i < shardIds.Length; i++) shardIds[i] = $"shard-{i:D3}";

        tagPool = new string[200];
        keyPool = new string[200];
        valPool = new string[200];
        for (int i = 0; i < 200; i++)
        {
            tagPool[i] = $"tag-item-{i:D3}";
            keyPool[i] = $"attr-key-{i:D3}";
            valPool[i] = $"attr-value-payload-{i:D3}";
        }

        simpleTemplate = CreateSimplePoco();
        wideTemplate = CreateWidePoco();
        complexSmallTemplate = CreateComplexPoco(5);
        complexLargeTemplate = CreateComplexPoco(50);
        collSmallTemplate = CreateCollectionPoco(5);
        collMediumTemplate = CreateCollectionPoco(25);
        collLargeTemplate = CreateCollectionPoco(100);

        simpleDocTemplate = new CrdtDocument<SimplePoco>(simpleTemplate, metadataManager.Initialize(simpleTemplate, timestamp));
        wideDocTemplate = new CrdtDocument<WidePoco>(wideTemplate, metadataManager.Initialize(wideTemplate, timestamp));
        complexSmallDocTemplate = new CrdtDocument<ComplexPoco>(complexSmallTemplate, metadataManager.Initialize(complexSmallTemplate, timestamp));
        complexLargeDocTemplate = new CrdtDocument<ComplexPoco>(complexLargeTemplate, metadataManager.Initialize(complexLargeTemplate, timestamp));
        collSmallDocTemplate = new CrdtDocument<CollectionHeavyPoco>(collSmallTemplate, metadataManager.Initialize(collSmallTemplate, timestamp));
        collMediumDocTemplate = new CrdtDocument<CollectionHeavyPoco>(collMediumTemplate, metadataManager.Initialize(collMediumTemplate, timestamp));
        collLargeDocTemplate = new CrdtDocument<CollectionHeavyPoco>(collLargeTemplate, metadataManager.Initialize(collLargeTemplate, timestamp));
    }

    // =========================================================================
    // DIMENSION 1: DOCUMENT vs METADATA OVERHEAD (Various Object Configurations)
    // =========================================================================

    // --- 1A. SimplePoco (3 scalar fields: Guid, LWW String, Counter Int) ---
    [Benchmark(Baseline = true, Description = "DataOnly: SimplePoco (3 props)")]
    [BenchmarkCategory("Doc: SimplePoco")]
    public SimplePoco SimplePoco_DataOnly() => CreateSimplePoco();

    [Benchmark(Description = "MetadataOnly: SimplePoco (3 props)")]
    [BenchmarkCategory("Doc: SimplePoco")]
    public CrdtMetadata SimplePoco_MetadataOnly() => metadataManager.Initialize(simpleTemplate, timestamp);

    [Benchmark(Description = "FullDoc: SimplePoco (Data + Metadata)")]
    [BenchmarkCategory("Doc: SimplePoco")]
    public CrdtDocument<SimplePoco> SimplePoco_FullDoc()
    {
        var data = CreateSimplePoco();
        var meta = metadataManager.Initialize(data, timestamp);
        return new CrdtDocument<SimplePoco>(data, meta);
    }

    [Benchmark(Description = "PackedJson: SimplePoco (Data + JSON Bytes)")]
    [BenchmarkCategory("Doc: SimplePoco")]
    public PackedCrdtDocument<SimplePoco> SimplePoco_PackedJson()
    {
        var data = CreateSimplePoco();
        var packed = metadataManager.Pack(simpleDocTemplate);
        return new PackedCrdtDocument<SimplePoco>(data, packed.MetadataBytes);
    }

    [Benchmark(Description = "PackedBrotli: SimplePoco (Data + Brotli Bytes)")]
    [BenchmarkCategory("Doc: SimplePoco")]
    public PackedCrdtDocument<SimplePoco> SimplePoco_PackedBrotli()
    {
        var data = CreateSimplePoco();
        var packed = brotliMetadataManager.Pack(simpleDocTemplate);
        return new PackedCrdtDocument<SimplePoco>(data, packed.MetadataBytes);
    }

    // --- 1B. WidePoco (21 scalar fields: 10 LWW, 5 Counter, 5 Max/MinWins) ---
    [Benchmark(Baseline = true, Description = "DataOnly: WidePoco (21 props)")]
    [BenchmarkCategory("Doc: WidePoco")]
    public WidePoco WidePoco_DataOnly() => CreateWidePoco();

    [Benchmark(Description = "MetadataOnly: WidePoco (21 props)")]
    [BenchmarkCategory("Doc: WidePoco")]
    public CrdtMetadata WidePoco_MetadataOnly() => metadataManager.Initialize(wideTemplate, timestamp);

    [Benchmark(Description = "FullDoc: WidePoco (Data + Metadata)")]
    [BenchmarkCategory("Doc: WidePoco")]
    public CrdtDocument<WidePoco> WidePoco_FullDoc()
    {
        var data = CreateWidePoco();
        var meta = metadataManager.Initialize(data, timestamp);
        return new CrdtDocument<WidePoco>(data, meta);
    }

    [Benchmark(Description = "PackedJson: WidePoco (Data + JSON Bytes)")]
    [BenchmarkCategory("Doc: WidePoco")]
    public PackedCrdtDocument<WidePoco> WidePoco_PackedJson()
    {
        var data = CreateWidePoco();
        var packed = metadataManager.Pack(wideDocTemplate);
        return new PackedCrdtDocument<WidePoco>(data, packed.MetadataBytes);
    }

    [Benchmark(Description = "PackedBrotli: WidePoco (Data + Brotli Bytes)")]
    [BenchmarkCategory("Doc: WidePoco")]
    public PackedCrdtDocument<WidePoco> WidePoco_PackedBrotli()
    {
        var data = CreateWidePoco();
        var packed = brotliMetadataManager.Pack(wideDocTemplate);
        return new PackedCrdtDocument<WidePoco>(data, packed.MetadataBytes);
    }

    // --- 1C. ComplexPoco Small (Nested object + 5 Tag objects in LCS Array) ---
    [Benchmark(Baseline = true, Description = "DataOnly: ComplexPoco (5 nested items)")]
    [BenchmarkCategory("Doc: ComplexPoco (5 items)")]
    public ComplexPoco ComplexPoco_Small_DataOnly() => CreateComplexPoco(5);

    [Benchmark(Description = "MetadataOnly: ComplexPoco (5 nested items)")]
    [BenchmarkCategory("Doc: ComplexPoco (5 items)")]
    public CrdtMetadata ComplexPoco_Small_MetadataOnly() => metadataManager.Initialize(complexSmallTemplate, timestamp);

    [Benchmark(Description = "FullDoc: ComplexPoco (5 nested items)")]
    [BenchmarkCategory("Doc: ComplexPoco (5 items)")]
    public CrdtDocument<ComplexPoco> ComplexPoco_Small_FullDoc()
    {
        var data = CreateComplexPoco(5);
        var meta = metadataManager.Initialize(data, timestamp);
        return new CrdtDocument<ComplexPoco>(data, meta);
    }

    [Benchmark(Description = "PackedJson: ComplexPoco (5 nested items)")]
    [BenchmarkCategory("Doc: ComplexPoco (5 items)")]
    public PackedCrdtDocument<ComplexPoco> ComplexPoco_Small_PackedJson()
    {
        var data = CreateComplexPoco(5);
        var packed = metadataManager.Pack(complexSmallDocTemplate);
        return new PackedCrdtDocument<ComplexPoco>(data, packed.MetadataBytes);
    }

    [Benchmark(Description = "PackedBrotli: ComplexPoco (5 nested items)")]
    [BenchmarkCategory("Doc: ComplexPoco (5 items)")]
    public PackedCrdtDocument<ComplexPoco> ComplexPoco_Small_PackedBrotli()
    {
        var data = CreateComplexPoco(5);
        var packed = brotliMetadataManager.Pack(complexSmallDocTemplate);
        return new PackedCrdtDocument<ComplexPoco>(data, packed.MetadataBytes);
    }

    // --- 1D. ComplexPoco Large (Nested object + 50 Tag objects in LCS Array) ---
    [Benchmark(Baseline = true, Description = "DataOnly: ComplexPoco (50 nested items)")]
    [BenchmarkCategory("Doc: ComplexPoco (50 items)")]
    public ComplexPoco ComplexPoco_Large_DataOnly() => CreateComplexPoco(50);

    [Benchmark(Description = "MetadataOnly: ComplexPoco (50 nested items)")]
    [BenchmarkCategory("Doc: ComplexPoco (50 items)")]
    public CrdtMetadata ComplexPoco_Large_MetadataOnly() => metadataManager.Initialize(complexLargeTemplate, timestamp);

    [Benchmark(Description = "FullDoc: ComplexPoco (50 nested items)")]
    [BenchmarkCategory("Doc: ComplexPoco (50 items)")]
    public CrdtDocument<ComplexPoco> ComplexPoco_Large_FullDoc()
    {
        var data = CreateComplexPoco(50);
        var meta = metadataManager.Initialize(data, timestamp);
        return new CrdtDocument<ComplexPoco>(data, meta);
    }

    [Benchmark(Description = "PackedJson: ComplexPoco (50 nested items)")]
    [BenchmarkCategory("Doc: ComplexPoco (50 items)")]
    public PackedCrdtDocument<ComplexPoco> ComplexPoco_Large_PackedJson()
    {
        var data = CreateComplexPoco(50);
        var packed = metadataManager.Pack(complexLargeDocTemplate);
        return new PackedCrdtDocument<ComplexPoco>(data, packed.MetadataBytes);
    }

    [Benchmark(Description = "PackedBrotli: ComplexPoco (50 nested items)")]
    [BenchmarkCategory("Doc: ComplexPoco (50 items)")]
    public PackedCrdtDocument<ComplexPoco> ComplexPoco_Large_PackedBrotli()
    {
        var data = CreateComplexPoco(50);
        var packed = brotliMetadataManager.Pack(complexLargeDocTemplate);
        return new PackedCrdtDocument<ComplexPoco>(data, packed.MetadataBytes);
    }

    // --- 1E. CollectionHeavyPoco Small (5 items each in OR-Set, LWW-Map, LSEQ) ---
    [Benchmark(Baseline = true, Description = "DataOnly: CollectionPoco (3x5 items)")]
    [BenchmarkCategory("Doc: CollectionPoco (5 items)")]
    public CollectionHeavyPoco CollectionPoco_Small_DataOnly() => CreateCollectionPoco(5);

    [Benchmark(Description = "MetadataOnly: CollectionPoco (3x5 items)")]
    [BenchmarkCategory("Doc: CollectionPoco (5 items)")]
    public CrdtMetadata CollectionPoco_Small_MetadataOnly() => metadataManager.Initialize(collSmallTemplate, timestamp);

    [Benchmark(Description = "FullDoc: CollectionPoco (3x5 items)")]
    [BenchmarkCategory("Doc: CollectionPoco (5 items)")]
    public CrdtDocument<CollectionHeavyPoco> CollectionPoco_Small_FullDoc()
    {
        var data = CreateCollectionPoco(5);
        var meta = metadataManager.Initialize(data, timestamp);
        return new CrdtDocument<CollectionHeavyPoco>(data, meta);
    }

    [Benchmark(Description = "PackedJson: CollectionPoco (3x5 items)")]
    [BenchmarkCategory("Doc: CollectionPoco (5 items)")]
    public PackedCrdtDocument<CollectionHeavyPoco> CollectionPoco_Small_PackedJson()
    {
        var data = CreateCollectionPoco(5);
        var packed = metadataManager.Pack(collSmallDocTemplate);
        return new PackedCrdtDocument<CollectionHeavyPoco>(data, packed.MetadataBytes);
    }

    [Benchmark(Description = "PackedBrotli: CollectionPoco (3x5 items)")]
    [BenchmarkCategory("Doc: CollectionPoco (5 items)")]
    public PackedCrdtDocument<CollectionHeavyPoco> CollectionPoco_Small_PackedBrotli()
    {
        var data = CreateCollectionPoco(5);
        var packed = brotliMetadataManager.Pack(collSmallDocTemplate);
        return new PackedCrdtDocument<CollectionHeavyPoco>(data, packed.MetadataBytes);
    }

    // --- 1F. CollectionHeavyPoco Medium (25 items each in OR-Set, LWW-Map, LSEQ) ---
    [Benchmark(Baseline = true, Description = "DataOnly: CollectionPoco (3x25 items)")]
    [BenchmarkCategory("Doc: CollectionPoco (25 items)")]
    public CollectionHeavyPoco CollectionPoco_Medium_DataOnly() => CreateCollectionPoco(25);

    [Benchmark(Description = "MetadataOnly: CollectionPoco (3x25 items)")]
    [BenchmarkCategory("Doc: CollectionPoco (25 items)")]
    public CrdtMetadata CollectionPoco_Medium_MetadataOnly() => metadataManager.Initialize(collMediumTemplate, timestamp);

    [Benchmark(Description = "FullDoc: CollectionPoco (3x25 items)")]
    [BenchmarkCategory("Doc: CollectionPoco (25 items)")]
    public CrdtDocument<CollectionHeavyPoco> CollectionPoco_Medium_FullDoc()
    {
        var data = CreateCollectionPoco(25);
        var meta = metadataManager.Initialize(data, timestamp);
        return new CrdtDocument<CollectionHeavyPoco>(data, meta);
    }

    [Benchmark(Description = "PackedJson: CollectionPoco (3x25 items)")]
    [BenchmarkCategory("Doc: CollectionPoco (25 items)")]
    public PackedCrdtDocument<CollectionHeavyPoco> CollectionPoco_Medium_PackedJson()
    {
        var data = CreateCollectionPoco(25);
        var packed = metadataManager.Pack(collMediumDocTemplate);
        return new PackedCrdtDocument<CollectionHeavyPoco>(data, packed.MetadataBytes);
    }

    [Benchmark(Description = "PackedBrotli: CollectionPoco (3x25 items)")]
    [BenchmarkCategory("Doc: CollectionPoco (25 items)")]
    public PackedCrdtDocument<CollectionHeavyPoco> CollectionPoco_Medium_PackedBrotli()
    {
        var data = CreateCollectionPoco(25);
        var packed = brotliMetadataManager.Pack(collMediumDocTemplate);
        return new PackedCrdtDocument<CollectionHeavyPoco>(data, packed.MetadataBytes);
    }

    // --- 1G. CollectionHeavyPoco Large (100 items each in OR-Set, LWW-Map, LSEQ) ---
    [Benchmark(Baseline = true, Description = "DataOnly: CollectionPoco (3x100 items)")]
    [BenchmarkCategory("Doc: CollectionPoco (100 items)")]
    public CollectionHeavyPoco CollectionPoco_Large_DataOnly() => CreateCollectionPoco(100);

    [Benchmark(Description = "MetadataOnly: CollectionPoco (3x100 items)")]
    [BenchmarkCategory("Doc: CollectionPoco (100 items)")]
    public CrdtMetadata CollectionPoco_Large_MetadataOnly() => metadataManager.Initialize(collLargeTemplate, timestamp);

    [Benchmark(Description = "FullDoc: CollectionPoco (3x100 items)")]
    [BenchmarkCategory("Doc: CollectionPoco (100 items)")]
    public CrdtDocument<CollectionHeavyPoco> CollectionPoco_Large_FullDoc()
    {
        var data = CreateCollectionPoco(100);
        var meta = metadataManager.Initialize(data, timestamp);
        return new CrdtDocument<CollectionHeavyPoco>(data, meta);
    }

    [Benchmark(Description = "PackedJson: CollectionPoco (3x100 items)")]
    [BenchmarkCategory("Doc: CollectionPoco (100 items)")]
    public PackedCrdtDocument<CollectionHeavyPoco> CollectionPoco_Large_PackedJson()
    {
        var data = CreateCollectionPoco(100);
        var packed = metadataManager.Pack(collLargeDocTemplate);
        return new PackedCrdtDocument<CollectionHeavyPoco>(data, packed.MetadataBytes);
    }

    [Benchmark(Description = "PackedBrotli: CollectionPoco (3x100 items)")]
    [BenchmarkCategory("Doc: CollectionPoco (100 items)")]
    public PackedCrdtDocument<CollectionHeavyPoco> CollectionPoco_Large_PackedBrotli()
    {
        var data = CreateCollectionPoco(100);
        var packed = brotliMetadataManager.Pack(collLargeDocTemplate);
        return new PackedCrdtDocument<CollectionHeavyPoco>(data, packed.MetadataBytes);
    }

    // =========================================================================
    // DIMENSION 2: DOTTED VERSION VECTOR (DVV) DIMENSIONS (Replicas x Dots)
    // =========================================================================

    [Benchmark(Baseline = true, Description = "DVV: 3 Replicas, 0 Dots (Linear)")]
    [BenchmarkCategory("DVV Dimensions")]
    public DottedVersionVector Dvv_3Replicas_0Dots() => CreateDvv(3, 0);

    [Benchmark(Description = "DVV: 3 Replicas, 5 Dots/Replica")]
    [BenchmarkCategory("DVV Dimensions")]
    public DottedVersionVector Dvv_3Replicas_5Dots() => CreateDvv(3, 5);

    [Benchmark(Description = "DVV: 10 Replicas, 0 Dots (Linear)")]
    [BenchmarkCategory("DVV Dimensions")]
    public DottedVersionVector Dvv_10Replicas_0Dots() => CreateDvv(10, 0);

    [Benchmark(Description = "DVV: 10 Replicas, 10 Dots/Replica")]
    [BenchmarkCategory("DVV Dimensions")]
    public DottedVersionVector Dvv_10Replicas_10Dots() => CreateDvv(10, 10);

    [Benchmark(Description = "DVV: 100 Replicas, 0 Dots (Linear)")]
    [BenchmarkCategory("DVV Dimensions")]
    public DottedVersionVector Dvv_100Replicas_0Dots() => CreateDvv(100, 0);

    [Benchmark(Description = "DVV: 100 Replicas, 20 Dots/Replica")]
    [BenchmarkCategory("DVV Dimensions")]
    public DottedVersionVector Dvv_100Replicas_20Dots() => CreateDvv(100, 20);

    // =========================================================================
    // DIMENSION 3: SHARD VERSION VECTOR MAP DIMENSIONS (Shards x Replicas x Dots)
    // =========================================================================

    [Benchmark(Baseline = true, Description = "Shards: 1 Shard x 5 Replicas (0 Dots)")]
    [BenchmarkCategory("Shard Map Dimensions")]
    public ShardVersionVectorMap Shards_1Shard_5Replicas_0Dots() => CreateShardMap(1, 5, 0);

    [Benchmark(Description = "Shards: 10 Shards x 5 Replicas (0 Dots)")]
    [BenchmarkCategory("Shard Map Dimensions")]
    public ShardVersionVectorMap Shards_10Shards_5Replicas_0Dots() => CreateShardMap(10, 5, 0);

    [Benchmark(Description = "Shards: 10 Shards x 25 Replicas (5 Dots)")]
    [BenchmarkCategory("Shard Map Dimensions")]
    public ShardVersionVectorMap Shards_10Shards_25Replicas_5Dots() => CreateShardMap(10, 25, 5);

    [Benchmark(Description = "Shards: 100 Shards x 5 Replicas (0 Dots)")]
    [BenchmarkCategory("Shard Map Dimensions")]
    public ShardVersionVectorMap Shards_100Shards_5Replicas_0Dots() => CreateShardMap(100, 5, 0);

    [Benchmark(Description = "Shards: 100 Shards x 50 Replicas (10 Dots)")]
    [BenchmarkCategory("Shard Map Dimensions")]
    public ShardVersionVectorMap Shards_100Shards_50Replicas_10Dots() => CreateShardMap(100, 50, 10);

    // =========================================================================
    // DIMENSION 4: REPLICA SCOPE & SERVICE RESOLUTION OVERHEAD
    // =========================================================================

    [Benchmark(Baseline = true, Description = "ReplicaScope: Bare (ReplicaContext only)")]
    [BenchmarkCategory("Replica Scope Overhead")]
    public string ReplicaScope_Bare()
    {
        using var replicaScope = scopeFactory.CreateScope("replica-bench");
        return replicaScope.ServiceProvider.GetRequiredService<ReplicaContext>().ReplicaId;
    }

    [Benchmark(Description = "ReplicaScope: Core Sync Services (Patcher, Applicator, Merger, MetaMgr)")]
    [BenchmarkCategory("Replica Scope Overhead")]
    public ICrdtPatcher ReplicaScope_CoreSyncServices()
    {
        using var replicaScope = scopeFactory.CreateScope("replica-bench");
        var sp = replicaScope.ServiceProvider;
        var patcher = sp.GetRequiredService<ICrdtPatcher>();
        _ = sp.GetRequiredService<ICrdtApplicator>();
        _ = sp.GetRequiredService<ICrdtMerger>();
        _ = sp.GetRequiredService<ICrdtMetadataManager>();
        return patcher;
    }

    [Benchmark(Description = "ReplicaScope: Async Pipeline (AsyncPatcher, AsyncApplicator, AsyncMerger)")]
    [BenchmarkCategory("Replica Scope Overhead")]
    public IAsyncCrdtPatcher ReplicaScope_AsyncPipeline()
    {
        using var replicaScope = scopeFactory.CreateScope("replica-bench");
        var sp = replicaScope.ServiceProvider;
        var asyncPatcher = sp.GetRequiredService<IAsyncCrdtPatcher>();
        _ = sp.GetRequiredService<IAsyncCrdtApplicator>();
        _ = sp.GetRequiredService<IAsyncCrdtMerger>();
        return asyncPatcher;
    }

    // =========================================================================
    // Helper factory methods
    // =========================================================================

    private static SimplePoco CreateSimplePoco() => new()
    {
        Id = Guid.Empty,
        Name = "Standard User Name",
        Score = 42
    };

    private static WidePoco CreateWidePoco() => new()
    {
        Id = Guid.Empty,
        Prop1 = "Value1",
        Prop2 = "Value2",
        Prop3 = "Value3",
        Prop4 = "Value4",
        Prop5 = "Value5",
        Prop6 = "Value6",
        Prop7 = "Value7",
        Prop8 = "Value8",
        Prop9 = "Value9",
        Prop10 = "Value10",
        Counter1 = 10,
        Counter2 = 20,
        Counter3 = 30,
        Counter4 = 40,
        Counter5 = 50,
        Max1 = 100,
        Max2 = 200,
        Max3 = 300,
        Min1 = 5,
        Min2 = 10
    };

    private ComplexPoco CreateComplexPoco(int tagCount)
    {
        var tags = new List<Tag>(tagCount);
        for (int i = 0; i < tagCount; i++)
        {
            tags.Add(new Tag { Id = i, Value = tagPool[i] });
        }

        return new ComplexPoco
        {
            Id = Guid.Empty,
            Description = "A complex POCO document with nested objects and tag items",
            ViewCount = 1024,
            Details = new Details
            {
                Author = "System Author",
                CreatedAt = DateTime.UnixEpoch,
                IsActive = true
            },
            Tags = tags
        };
    }

    private CollectionHeavyPoco CreateCollectionPoco(int count)
    {
        var tags = new List<string>(count);
        var attrs = new Dictionary<string, string>(count);
        var logs = new List<string>(count);

        for (int i = 0; i < count; i++)
        {
            tags.Add(tagPool[i]);
            attrs[keyPool[i]] = valPool[i];
            logs.Add(valPool[i]);
        }

        return new CollectionHeavyPoco
        {
            Id = Guid.Empty,
            Tags = tags,
            Attributes = attrs,
            Logs = logs
        };
    }

    private DottedVersionVector CreateDvv(int replicas, int dotsPerReplica)
    {
        var dvv = new DottedVersionVector();
        for (int r = 0; r < replicas; r++)
        {
            var repId = replicaIds[r];
            dvv.Versions[repId] = 100;

            for (int d = 0; d < dotsPerReplica; d++)
            {
                // Add non-contiguous versions (102, 104, 106...) to force isolated dot storage
                dvv.Add(repId, 102 + (d * 2));
            }
        }
        return dvv;
    }

    private ShardVersionVectorMap CreateShardMap(int shards, int replicas, int dotsPerReplica)
    {
        var map = new ShardVersionVectorMap();
        for (int s = 0; s < shards; s++)
        {
            map[shardIds[s]] = CreateDvv(replicas, dotsPerReplica);
        }
        return map;
    }
}