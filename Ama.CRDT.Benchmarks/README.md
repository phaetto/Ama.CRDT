# Ama.CRDT.Benchmarks

These benchmarks test the individual CRDT strategy application, patch generation, and other performance metrics.

<!-- BENCHMARKS_START -->
```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9974)
Intel Core i9-10850K CPU 3.60GHz, 1 CPU, 20 logical and 10 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3 [AttachedDebugger]

Job=MediumRun  Toolchain=InProcessNoEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
| Method            | Mean       | Error    | StdDev   | Gen0   | Allocated |
|------------------ |-----------:|---------:|---------:|-------:|----------:|
| ApplyPatchSimple  |   370.2 ns |  5.67 ns |  7.95 ns | 0.0677 |     712 B |
| ApplyPatchComplex | 1,300.0 ns | 10.63 ns | 15.24 ns | 0.2270 |    2392 B |


```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9974)
Intel Core i9-10850K CPU 3.60GHz, 1 CPU, 20 logical and 10 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3 [AttachedDebugger]

Job=MediumRun  Toolchain=InProcessNoEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
| Method               | Mean       | Error    | StdDev   | Gen0   | Gen1   | Allocated |
|--------------------- |-----------:|---------:|---------:|-------:|-------:|----------:|
| GeneratePatchSimple  |   752.9 ns | 16.15 ns | 23.17 ns | 0.1631 |      - |   1.67 KB |
| GeneratePatchComplex | 3,474.3 ns | 43.81 ns | 64.21 ns | 0.5951 | 0.0038 |   6.09 KB |


```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9974)
Intel Core i9-10850K CPU 3.60GHz, 1 CPU, 20 logical and 10 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3 [AttachedDebugger]

Job=MediumRun  Toolchain=InProcessNoEmitToolchain  InvocationCount=1  
IterationCount=15  LaunchCount=2  UnrollFactor=1  
WarmupCount=10  

```
| Method                            | Mean        | Error      | StdDev     | Median      | Allocated |
|---------------------------------- |------------:|-----------:|-----------:|------------:|----------:|
| &#39;Strategy.Apply: LWW&#39;             |   397.34 ns |  54.476 ns |  79.851 ns |   417.00 ns |     208 B |
| &#39;Strategy.Apply: FWW&#39;             |   350.69 ns |  41.403 ns |  60.688 ns |   372.00 ns |     211 B |
| &#39;Strategy.Apply: Counter&#39;         |   569.71 ns |  65.161 ns |  93.452 ns |   552.50 ns |     564 B |
| &#39;Strategy.Apply: GCounter&#39;        |   623.54 ns | 122.461 ns | 175.630 ns |   523.50 ns |     535 B |
| &#39;Strategy.Apply: BoundedCounter&#39;  |    86.33 ns |  10.534 ns |  14.767 ns |    82.00 ns |     152 B |
| &#39;Strategy.Apply: MaxWins&#39;         |    90.93 ns |  13.269 ns |  18.601 ns |    85.00 ns |     152 B |
| &#39;Strategy.Apply: MinWins&#39;         |    85.00 ns |   6.658 ns |   9.549 ns |    85.00 ns |     152 B |
| &#39;Strategy.Apply: AverageRegister&#39; |   707.48 ns | 229.500 ns | 336.398 ns |   492.00 ns |     823 B |
| &#39;Strategy.Apply: GSet&#39;            |   536.17 ns | 129.530 ns | 193.874 ns |   438.50 ns |     516 B |
| &#39;Strategy.Apply: TwoPhaseSet&#39;     |   448.31 ns |  64.259 ns |  94.190 ns |   416.00 ns |     535 B |
| &#39;Strategy.Apply: LwwSet&#39;          | 1,672.70 ns | 264.490 ns | 395.876 ns | 1,524.50 ns |    2887 B |
| &#39;Strategy.Apply: FwwSet&#39;          | 1,697.11 ns | 177.582 ns | 254.683 ns | 1,764.50 ns |    2567 B |
| &#39;Strategy.Apply: OrSet&#39;           | 1,603.72 ns | 300.135 ns | 439.934 ns | 1,365.00 ns |    3127 B |
| &#39;Strategy.Apply: ArrayLcs&#39;        |   956.83 ns | 137.222 ns | 201.139 ns | 1,088.00 ns |     364 B |
| &#39;Strategy.Apply: FixedSizeArray&#39;  |   486.89 ns |  40.317 ns |  57.822 ns |   478.00 ns |     315 B |
| &#39;Strategy.Apply: Lseq&#39;            |   881.31 ns | 301.806 ns | 442.383 ns | 1,173.00 ns |     327 B |
| &#39;Strategy.Apply: VoteCounter&#39;     |   724.40 ns | 154.410 ns | 231.114 ns |   600.00 ns |     527 B |
| &#39;Strategy.Apply: StateMachine&#39;    |   611.33 ns |  40.903 ns |  61.221 ns |   590.50 ns |     271 B |
| &#39;Strategy.Apply: PriorityQueue&#39;   | 5,870.50 ns | 466.267 ns | 668.706 ns | 5,549.00 ns |    8679 B |
| &#39;Strategy.Apply: SortedSet&#39;       | 3,372.57 ns | 388.734 ns | 557.511 ns | 3,043.50 ns |    4503 B |
| &#39;Strategy.Apply: RGA&#39;             | 1,870.41 ns | 483.782 ns | 678.195 ns | 1,501.00 ns |    2983 B |
| &#39;Strategy.Apply: CounterMap&#39;      | 1,093.79 ns |  70.525 ns | 103.375 ns | 1,053.00 ns |    1663 B |
| &#39;Strategy.Apply: LwwMap&#39;          |   964.70 ns |  81.755 ns | 122.366 ns |   925.50 ns |    1511 B |
| &#39;Strategy.Apply: FwwMap&#39;          |   663.79 ns |  41.905 ns |  60.098 ns |   661.00 ns |    1191 B |
| &#39;Strategy.Apply: MaxWinsMap&#39;      |   624.79 ns |  43.087 ns |  61.794 ns |   605.00 ns |     391 B |
| &#39;Strategy.Apply: MinWinsMap&#39;      |   148.04 ns |  11.084 ns |  15.897 ns |   145.00 ns |     364 B |
| &#39;Strategy.Apply: OrMap&#39;           | 1,747.71 ns | 119.472 ns | 175.120 ns | 1,705.00 ns |    2292 B |
| &#39;Strategy.Apply: Graph&#39;           |    84.74 ns |   6.679 ns |   9.363 ns |    83.00 ns |     152 B |
| &#39;Strategy.Apply: TwoPhaseGraph&#39;   |   417.27 ns |  37.820 ns |  56.607 ns |   405.50 ns |     367 B |
| &#39;Strategy.Apply: ReplicatedTree&#39;  |   519.53 ns |  60.936 ns |  91.207 ns |   504.00 ns |     607 B |
| &#39;Strategy.Apply: EpochBound&#39;      | 1,746.71 ns |  15.839 ns |  22.715 ns | 1,750.00 ns |     271 B |
| &#39;Strategy.Apply: ApprovalQuorum&#39;  |    91.63 ns |  11.142 ns |  16.676 ns |    92.00 ns |     152 B |


```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9974)
Intel Core i9-10850K CPU 3.60GHz, 1 CPU, 20 logical and 10 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3 [AttachedDebugger]

Job=MediumRun  Toolchain=InProcessNoEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
| Method                               | Mean       | Error    | StdDev   | Median     | Gen0   | Allocated |
|------------------------------------- |-----------:|---------:|---------:|-----------:|-------:|----------:|
| &#39;Strategy.Generate: LWW&#39;             |   124.0 ns |  0.50 ns |  0.75 ns |   124.1 ns | 0.0143 |     152 B |
| &#39;Strategy.Generate: FWW&#39;             |   128.9 ns |  0.74 ns |  1.09 ns |   128.8 ns | 0.0143 |     152 B |
| &#39;Strategy.Generate: Counter&#39;         |   153.5 ns |  1.23 ns |  1.84 ns |   153.3 ns | 0.0329 |     344 B |
| &#39;Strategy.Generate: GCounter&#39;        |   196.4 ns |  4.54 ns |  6.79 ns |   193.3 ns | 0.0329 |     344 B |
| &#39;Strategy.Generate: BoundedCounter&#39;  |   151.1 ns |  0.74 ns |  1.08 ns |   151.0 ns | 0.0329 |     344 B |
| &#39;Strategy.Generate: MaxWins&#39;         |   114.9 ns |  1.20 ns |  1.80 ns |   114.5 ns | 0.0191 |     200 B |
| &#39;Strategy.Generate: MinWins&#39;         |   116.4 ns |  1.37 ns |  1.92 ns |   116.6 ns | 0.0191 |     200 B |
| &#39;Strategy.Generate: AverageRegister&#39; |   115.4 ns |  1.30 ns |  1.86 ns |   115.0 ns | 0.0206 |     216 B |
| &#39;Strategy.Generate: GSet&#39;            |   365.1 ns |  3.50 ns |  5.13 ns |   364.6 ns | 0.0763 |     800 B |
| &#39;Strategy.Generate: TwoPhaseSet&#39;     |   682.4 ns |  6.88 ns |  9.86 ns |   681.0 ns | 0.1574 |    1648 B |
| &#39;Strategy.Generate: LwwSet&#39;          |   475.5 ns |  6.02 ns |  8.64 ns |   476.1 ns | 0.1192 |    1248 B |
| &#39;Strategy.Generate: FwwSet&#39;          |   474.8 ns |  6.18 ns |  8.47 ns |   473.8 ns | 0.1192 |    1248 B |
| &#39;Strategy.Generate: OrSet&#39;           | 1,012.5 ns |  8.85 ns | 12.41 ns | 1,011.7 ns | 0.2060 |    2160 B |
| &#39;Strategy.Generate: ArrayLcs&#39;        | 1,402.3 ns | 13.52 ns | 20.23 ns | 1,398.6 ns | 0.3109 |    3264 B |
| &#39;Strategy.Generate: FixedSizeArray&#39;  |   381.3 ns |  2.42 ns |  3.55 ns |   381.3 ns | 0.0596 |     624 B |
| &#39;Strategy.Generate: Lseq&#39;            |   362.5 ns |  3.17 ns |  4.65 ns |   363.0 ns | 0.0839 |     880 B |
| &#39;Strategy.Generate: VoteCounter&#39;     |   445.5 ns |  4.94 ns |  7.09 ns |   445.3 ns | 0.1044 |    1096 B |
| &#39;Strategy.Generate: StateMachine&#39;    |   170.6 ns |  2.38 ns |  3.48 ns |   168.7 ns | 0.0143 |     152 B |
| &#39;Strategy.Generate: PriorityQueue&#39;   | 4,967.9 ns | 44.78 ns | 67.03 ns | 4,952.8 ns | 0.7401 |    7800 B |
| &#39;Strategy.Generate: SortedSet&#39;       | 1,825.7 ns | 17.54 ns | 25.72 ns | 1,832.1 ns | 0.3395 |    3560 B |
| &#39;Strategy.Generate: RGA&#39;             |   913.3 ns | 10.23 ns | 15.32 ns |   911.8 ns | 0.2518 |    2640 B |
| &#39;Strategy.Generate: CounterMap&#39;      | 1,015.7 ns |  9.61 ns | 14.38 ns | 1,014.1 ns | 0.2823 |    2952 B |
| &#39;Strategy.Generate: LwwMap&#39;          | 1,181.9 ns | 17.00 ns | 25.45 ns | 1,180.1 ns | 0.2594 |    2728 B |
| &#39;Strategy.Generate: FwwMap&#39;          | 1,021.4 ns |  8.41 ns | 11.80 ns | 1,020.1 ns | 0.2499 |    2632 B |
| &#39;Strategy.Generate: MaxWinsMap&#39;      |   598.3 ns |  2.75 ns |  3.85 ns |   597.7 ns | 0.1478 |    1552 B |
| &#39;Strategy.Generate: MinWinsMap&#39;      |   615.4 ns | 15.90 ns | 22.81 ns |   608.3 ns | 0.1478 |    1552 B |
| &#39;Strategy.Generate: OrMap&#39;           | 1,381.0 ns | 14.04 ns | 20.57 ns | 1,378.5 ns | 0.2956 |    3104 B |
| &#39;Strategy.Generate: Graph&#39;           |   395.1 ns |  2.36 ns |  3.30 ns |   395.4 ns | 0.0968 |    1016 B |
| &#39;Strategy.Generate: TwoPhaseGraph&#39;   |   433.7 ns |  3.58 ns |  5.36 ns |   433.4 ns | 0.1245 |    1304 B |
| &#39;Strategy.Generate: ReplicatedTree&#39;  |   563.2 ns |  4.73 ns |  6.93 ns |   561.4 ns | 0.1087 |    1144 B |
| &#39;Strategy.Generate: EpochBound&#39;      |   730.8 ns |  2.39 ns |  3.35 ns |   730.1 ns | 0.0820 |     864 B |
| &#39;Strategy.Generate: ApprovalQuorum&#39;  |   426.0 ns |  3.88 ns |  5.44 ns |   426.7 ns | 0.0763 |     800 B |


```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9974)
Intel Core i9-10850K CPU 3.60GHz, 1 CPU, 20 logical and 10 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3 [AttachedDebugger]

Job=MediumRun  Toolchain=InProcessNoEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
| Method                                 | Mean      | Error    | StdDev   | Median    | Gen0   | Allocated |
|--------------------------------------- |----------:|---------:|---------:|----------:|-------:|----------:|
| &#39;Strategy.GenerateOp: LWW&#39;             |  54.26 ns | 0.093 ns | 0.134 ns |  54.24 ns |      - |         - |
| &#39;Strategy.GenerateOp: FWW&#39;             |  54.64 ns | 0.285 ns | 0.418 ns |  54.42 ns |      - |         - |
| &#39;Strategy.GenerateOp: Counter&#39;         |  76.76 ns | 0.360 ns | 0.527 ns |  76.75 ns | 0.0084 |      88 B |
| &#39;Strategy.GenerateOp: GCounter&#39;        | 106.17 ns | 0.819 ns | 1.175 ns | 105.83 ns | 0.0084 |      88 B |
| &#39;Strategy.GenerateOp: BoundedCounter&#39;  |  55.43 ns | 1.204 ns | 1.802 ns |  54.27 ns |      - |         - |
| &#39;Strategy.GenerateOp: MaxWins&#39;         |  54.47 ns | 0.181 ns | 0.270 ns |  54.38 ns |      - |         - |
| &#39;Strategy.GenerateOp: MinWins&#39;         |  54.66 ns | 0.147 ns | 0.216 ns |  54.58 ns |      - |         - |
| &#39;Strategy.GenerateOp: AverageRegister&#39; |  54.36 ns | 0.057 ns | 0.080 ns |  54.36 ns |      - |         - |
| &#39;Strategy.GenerateOp: GSet&#39;            |  84.71 ns | 0.839 ns | 1.177 ns |  84.03 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: TwoPhaseSet&#39;     |  54.06 ns | 0.054 ns | 0.077 ns |  54.05 ns |      - |         - |
| &#39;Strategy.GenerateOp: LwwSet&#39;          |  54.70 ns | 0.239 ns | 0.358 ns |  54.54 ns |      - |         - |
| &#39;Strategy.GenerateOp: FwwSet&#39;          |  54.52 ns | 0.267 ns | 0.383 ns |  54.52 ns |      - |         - |
| &#39;Strategy.GenerateOp: OrSet&#39;           | 147.09 ns | 1.147 ns | 1.716 ns | 146.43 ns | 0.0069 |      72 B |
| &#39;Strategy.GenerateOp: ArrayLcs&#39;        | 398.72 ns | 0.967 ns | 1.448 ns | 398.70 ns | 0.0201 |     216 B |
| &#39;Strategy.GenerateOp: FixedSizeArray&#39;  |  97.56 ns | 0.639 ns | 0.916 ns |  97.14 ns | 0.0053 |      56 B |
| &#39;Strategy.GenerateOp: Lseq&#39;            | 117.10 ns | 0.438 ns | 0.614 ns | 116.98 ns | 0.0107 |     112 B |
| &#39;Strategy.GenerateOp: VoteCounter&#39;     |  59.27 ns | 0.207 ns | 0.276 ns |  59.20 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: StateMachine&#39;    | 231.72 ns | 0.716 ns | 1.026 ns | 231.60 ns |      - |         - |
| &#39;Strategy.GenerateOp: PriorityQueue&#39;   |  83.59 ns | 0.411 ns | 0.590 ns |  83.47 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: SortedSet&#39;       |  62.16 ns | 0.360 ns | 0.539 ns |  62.21 ns | 0.0053 |      56 B |
| &#39;Strategy.GenerateOp: RGA&#39;             | 277.75 ns | 5.135 ns | 7.199 ns | 280.04 ns | 0.0580 |     608 B |
| &#39;Strategy.GenerateOp: CounterMap&#39;      |  59.79 ns | 0.153 ns | 0.214 ns |  59.76 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: LwwMap&#39;          |  59.50 ns | 0.100 ns | 0.143 ns |  59.49 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: FwwMap&#39;          |  59.54 ns | 0.175 ns | 0.251 ns |  59.44 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: MaxWinsMap&#39;      |  59.41 ns | 0.131 ns | 0.192 ns |  59.35 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: MinWinsMap&#39;      |  59.65 ns | 0.108 ns | 0.158 ns |  59.61 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: OrMap&#39;           | 174.51 ns | 1.170 ns | 1.752 ns | 174.36 ns | 0.0107 |     112 B |
| &#39;Strategy.GenerateOp: Graph&#39;           |  59.10 ns | 0.846 ns | 1.267 ns |  58.53 ns | 0.0023 |      24 B |
| &#39;Strategy.GenerateOp: TwoPhaseGraph&#39;   |  80.90 ns | 0.372 ns | 0.497 ns |  80.73 ns | 0.0023 |      24 B |
| &#39;Strategy.GenerateOp: ReplicatedTree&#39;  | 137.83 ns | 0.713 ns | 1.067 ns | 138.07 ns | 0.0053 |      56 B |
| &#39;Strategy.GenerateOp: EpochBound&#39;      | 561.84 ns | 2.926 ns | 4.101 ns | 562.15 ns | 0.0140 |     152 B |
| &#39;Strategy.GenerateOp: EpochClear&#39;      | 349.26 ns | 2.094 ns | 3.003 ns | 348.02 ns | 0.0079 |      88 B |
| &#39;Strategy.GenerateOp: ApprovalQuorum&#39;  | 280.85 ns | 1.268 ns | 1.778 ns | 280.22 ns | 0.0082 |      88 B |


<!-- BENCHMARKS_END -->