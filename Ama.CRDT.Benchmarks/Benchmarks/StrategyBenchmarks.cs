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
    private ICrdtPatcher patcher = null!;
    private ICrdtApplicator applicator = null!;
    private ICrdtMetadataManager metadataManager = null!;

    private StrategyPoco basePoco = null!;
    private CrdtDocument<StrategyPoco> fromDoc = default;

    #region Per-strategy data
    // LWW
    private CrdtDocument<StrategyPoco> toDocForLww = default;
    private CrdtPatch lwwPatch = default;

    // Counter
    private CrdtDocument<StrategyPoco> toDocForCounter = default;
    private CrdtPatch counterPatch = default;

    // GCounter
    private CrdtDocument<StrategyPoco> toDocForGCounter = default;
    private CrdtPatch gCounterPatch = default;

    // BoundedCounter
    private CrdtDocument<StrategyPoco> toDocForBoundedCounter = default;
    private CrdtPatch boundedCounterPatch = default;

    // MaxWins
    private CrdtDocument<StrategyPoco> toDocForMaxWins = default;
    private CrdtPatch maxWinsPatch = default;

    // MinWins
    private CrdtDocument<StrategyPoco> toDocForMinWins = default;
    private CrdtPatch minWinsPatch = default;

    // AverageRegister
    private CrdtDocument<StrategyPoco> toDocForAverageRegister = default;
    private CrdtPatch averageRegisterPatch = default;

    // GSet
    private CrdtDocument<StrategyPoco> toDocForGSet = default;
    private CrdtPatch gSetPatch = default;

    // TwoPhaseSet
    private CrdtDocument<StrategyPoco> toDocForTwoPhaseSet = default;
    private CrdtPatch twoPhaseSetPatch = default;

    // LwwSet
    private CrdtDocument<StrategyPoco> toDocForLwwSet = default;
    private CrdtPatch lwwSetPatch = default;

    // OrSet
    private CrdtDocument<StrategyPoco> toDocForOrSet = default;
    private CrdtPatch orSetPatch = default;

    // ArrayLcs
    private CrdtDocument<StrategyPoco> toDocForArrayLcs = default;
    private CrdtPatch arrayLcsPatch = default;

    // FixedSizeArray
    private CrdtDocument<StrategyPoco> toDocForFixedSizeArray = default;
    private CrdtPatch fixedSizeArrayPatch = default;

    // Lseq
    private CrdtDocument<StrategyPoco> toDocForLseq = default;
    private CrdtPatch lseqPatch = default;

    // VoteCounter
    private CrdtDocument<StrategyPoco> toDocForVoteCounter = default;
    private CrdtPatch voteCounterPatch = default;

    // StateMachine
    private CrdtDocument<StrategyPoco> toDocForStateMachine = default;
    private CrdtPatch stateMachinePatch = default;

    // ExclusiveLock
    private CrdtDocument<StrategyPoco> toDocForExclusiveLock = default;
    private CrdtPatch exclusiveLockPatch = default;

    // PriorityQueue
    private CrdtDocument<StrategyPoco> toDocForPriorityQueue = default;
    private CrdtPatch priorityQueuePatch = default;

    // SortedSet
    private CrdtDocument<StrategyPoco> toDocForSortedSet = default;
    private CrdtPatch sortedSetPatch = default;

    #endregion

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddCrdt();
        services.AddSingleton<MyStateMachine>();
        var serviceProvider = services.BuildServiceProvider();

        patcher = serviceProvider.GetRequiredService<ICrdtPatcher>();
        applicator = serviceProvider.GetRequiredService<ICrdtApplicator>();
        metadataManager = serviceProvider.GetRequiredService<ICrdtMetadataManager>();

        basePoco = new StrategyPoco();
        var fromMeta = metadataManager.Initialize(basePoco);
        fromDoc = new CrdtDocument<StrategyPoco>(basePoco, fromMeta);
        long timestamp = 1;

        #region Setup per strategy
        // LWW
        var toPocoLww = basePoco.Clone();
        toPocoLww.LwwValue = "updated";
        toDocForLww = CreateToDoc(fromDoc, toPocoLww, timestamp++);
        lwwPatch = patcher.GeneratePatch(fromDoc, toDocForLww);

        // Counter
        var toPocoCounter = basePoco.Clone();
        toPocoCounter.Counter = 10;
        toDocForCounter = CreateToDoc(fromDoc, toPocoCounter, timestamp++);
        counterPatch = patcher.GeneratePatch(fromDoc, toDocForCounter);

        // GCounter
        var toPocoGCounter = basePoco.Clone();
        toPocoGCounter.GCounter = 10;
        toDocForGCounter = CreateToDoc(fromDoc, toPocoGCounter, timestamp++);
        gCounterPatch = patcher.GeneratePatch(fromDoc, toDocForGCounter);

        // BoundedCounter
        var toPocoBoundedCounter = basePoco.Clone();
        toPocoBoundedCounter.BoundedCounter = 10;
        toDocForBoundedCounter = CreateToDoc(fromDoc, toPocoBoundedCounter, timestamp++);
        boundedCounterPatch = patcher.GeneratePatch(fromDoc, toDocForBoundedCounter);

        // MaxWins
        var toPocoMaxWins = basePoco.Clone();
        toPocoMaxWins.MaxWins = 100;
        toDocForMaxWins = CreateToDoc(fromDoc, toPocoMaxWins, timestamp++);
        maxWinsPatch = patcher.GeneratePatch(fromDoc, toDocForMaxWins);

        // MinWins
        var toPocoMinWins = basePoco.Clone();
        toPocoMinWins.MinWins = 10;
        toDocForMinWins = CreateToDoc(fromDoc, toPocoMinWins, timestamp++);
        minWinsPatch = patcher.GeneratePatch(fromDoc, toDocForMinWins);

        // AverageRegister
        var toPocoAverageRegister = basePoco.Clone();
        toPocoAverageRegister.Average = 123.45m;
        toDocForAverageRegister = CreateToDoc(fromDoc, toPocoAverageRegister, timestamp++);
        averageRegisterPatch = patcher.GeneratePatch(fromDoc, toDocForAverageRegister);

        // GSet
        var toPocoGSet = basePoco.Clone();
        toPocoGSet.GSet.Add("A");
        toDocForGSet = CreateToDoc(fromDoc, toPocoGSet, timestamp++);
        gSetPatch = patcher.GeneratePatch(fromDoc, toDocForGSet);

        // TwoPhaseSet
        var toPocoTwoPhaseSet = basePoco.Clone();
        toPocoTwoPhaseSet.TwoPhaseSet.Remove("A");
        toDocForTwoPhaseSet = CreateToDoc(fromDoc, toPocoTwoPhaseSet, timestamp++);
        twoPhaseSetPatch = patcher.GeneratePatch(fromDoc, toDocForTwoPhaseSet);

        // LwwSet
        var toPocoLwwSet = basePoco.Clone();
        toPocoLwwSet.LwwSet.Remove("A");
        toPocoLwwSet.LwwSet.Add("C");
        toDocForLwwSet = CreateToDoc(fromDoc, toPocoLwwSet, timestamp++);
        lwwSetPatch = patcher.GeneratePatch(fromDoc, toDocForLwwSet);

        // OrSet
        var toPocoOrSet = basePoco.Clone();
        toPocoOrSet.OrSet.Remove("A");
        toPocoOrSet.OrSet.Add("C");
        toDocForOrSet = CreateToDoc(fromDoc, toPocoOrSet, timestamp++);
        orSetPatch = patcher.GeneratePatch(fromDoc, toDocForOrSet);

        // ArrayLcs
        var toPocoArrayLcs = basePoco.Clone();
        toPocoArrayLcs.LcsList.Insert(1, "D");
        toPocoArrayLcs.LcsList.Remove("C");
        toDocForArrayLcs = CreateToDoc(fromDoc, toPocoArrayLcs, timestamp++);
        arrayLcsPatch = patcher.GeneratePatch(fromDoc, toDocForArrayLcs);

        // FixedSizeArray
        var toPocoFixedSizeArray = basePoco.Clone();
        toPocoFixedSizeArray.FixedArray[1] = "Z";
        toDocForFixedSizeArray = CreateToDoc(fromDoc, toPocoFixedSizeArray, timestamp++);
        fixedSizeArrayPatch = patcher.GeneratePatch(fromDoc, toDocForFixedSizeArray);

        // Lseq
        var toPocoLseq = basePoco.Clone();
        toPocoLseq.LseqList.Insert(1, "D");
        toDocForLseq = CreateToDoc(fromDoc, toPocoLseq, timestamp++);
        lseqPatch = patcher.GeneratePatch(fromDoc, toDocForLseq);

        // VoteCounter
        var toPocoVoteCounter = basePoco.Clone();
        toPocoVoteCounter.Votes["OptionA"].Remove("Voter1");
        toPocoVoteCounter.Votes["OptionB"].Add("Voter1");
        toDocForVoteCounter = CreateToDoc(fromDoc, toPocoVoteCounter, timestamp++);
        voteCounterPatch = patcher.GeneratePatch(fromDoc, toDocForVoteCounter);

        // StateMachine
        var toPocoStateMachine = basePoco.Clone();
        toPocoStateMachine.State = "InProgress";
        toDocForStateMachine = CreateToDoc(fromDoc, toPocoStateMachine, timestamp++);
        stateMachinePatch = patcher.GeneratePatch(fromDoc, toDocForStateMachine);

        // ExclusiveLock
        var toPocoExclusiveLock = basePoco.Clone();
        toPocoExclusiveLock.LockedValue = "New Value";
        toPocoExclusiveLock.LockHolder = "benchmark-replica";
        toDocForExclusiveLock = CreateToDoc(fromDoc, toPocoExclusiveLock, timestamp++);
        exclusiveLockPatch = patcher.GeneratePatch(fromDoc, toDocForExclusiveLock);

        // PriorityQueue
        var toPocoPriorityQueue = basePoco.Clone();
        toPocoPriorityQueue.PrioQueue.Add(new PrioItem { Id = 3, Priority = 5, Value = "C" });
        toPocoPriorityQueue.PrioQueue[0].Value = "A_updated";
        toDocForPriorityQueue = CreateToDoc(fromDoc, toPocoPriorityQueue, timestamp++);
        priorityQueuePatch = patcher.GeneratePatch(fromDoc, toDocForPriorityQueue);

        // SortedSet
        var toPocoSortedSet = basePoco.Clone();
        toPocoSortedSet.SortedSet.Add(new PrioItem { Id = 3, Priority = 5, Value = "C" });
        toPocoSortedSet.SortedSet.RemoveAll(p => p.Id == 1);
        toDocForSortedSet = CreateToDoc(fromDoc, toPocoSortedSet, timestamp++);
        sortedSetPatch = patcher.GeneratePatch(fromDoc, toDocForSortedSet);
        #endregion
    }

    #region GeneratePatch Benchmarks
    [Benchmark(Description = "GeneratePatch: LWW")]
    public CrdtPatch GeneratePatch_Lww() => patcher.GeneratePatch(fromDoc, toDocForLww);

    [Benchmark(Description = "GeneratePatch: Counter")]
    public CrdtPatch GeneratePatch_Counter() => patcher.GeneratePatch(fromDoc, toDocForCounter);

    [Benchmark(Description = "GeneratePatch: GCounter")]
    public CrdtPatch GeneratePatch_GCounter() => patcher.GeneratePatch(fromDoc, toDocForGCounter);

    [Benchmark(Description = "GeneratePatch: BoundedCounter")]
    public CrdtPatch GeneratePatch_BoundedCounter() => patcher.GeneratePatch(fromDoc, toDocForBoundedCounter);

    [Benchmark(Description = "GeneratePatch: MaxWins")]
    public CrdtPatch GeneratePatch_MaxWins() => patcher.GeneratePatch(fromDoc, toDocForMaxWins);

    [Benchmark(Description = "GeneratePatch: MinWins")]
    public CrdtPatch GeneratePatch_MinWins() => patcher.GeneratePatch(fromDoc, toDocForMinWins);

    [Benchmark(Description = "GeneratePatch: AverageRegister")]
    public CrdtPatch GeneratePatch_AverageRegister() => patcher.GeneratePatch(fromDoc, toDocForAverageRegister);

    [Benchmark(Description = "GeneratePatch: GSet")]
    public CrdtPatch GeneratePatch_GSet() => patcher.GeneratePatch(fromDoc, toDocForGSet);

    [Benchmark(Description = "GeneratePatch: TwoPhaseSet")]
    public CrdtPatch GeneratePatch_TwoPhaseSet() => patcher.GeneratePatch(fromDoc, toDocForTwoPhaseSet);

    [Benchmark(Description = "GeneratePatch: LwwSet")]
    public CrdtPatch GeneratePatch_LwwSet() => patcher.GeneratePatch(fromDoc, toDocForLwwSet);

    [Benchmark(Description = "GeneratePatch: OrSet")]
    public CrdtPatch GeneratePatch_OrSet() => patcher.GeneratePatch(fromDoc, toDocForOrSet);

    [Benchmark(Description = "GeneratePatch: ArrayLcs")]
    public CrdtPatch GeneratePatch_ArrayLcs() => patcher.GeneratePatch(fromDoc, toDocForArrayLcs);

    [Benchmark(Description = "GeneratePatch: FixedSizeArray")]
    public CrdtPatch GeneratePatch_FixedSizeArray() => patcher.GeneratePatch(fromDoc, toDocForFixedSizeArray);

    [Benchmark(Description = "GeneratePatch: Lseq")]
    public CrdtPatch GeneratePatch_Lseq() => patcher.GeneratePatch(fromDoc, toDocForLseq);

    [Benchmark(Description = "GeneratePatch: VoteCounter")]
    public CrdtPatch GeneratePatch_VoteCounter() => patcher.GeneratePatch(fromDoc, toDocForVoteCounter);

    [Benchmark(Description = "GeneratePatch: StateMachine")]
    public CrdtPatch GeneratePatch_StateMachine() => patcher.GeneratePatch(fromDoc, toDocForStateMachine);

    [Benchmark(Description = "GeneratePatch: ExclusiveLock")]
    public CrdtPatch GeneratePatch_ExclusiveLock() => patcher.GeneratePatch(fromDoc, toDocForExclusiveLock);

    [Benchmark(Description = "GeneratePatch: PriorityQueue")]
    public CrdtPatch GeneratePatch_PriorityQueue() => patcher.GeneratePatch(fromDoc, toDocForPriorityQueue);

    [Benchmark(Description = "GeneratePatch: SortedSet")]
    public CrdtPatch GeneratePatch_SortedSet() => patcher.GeneratePatch(fromDoc, toDocForSortedSet);
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

    private CrdtDocument<T> CreateToDoc<T>(CrdtDocument<T> from, T toPoco, long timestamp) where T : class
    {
        var toMeta = metadataManager.Clone(from.Metadata);
        var toDoc = new CrdtDocument<T>(toPoco, toMeta);
        metadataManager.Initialize(toDoc, new EpochTimestamp(timestamp));
        return toDoc;
    }
}