namespace Ama.CRDT.Benchmarks.Benchmarks;

using Ama.CRDT.Benchmarks.Models;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;

[Config(typeof(AntiVirusFriendlyConfig))]
[MemoryDiagnoser]
public class StrategyBenchmarks
{
    private IServiceScope scope;
    private ICrdtPatcher patcher = null!;
    private ICrdtApplicator applicator = null!;
    private ICrdtMetadataManager metadataManager = null!;

    private StrategyPoco basePoco = null!;
    private CrdtDocument<StrategyPoco> fromDoc = default!;

    #region Per-strategy data
    // LWW
    private StrategyPoco toPocoForLww = default!;
    private CrdtPatch lwwPatch = default!;

    // Counter
    private StrategyPoco toPocoForCounter = default!;
    private CrdtPatch counterPatch = default!;

    // GCounter
    private StrategyPoco toPocoForGCounter = default!;
    private CrdtPatch gCounterPatch = default!;

    // BoundedCounter
    private StrategyPoco toPocoForBoundedCounter = default!;
    private CrdtPatch boundedCounterPatch = default!;

    // MaxWins
    private StrategyPoco toPocoForMaxWins = default!;
    private CrdtPatch maxWinsPatch = default!;

    // MinWins
    private StrategyPoco toPocoForMinWins = default!;
    private CrdtPatch minWinsPatch = default!;

    // AverageRegister
    private StrategyPoco toPocoForAverageRegister = default!;
    private CrdtPatch averageRegisterPatch = default!;

    // GSet
    private StrategyPoco toPocoForGSet = default!;
    private CrdtPatch gSetPatch = default!;

    // TwoPhaseSet
    private StrategyPoco toPocoForTwoPhaseSet = default!;
    private CrdtPatch twoPhaseSetPatch = default!;

    // LwwSet
    private StrategyPoco toPocoForLwwSet = default!;
    private CrdtPatch lwwSetPatch = default!;

    // OrSet
    private StrategyPoco toPocoForOrSet = default!;
    private CrdtPatch orSetPatch = default!;

    // ArrayLcs
    private StrategyPoco toPocoForArrayLcs = default!;
    private CrdtPatch arrayLcsPatch = default!;

    // FixedSizeArray
    private StrategyPoco toPocoForFixedSizeArray = default!;
    private CrdtPatch fixedSizeArrayPatch = default!;

    // Lseq
    private StrategyPoco toPocoForLseq = default!;
    private CrdtPatch lseqPatch = default!;

    // VoteCounter
    private StrategyPoco toPocoForVoteCounter = default!;
    private CrdtPatch voteCounterPatch = default!;

    // StateMachine
    private StrategyPoco toPocoForStateMachine = default!;
    private CrdtPatch stateMachinePatch = default!;

    // ExclusiveLock
    private StrategyPoco toPocoForExclusiveLock = default!;
    private CrdtPatch exclusiveLockPatch = default!;

    // PriorityQueue
    private StrategyPoco toPocoForPriorityQueue = default!;
    private CrdtPatch priorityQueuePatch = default!;

    // SortedSet
    private StrategyPoco toPocoForSortedSet = default!;
    private CrdtPatch sortedSetPatch = default!;

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
        applicator = scope.ServiceProvider.GetRequiredService<ICrdtApplicator>();
        metadataManager = scope.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();

        basePoco = new StrategyPoco();
        var fromMeta = metadataManager.Initialize(basePoco);
        fromDoc = new CrdtDocument<StrategyPoco>(basePoco, fromMeta);

        #region Setup per strategy
        // LWW
        toPocoForLww = basePoco.Clone();
        toPocoForLww.LwwValue = "updated";
        lwwPatch = patcher.GeneratePatch(fromDoc, toPocoForLww);

        // Counter
        toPocoForCounter = basePoco.Clone();
        toPocoForCounter.Counter = 10;
        counterPatch = patcher.GeneratePatch(fromDoc, toPocoForCounter);

        // GCounter
        toPocoForGCounter = basePoco.Clone();
        toPocoForGCounter.GCounter = 10;
        gCounterPatch = patcher.GeneratePatch(fromDoc, toPocoForGCounter);

        // BoundedCounter
        toPocoForBoundedCounter = basePoco.Clone();
        toPocoForBoundedCounter.BoundedCounter = 10;
        boundedCounterPatch = patcher.GeneratePatch(fromDoc, toPocoForBoundedCounter);

        // MaxWins
        toPocoForMaxWins = basePoco.Clone();
        toPocoForMaxWins.MaxWins = 100;
        maxWinsPatch = patcher.GeneratePatch(fromDoc, toPocoForMaxWins);

        // MinWins
        toPocoForMinWins = basePoco.Clone();
        toPocoForMinWins.MinWins = 10;
        minWinsPatch = patcher.GeneratePatch(fromDoc, toPocoForMinWins);

        // AverageRegister
        toPocoForAverageRegister = basePoco.Clone();
        toPocoForAverageRegister.Average = 123.45m;
        averageRegisterPatch = patcher.GeneratePatch(fromDoc, toPocoForAverageRegister);

        // GSet
        toPocoForGSet = basePoco.Clone();
        toPocoForGSet.GSet.Add("A");
        gSetPatch = patcher.GeneratePatch(fromDoc, toPocoForGSet);

        // TwoPhaseSet
        toPocoForTwoPhaseSet = basePoco.Clone();
        toPocoForTwoPhaseSet.TwoPhaseSet.Remove("A");
        twoPhaseSetPatch = patcher.GeneratePatch(fromDoc, toPocoForTwoPhaseSet);

        // LwwSet
        toPocoForLwwSet = basePoco.Clone();
        toPocoForLwwSet.LwwSet.Remove("A");
        toPocoForLwwSet.LwwSet.Add("C");
        lwwSetPatch = patcher.GeneratePatch(fromDoc, toPocoForLwwSet);

        // OrSet
        toPocoForOrSet = basePoco.Clone();
        toPocoForOrSet.OrSet.Remove("A");
        toPocoForOrSet.OrSet.Add("C");
        orSetPatch = patcher.GeneratePatch(fromDoc, toPocoForOrSet);

        // ArrayLcs
        toPocoForArrayLcs = basePoco.Clone();
        toPocoForArrayLcs.LcsList.Insert(1, "D");
        toPocoForArrayLcs.LcsList.Remove("C");
        arrayLcsPatch = patcher.GeneratePatch(fromDoc, toPocoForArrayLcs);

        // FixedSizeArray
        toPocoForFixedSizeArray = basePoco.Clone();
        toPocoForFixedSizeArray.FixedArray[1] = "Z";
        fixedSizeArrayPatch = patcher.GeneratePatch(fromDoc, toPocoForFixedSizeArray);

        // Lseq
        toPocoForLseq = basePoco.Clone();
        toPocoForLseq.LseqList.Insert(1, "D");
        lseqPatch = patcher.GeneratePatch(fromDoc, toPocoForLseq);

        // VoteCounter
        toPocoForVoteCounter = basePoco.Clone();
        toPocoForVoteCounter.Votes["OptionA"].Remove("Voter1");
        toPocoForVoteCounter.Votes["OptionB"].Add("Voter1");
        voteCounterPatch = patcher.GeneratePatch(fromDoc, toPocoForVoteCounter);

        // StateMachine
        toPocoForStateMachine = basePoco.Clone();
        toPocoForStateMachine.State = "InProgress";
        stateMachinePatch = patcher.GeneratePatch(fromDoc, toPocoForStateMachine);

        // ExclusiveLock
        toPocoForExclusiveLock = basePoco.Clone();
        toPocoForExclusiveLock.LockedValue = "New Value";
        toPocoForExclusiveLock.LockHolder = "benchmark-replica";
        exclusiveLockPatch = patcher.GeneratePatch(fromDoc, toPocoForExclusiveLock);

        // PriorityQueue
        toPocoForPriorityQueue = basePoco.Clone();
        toPocoForPriorityQueue.PrioQueue.Add(new PrioItem { Id = 3, Priority = 5, Value = "C" });
        toPocoForPriorityQueue.PrioQueue[0].Value = "A_updated";
        priorityQueuePatch = patcher.GeneratePatch(fromDoc, toPocoForPriorityQueue);

        // SortedSet
        toPocoForSortedSet = basePoco.Clone();
        toPocoForSortedSet.SortedSet.Add(new PrioItem { Id = 3, Priority = 5, Value = "C" });
        toPocoForSortedSet.SortedSet.RemoveAll(p => p.Id == 1);
        sortedSetPatch = patcher.GeneratePatch(fromDoc, toPocoForSortedSet);
        #endregion
    }

    #region GeneratePatch Benchmarks
    [Benchmark(Description = "GeneratePatch: LWW")]
    public CrdtPatch GeneratePatch_Lww() => patcher.GeneratePatch(fromDoc, toPocoForLww);

    [Benchmark(Description = "GeneratePatch: Counter")]
    public CrdtPatch GeneratePatch_Counter() => patcher.GeneratePatch(fromDoc, toPocoForCounter);

    [Benchmark(Description = "GeneratePatch: GCounter")]
    public CrdtPatch GeneratePatch_GCounter() => patcher.GeneratePatch(fromDoc, toPocoForGCounter);

    [Benchmark(Description = "GeneratePatch: BoundedCounter")]
    public CrdtPatch GeneratePatch_BoundedCounter() => patcher.GeneratePatch(fromDoc, toPocoForBoundedCounter);

    [Benchmark(Description = "GeneratePatch: MaxWins")]
    public CrdtPatch GeneratePatch_MaxWins() => patcher.GeneratePatch(fromDoc, toPocoForMaxWins);

    [Benchmark(Description = "GeneratePatch: MinWins")]
    public CrdtPatch GeneratePatch_MinWins() => patcher.GeneratePatch(fromDoc, toPocoForMinWins);

    [Benchmark(Description = "GeneratePatch: AverageRegister")]
    public CrdtPatch GeneratePatch_AverageRegister() => patcher.GeneratePatch(fromDoc, toPocoForAverageRegister);

    [Benchmark(Description = "GeneratePatch: GSet")]
    public CrdtPatch GeneratePatch_GSet() => patcher.GeneratePatch(fromDoc, toPocoForGSet);

    [Benchmark(Description = "GeneratePatch: TwoPhaseSet")]
    public CrdtPatch GeneratePatch_TwoPhaseSet() => patcher.GeneratePatch(fromDoc, toPocoForTwoPhaseSet);

    [Benchmark(Description = "GeneratePatch: LwwSet")]
    public CrdtPatch GeneratePatch_LwwSet() => patcher.GeneratePatch(fromDoc, toPocoForLwwSet);

    [Benchmark(Description = "GeneratePatch: OrSet")]
    public CrdtPatch GeneratePatch_OrSet() => patcher.GeneratePatch(fromDoc, toPocoForOrSet);

    [Benchmark(Description = "GeneratePatch: ArrayLcs")]
    public CrdtPatch GeneratePatch_ArrayLcs() => patcher.GeneratePatch(fromDoc, toPocoForArrayLcs);

    [Benchmark(Description = "GeneratePatch: FixedSizeArray")]
    public CrdtPatch GeneratePatch_FixedSizeArray() => patcher.GeneratePatch(fromDoc, toPocoForFixedSizeArray);

    [Benchmark(Description = "GeneratePatch: Lseq")]
    public CrdtPatch GeneratePatch_Lseq() => patcher.GeneratePatch(fromDoc, toPocoForLseq);

    [Benchmark(Description = "GeneratePatch: VoteCounter")]
    public CrdtPatch GeneratePatch_VoteCounter() => patcher.GeneratePatch(fromDoc, toPocoForVoteCounter);

    [Benchmark(Description = "GeneratePatch: StateMachine")]
    public CrdtPatch GeneratePatch_StateMachine() => patcher.GeneratePatch(fromDoc, toPocoForStateMachine);

    [Benchmark(Description = "GeneratePatch: ExclusiveLock")]
    public CrdtPatch GeneratePatch_ExclusiveLock() => patcher.GeneratePatch(fromDoc, toPocoForExclusiveLock);

    [Benchmark(Description = "GeneratePatch: PriorityQueue")]
    public CrdtPatch GeneratePatch_PriorityQueue() => patcher.GeneratePatch(fromDoc, toPocoForPriorityQueue);

    [Benchmark(Description = "GeneratePatch: SortedSet")]
    public CrdtPatch GeneratePatch_SortedSet() => patcher.GeneratePatch(fromDoc, toPocoForSortedSet);
    #endregion

    #region ApplyPatch Benchmarks
    [Benchmark(Description = "ApplyPatch: LWW")]
    public StrategyPoco ApplyPatch_Lww() => applicator.ApplyPatch(new CrdtDocument<StrategyPoco>(basePoco.Clone(), new CrdtMetadata()), lwwPatch);

    [Benchmark(Description = "ApplyPatch: Counter")]
    public StrategyPoco ApplyPatch_Counter() => applicator.ApplyPatch(new CrdtDocument<StrategyPoco>(basePoco.Clone(), new CrdtMetadata()), counterPatch);

    [Benchmark(Description = "ApplyPatch: GCounter")]
    public StrategyPoco ApplyPatch_GCounter() => applicator.ApplyPatch(new CrdtDocument<StrategyPoco>(basePoco.Clone(), new CrdtMetadata()), gCounterPatch);

    [Benchmark(Description = "ApplyPatch: BoundedCounter")]
    public StrategyPoco ApplyPatch_BoundedCounter() => applicator.ApplyPatch(new CrdtDocument<StrategyPoco>(basePoco.Clone(), new CrdtMetadata()), boundedCounterPatch);

    [Benchmark(Description = "ApplyPatch: MaxWins")]
    public StrategyPoco ApplyPatch_MaxWins() => applicator.ApplyPatch(new CrdtDocument<StrategyPoco>(basePoco.Clone(), new CrdtMetadata()), maxWinsPatch);

    [Benchmark(Description = "ApplyPatch: MinWins")]
    public StrategyPoco ApplyPatch_MinWins() => applicator.ApplyPatch(new CrdtDocument<StrategyPoco>(basePoco.Clone(), new CrdtMetadata()), minWinsPatch);

    [Benchmark(Description = "ApplyPatch: AverageRegister")]
    public StrategyPoco ApplyPatch_AverageRegister() => applicator.ApplyPatch(new CrdtDocument<StrategyPoco>(basePoco.Clone(), new CrdtMetadata()), averageRegisterPatch);

    [Benchmark(Description = "ApplyPatch: GSet")]
    public StrategyPoco ApplyPatch_GSet() => applicator.ApplyPatch(new CrdtDocument<StrategyPoco>(basePoco.Clone(), new CrdtMetadata()), gSetPatch);

    [Benchmark(Description = "ApplyPatch: TwoPhaseSet")]
    public StrategyPoco ApplyPatch_TwoPhaseSet() => applicator.ApplyPatch(new CrdtDocument<StrategyPoco>(basePoco.Clone(), new CrdtMetadata()), twoPhaseSetPatch);

    [Benchmark(Description = "ApplyPatch: LwwSet")]
    public StrategyPoco ApplyPatch_LwwSet() => applicator.ApplyPatch(new CrdtDocument<StrategyPoco>(basePoco.Clone(), new CrdtMetadata()), lwwSetPatch);

    [Benchmark(Description = "ApplyPatch: OrSet")]
    public StrategyPoco ApplyPatch_OrSet() => applicator.ApplyPatch(new CrdtDocument<StrategyPoco>(basePoco.Clone(), new CrdtMetadata()), orSetPatch);

    [Benchmark(Description = "ApplyPatch: ArrayLcs")]
    public StrategyPoco ApplyPatch_ArrayLcs() => applicator.ApplyPatch(new CrdtDocument<StrategyPoco>(basePoco.Clone(), new CrdtMetadata()), arrayLcsPatch);

    [Benchmark(Description = "ApplyPatch: FixedSizeArray")]
    public StrategyPoco ApplyPatch_FixedSizeArray() => applicator.ApplyPatch(new CrdtDocument<StrategyPoco>(basePoco.Clone(), new CrdtMetadata()), fixedSizeArrayPatch);

    [Benchmark(Description = "ApplyPatch: Lseq")]
    public StrategyPoco ApplyPatch_Lseq() => applicator.ApplyPatch(new CrdtDocument<StrategyPoco>(basePoco.Clone(), new CrdtMetadata()), lseqPatch);

    [Benchmark(Description = "ApplyPatch: VoteCounter")]
    public StrategyPoco ApplyPatch_VoteCounter() => applicator.ApplyPatch(new CrdtDocument<StrategyPoco>(basePoco.Clone(), new CrdtMetadata()), voteCounterPatch);

    [Benchmark(Description = "ApplyPatch: StateMachine")]
    public StrategyPoco ApplyPatch_StateMachine() => applicator.ApplyPatch(new CrdtDocument<StrategyPoco>(basePoco.Clone(), new CrdtMetadata()), stateMachinePatch);

    [Benchmark(Description = "ApplyPatch: ExclusiveLock")]
    public StrategyPoco ApplyPatch_ExclusiveLock() => applicator.ApplyPatch(new CrdtDocument<StrategyPoco>(basePoco.Clone(), new CrdtMetadata()), exclusiveLockPatch);

    [Benchmark(Description = "ApplyPatch: PriorityQueue")]
    public StrategyPoco ApplyPatch_PriorityQueue() => applicator.ApplyPatch(new CrdtDocument<StrategyPoco>(basePoco.Clone(), new CrdtMetadata()), priorityQueuePatch);

    [Benchmark(Description = "ApplyPatch: SortedSet")]
    public StrategyPoco ApplyPatch_SortedSet() => applicator.ApplyPatch(new CrdtDocument<StrategyPoco>(basePoco.Clone(), new CrdtMetadata()), sortedSetPatch);
    #endregion
}