namespace Ama.CRDT.Benchmarks.Benchmarks;

using System.Collections.Generic;
using System.Reflection;
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
public class StrategyGenerateBenchmarks
{
    private IServiceScope scope;
    private ICrdtPatcher patcher = null!;
    private ICrdtMetadataManager metadataManager = null!;
    private ICrdtStrategyProvider strategyProvider = null!;
    private ICrdtTimestamp timestamp = null!;

    private StrategyPoco basePoco = null!;
    private CrdtDocument<StrategyPoco> fromDoc = default!;

    // Reusable list for GeneratePatch benchmarking to avoid list allocation overhead
    private readonly List<CrdtOperation> opsBuffer = new(10);

    #region Per-strategy context fields
    private StrategyPoco toPocoForLww = default!;
    private PropertyInfo lwwProp = null!;
    private ICrdtStrategy lwwStrategy = null!;
    private string lwwPath = null!;

    private StrategyPoco toPocoForFww = default!;
    private PropertyInfo fwwProp = null!;
    private ICrdtStrategy fwwStrategy = null!;
    private string fwwPath = null!;

    private StrategyPoco toPocoForCounter = default!;
    private PropertyInfo counterProp = null!;
    private ICrdtStrategy counterStrategy = null!;
    private string counterPath = null!;

    private StrategyPoco toPocoForGCounter = default!;
    private PropertyInfo gCounterProp = null!;
    private ICrdtStrategy gCounterStrategy = null!;
    private string gCounterPath = null!;

    private StrategyPoco toPocoForBoundedCounter = default!;
    private PropertyInfo boundedCounterProp = null!;
    private ICrdtStrategy boundedCounterStrategy = null!;
    private string boundedCounterPath = null!;

    private StrategyPoco toPocoForMaxWins = default!;
    private PropertyInfo maxWinsProp = null!;
    private ICrdtStrategy maxWinsStrategy = null!;
    private string maxWinsPath = null!;

    private StrategyPoco toPocoForMinWins = default!;
    private PropertyInfo minWinsProp = null!;
    private ICrdtStrategy minWinsStrategy = null!;
    private string minWinsPath = null!;

    private StrategyPoco toPocoForAverageRegister = default!;
    private PropertyInfo averageProp = null!;
    private ICrdtStrategy averageStrategy = null!;
    private string averagePath = null!;

    private StrategyPoco toPocoForGSet = default!;
    private PropertyInfo gSetProp = null!;
    private ICrdtStrategy gSetStrategy = null!;
    private string gSetPath = null!;

    private StrategyPoco toPocoForTwoPhaseSet = default!;
    private PropertyInfo twoPhaseSetProp = null!;
    private ICrdtStrategy twoPhaseSetStrategy = null!;
    private string twoPhaseSetPath = null!;

    private StrategyPoco toPocoForLwwSet = default!;
    private PropertyInfo lwwSetProp = null!;
    private ICrdtStrategy lwwSetStrategy = null!;
    private string lwwSetPath = null!;

    private StrategyPoco toPocoForFwwSet = default!;
    private PropertyInfo fwwSetProp = null!;
    private ICrdtStrategy fwwSetStrategy = null!;
    private string fwwSetPath = null!;

    private StrategyPoco toPocoForOrSet = default!;
    private PropertyInfo orSetProp = null!;
    private ICrdtStrategy orSetStrategy = null!;
    private string orSetPath = null!;

    private StrategyPoco toPocoForArrayLcs = default!;
    private PropertyInfo arrayLcsProp = null!;
    private ICrdtStrategy arrayLcsStrategy = null!;
    private string arrayLcsPath = null!;

    private StrategyPoco toPocoForFixedSizeArray = default!;
    private PropertyInfo fixedSizeArrayProp = null!;
    private ICrdtStrategy fixedSizeArrayStrategy = null!;
    private string fixedSizeArrayPath = null!;

    private StrategyPoco toPocoForLseq = default!;
    private PropertyInfo lseqProp = null!;
    private ICrdtStrategy lseqStrategy = null!;
    private string lseqPath = null!;

    private StrategyPoco toPocoForVoteCounter = default!;
    private PropertyInfo voteCounterProp = null!;
    private ICrdtStrategy voteCounterStrategy = null!;
    private string voteCounterPath = null!;

    private StrategyPoco toPocoForStateMachine = default!;
    private PropertyInfo stateMachineProp = null!;
    private ICrdtStrategy stateMachineStrategy = null!;
    private string stateMachinePath = null!;

    private StrategyPoco toPocoForPriorityQueue = default!;
    private PropertyInfo priorityQueueProp = null!;
    private ICrdtStrategy priorityQueueStrategy = null!;
    private string priorityQueuePath = null!;

    private StrategyPoco toPocoForSortedSet = default!;
    private PropertyInfo sortedSetProp = null!;
    private ICrdtStrategy sortedSetStrategy = null!;
    private string sortedSetPath = null!;

    private StrategyPoco toPocoForRga = default!;
    private PropertyInfo rgaProp = null!;
    private ICrdtStrategy rgaStrategy = null!;
    private string rgaPath = null!;

    // Missing strategies
    private StrategyPoco toPocoForCounterMap = default!;
    private PropertyInfo counterMapProp = null!;
    private ICrdtStrategy counterMapStrategy = null!;
    private string counterMapPath = null!;

    private StrategyPoco toPocoForLwwMap = default!;
    private PropertyInfo lwwMapProp = null!;
    private ICrdtStrategy lwwMapStrategy = null!;
    private string lwwMapPath = null!;

    private StrategyPoco toPocoForFwwMap = default!;
    private PropertyInfo fwwMapProp = null!;
    private ICrdtStrategy fwwMapStrategy = null!;
    private string fwwMapPath = null!;

    private StrategyPoco toPocoForMaxWinsMap = default!;
    private PropertyInfo maxWinsMapProp = null!;
    private ICrdtStrategy maxWinsMapStrategy = null!;
    private string maxWinsMapPath = null!;

    private StrategyPoco toPocoForMinWinsMap = default!;
    private PropertyInfo minWinsMapProp = null!;
    private ICrdtStrategy minWinsMapStrategy = null!;
    private string minWinsMapPath = null!;

    private StrategyPoco toPocoForOrMap = default!;
    private PropertyInfo orMapProp = null!;
    private ICrdtStrategy orMapStrategy = null!;
    private string orMapPath = null!;

    private StrategyPoco toPocoForGraph = default!;
    private PropertyInfo graphProp = null!;
    private ICrdtStrategy graphStrategy = null!;
    private string graphPath = null!;

    private StrategyPoco toPocoForTwoPhaseGraph = default!;
    private PropertyInfo twoPhaseGraphProp = null!;
    private ICrdtStrategy twoPhaseGraphStrategy = null!;
    private string twoPhaseGraphPath = null!;

    private StrategyPoco toPocoForReplicatedTree = default!;
    private PropertyInfo replicatedTreeProp = null!;
    private ICrdtStrategy replicatedTreeStrategy = null!;
    private string replicatedTreePath = null!;

    private StrategyPoco toPocoForEpochBound = default!;
    private PropertyInfo epochBoundProp = null!;
    private ICrdtStrategy epochBoundStrategy = null!;
    private string epochBoundPath = null!;

    private StrategyPoco toPocoForApprovalQuorum = default!;
    private PropertyInfo approvalQuorumProp = null!;
    private ICrdtStrategy approvalQuorumStrategy = null!;
    private string approvalQuorumPath = null!;
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

        #region Setup per strategy properties
        toPocoForLww = basePoco.Clone();
        toPocoForLww.LwwValue = "updated";
        SetupStrategyContext(nameof(StrategyPoco.LwwValue), out lwwProp, out lwwStrategy, out lwwPath);

        toPocoForFww = basePoco.Clone();
        toPocoForFww.FwwValue = "updated";
        SetupStrategyContext(nameof(StrategyPoco.FwwValue), out fwwProp, out fwwStrategy, out fwwPath);

        toPocoForCounter = basePoco.Clone();
        toPocoForCounter.Counter = 10;
        SetupStrategyContext(nameof(StrategyPoco.Counter), out counterProp, out counterStrategy, out counterPath);

        toPocoForGCounter = basePoco.Clone();
        toPocoForGCounter.GCounter = 10;
        SetupStrategyContext(nameof(StrategyPoco.GCounter), out gCounterProp, out gCounterStrategy, out gCounterPath);

        toPocoForBoundedCounter = basePoco.Clone();
        toPocoForBoundedCounter.BoundedCounter = 10;
        SetupStrategyContext(nameof(StrategyPoco.BoundedCounter), out boundedCounterProp, out boundedCounterStrategy, out boundedCounterPath);

        toPocoForMaxWins = basePoco.Clone();
        toPocoForMaxWins.MaxWins = 100;
        SetupStrategyContext(nameof(StrategyPoco.MaxWins), out maxWinsProp, out maxWinsStrategy, out maxWinsPath);

        toPocoForMinWins = basePoco.Clone();
        toPocoForMinWins.MinWins = 10;
        SetupStrategyContext(nameof(StrategyPoco.MinWins), out minWinsProp, out minWinsStrategy, out minWinsPath);

        toPocoForAverageRegister = basePoco.Clone();
        toPocoForAverageRegister.Average = 123.45m;
        SetupStrategyContext(nameof(StrategyPoco.Average), out averageProp, out averageStrategy, out averagePath);

        toPocoForGSet = basePoco.Clone();
        toPocoForGSet.GSet.Add("A");
        SetupStrategyContext(nameof(StrategyPoco.GSet), out gSetProp, out gSetStrategy, out gSetPath);

        toPocoForTwoPhaseSet = basePoco.Clone();
        toPocoForTwoPhaseSet.TwoPhaseSet.Remove("A");
        SetupStrategyContext(nameof(StrategyPoco.TwoPhaseSet), out twoPhaseSetProp, out twoPhaseSetStrategy, out twoPhaseSetPath);

        toPocoForLwwSet = basePoco.Clone();
        toPocoForLwwSet.LwwSet.Remove("A");
        toPocoForLwwSet.LwwSet.Add("C");
        SetupStrategyContext(nameof(StrategyPoco.LwwSet), out lwwSetProp, out lwwSetStrategy, out lwwSetPath);

        toPocoForFwwSet = basePoco.Clone();
        toPocoForFwwSet.FwwSet.Remove("A");
        toPocoForFwwSet.FwwSet.Add("C");
        SetupStrategyContext(nameof(StrategyPoco.FwwSet), out fwwSetProp, out fwwSetStrategy, out fwwSetPath);

        toPocoForOrSet = basePoco.Clone();
        toPocoForOrSet.OrSet.Remove("A");
        toPocoForOrSet.OrSet.Add("C");
        SetupStrategyContext(nameof(StrategyPoco.OrSet), out orSetProp, out orSetStrategy, out orSetPath);

        toPocoForArrayLcs = basePoco.Clone();
        toPocoForArrayLcs.LcsList.Insert(1, "D");
        toPocoForArrayLcs.LcsList.Remove("C");
        SetupStrategyContext(nameof(StrategyPoco.LcsList), out arrayLcsProp, out arrayLcsStrategy, out arrayLcsPath);

        toPocoForFixedSizeArray = basePoco.Clone();
        toPocoForFixedSizeArray.FixedArray[1] = "Z";
        SetupStrategyContext(nameof(StrategyPoco.FixedArray), out fixedSizeArrayProp, out fixedSizeArrayStrategy, out fixedSizeArrayPath);

        toPocoForLseq = basePoco.Clone();
        toPocoForLseq.LseqList.Insert(1, "D");
        SetupStrategyContext(nameof(StrategyPoco.LseqList), out lseqProp, out lseqStrategy, out lseqPath);

        toPocoForVoteCounter = basePoco.Clone();
        toPocoForVoteCounter.Votes["OptionA"].Remove("Voter1");
        toPocoForVoteCounter.Votes["OptionB"].Add("Voter1");
        SetupStrategyContext(nameof(StrategyPoco.Votes), out voteCounterProp, out voteCounterStrategy, out voteCounterPath);

        toPocoForStateMachine = basePoco.Clone();
        toPocoForStateMachine.State = "InProgress";
        SetupStrategyContext(nameof(StrategyPoco.State), out stateMachineProp, out stateMachineStrategy, out stateMachinePath);

        toPocoForPriorityQueue = basePoco.Clone();
        toPocoForPriorityQueue.PrioQueue.Add(new PrioItem { Id = 3, Priority = 5, Value = "C" });
        toPocoForPriorityQueue.PrioQueue[0].Value = "A_updated";
        SetupStrategyContext(nameof(StrategyPoco.PrioQueue), out priorityQueueProp, out priorityQueueStrategy, out priorityQueuePath);

        toPocoForSortedSet = basePoco.Clone();
        toPocoForSortedSet.SortedSet.Add(new PrioItem { Id = 3, Priority = 5, Value = "C" });
        toPocoForSortedSet.SortedSet.RemoveAll(p => p.Id == 1);
        SetupStrategyContext(nameof(StrategyPoco.SortedSet), out sortedSetProp, out sortedSetStrategy, out sortedSetPath);

        toPocoForRga = basePoco.Clone();
        toPocoForRga.RgaList.Insert(1, "D");
        toPocoForRga.RgaList.Remove("C");
        SetupStrategyContext(nameof(StrategyPoco.RgaList), out rgaProp, out rgaStrategy, out rgaPath);

        toPocoForCounterMap = basePoco.Clone();
        toPocoForCounterMap.CounterMap["key1"] = 5;
        SetupStrategyContext(nameof(StrategyPoco.CounterMap), out counterMapProp, out counterMapStrategy, out counterMapPath);

        toPocoForLwwMap = basePoco.Clone();
        toPocoForLwwMap.LwwMap["key1"] = "newVal";
        SetupStrategyContext(nameof(StrategyPoco.LwwMap), out lwwMapProp, out lwwMapStrategy, out lwwMapPath);

        toPocoForFwwMap = basePoco.Clone();
        toPocoForFwwMap.FwwMap["A"] = "newVal";
        SetupStrategyContext(nameof(StrategyPoco.FwwMap), out fwwMapProp, out fwwMapStrategy, out fwwMapPath);

        toPocoForMaxWinsMap = basePoco.Clone();
        toPocoForMaxWinsMap.MaxWinsMap["key1"] = 100;
        SetupStrategyContext(nameof(StrategyPoco.MaxWinsMap), out maxWinsMapProp, out maxWinsMapStrategy, out maxWinsMapPath);

        toPocoForMinWinsMap = basePoco.Clone();
        toPocoForMinWinsMap.MinWinsMap["key1"] = -100;
        SetupStrategyContext(nameof(StrategyPoco.MinWinsMap), out minWinsMapProp, out minWinsMapStrategy, out minWinsMapPath);

        toPocoForOrMap = basePoco.Clone();
        toPocoForOrMap.OrMap["key1"] = "updated";
        SetupStrategyContext(nameof(StrategyPoco.OrMap), out orMapProp, out orMapStrategy, out orMapPath);

        toPocoForGraph = basePoco.Clone();
        toPocoForGraph.Graph.Vertices.Add("V3");
        toPocoForGraph.Graph.Edges.Add(new Edge("V1", "V3", null));
        SetupStrategyContext(nameof(StrategyPoco.Graph), out graphProp, out graphStrategy, out graphPath);

        toPocoForTwoPhaseGraph = basePoco.Clone();
        toPocoForTwoPhaseGraph.TwoPhaseGraph.Vertices.Add("V3");
        SetupStrategyContext(nameof(StrategyPoco.TwoPhaseGraph), out twoPhaseGraphProp, out twoPhaseGraphStrategy, out twoPhaseGraphPath);

        toPocoForReplicatedTree = basePoco.Clone();
        toPocoForReplicatedTree.Tree.Nodes.Add("Node3", new TreeNode { Id = "Node1"});
        SetupStrategyContext(nameof(StrategyPoco.Tree), out replicatedTreeProp, out replicatedTreeStrategy, out replicatedTreePath);

        toPocoForEpochBound = basePoco.Clone();
        toPocoForEpochBound.EpochBoundValue = "updated";
        SetupStrategyContext(nameof(StrategyPoco.EpochBoundValue), out epochBoundProp, out epochBoundStrategy, out epochBoundPath);

        toPocoForApprovalQuorum = basePoco.Clone();
        toPocoForApprovalQuorum.QuorumBoundValue = "updated";
        SetupStrategyContext(nameof(StrategyPoco.QuorumBoundValue), out approvalQuorumProp, out approvalQuorumStrategy, out approvalQuorumPath);
        #endregion
    }

    private void SetupStrategyContext(
        string propertyName, 
        out PropertyInfo prop, 
        out ICrdtStrategy strategy, 
        out string path)
    {
        prop = typeof(StrategyPoco).GetProperty(propertyName)!;
        strategy = strategyProvider.GetStrategy(prop);
        path = $"$.{char.ToLowerInvariant(propertyName[0])}{propertyName.Substring(1)}";
    }

    #region GeneratePatch Benchmarks
    [Benchmark(Description = "Strategy.Generate: LWW")]
    public void Generate_Lww()
    {
        opsBuffer.Clear();
        var ctx = new GeneratePatchContext(opsBuffer, new List<DifferentiateObjectContext>(), lwwPath, lwwProp, basePoco.LwwValue, toPocoForLww.LwwValue, basePoco, toPocoForLww, fromDoc.Metadata, timestamp, 0);
        lwwStrategy.GeneratePatch(ctx);
    }
    
    [Benchmark(Description = "Strategy.Generate: FWW")]
    public void Generate_Fww()
    {
        opsBuffer.Clear();
        var ctx = new GeneratePatchContext(opsBuffer, new List<DifferentiateObjectContext>(), fwwPath, fwwProp, basePoco.FwwValue, toPocoForFww.FwwValue, basePoco, toPocoForFww, fromDoc.Metadata, timestamp, 0);
        fwwStrategy.GeneratePatch(ctx);
    }

    [Benchmark(Description = "Strategy.Generate: Counter")]
    public void Generate_Counter()
    {
        opsBuffer.Clear();
        var ctx = new GeneratePatchContext(opsBuffer, new List<DifferentiateObjectContext>(), counterPath, counterProp, basePoco.Counter, toPocoForCounter.Counter, basePoco, toPocoForCounter, fromDoc.Metadata, timestamp, 0);
        counterStrategy.GeneratePatch(ctx);
    }

    [Benchmark(Description = "Strategy.Generate: GCounter")]
    public void Generate_GCounter()
    {
        opsBuffer.Clear();
        var ctx = new GeneratePatchContext(opsBuffer, new List<DifferentiateObjectContext>(), gCounterPath, gCounterProp, basePoco.GCounter, toPocoForGCounter.GCounter, basePoco, toPocoForGCounter, fromDoc.Metadata, timestamp, 0);
        gCounterStrategy.GeneratePatch(ctx);
    }

    [Benchmark(Description = "Strategy.Generate: BoundedCounter")]
    public void Generate_BoundedCounter()
    {
        opsBuffer.Clear();
        var ctx = new GeneratePatchContext(opsBuffer, new List<DifferentiateObjectContext>(), boundedCounterPath, boundedCounterProp, basePoco.BoundedCounter, toPocoForBoundedCounter.BoundedCounter, basePoco, toPocoForBoundedCounter, fromDoc.Metadata, timestamp, 0);
        boundedCounterStrategy.GeneratePatch(ctx);
    }

    [Benchmark(Description = "Strategy.Generate: MaxWins")]
    public void Generate_MaxWins()
    {
        opsBuffer.Clear();
        var ctx = new GeneratePatchContext(opsBuffer, new List<DifferentiateObjectContext>(), maxWinsPath, maxWinsProp, basePoco.MaxWins, toPocoForMaxWins.MaxWins, basePoco, toPocoForMaxWins, fromDoc.Metadata, timestamp, 0);
        maxWinsStrategy.GeneratePatch(ctx);
    }

    [Benchmark(Description = "Strategy.Generate: MinWins")]
    public void Generate_MinWins()
    {
        opsBuffer.Clear();
        var ctx = new GeneratePatchContext(opsBuffer, new List<DifferentiateObjectContext>(), minWinsPath, minWinsProp, basePoco.MinWins, toPocoForMinWins.MinWins, basePoco, toPocoForMinWins, fromDoc.Metadata, timestamp, 0);
        minWinsStrategy.GeneratePatch(ctx);
    }

    [Benchmark(Description = "Strategy.Generate: AverageRegister")]
    public void Generate_AverageRegister()
    {
        opsBuffer.Clear();
        var ctx = new GeneratePatchContext(opsBuffer, new List<DifferentiateObjectContext>(), averagePath, averageProp, basePoco.Average, toPocoForAverageRegister.Average, basePoco, toPocoForAverageRegister, fromDoc.Metadata, timestamp, 0);
        averageStrategy.GeneratePatch(ctx);
    }

    [Benchmark(Description = "Strategy.Generate: GSet")]
    public void Generate_GSet()
    {
        opsBuffer.Clear();
        var ctx = new GeneratePatchContext(opsBuffer, new List<DifferentiateObjectContext>(), gSetPath, gSetProp, basePoco.GSet, toPocoForGSet.GSet, basePoco, toPocoForGSet, fromDoc.Metadata, timestamp, 0);
        gSetStrategy.GeneratePatch(ctx);
    }

    [Benchmark(Description = "Strategy.Generate: TwoPhaseSet")]
    public void Generate_TwoPhaseSet()
    {
        opsBuffer.Clear();
        var ctx = new GeneratePatchContext(opsBuffer, new List<DifferentiateObjectContext>(), twoPhaseSetPath, twoPhaseSetProp, basePoco.TwoPhaseSet, toPocoForTwoPhaseSet.TwoPhaseSet, basePoco, toPocoForTwoPhaseSet, fromDoc.Metadata, timestamp, 0);
        twoPhaseSetStrategy.GeneratePatch(ctx);
    }

    [Benchmark(Description = "Strategy.Generate: LwwSet")]
    public void Generate_LwwSet()
    {
        opsBuffer.Clear();
        var ctx = new GeneratePatchContext(opsBuffer, new List<DifferentiateObjectContext>(), lwwSetPath, lwwSetProp, basePoco.LwwSet, toPocoForLwwSet.LwwSet, basePoco, toPocoForLwwSet, fromDoc.Metadata, timestamp, 0);
        lwwSetStrategy.GeneratePatch(ctx);
    }

    [Benchmark(Description = "Strategy.Generate: FwwSet")]
    public void Generate_FwwSet()
    {
        opsBuffer.Clear();
        var ctx = new GeneratePatchContext(opsBuffer, new List<DifferentiateObjectContext>(), fwwSetPath, fwwSetProp, basePoco.FwwSet, toPocoForFwwSet.FwwSet, basePoco, toPocoForFwwSet, fromDoc.Metadata, timestamp, 0);
        fwwSetStrategy.GeneratePatch(ctx);
    }

    [Benchmark(Description = "Strategy.Generate: OrSet")]
    public void Generate_OrSet()
    {
        opsBuffer.Clear();
        var ctx = new GeneratePatchContext(opsBuffer, new List<DifferentiateObjectContext>(), orSetPath, orSetProp, basePoco.OrSet, toPocoForOrSet.OrSet, basePoco, toPocoForOrSet, fromDoc.Metadata, timestamp, 0);
        orSetStrategy.GeneratePatch(ctx);
    }

    [Benchmark(Description = "Strategy.Generate: ArrayLcs")]
    public void Generate_ArrayLcs()
    {
        opsBuffer.Clear();
        var ctx = new GeneratePatchContext(opsBuffer, new List<DifferentiateObjectContext>(), arrayLcsPath, arrayLcsProp, basePoco.LcsList, toPocoForArrayLcs.LcsList, basePoco, toPocoForArrayLcs, fromDoc.Metadata, timestamp, 0);
        arrayLcsStrategy.GeneratePatch(ctx);
    }

    [Benchmark(Description = "Strategy.Generate: FixedSizeArray")]
    public void Generate_FixedSizeArray()
    {
        opsBuffer.Clear();
        var ctx = new GeneratePatchContext(opsBuffer, new List<DifferentiateObjectContext>(), fixedSizeArrayPath, fixedSizeArrayProp, basePoco.FixedArray, toPocoForFixedSizeArray.FixedArray, basePoco, toPocoForFixedSizeArray, fromDoc.Metadata, timestamp, 0);
        fixedSizeArrayStrategy.GeneratePatch(ctx);
    }

    [Benchmark(Description = "Strategy.Generate: Lseq")]
    public void Generate_Lseq()
    {
        opsBuffer.Clear();
        var ctx = new GeneratePatchContext(opsBuffer, new List<DifferentiateObjectContext>(), lseqPath, lseqProp, basePoco.LseqList, toPocoForLseq.LseqList, basePoco, toPocoForLseq, fromDoc.Metadata, timestamp, 0);
        lseqStrategy.GeneratePatch(ctx);
    }

    [Benchmark(Description = "Strategy.Generate: VoteCounter")]
    public void Generate_VoteCounter()
    {
        opsBuffer.Clear();
        var ctx = new GeneratePatchContext(opsBuffer, new List<DifferentiateObjectContext>(), voteCounterPath, voteCounterProp, basePoco.Votes, toPocoForVoteCounter.Votes, basePoco, toPocoForVoteCounter, fromDoc.Metadata, timestamp, 0);
        voteCounterStrategy.GeneratePatch(ctx);
    }

    [Benchmark(Description = "Strategy.Generate: StateMachine")]
    public void Generate_StateMachine()
    {
        opsBuffer.Clear();
        var ctx = new GeneratePatchContext(opsBuffer, new List<DifferentiateObjectContext>(), stateMachinePath, stateMachineProp, basePoco.State, toPocoForStateMachine.State, basePoco, toPocoForStateMachine, fromDoc.Metadata, timestamp, 0);
        stateMachineStrategy.GeneratePatch(ctx);
    }

    [Benchmark(Description = "Strategy.Generate: PriorityQueue")]
    public void Generate_PriorityQueue()
    {
        opsBuffer.Clear();
        var ctx = new GeneratePatchContext(opsBuffer, new List<DifferentiateObjectContext>(), priorityQueuePath, priorityQueueProp, basePoco.PrioQueue, toPocoForPriorityQueue.PrioQueue, basePoco, toPocoForPriorityQueue, fromDoc.Metadata, timestamp, 0);
        priorityQueueStrategy.GeneratePatch(ctx);
    }

    [Benchmark(Description = "Strategy.Generate: SortedSet")]
    public void Generate_SortedSet()
    {
        opsBuffer.Clear();
        var ctx = new GeneratePatchContext(opsBuffer, new List<DifferentiateObjectContext>(), sortedSetPath, sortedSetProp, basePoco.SortedSet, toPocoForSortedSet.SortedSet, basePoco, toPocoForSortedSet, fromDoc.Metadata, timestamp, 0);
        sortedSetStrategy.GeneratePatch(ctx);
    }

    [Benchmark(Description = "Strategy.Generate: RGA")]
    public void Generate_Rga()
    {
        opsBuffer.Clear();
        var ctx = new GeneratePatchContext(opsBuffer, new List<DifferentiateObjectContext>(), rgaPath, rgaProp, basePoco.RgaList, toPocoForRga.RgaList, basePoco, toPocoForRga, fromDoc.Metadata, timestamp, 0);
        rgaStrategy.GeneratePatch(ctx);
    }

    [Benchmark(Description = "Strategy.Generate: CounterMap")]
    public void Generate_CounterMap()
    {
        opsBuffer.Clear();
        var ctx = new GeneratePatchContext(opsBuffer, new List<DifferentiateObjectContext>(), counterMapPath, counterMapProp, basePoco.CounterMap, toPocoForCounterMap.CounterMap, basePoco, toPocoForCounterMap, fromDoc.Metadata, timestamp, 0);
        counterMapStrategy.GeneratePatch(ctx);
    }

    [Benchmark(Description = "Strategy.Generate: LwwMap")]
    public void Generate_LwwMap()
    {
        opsBuffer.Clear();
        var ctx = new GeneratePatchContext(opsBuffer, new List<DifferentiateObjectContext>(), lwwMapPath, lwwMapProp, basePoco.LwwMap, toPocoForLwwMap.LwwMap, basePoco, toPocoForLwwMap, fromDoc.Metadata, timestamp, 0);
        lwwMapStrategy.GeneratePatch(ctx);
    }

    [Benchmark(Description = "Strategy.Generate: FwwMap")]
    public void Generate_FwwMap()
    {
        opsBuffer.Clear();
        var ctx = new GeneratePatchContext(opsBuffer, new List<DifferentiateObjectContext>(), fwwMapPath, fwwMapProp, basePoco.FwwMap, toPocoForFwwMap.FwwMap, basePoco, toPocoForFwwMap, fromDoc.Metadata, timestamp, 0);
        fwwMapStrategy.GeneratePatch(ctx);
    }

    [Benchmark(Description = "Strategy.Generate: MaxWinsMap")]
    public void Generate_MaxWinsMap()
    {
        opsBuffer.Clear();
        var ctx = new GeneratePatchContext(opsBuffer, new List<DifferentiateObjectContext>(), maxWinsMapPath, maxWinsMapProp, basePoco.MaxWinsMap, toPocoForMaxWinsMap.MaxWinsMap, basePoco, toPocoForMaxWinsMap, fromDoc.Metadata, timestamp, 0);
        maxWinsMapStrategy.GeneratePatch(ctx);
    }

    [Benchmark(Description = "Strategy.Generate: MinWinsMap")]
    public void Generate_MinWinsMap()
    {
        opsBuffer.Clear();
        var ctx = new GeneratePatchContext(opsBuffer, new List<DifferentiateObjectContext>(), minWinsMapPath, minWinsMapProp, basePoco.MinWinsMap, toPocoForMinWinsMap.MinWinsMap, basePoco, toPocoForMinWinsMap, fromDoc.Metadata, timestamp, 0);
        minWinsMapStrategy.GeneratePatch(ctx);
    }

    [Benchmark(Description = "Strategy.Generate: OrMap")]
    public void Generate_OrMap()
    {
        opsBuffer.Clear();
        var ctx = new GeneratePatchContext(opsBuffer, new List<DifferentiateObjectContext>(), orMapPath, orMapProp, basePoco.OrMap, toPocoForOrMap.OrMap, basePoco, toPocoForOrMap, fromDoc.Metadata, timestamp, 0);
        orMapStrategy.GeneratePatch(ctx);
    }

    [Benchmark(Description = "Strategy.Generate: Graph")]
    public void Generate_Graph()
    {
        opsBuffer.Clear();
        var ctx = new GeneratePatchContext(opsBuffer, new List<DifferentiateObjectContext>(), graphPath, graphProp, basePoco.Graph, toPocoForGraph.Graph, basePoco, toPocoForGraph, fromDoc.Metadata, timestamp, 0);
        graphStrategy.GeneratePatch(ctx);
    }

    [Benchmark(Description = "Strategy.Generate: TwoPhaseGraph")]
    public void Generate_TwoPhaseGraph()
    {
        opsBuffer.Clear();
        var ctx = new GeneratePatchContext(opsBuffer, new List<DifferentiateObjectContext>(), twoPhaseGraphPath, twoPhaseGraphProp, basePoco.TwoPhaseGraph, toPocoForTwoPhaseGraph.TwoPhaseGraph, basePoco, toPocoForTwoPhaseGraph, fromDoc.Metadata, timestamp, 0);
        twoPhaseGraphStrategy.GeneratePatch(ctx);
    }

    [Benchmark(Description = "Strategy.Generate: ReplicatedTree")]
    public void Generate_ReplicatedTree()
    {
        opsBuffer.Clear();
        var ctx = new GeneratePatchContext(opsBuffer, new List<DifferentiateObjectContext>(), replicatedTreePath, replicatedTreeProp, basePoco.Tree, toPocoForReplicatedTree.Tree, basePoco, toPocoForReplicatedTree, fromDoc.Metadata, timestamp, 0);
        replicatedTreeStrategy.GeneratePatch(ctx);
    }

    [Benchmark(Description = "Strategy.Generate: EpochBound")]
    public void Generate_EpochBound()
    {
        opsBuffer.Clear();
        var ctx = new GeneratePatchContext(opsBuffer, new List<DifferentiateObjectContext>(), epochBoundPath, epochBoundProp, basePoco.EpochBoundValue, toPocoForEpochBound.EpochBoundValue, basePoco, toPocoForEpochBound, fromDoc.Metadata, timestamp, 0);
        epochBoundStrategy.GeneratePatch(ctx);
    }

    [Benchmark(Description = "Strategy.Generate: ApprovalQuorum")]
    public void Generate_ApprovalQuorum()
    {
        opsBuffer.Clear();
        var ctx = new GeneratePatchContext(opsBuffer, new List<DifferentiateObjectContext>(), approvalQuorumPath, approvalQuorumProp, basePoco.QuorumBoundValue, toPocoForApprovalQuorum.QuorumBoundValue, basePoco, toPocoForApprovalQuorum, fromDoc.Metadata, timestamp, 0);
        approvalQuorumStrategy.GeneratePatch(ctx);
    }
    #endregion
}