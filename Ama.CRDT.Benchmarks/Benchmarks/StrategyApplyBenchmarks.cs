namespace Ama.CRDT.Benchmarks.Benchmarks;

using System.Collections.Generic;
using Ama.CRDT.Benchmarks.Models;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Strategies;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;

[Config(typeof(AntiVirusFriendlyConfig))]
[MemoryDiagnoser]
public class StrategyApplyBenchmarks
{
    private IServiceScope scope;
    private ICrdtPatcher patcher = null!;
    private ICrdtMetadataManager metadataManager = null!;
    private ICrdtStrategyProvider strategyProvider = null!;
    private ICrdtTimestamp timestamp = null!;

    private StrategyPoco basePoco = null!;
    private CrdtDocument<StrategyPoco> fromDoc = default!;

    // We use a batching pattern to benchmark state mutation without triggering
    // BenchmarkDotNet's InvocationCount=1 penalty.
    private const int BatchSize = 100;
    private readonly CrdtDocument<StrategyPoco>[] applyBatch = new CrdtDocument<StrategyPoco>[BatchSize];

    #region Per-strategy operations and components
    private ICrdtStrategy lwwStrategy = null!;
    private IReadOnlyList<CrdtOperation> lwwOps = null!;

    private ICrdtStrategy counterStrategy = null!;
    private IReadOnlyList<CrdtOperation> counterOps = null!;

    private ICrdtStrategy gCounterStrategy = null!;
    private IReadOnlyList<CrdtOperation> gCounterOps = null!;

    private ICrdtStrategy boundedCounterStrategy = null!;
    private IReadOnlyList<CrdtOperation> boundedCounterOps = null!;

    private ICrdtStrategy maxWinsStrategy = null!;
    private IReadOnlyList<CrdtOperation> maxWinsOps = null!;

    private ICrdtStrategy minWinsStrategy = null!;
    private IReadOnlyList<CrdtOperation> minWinsOps = null!;

    private ICrdtStrategy averageStrategy = null!;
    private IReadOnlyList<CrdtOperation> averageOps = null!;

    private ICrdtStrategy gSetStrategy = null!;
    private IReadOnlyList<CrdtOperation> gSetOps = null!;

    private ICrdtStrategy twoPhaseSetStrategy = null!;
    private IReadOnlyList<CrdtOperation> twoPhaseSetOps = null!;

    private ICrdtStrategy lwwSetStrategy = null!;
    private IReadOnlyList<CrdtOperation> lwwSetOps = null!;

    private ICrdtStrategy orSetStrategy = null!;
    private IReadOnlyList<CrdtOperation> orSetOps = null!;

    private ICrdtStrategy arrayLcsStrategy = null!;
    private IReadOnlyList<CrdtOperation> arrayLcsOps = null!;

    private ICrdtStrategy fixedSizeArrayStrategy = null!;
    private IReadOnlyList<CrdtOperation> fixedSizeArrayOps = null!;

    private ICrdtStrategy lseqStrategy = null!;
    private IReadOnlyList<CrdtOperation> lseqOps = null!;

    private ICrdtStrategy voteCounterStrategy = null!;
    private IReadOnlyList<CrdtOperation> voteCounterOps = null!;

    private ICrdtStrategy stateMachineStrategy = null!;
    private IReadOnlyList<CrdtOperation> stateMachineOps = null!;

    private ICrdtStrategy priorityQueueStrategy = null!;
    private IReadOnlyList<CrdtOperation> priorityQueueOps = null!;

    private ICrdtStrategy sortedSetStrategy = null!;
    private IReadOnlyList<CrdtOperation> sortedSetOps = null!;

    private ICrdtStrategy rgaStrategy = null!;
    private IReadOnlyList<CrdtOperation> rgaOps = null!;

    // Additional Map and Graph strategies
    private ICrdtStrategy counterMapStrategy = null!;
    private IReadOnlyList<CrdtOperation> counterMapOps = null!;

    private ICrdtStrategy lwwMapStrategy = null!;
    private IReadOnlyList<CrdtOperation> lwwMapOps = null!;

    private ICrdtStrategy maxWinsMapStrategy = null!;
    private IReadOnlyList<CrdtOperation> maxWinsMapOps = null!;

    private ICrdtStrategy minWinsMapStrategy = null!;
    private IReadOnlyList<CrdtOperation> minWinsMapOps = null!;

    private ICrdtStrategy orMapStrategy = null!;
    private IReadOnlyList<CrdtOperation> orMapOps = null!;

    private ICrdtStrategy graphStrategy = null!;
    private IReadOnlyList<CrdtOperation> graphOps = null!;

    private ICrdtStrategy twoPhaseGraphStrategy = null!;
    private IReadOnlyList<CrdtOperation> twoPhaseGraphOps = null!;

    private ICrdtStrategy replicatedTreeStrategy = null!;
    private IReadOnlyList<CrdtOperation> replicatedTreeOps = null!;
    #endregion

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddCrdt();
        services.AddScoped<MyStateMachine>();
        var serviceProvider = services.BuildServiceProvider();
        var serviceScopeFactory = serviceProvider.GetService<ICrdtScopeFactory>();
        scope = serviceScopeFactory.CreateScope("replica-id");

        patcher = scope.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        metadataManager = scope.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();
        strategyProvider = scope.ServiceProvider.GetRequiredService<ICrdtStrategyProvider>();
        timestamp = scope.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>().Now();

        basePoco = new StrategyPoco();
        var fromMeta = metadataManager.Initialize(basePoco);
        fromDoc = new CrdtDocument<StrategyPoco>(basePoco, fromMeta);

        #region Setup per strategy mutations
        var toPocoForLww = basePoco.Clone();
        toPocoForLww.LwwValue = "updated";
        lwwOps = SetupStrategyAndOps(nameof(StrategyPoco.LwwValue), toPocoForLww, out lwwStrategy);

        var toPocoForCounter = basePoco.Clone();
        toPocoForCounter.Counter = 10;
        counterOps = SetupStrategyAndOps(nameof(StrategyPoco.Counter), toPocoForCounter, out counterStrategy);

        var toPocoForGCounter = basePoco.Clone();
        toPocoForGCounter.GCounter = 10;
        gCounterOps = SetupStrategyAndOps(nameof(StrategyPoco.GCounter), toPocoForGCounter, out gCounterStrategy);

        var toPocoForBoundedCounter = basePoco.Clone();
        toPocoForBoundedCounter.BoundedCounter = 10;
        boundedCounterOps = SetupStrategyAndOps(nameof(StrategyPoco.BoundedCounter), toPocoForBoundedCounter, out boundedCounterStrategy);

        var toPocoForMaxWins = basePoco.Clone();
        toPocoForMaxWins.MaxWins = 100;
        maxWinsOps = SetupStrategyAndOps(nameof(StrategyPoco.MaxWins), toPocoForMaxWins, out maxWinsStrategy);

        var toPocoForMinWins = basePoco.Clone();
        toPocoForMinWins.MinWins = 10;
        minWinsOps = SetupStrategyAndOps(nameof(StrategyPoco.MinWins), toPocoForMinWins, out minWinsStrategy);

        var toPocoForAverageRegister = basePoco.Clone();
        toPocoForAverageRegister.Average = 123.45m;
        averageOps = SetupStrategyAndOps(nameof(StrategyPoco.Average), toPocoForAverageRegister, out averageStrategy);

        var toPocoForGSet = basePoco.Clone();
        toPocoForGSet.GSet.Add("A");
        gSetOps = SetupStrategyAndOps(nameof(StrategyPoco.GSet), toPocoForGSet, out gSetStrategy);

        var toPocoForTwoPhaseSet = basePoco.Clone();
        toPocoForTwoPhaseSet.TwoPhaseSet.Remove("A");
        twoPhaseSetOps = SetupStrategyAndOps(nameof(StrategyPoco.TwoPhaseSet), toPocoForTwoPhaseSet, out twoPhaseSetStrategy);

        var toPocoForLwwSet = basePoco.Clone();
        toPocoForLwwSet.LwwSet.Remove("A");
        toPocoForLwwSet.LwwSet.Add("C");
        lwwSetOps = SetupStrategyAndOps(nameof(StrategyPoco.LwwSet), toPocoForLwwSet, out lwwSetStrategy);

        var toPocoForOrSet = basePoco.Clone();
        toPocoForOrSet.OrSet.Remove("A");
        toPocoForOrSet.OrSet.Add("C");
        orSetOps = SetupStrategyAndOps(nameof(StrategyPoco.OrSet), toPocoForOrSet, out orSetStrategy);

        var toPocoForArrayLcs = basePoco.Clone();
        toPocoForArrayLcs.LcsList.Insert(1, "D");
        toPocoForArrayLcs.LcsList.Remove("C");
        arrayLcsOps = SetupStrategyAndOps(nameof(StrategyPoco.LcsList), toPocoForArrayLcs, out arrayLcsStrategy);

        var toPocoForFixedSizeArray = basePoco.Clone();
        toPocoForFixedSizeArray.FixedArray[1] = "Z";
        fixedSizeArrayOps = SetupStrategyAndOps(nameof(StrategyPoco.FixedArray), toPocoForFixedSizeArray, out fixedSizeArrayStrategy);

        var toPocoForLseq = basePoco.Clone();
        toPocoForLseq.LseqList.Insert(1, "D");
        lseqOps = SetupStrategyAndOps(nameof(StrategyPoco.LseqList), toPocoForLseq, out lseqStrategy);

        var toPocoForVoteCounter = basePoco.Clone();
        toPocoForVoteCounter.Votes["OptionA"].Remove("Voter1");
        toPocoForVoteCounter.Votes["OptionB"].Add("Voter1");
        voteCounterOps = SetupStrategyAndOps(nameof(StrategyPoco.Votes), toPocoForVoteCounter, out voteCounterStrategy);

        var toPocoForStateMachine = basePoco.Clone();
        toPocoForStateMachine.State = "InProgress";
        stateMachineOps = SetupStrategyAndOps(nameof(StrategyPoco.State), toPocoForStateMachine, out stateMachineStrategy);

        var toPocoForPriorityQueue = basePoco.Clone();
        toPocoForPriorityQueue.PrioQueue.Add(new PrioItem { Id = 3, Priority = 5, Value = "C" });
        toPocoForPriorityQueue.PrioQueue[0].Value = "A_updated";
        priorityQueueOps = SetupStrategyAndOps(nameof(StrategyPoco.PrioQueue), toPocoForPriorityQueue, out priorityQueueStrategy);

        var toPocoForSortedSet = basePoco.Clone();
        toPocoForSortedSet.SortedSet.Add(new PrioItem { Id = 3, Priority = 5, Value = "C" });
        toPocoForSortedSet.SortedSet.RemoveAll(p => p.Id == 1);
        sortedSetOps = SetupStrategyAndOps(nameof(StrategyPoco.SortedSet), toPocoForSortedSet, out sortedSetStrategy);

        var toPocoForRga = basePoco.Clone();
        toPocoForRga.RgaList.Insert(1, "D");
        toPocoForRga.RgaList.Remove("C");
        rgaOps = SetupStrategyAndOps(nameof(StrategyPoco.RgaList), toPocoForRga, out rgaStrategy);

        var toPocoForCounterMap = basePoco.Clone();
        toPocoForCounterMap.CounterMap["A"] = 5;
        toPocoForCounterMap.CounterMap.Add("C", 10);
        counterMapOps = SetupStrategyAndOps(nameof(StrategyPoco.CounterMap), toPocoForCounterMap, out counterMapStrategy);

        var toPocoForLwwMap = basePoco.Clone();
        toPocoForLwwMap.LwwMap["A"] = "updated";
        toPocoForLwwMap.LwwMap.Add("C", "new");
        lwwMapOps = SetupStrategyAndOps(nameof(StrategyPoco.LwwMap), toPocoForLwwMap, out lwwMapStrategy);

        var toPocoForMaxWinsMap = basePoco.Clone();
        toPocoForMaxWinsMap.MaxWinsMap["A"] = 50;
        toPocoForMaxWinsMap.MaxWinsMap.Add("C", 100);
        maxWinsMapOps = SetupStrategyAndOps(nameof(StrategyPoco.MaxWinsMap), toPocoForMaxWinsMap, out maxWinsMapStrategy);

        var toPocoForMinWinsMap = basePoco.Clone();
        toPocoForMinWinsMap.MinWinsMap["A"] = 1;
        toPocoForMinWinsMap.MinWinsMap.Add("C", 5);
        minWinsMapOps = SetupStrategyAndOps(nameof(StrategyPoco.MinWinsMap), toPocoForMinWinsMap, out minWinsMapStrategy);

        var toPocoForOrMap = basePoco.Clone();
        toPocoForOrMap.OrMap.Remove("A");
        toPocoForOrMap.OrMap.Add("C", "new");
        orMapOps = SetupStrategyAndOps(nameof(StrategyPoco.OrMap), toPocoForOrMap, out orMapStrategy);

        var toPocoForGraph = basePoco.Clone();
        toPocoForGraph.Graph.Vertices.Add("Vertex1");
        graphOps = SetupStrategyAndOps(nameof(StrategyPoco.Graph), toPocoForGraph, out graphStrategy);

        var toPocoForTwoPhaseGraph = basePoco.Clone();
        toPocoForTwoPhaseGraph.TwoPhaseGraph.Vertices.Add("Vertex2");
        twoPhaseGraphOps = SetupStrategyAndOps(nameof(StrategyPoco.TwoPhaseGraph), toPocoForTwoPhaseGraph, out twoPhaseGraphStrategy);

        var toPocoForReplicatedTree = basePoco.Clone();
        toPocoForReplicatedTree.Tree.Nodes.Add("Node1", new TreeNode { Id = 1 });
        replicatedTreeOps = SetupStrategyAndOps(nameof(StrategyPoco.Tree), toPocoForReplicatedTree, out replicatedTreeStrategy);
        #endregion
    }

    [IterationSetup(Targets = [
        nameof(Apply_Lww), nameof(Apply_Counter), nameof(Apply_GCounter),
        nameof(Apply_BoundedCounter), nameof(Apply_MaxWins), nameof(Apply_MinWins),
        nameof(Apply_AverageRegister), nameof(Apply_GSet), nameof(Apply_TwoPhaseSet),
        nameof(Apply_LwwSet), nameof(Apply_OrSet), nameof(Apply_ArrayLcs),
        nameof(Apply_FixedSizeArray), nameof(Apply_Lseq), nameof(Apply_VoteCounter),
        nameof(Apply_StateMachine), nameof(Apply_PriorityQueue), nameof(Apply_SortedSet),
        nameof(Apply_Rga), nameof(Apply_CounterMap), nameof(Apply_LwwMap), 
        nameof(Apply_MaxWinsMap), nameof(Apply_MinWinsMap), nameof(Apply_OrMap), 
        nameof(Apply_Graph), nameof(Apply_TwoPhaseGraph), nameof(Apply_ReplicatedTree)
    ])]
    public void IterationSetup()
    {
        // Pre-allocate a batch of fresh documents for the inner loop.
        for (int i = 0; i < BatchSize; i++)
        {
            var clonedPoco = basePoco.Clone();
            var clonedMeta = metadataManager.Initialize(clonedPoco);
            applyBatch[i] = new CrdtDocument<StrategyPoco>(clonedPoco, clonedMeta);
        }
    }

    private IReadOnlyList<CrdtOperation> SetupStrategyAndOps(
        string propertyName, 
        StrategyPoco toPoco, 
        out ICrdtStrategy strategy)
    {
        var prop = typeof(StrategyPoco).GetProperty(propertyName)!;
        strategy = strategyProvider.GetStrategy(prop);
        var path = $"$.{char.ToLowerInvariant(propertyName[0])}{propertyName.Substring(1)}";
        
        var fromValue = prop.GetValue(basePoco);
        var toValue = prop.GetValue(toPoco);

        var ops = new List<CrdtOperation>();
        var ctx = new GeneratePatchContext(ops, new List<DifferentiateObjectContext>(), path, prop, fromValue, toValue, basePoco, toPoco, fromDoc.Metadata, timestamp, 0);
        strategy.GeneratePatch(ctx);
        return ops;
    }

    #region ApplyOperation Benchmarks
    [Benchmark(Description = "Strategy.Apply: LWW", OperationsPerInvoke = BatchSize)]
    public void Apply_Lww()
    {
        for (int i = 0; i < BatchSize; i++)
        {
            var doc = applyBatch[i];
            for (int j = 0; j < lwwOps.Count; j++)
                lwwStrategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, lwwOps[j]));
        }
    }

    [Benchmark(Description = "Strategy.Apply: Counter", OperationsPerInvoke = BatchSize)]
    public void Apply_Counter()
    {
        for (int i = 0; i < BatchSize; i++)
        {
            var doc = applyBatch[i];
            for (int j = 0; j < counterOps.Count; j++)
                counterStrategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, counterOps[j]));
        }
    }

    [Benchmark(Description = "Strategy.Apply: GCounter", OperationsPerInvoke = BatchSize)]
    public void Apply_GCounter()
    {
        for (int i = 0; i < BatchSize; i++)
        {
            var doc = applyBatch[i];
            for (int j = 0; j < gCounterOps.Count; j++)
                gCounterStrategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, gCounterOps[j]));
        }
    }

    [Benchmark(Description = "Strategy.Apply: BoundedCounter", OperationsPerInvoke = BatchSize)]
    public void Apply_BoundedCounter()
    {
        for (int i = 0; i < BatchSize; i++)
        {
            var doc = applyBatch[i];
            for (int j = 0; j < boundedCounterOps.Count; j++)
                boundedCounterStrategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, boundedCounterOps[j]));
        }
    }

    [Benchmark(Description = "Strategy.Apply: MaxWins", OperationsPerInvoke = BatchSize)]
    public void Apply_MaxWins()
    {
        for (int i = 0; i < BatchSize; i++)
        {
            var doc = applyBatch[i];
            for (int j = 0; j < maxWinsOps.Count; j++)
                maxWinsStrategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, maxWinsOps[j]));
        }
    }

    [Benchmark(Description = "Strategy.Apply: MinWins", OperationsPerInvoke = BatchSize)]
    public void Apply_MinWins()
    {
        for (int i = 0; i < BatchSize; i++)
        {
            var doc = applyBatch[i];
            for (int j = 0; j < minWinsOps.Count; j++)
                minWinsStrategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, minWinsOps[j]));
        }
    }

    [Benchmark(Description = "Strategy.Apply: AverageRegister", OperationsPerInvoke = BatchSize)]
    public void Apply_AverageRegister()
    {
        for (int i = 0; i < BatchSize; i++)
        {
            var doc = applyBatch[i];
            for (int j = 0; j < averageOps.Count; j++)
                averageStrategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, averageOps[j]));
        }
    }

    [Benchmark(Description = "Strategy.Apply: GSet", OperationsPerInvoke = BatchSize)]
    public void Apply_GSet()
    {
        for (int i = 0; i < BatchSize; i++)
        {
            var doc = applyBatch[i];
            for (int j = 0; j < gSetOps.Count; j++)
                gSetStrategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, gSetOps[j]));
        }
    }

    [Benchmark(Description = "Strategy.Apply: TwoPhaseSet", OperationsPerInvoke = BatchSize)]
    public void Apply_TwoPhaseSet()
    {
        for (int i = 0; i < BatchSize; i++)
        {
            var doc = applyBatch[i];
            for (int j = 0; j < twoPhaseSetOps.Count; j++)
                twoPhaseSetStrategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, twoPhaseSetOps[j]));
        }
    }

    [Benchmark(Description = "Strategy.Apply: LwwSet", OperationsPerInvoke = BatchSize)]
    public void Apply_LwwSet()
    {
        for (int i = 0; i < BatchSize; i++)
        {
            var doc = applyBatch[i];
            for (int j = 0; j < lwwSetOps.Count; j++)
                lwwSetStrategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, lwwSetOps[j]));
        }
    }

    [Benchmark(Description = "Strategy.Apply: OrSet", OperationsPerInvoke = BatchSize)]
    public void Apply_OrSet()
    {
        for (int i = 0; i < BatchSize; i++)
        {
            var doc = applyBatch[i];
            for (int j = 0; j < orSetOps.Count; j++)
                orSetStrategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, orSetOps[j]));
        }
    }

    [Benchmark(Description = "Strategy.Apply: ArrayLcs", OperationsPerInvoke = BatchSize)]
    public void Apply_ArrayLcs()
    {
        for (int i = 0; i < BatchSize; i++)
        {
            var doc = applyBatch[i];
            for (int j = 0; j < arrayLcsOps.Count; j++)
                arrayLcsStrategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, arrayLcsOps[j]));
        }
    }

    [Benchmark(Description = "Strategy.Apply: FixedSizeArray", OperationsPerInvoke = BatchSize)]
    public void Apply_FixedSizeArray()
    {
        for (int i = 0; i < BatchSize; i++)
        {
            var doc = applyBatch[i];
            for (int j = 0; j < fixedSizeArrayOps.Count; j++)
                fixedSizeArrayStrategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, fixedSizeArrayOps[j]));
        }
    }

    [Benchmark(Description = "Strategy.Apply: Lseq", OperationsPerInvoke = BatchSize)]
    public void Apply_Lseq()
    {
        for (int i = 0; i < BatchSize; i++)
        {
            var doc = applyBatch[i];
            for (int j = 0; j < lseqOps.Count; j++)
                lseqStrategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, lseqOps[j]));
        }
    }

    [Benchmark(Description = "Strategy.Apply: VoteCounter", OperationsPerInvoke = BatchSize)]
    public void Apply_VoteCounter()
    {
        for (int i = 0; i < BatchSize; i++)
        {
            var doc = applyBatch[i];
            for (int j = 0; j < voteCounterOps.Count; j++)
                voteCounterStrategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, voteCounterOps[j]));
        }
    }

    [Benchmark(Description = "Strategy.Apply: StateMachine", OperationsPerInvoke = BatchSize)]
    public void Apply_StateMachine()
    {
        for (int i = 0; i < BatchSize; i++)
        {
            var doc = applyBatch[i];
            for (int j = 0; j < stateMachineOps.Count; j++)
                stateMachineStrategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, stateMachineOps[j]));
        }
    }

    [Benchmark(Description = "Strategy.Apply: PriorityQueue", OperationsPerInvoke = BatchSize)]
    public void Apply_PriorityQueue()
    {
        for (int i = 0; i < BatchSize; i++)
        {
            var doc = applyBatch[i];
            for (int j = 0; j < priorityQueueOps.Count; j++)
                priorityQueueStrategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, priorityQueueOps[j]));
        }
    }

    [Benchmark(Description = "Strategy.Apply: SortedSet", OperationsPerInvoke = BatchSize)]
    public void Apply_SortedSet()
    {
        for (int i = 0; i < BatchSize; i++)
        {
            var doc = applyBatch[i];
            for (int j = 0; j < sortedSetOps.Count; j++)
                sortedSetStrategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, sortedSetOps[j]));
        }
    }

    [Benchmark(Description = "Strategy.Apply: RGA", OperationsPerInvoke = BatchSize)]
    public void Apply_Rga()
    {
        for (int i = 0; i < BatchSize; i++)
        {
            var doc = applyBatch[i];
            for (int j = 0; j < rgaOps.Count; j++)
                rgaStrategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, rgaOps[j]));
        }
    }

    [Benchmark(Description = "Strategy.Apply: CounterMap", OperationsPerInvoke = BatchSize)]
    public void Apply_CounterMap()
    {
        for (int i = 0; i < BatchSize; i++)
        {
            var doc = applyBatch[i];
            for (int j = 0; j < counterMapOps.Count; j++)
                counterMapStrategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, counterMapOps[j]));
        }
    }

    [Benchmark(Description = "Strategy.Apply: LwwMap", OperationsPerInvoke = BatchSize)]
    public void Apply_LwwMap()
    {
        for (int i = 0; i < BatchSize; i++)
        {
            var doc = applyBatch[i];
            for (int j = 0; j < lwwMapOps.Count; j++)
                lwwMapStrategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, lwwMapOps[j]));
        }
    }

    [Benchmark(Description = "Strategy.Apply: MaxWinsMap", OperationsPerInvoke = BatchSize)]
    public void Apply_MaxWinsMap()
    {
        for (int i = 0; i < BatchSize; i++)
        {
            var doc = applyBatch[i];
            for (int j = 0; j < maxWinsMapOps.Count; j++)
                maxWinsMapStrategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, maxWinsMapOps[j]));
        }
    }

    [Benchmark(Description = "Strategy.Apply: MinWinsMap", OperationsPerInvoke = BatchSize)]
    public void Apply_MinWinsMap()
    {
        for (int i = 0; i < BatchSize; i++)
        {
            var doc = applyBatch[i];
            for (int j = 0; j < minWinsMapOps.Count; j++)
                minWinsMapStrategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, minWinsMapOps[j]));
        }
    }

    [Benchmark(Description = "Strategy.Apply: OrMap", OperationsPerInvoke = BatchSize)]
    public void Apply_OrMap()
    {
        for (int i = 0; i < BatchSize; i++)
        {
            var doc = applyBatch[i];
            for (int j = 0; j < orMapOps.Count; j++)
                orMapStrategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, orMapOps[j]));
        }
    }

    [Benchmark(Description = "Strategy.Apply: Graph", OperationsPerInvoke = BatchSize)]
    public void Apply_Graph()
    {
        for (int i = 0; i < BatchSize; i++)
        {
            var doc = applyBatch[i];
            for (int j = 0; j < graphOps.Count; j++)
                graphStrategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, graphOps[j]));
        }
    }

    [Benchmark(Description = "Strategy.Apply: TwoPhaseGraph", OperationsPerInvoke = BatchSize)]
    public void Apply_TwoPhaseGraph()
    {
        for (int i = 0; i < BatchSize; i++)
        {
            var doc = applyBatch[i];
            for (int j = 0; j < twoPhaseGraphOps.Count; j++)
                twoPhaseGraphStrategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, twoPhaseGraphOps[j]));
        }
    }

    [Benchmark(Description = "Strategy.Apply: ReplicatedTree", OperationsPerInvoke = BatchSize)]
    public void Apply_ReplicatedTree()
    {
        for (int i = 0; i < BatchSize; i++)
        {
            var doc = applyBatch[i];
            for (int j = 0; j < replicatedTreeOps.Count; j++)
                replicatedTreeStrategy.ApplyOperation(new ApplyOperationContext(doc.Data, doc.Metadata, replicatedTreeOps[j]));
        }
    }
    #endregion
}