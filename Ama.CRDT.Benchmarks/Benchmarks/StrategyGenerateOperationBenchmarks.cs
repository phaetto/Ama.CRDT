namespace Ama.CRDT.Benchmarks.Benchmarks;

using Ama.CRDT.Benchmarks.Models;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Strategies;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;

[Config(typeof(AntiVirusFriendlyConfig))]
[MemoryDiagnoser]
public class StrategyGenerateOperationBenchmarks
{
    private IServiceScope scope;
    private ICrdtMetadataManager metadataManager = null!;
    private ICrdtStrategyProvider strategyProvider = null!;
    private ICrdtTimestamp timestamp = null!;

    private StrategyPoco basePoco = null!;
    private CrdtDocument<StrategyPoco> baseDoc = default!;
    private string replicaId = "replica-id";

    // We use a batching pattern to benchmark generation without triggering
    // BenchmarkDotNet's InvocationCount=1 penalty.
    private const int BatchSize = 100;

    #region Per-strategy operations and components
    private ICrdtStrategy lwwStrategy = null!;
    private GenerateOperationContext lwwContext = default!;
    
    private ICrdtStrategy fwwStrategy = null!;
    private GenerateOperationContext fwwContext = default!;

    private ICrdtStrategy counterStrategy = null!;
    private GenerateOperationContext counterContext = default!;

    private ICrdtStrategy gCounterStrategy = null!;
    private GenerateOperationContext gCounterContext = default!;

    private ICrdtStrategy boundedCounterStrategy = null!;
    private GenerateOperationContext boundedCounterContext = default!;

    private ICrdtStrategy maxWinsStrategy = null!;
    private GenerateOperationContext maxWinsContext = default!;

    private ICrdtStrategy minWinsStrategy = null!;
    private GenerateOperationContext minWinsContext = default!;

    private ICrdtStrategy averageStrategy = null!;
    private GenerateOperationContext averageContext = default!;

    private ICrdtStrategy gSetStrategy = null!;
    private GenerateOperationContext gSetContext = default!;

    private ICrdtStrategy twoPhaseSetStrategy = null!;
    private GenerateOperationContext twoPhaseSetContext = default!;

    private ICrdtStrategy lwwSetStrategy = null!;
    private GenerateOperationContext lwwSetContext = default!;
    
    private ICrdtStrategy fwwSetStrategy = null!;
    private GenerateOperationContext fwwSetContext = default!;

    private ICrdtStrategy orSetStrategy = null!;
    private GenerateOperationContext orSetContext = default!;

    private ICrdtStrategy arrayLcsStrategy = null!;
    private GenerateOperationContext arrayLcsContext = default!;

    private ICrdtStrategy fixedSizeArrayStrategy = null!;
    private GenerateOperationContext fixedSizeArrayContext = default!;

    private ICrdtStrategy lseqStrategy = null!;
    private GenerateOperationContext lseqContext = default!;

    private ICrdtStrategy voteCounterStrategy = null!;
    private GenerateOperationContext voteCounterContext = default!;

    private ICrdtStrategy stateMachineStrategy = null!;
    private GenerateOperationContext stateMachineContext = default!;

    private ICrdtStrategy priorityQueueStrategy = null!;
    private GenerateOperationContext priorityQueueContext = default!;

    private ICrdtStrategy sortedSetStrategy = null!;
    private GenerateOperationContext sortedSetContext = default!;

    private ICrdtStrategy rgaStrategy = null!;
    private GenerateOperationContext rgaContext = default!;

    private ICrdtStrategy counterMapStrategy = null!;
    private GenerateOperationContext counterMapContext = default!;

    private ICrdtStrategy lwwMapStrategy = null!;
    private GenerateOperationContext lwwMapContext = default!;
    
    private ICrdtStrategy fwwMapStrategy = null!;
    private GenerateOperationContext fwwMapContext = default!;

    private ICrdtStrategy maxWinsMapStrategy = null!;
    private GenerateOperationContext maxWinsMapContext = default!;

    private ICrdtStrategy minWinsMapStrategy = null!;
    private GenerateOperationContext minWinsMapContext = default!;

    private ICrdtStrategy orMapStrategy = null!;
    private GenerateOperationContext orMapContext = default!;

    private ICrdtStrategy graphStrategy = null!;
    private GenerateOperationContext graphContext = default!;

    private ICrdtStrategy twoPhaseGraphStrategy = null!;
    private GenerateOperationContext twoPhaseGraphContext = default!;

    private ICrdtStrategy replicatedTreeStrategy = null!;
    private GenerateOperationContext replicatedTreeContext = default!;
    #endregion

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddCrdt();
        services.AddScoped<MyStateMachine>();
        var serviceProvider = services.BuildServiceProvider();
        var serviceScopeFactory = serviceProvider.GetService<ICrdtScopeFactory>();
        scope = serviceScopeFactory.CreateScope(replicaId);

        metadataManager = scope.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();
        strategyProvider = scope.ServiceProvider.GetRequiredService<ICrdtStrategyProvider>();
        timestamp = scope.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>().Now();

        basePoco = new StrategyPoco();
        var fromMeta = metadataManager.Initialize(basePoco);
        baseDoc = new CrdtDocument<StrategyPoco>(basePoco, fromMeta);

        #region Setup per strategy contexts
        SetupStrategyAndContext(nameof(StrategyPoco.LwwValue), new SetIntent("updated"), out lwwStrategy, out lwwContext);
        SetupStrategyAndContext(nameof(StrategyPoco.FwwValue), new SetIntent("updated"), out fwwStrategy, out fwwContext);
        SetupStrategyAndContext(nameof(StrategyPoco.Counter), new IncrementIntent(10), out counterStrategy, out counterContext);
        SetupStrategyAndContext(nameof(StrategyPoco.GCounter), new IncrementIntent(10u), out gCounterStrategy, out gCounterContext);
        SetupStrategyAndContext(nameof(StrategyPoco.BoundedCounter), new IncrementIntent(10), out boundedCounterStrategy, out boundedCounterContext);
        SetupStrategyAndContext(nameof(StrategyPoco.MaxWins), new SetIntent(100), out maxWinsStrategy, out maxWinsContext);
        SetupStrategyAndContext(nameof(StrategyPoco.MinWins), new SetIntent(10), out minWinsStrategy, out minWinsContext);
        SetupStrategyAndContext(nameof(StrategyPoco.Average), new SetIntent(123.45m), out averageStrategy, out averageContext);
        SetupStrategyAndContext(nameof(StrategyPoco.GSet), new AddIntent("C"), out gSetStrategy, out gSetContext);
        SetupStrategyAndContext(nameof(StrategyPoco.TwoPhaseSet), new AddIntent("C"), out twoPhaseSetStrategy, out twoPhaseSetContext);
        SetupStrategyAndContext(nameof(StrategyPoco.LwwSet), new AddIntent("C"), out lwwSetStrategy, out lwwSetContext);
        SetupStrategyAndContext(nameof(StrategyPoco.FwwSet), new AddIntent("C"), out fwwSetStrategy, out fwwSetContext);
        SetupStrategyAndContext(nameof(StrategyPoco.OrSet), new AddIntent("C"), out orSetStrategy, out orSetContext);
        SetupStrategyAndContext(nameof(StrategyPoco.LcsList), new InsertIntent(1, "D"), out arrayLcsStrategy, out arrayLcsContext);
        SetupStrategyAndContext(nameof(StrategyPoco.FixedArray), new SetIndexIntent(1, "Z"), out fixedSizeArrayStrategy, out fixedSizeArrayContext);
        SetupStrategyAndContext(nameof(StrategyPoco.LseqList), new InsertIntent(1, "D"), out lseqStrategy, out lseqContext);
        SetupStrategyAndContext(nameof(StrategyPoco.Votes), new VoteIntent("Voter1", "OptionA"), out voteCounterStrategy, out voteCounterContext);
        SetupStrategyAndContext(nameof(StrategyPoco.State), new SetIntent("InProgress"), out stateMachineStrategy, out stateMachineContext);
        SetupStrategyAndContext(nameof(StrategyPoco.PrioQueue), new AddIntent(new PrioItem { Id = 3, Priority = 5, Value = "C" }), out priorityQueueStrategy, out priorityQueueContext);
        SetupStrategyAndContext(nameof(StrategyPoco.SortedSet), new AddIntent(new PrioItem { Id = 3, Priority = 5, Value = "C" }), out sortedSetStrategy, out sortedSetContext);
        SetupStrategyAndContext(nameof(StrategyPoco.RgaList), new InsertIntent(1, "D"), out rgaStrategy, out rgaContext);
        SetupStrategyAndContext(nameof(StrategyPoco.CounterMap), new MapIncrementIntent("A", 5), out counterMapStrategy, out counterMapContext);
        SetupStrategyAndContext(nameof(StrategyPoco.LwwMap), new MapSetIntent("A", "updated"), out lwwMapStrategy, out lwwMapContext);
        SetupStrategyAndContext(nameof(StrategyPoco.FwwMap), new MapSetIntent("A", "updated"), out fwwMapStrategy, out fwwMapContext);
        SetupStrategyAndContext(nameof(StrategyPoco.MaxWinsMap), new MapSetIntent("A", 50), out maxWinsMapStrategy, out maxWinsMapContext);
        SetupStrategyAndContext(nameof(StrategyPoco.MinWinsMap), new MapSetIntent("A", 1), out minWinsMapStrategy, out minWinsMapContext);
        SetupStrategyAndContext(nameof(StrategyPoco.OrMap), new MapSetIntent("A", "new"), out orMapStrategy, out orMapContext);
        SetupStrategyAndContext(nameof(StrategyPoco.Graph), new AddVertexIntent("Vertex2"), out graphStrategy, out graphContext);
        SetupStrategyAndContext(nameof(StrategyPoco.TwoPhaseGraph), new AddVertexIntent("Vertex3"), out twoPhaseGraphStrategy, out twoPhaseGraphContext);
        SetupStrategyAndContext(nameof(StrategyPoco.Tree), new AddNodeIntent(new TreeNode { Id = 2, Value = "Node2" }), out replicatedTreeStrategy, out replicatedTreeContext);
        #endregion
    }

    private void SetupStrategyAndContext(
        string propertyName, 
        IOperationIntent intent, 
        out ICrdtStrategy strategy, 
        out GenerateOperationContext context)
    {
        var prop = typeof(StrategyPoco).GetProperty(propertyName)!;
        strategy = strategyProvider.GetStrategy(prop);
        var path = $"$.{char.ToLowerInvariant(propertyName[0])}{propertyName.Substring(1)}";
        
        context = new GenerateOperationContext(
            baseDoc.Data, 
            baseDoc.Metadata, 
            path, 
            prop, 
            intent, 
            timestamp, 
            0
        );
    }

    #region GenerateOperation Benchmarks
    [Benchmark(Description = "Strategy.GenerateOp: LWW", OperationsPerInvoke = BatchSize)]
    public void GenerateOp_Lww()
    {
        for (int i = 0; i < BatchSize; i++)
            lwwStrategy.GenerateOperation(lwwContext);
    }
    
    [Benchmark(Description = "Strategy.GenerateOp: FWW", OperationsPerInvoke = BatchSize)]
    public void GenerateOp_Fww()
    {
        for (int i = 0; i < BatchSize; i++)
            fwwStrategy.GenerateOperation(fwwContext);
    }

    [Benchmark(Description = "Strategy.GenerateOp: Counter", OperationsPerInvoke = BatchSize)]
    public void GenerateOp_Counter()
    {
        for (int i = 0; i < BatchSize; i++)
            counterStrategy.GenerateOperation(counterContext);
    }

    [Benchmark(Description = "Strategy.GenerateOp: GCounter", OperationsPerInvoke = BatchSize)]
    public void GenerateOp_GCounter()
    {
        for (int i = 0; i < BatchSize; i++)
            gCounterStrategy.GenerateOperation(gCounterContext);
    }

    [Benchmark(Description = "Strategy.GenerateOp: BoundedCounter", OperationsPerInvoke = BatchSize)]
    public void GenerateOp_BoundedCounter()
    {
        for (int i = 0; i < BatchSize; i++)
            boundedCounterStrategy.GenerateOperation(boundedCounterContext);
    }

    [Benchmark(Description = "Strategy.GenerateOp: MaxWins", OperationsPerInvoke = BatchSize)]
    public void GenerateOp_MaxWins()
    {
        for (int i = 0; i < BatchSize; i++)
            maxWinsStrategy.GenerateOperation(maxWinsContext);
    }

    [Benchmark(Description = "Strategy.GenerateOp: MinWins", OperationsPerInvoke = BatchSize)]
    public void GenerateOp_MinWins()
    {
        for (int i = 0; i < BatchSize; i++)
            minWinsStrategy.GenerateOperation(minWinsContext);
    }

    [Benchmark(Description = "Strategy.GenerateOp: AverageRegister", OperationsPerInvoke = BatchSize)]
    public void GenerateOp_AverageRegister()
    {
        for (int i = 0; i < BatchSize; i++)
            averageStrategy.GenerateOperation(averageContext);
    }

    [Benchmark(Description = "Strategy.GenerateOp: GSet", OperationsPerInvoke = BatchSize)]
    public void GenerateOp_GSet()
    {
        for (int i = 0; i < BatchSize; i++)
            gSetStrategy.GenerateOperation(gSetContext);
    }

    [Benchmark(Description = "Strategy.GenerateOp: TwoPhaseSet", OperationsPerInvoke = BatchSize)]
    public void GenerateOp_TwoPhaseSet()
    {
        for (int i = 0; i < BatchSize; i++)
            twoPhaseSetStrategy.GenerateOperation(twoPhaseSetContext);
    }

    [Benchmark(Description = "Strategy.GenerateOp: LwwSet", OperationsPerInvoke = BatchSize)]
    public void GenerateOp_LwwSet()
    {
        for (int i = 0; i < BatchSize; i++)
            lwwSetStrategy.GenerateOperation(lwwSetContext);
    }
    
    [Benchmark(Description = "Strategy.GenerateOp: FwwSet", OperationsPerInvoke = BatchSize)]
    public void GenerateOp_FwwSet()
    {
        for (int i = 0; i < BatchSize; i++)
            fwwSetStrategy.GenerateOperation(fwwSetContext);
    }

    [Benchmark(Description = "Strategy.GenerateOp: OrSet", OperationsPerInvoke = BatchSize)]
    public void GenerateOp_OrSet()
    {
        for (int i = 0; i < BatchSize; i++)
            orSetStrategy.GenerateOperation(orSetContext);
    }

    [Benchmark(Description = "Strategy.GenerateOp: ArrayLcs", OperationsPerInvoke = BatchSize)]
    public void GenerateOp_ArrayLcs()
    {
        for (int i = 0; i < BatchSize; i++)
            arrayLcsStrategy.GenerateOperation(arrayLcsContext);
    }

    [Benchmark(Description = "Strategy.GenerateOp: FixedSizeArray", OperationsPerInvoke = BatchSize)]
    public void GenerateOp_FixedSizeArray()
    {
        for (int i = 0; i < BatchSize; i++)
            fixedSizeArrayStrategy.GenerateOperation(fixedSizeArrayContext);
    }

    [Benchmark(Description = "Strategy.GenerateOp: Lseq", OperationsPerInvoke = BatchSize)]
    public void GenerateOp_Lseq()
    {
        for (int i = 0; i < BatchSize; i++)
            lseqStrategy.GenerateOperation(lseqContext);
    }

    [Benchmark(Description = "Strategy.GenerateOp: VoteCounter", OperationsPerInvoke = BatchSize)]
    public void GenerateOp_VoteCounter()
    {
        for (int i = 0; i < BatchSize; i++)
            voteCounterStrategy.GenerateOperation(voteCounterContext);
    }

    [Benchmark(Description = "Strategy.GenerateOp: StateMachine", OperationsPerInvoke = BatchSize)]
    public void GenerateOp_StateMachine()
    {
        for (int i = 0; i < BatchSize; i++)
            stateMachineStrategy.GenerateOperation(stateMachineContext);
    }

    [Benchmark(Description = "Strategy.GenerateOp: PriorityQueue", OperationsPerInvoke = BatchSize)]
    public void GenerateOp_PriorityQueue()
    {
        for (int i = 0; i < BatchSize; i++)
            priorityQueueStrategy.GenerateOperation(priorityQueueContext);
    }

    [Benchmark(Description = "Strategy.GenerateOp: SortedSet", OperationsPerInvoke = BatchSize)]
    public void GenerateOp_SortedSet()
    {
        for (int i = 0; i < BatchSize; i++)
            sortedSetStrategy.GenerateOperation(sortedSetContext);
    }

    [Benchmark(Description = "Strategy.GenerateOp: RGA", OperationsPerInvoke = BatchSize)]
    public void GenerateOp_Rga()
    {
        for (int i = 0; i < BatchSize; i++)
            rgaStrategy.GenerateOperation(rgaContext);
    }

    [Benchmark(Description = "Strategy.GenerateOp: CounterMap", OperationsPerInvoke = BatchSize)]
    public void GenerateOp_CounterMap()
    {
        for (int i = 0; i < BatchSize; i++)
            counterMapStrategy.GenerateOperation(counterMapContext);
    }

    [Benchmark(Description = "Strategy.GenerateOp: LwwMap", OperationsPerInvoke = BatchSize)]
    public void GenerateOp_LwwMap()
    {
        for (int i = 0; i < BatchSize; i++)
            lwwMapStrategy.GenerateOperation(lwwMapContext);
    }
    
    [Benchmark(Description = "Strategy.GenerateOp: FwwMap", OperationsPerInvoke = BatchSize)]
    public void GenerateOp_FwwMap()
    {
        for (int i = 0; i < BatchSize; i++)
            fwwMapStrategy.GenerateOperation(fwwMapContext);
    }

    [Benchmark(Description = "Strategy.GenerateOp: MaxWinsMap", OperationsPerInvoke = BatchSize)]
    public void GenerateOp_MaxWinsMap()
    {
        for (int i = 0; i < BatchSize; i++)
            maxWinsMapStrategy.GenerateOperation(maxWinsMapContext);
    }

    [Benchmark(Description = "Strategy.GenerateOp: MinWinsMap", OperationsPerInvoke = BatchSize)]
    public void GenerateOp_MinWinsMap()
    {
        for (int i = 0; i < BatchSize; i++)
            minWinsMapStrategy.GenerateOperation(minWinsMapContext);
    }

    [Benchmark(Description = "Strategy.GenerateOp: OrMap", OperationsPerInvoke = BatchSize)]
    public void GenerateOp_OrMap()
    {
        for (int i = 0; i < BatchSize; i++)
            orMapStrategy.GenerateOperation(orMapContext);
    }

    [Benchmark(Description = "Strategy.GenerateOp: Graph", OperationsPerInvoke = BatchSize)]
    public void GenerateOp_Graph()
    {
        for (int i = 0; i < BatchSize; i++)
            graphStrategy.GenerateOperation(graphContext);
    }

    [Benchmark(Description = "Strategy.GenerateOp: TwoPhaseGraph", OperationsPerInvoke = BatchSize)]
    public void GenerateOp_TwoPhaseGraph()
    {
        for (int i = 0; i < BatchSize; i++)
            twoPhaseGraphStrategy.GenerateOperation(twoPhaseGraphContext);
    }

    [Benchmark(Description = "Strategy.GenerateOp: ReplicatedTree", OperationsPerInvoke = BatchSize)]
    public void GenerateOp_ReplicatedTree()
    {
        for (int i = 0; i < BatchSize; i++)
            replicatedTreeStrategy.GenerateOperation(replicatedTreeContext);
    }
    #endregion
}