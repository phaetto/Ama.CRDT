# Ama.CRDT.Benchmarks

These benchmarks test the individual CRDT strategy application, patch generation, and other performance metrics.

<!-- BENCHMARKS_START -->
```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
Intel Core i9-10850K CPU 3.60GHz, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.201
  [Host] : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3

Job=MediumRun  Toolchain=InProcessNoEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
| Method            | Mean       | Error    | StdDev   | Gen0   | Allocated |
|------------------ |-----------:|---------:|---------:|-------:|----------:|
| ApplyPatchSimple  |   303.6 ns |  1.78 ns |  2.56 ns | 0.0772 |     808 B |
| ApplyPatchComplex | 1,291.8 ns | 12.85 ns | 19.24 ns | 0.2937 |    3088 B |


```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
Intel Core i9-10850K CPU 3.60GHz, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.201
  [Host] : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3

Job=MediumRun  Toolchain=InProcessNoEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
| Method               | Mean       | Error    | StdDev   | Gen0   | Gen1   | Allocated |
|--------------------- |-----------:|---------:|---------:|-------:|-------:|----------:|
| GeneratePatchSimple  |   685.8 ns |  4.68 ns |  6.86 ns | 0.1640 |      - |   1.68 KB |
| GeneratePatchComplex | 3,084.8 ns | 12.86 ns | 18.44 ns | 0.5836 | 0.0038 |   5.99 KB |


```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
Intel Core i9-10850K CPU 3.60GHz, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.201
  [Host] : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3

Job=MediumRun  Toolchain=InProcessNoEmitToolchain  InvocationCount=1  
IterationCount=15  LaunchCount=2  UnrollFactor=1  
WarmupCount=10  

```
| Method                            | Mean        | Error      | StdDev     | Median      | Allocated |
|---------------------------------- |------------:|-----------:|-----------:|------------:|----------:|
| &#39;Strategy.Apply: LWW&#39;             |   375.55 ns |  35.565 ns |  52.130 ns |   382.00 ns |     224 B |
| &#39;Strategy.Apply: FWW&#39;             |    19.35 ns |   2.808 ns |   3.937 ns |    18.00 ns |         - |
| &#39;Strategy.Apply: Counter&#39;         |   374.57 ns |  30.807 ns |  44.183 ns |   367.50 ns |     488 B |
| &#39;Strategy.Apply: GCounter&#39;        |   443.34 ns |  22.150 ns |  32.467 ns |   434.00 ns |     541 B |
| &#39;Strategy.Apply: BoundedCounter&#39;  |    80.52 ns |   7.041 ns |   9.870 ns |    78.00 ns |     128 B |
| &#39;Strategy.Apply: MaxWins&#39;         |    82.36 ns |   8.468 ns |  12.145 ns |    81.00 ns |     128 B |
| &#39;Strategy.Apply: MinWins&#39;         |    81.62 ns |   8.411 ns |  11.514 ns |    83.00 ns |     128 B |
| &#39;Strategy.Apply: AverageRegister&#39; |   583.10 ns | 147.061 ns | 215.560 ns |   454.00 ns |    1061 B |
| &#39;Strategy.Apply: GSet&#39;            |   550.19 ns |  88.891 ns | 124.613 ns |   483.00 ns |     723 B |
| &#39;Strategy.Apply: TwoPhaseSet&#39;     |   480.80 ns |  63.863 ns |  91.591 ns |   435.00 ns |     909 B |
| &#39;Strategy.Apply: LwwSet&#39;          | 1,451.83 ns | 239.580 ns | 351.173 ns | 1,232.00 ns |    2277 B |
| &#39;Strategy.Apply: FwwSet&#39;          | 1,265.96 ns | 218.693 ns | 291.949 ns | 1,459.00 ns |    1883 B |
| &#39;Strategy.Apply: OrSet&#39;           | 1,439.90 ns | 294.237 ns | 431.288 ns | 1,207.00 ns |    2789 B |
| &#39;Strategy.Apply: ArrayLcs&#39;        |   966.20 ns | 146.882 ns | 219.847 ns |   961.50 ns |     533 B |
| &#39;Strategy.Apply: FixedSizeArray&#39;  |   465.53 ns |  62.450 ns |  93.472 ns |   415.00 ns |     429 B |
| &#39;Strategy.Apply: Lseq&#39;            |   849.82 ns | 281.359 ns | 403.517 ns |   804.50 ns |     368 B |
| &#39;Strategy.Apply: VoteCounter&#39;     |   685.50 ns | 129.442 ns | 193.743 ns |   578.00 ns |     589 B |
| &#39;Strategy.Apply: StateMachine&#39;    |   652.32 ns |  58.836 ns |  84.381 ns |   609.50 ns |     442 B |
| &#39;Strategy.Apply: PriorityQueue&#39;   | 5,946.79 ns | 379.130 ns | 543.737 ns | 5,675.00 ns |   10120 B |
| &#39;Strategy.Apply: SortedSet&#39;       | 4,699.50 ns | 315.432 ns | 452.384 ns | 4,412.50 ns |    7101 B |
| &#39;Strategy.Apply: RGA&#39;             | 2,219.62 ns | 646.353 ns | 947.416 ns | 1,990.00 ns |    3138 B |
| &#39;Strategy.Apply: CounterMap&#39;      |   984.20 ns |  73.036 ns | 109.316 ns |   934.00 ns |    1333 B |
| &#39;Strategy.Apply: LwwMap&#39;          |   810.70 ns |  74.929 ns | 112.150 ns |   763.50 ns |     997 B |
| &#39;Strategy.Apply: FwwMap&#39;          |   428.86 ns |  29.807 ns |  42.748 ns |   407.50 ns |     455 B |
| &#39;Strategy.Apply: MaxWinsMap&#39;      |   693.93 ns |  38.801 ns |  55.648 ns |   670.50 ns |     669 B |
| &#39;Strategy.Apply: MinWinsMap&#39;      |   133.70 ns |   9.556 ns |  13.397 ns |   130.00 ns |     306 B |
| &#39;Strategy.Apply: OrMap&#39;           | 1,529.34 ns |  81.352 ns | 119.245 ns | 1,510.00 ns |    2253 B |
| &#39;Strategy.Apply: Graph&#39;           |    80.14 ns |   7.459 ns |  10.934 ns |    76.00 ns |     128 B |
| &#39;Strategy.Apply: TwoPhaseGraph&#39;   |   441.50 ns |  34.490 ns |  51.622 ns |   420.00 ns |     573 B |
| &#39;Strategy.Apply: ReplicatedTree&#39;  |   501.50 ns |  59.860 ns |  85.850 ns |   459.00 ns |     893 B |
| &#39;Strategy.Apply: EpochBound&#39;      |   359.67 ns |  12.400 ns |  18.559 ns |   360.50 ns |     293 B |
| &#39;Strategy.Apply: ApprovalQuorum&#39;  |    81.04 ns |  11.037 ns |  15.829 ns |    75.50 ns |     128 B |


```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
Intel Core i9-10850K CPU 3.60GHz, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.201
  [Host] : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3

Job=MediumRun  Toolchain=InProcessNoEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
| Method                               | Mean        | Error     | StdDev     | Median      | Gen0   | Allocated |
|------------------------------------- |------------:|----------:|-----------:|------------:|-------:|----------:|
| &#39;Strategy.Generate: LWW&#39;             |   117.02 ns |  0.640 ns |   0.938 ns |   116.77 ns | 0.0130 |     136 B |
| &#39;Strategy.Generate: FWW&#39;             |    41.85 ns |  0.339 ns |   0.507 ns |    41.74 ns | 0.0130 |     136 B |
| &#39;Strategy.Generate: Counter&#39;         |   147.74 ns |  1.869 ns |   2.797 ns |   148.37 ns | 0.0312 |     328 B |
| &#39;Strategy.Generate: GCounter&#39;        |   183.96 ns |  1.377 ns |   1.975 ns |   183.71 ns | 0.0312 |     328 B |
| &#39;Strategy.Generate: BoundedCounter&#39;  |   145.40 ns |  0.994 ns |   1.488 ns |   145.93 ns | 0.0312 |     328 B |
| &#39;Strategy.Generate: MaxWins&#39;         |   101.17 ns |  0.463 ns |   0.664 ns |   101.17 ns | 0.0175 |     184 B |
| &#39;Strategy.Generate: MinWins&#39;         |   103.34 ns |  1.699 ns |   2.542 ns |   103.14 ns | 0.0175 |     184 B |
| &#39;Strategy.Generate: AverageRegister&#39; |   107.47 ns |  3.357 ns |   4.815 ns |   105.23 ns | 0.0191 |     200 B |
| &#39;Strategy.Generate: GSet&#39;            |   346.09 ns |  3.192 ns |   4.578 ns |   345.83 ns | 0.0777 |     816 B |
| &#39;Strategy.Generate: TwoPhaseSet&#39;     |   721.78 ns | 71.565 ns | 100.325 ns |   661.70 ns | 0.1583 |    1664 B |
| &#39;Strategy.Generate: LwwSet&#39;          |   489.16 ns |  1.945 ns |   2.850 ns |   489.41 ns | 0.1202 |    1264 B |
| &#39;Strategy.Generate: FwwSet&#39;          |   477.88 ns | 17.690 ns |  25.929 ns |   469.25 ns | 0.1206 |    1264 B |
| &#39;Strategy.Generate: OrSet&#39;           |   972.47 ns |  5.422 ns |   8.115 ns |   971.66 ns | 0.2079 |    2192 B |
| &#39;Strategy.Generate: ArrayLcs&#39;        | 1,363.43 ns | 18.359 ns |  26.911 ns | 1,360.42 ns | 0.3128 |    3280 B |
| &#39;Strategy.Generate: FixedSizeArray&#39;  |   351.22 ns |  1.813 ns |   2.713 ns |   351.98 ns | 0.0577 |     608 B |
| &#39;Strategy.Generate: Lseq&#39;            |   340.03 ns |  3.592 ns |   5.265 ns |   339.47 ns | 0.0854 |     896 B |
| &#39;Strategy.Generate: VoteCounter&#39;     |   420.07 ns |  2.783 ns |   4.165 ns |   420.80 ns | 0.1030 |    1080 B |
| &#39;Strategy.Generate: StateMachine&#39;    |   159.58 ns |  3.615 ns |   5.410 ns |   160.20 ns | 0.0129 |     136 B |
| &#39;Strategy.Generate: PriorityQueue&#39;   | 4,402.72 ns | 19.623 ns |  28.763 ns | 4,404.91 ns | 0.7706 |    8096 B |
| &#39;Strategy.Generate: SortedSet&#39;       | 1,658.18 ns | 10.317 ns |  14.796 ns | 1,655.30 ns | 0.3223 |    3384 B |
| &#39;Strategy.Generate: RGA&#39;             |   834.37 ns | 23.051 ns |  33.789 ns |   828.39 ns | 0.2537 |    2656 B |
| &#39;Strategy.Generate: CounterMap&#39;      |   959.65 ns |  8.307 ns |  12.433 ns |   957.05 ns | 0.2823 |    2968 B |
| &#39;Strategy.Generate: LwwMap&#39;          | 1,177.33 ns | 29.983 ns |  44.878 ns | 1,167.90 ns | 0.2613 |    2744 B |
| &#39;Strategy.Generate: FwwMap&#39;          |   997.21 ns |  4.632 ns |   6.340 ns |   996.77 ns | 0.2575 |    2712 B |
| &#39;Strategy.Generate: MaxWinsMap&#39;      |   569.53 ns |  3.614 ns |   5.297 ns |   567.99 ns | 0.1497 |    1568 B |
| &#39;Strategy.Generate: MinWinsMap&#39;      |   579.45 ns |  2.095 ns |   2.936 ns |   579.65 ns | 0.1497 |    1568 B |
| &#39;Strategy.Generate: OrMap&#39;           | 1,379.13 ns |  7.142 ns |  10.469 ns | 1,376.71 ns | 0.3014 |    3152 B |
| &#39;Strategy.Generate: Graph&#39;           |   375.16 ns |  1.906 ns |   2.734 ns |   375.14 ns | 0.0863 |     904 B |
| &#39;Strategy.Generate: TwoPhaseGraph&#39;   |   409.81 ns |  3.323 ns |   4.870 ns |   409.55 ns | 0.1044 |    1096 B |
| &#39;Strategy.Generate: ReplicatedTree&#39;  |   530.89 ns |  3.201 ns |   4.692 ns |   529.94 ns | 0.1078 |    1128 B |
| &#39;Strategy.Generate: EpochBound&#39;      |   461.82 ns |  2.968 ns |   4.443 ns |   461.59 ns | 0.0753 |     792 B |
| &#39;Strategy.Generate: ApprovalQuorum&#39;  |   438.13 ns |  2.196 ns |   3.149 ns |   438.72 ns | 0.0749 |     784 B |


```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
Intel Core i9-10850K CPU 3.60GHz, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.201
  [Host] : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3

Job=MediumRun  Toolchain=InProcessNoEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
| Method                                 | Mean      | Error    | StdDev   | Median    | Gen0   | Allocated |
|--------------------------------------- |----------:|---------:|---------:|----------:|-------:|----------:|
| &#39;Strategy.GenerateOp: LWW&#39;             |  55.24 ns | 1.490 ns | 2.230 ns |  54.11 ns |      - |         - |
| &#39;Strategy.GenerateOp: FWW&#39;             |  54.20 ns | 0.152 ns | 0.223 ns |  54.21 ns |      - |         - |
| &#39;Strategy.GenerateOp: Counter&#39;         |  78.04 ns | 1.424 ns | 2.087 ns |  77.47 ns | 0.0084 |      88 B |
| &#39;Strategy.GenerateOp: GCounter&#39;        | 101.92 ns | 1.794 ns | 2.685 ns | 101.70 ns | 0.0084 |      88 B |
| &#39;Strategy.GenerateOp: BoundedCounter&#39;  |  54.71 ns | 0.701 ns | 1.049 ns |  54.03 ns |      - |         - |
| &#39;Strategy.GenerateOp: MaxWins&#39;         |  53.97 ns | 0.064 ns | 0.092 ns |  53.97 ns |      - |         - |
| &#39;Strategy.GenerateOp: MinWins&#39;         |  54.45 ns | 0.148 ns | 0.213 ns |  54.45 ns |      - |         - |
| &#39;Strategy.GenerateOp: AverageRegister&#39; |  53.84 ns | 0.071 ns | 0.106 ns |  53.83 ns |      - |         - |
| &#39;Strategy.GenerateOp: GSet&#39;            |  80.57 ns | 0.554 ns | 0.795 ns |  80.42 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: TwoPhaseSet&#39;     |  55.93 ns | 0.048 ns | 0.069 ns |  55.94 ns |      - |         - |
| &#39;Strategy.GenerateOp: LwwSet&#39;          |  56.18 ns | 0.179 ns | 0.268 ns |  56.13 ns |      - |         - |
| &#39;Strategy.GenerateOp: FwwSet&#39;          |  56.68 ns | 0.319 ns | 0.457 ns |  56.65 ns |      - |         - |
| &#39;Strategy.GenerateOp: OrSet&#39;           | 149.51 ns | 1.854 ns | 2.718 ns | 148.70 ns | 0.0069 |      72 B |
| &#39;Strategy.GenerateOp: ArrayLcs&#39;        | 413.34 ns | 1.081 ns | 1.619 ns | 413.45 ns | 0.0293 |     312 B |
| &#39;Strategy.GenerateOp: FixedSizeArray&#39;  |  93.86 ns | 0.699 ns | 1.047 ns |  93.48 ns | 0.0053 |      56 B |
| &#39;Strategy.GenerateOp: Lseq&#39;            | 103.87 ns | 0.791 ns | 1.184 ns | 103.65 ns | 0.0107 |     112 B |
| &#39;Strategy.GenerateOp: VoteCounter&#39;     |  59.02 ns | 0.222 ns | 0.325 ns |  59.06 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: StateMachine&#39;    | 246.53 ns | 2.097 ns | 3.138 ns | 245.89 ns | 0.0082 |      88 B |
| &#39;Strategy.GenerateOp: PriorityQueue&#39;   |  84.77 ns | 0.234 ns | 0.350 ns |  84.74 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: SortedSet&#39;       |  62.01 ns | 0.523 ns | 0.751 ns |  62.14 ns | 0.0053 |      56 B |
| &#39;Strategy.GenerateOp: RGA&#39;             | 237.30 ns | 2.955 ns | 4.331 ns | 236.06 ns | 0.0580 |     608 B |
| &#39;Strategy.GenerateOp: CounterMap&#39;      |  61.46 ns | 0.214 ns | 0.314 ns |  61.53 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: LwwMap&#39;          |  60.80 ns | 0.184 ns | 0.270 ns |  60.71 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: FwwMap&#39;          |  68.45 ns | 0.219 ns | 0.320 ns |  68.42 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: MaxWinsMap&#39;      |  59.55 ns | 0.275 ns | 0.411 ns |  59.33 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: MinWinsMap&#39;      |  59.42 ns | 0.162 ns | 0.238 ns |  59.36 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: OrMap&#39;           | 167.98 ns | 1.374 ns | 2.014 ns | 168.53 ns | 0.0107 |     112 B |
| &#39;Strategy.GenerateOp: Graph&#39;           |  58.32 ns | 0.098 ns | 0.140 ns |  58.34 ns | 0.0023 |      24 B |
| &#39;Strategy.GenerateOp: TwoPhaseGraph&#39;   |  67.96 ns | 0.406 ns | 0.595 ns |  67.87 ns | 0.0023 |      24 B |
| &#39;Strategy.GenerateOp: ReplicatedTree&#39;  | 126.89 ns | 0.358 ns | 0.514 ns | 126.86 ns | 0.0053 |      56 B |
| &#39;Strategy.GenerateOp: EpochBound&#39;      | 331.06 ns | 2.430 ns | 3.637 ns | 330.08 ns | 0.0195 |     208 B |
| &#39;Strategy.GenerateOp: EpochClear&#39;      |  70.61 ns | 0.356 ns | 0.499 ns |  70.39 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: ApprovalQuorum&#39;  | 322.87 ns | 2.108 ns | 3.155 ns | 321.42 ns | 0.0189 |     200 B |


<!-- BENCHMARKS_END -->