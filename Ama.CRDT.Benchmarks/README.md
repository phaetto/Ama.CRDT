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
| Method            | Mean       | Error   | StdDev  | Gen0   | Allocated |
|------------------ |-----------:|--------:|--------:|-------:|----------:|
| ApplyPatchSimple  |   311.4 ns | 3.49 ns | 5.23 ns | 0.0648 |     680 B |
| ApplyPatchComplex | 1,269.9 ns | 6.84 ns | 9.81 ns | 0.2575 |    2704 B |


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
| GeneratePatchSimple  |   669.3 ns |  2.55 ns |  3.66 ns | 0.1640 |      - |   1.68 KB |
| GeneratePatchComplex | 3,074.5 ns | 26.31 ns | 39.37 ns | 0.5836 | 0.0038 |   5.99 KB |


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
| &#39;Strategy.Apply: LWW&#39;             |   425.42 ns |  32.942 ns |  45.091 ns |   427.00 ns |     258 B |
| &#39;Strategy.Apply: FWW&#39;             |    22.05 ns |   3.610 ns |   5.177 ns |    21.75 ns |         - |
| &#39;Strategy.Apply: Counter&#39;         |   366.95 ns |  39.907 ns |  59.731 ns |   338.50 ns |     491 B |
| &#39;Strategy.Apply: GCounter&#39;        |   453.62 ns |  21.471 ns |  31.472 ns |   448.00 ns |     541 B |
| &#39;Strategy.Apply: BoundedCounter&#39;  |    88.41 ns |   6.595 ns |   9.246 ns |    86.00 ns |     168 B |
| &#39;Strategy.Apply: MaxWins&#39;         |    78.35 ns |   6.705 ns |   9.178 ns |    77.00 ns |     128 B |
| &#39;Strategy.Apply: MinWins&#39;         |    80.46 ns |   6.812 ns |   9.769 ns |    78.50 ns |     128 B |
| &#39;Strategy.Apply: AverageRegister&#39; |   590.83 ns | 183.881 ns | 275.224 ns |   440.50 ns |    1061 B |
| &#39;Strategy.Apply: GSet&#39;            |   610.79 ns | 112.212 ns | 160.931 ns |   559.00 ns |     733 B |
| &#39;Strategy.Apply: TwoPhaseSet&#39;     |   497.67 ns |  56.683 ns |  84.840 ns |   462.00 ns |     909 B |
| &#39;Strategy.Apply: LwwSet&#39;          | 1,398.13 ns | 222.913 ns | 333.646 ns | 1,203.00 ns |    2277 B |
| &#39;Strategy.Apply: FwwSet&#39;          | 1,253.89 ns | 185.531 ns | 266.084 ns | 1,441.00 ns |    1890 B |
| &#39;Strategy.Apply: OrSet&#39;           | 1,307.75 ns | 256.705 ns | 384.223 ns | 1,081.00 ns |    2789 B |
| &#39;Strategy.Apply: ArrayLcs&#39;        |   855.93 ns | 125.390 ns | 187.677 ns |   770.00 ns |     533 B |
| &#39;Strategy.Apply: FixedSizeArray&#39;  |   450.69 ns |  47.633 ns |  69.819 ns |   413.00 ns |     419 B |
| &#39;Strategy.Apply: Lseq&#39;            |   841.28 ns | 273.799 ns | 401.331 ns | 1,099.00 ns |     421 B |
| &#39;Strategy.Apply: VoteCounter&#39;     |   696.23 ns | 110.552 ns | 165.469 ns |   610.50 ns |     586 B |
| &#39;Strategy.Apply: StateMachine&#39;    |   621.93 ns |  40.789 ns |  58.498 ns |   592.50 ns |     445 B |
| &#39;Strategy.Apply: PriorityQueue&#39;   | 6,250.41 ns | 406.313 ns | 595.568 ns | 6,038.00 ns |   10389 B |
| &#39;Strategy.Apply: SortedSet&#39;       | 4,815.93 ns | 418.758 ns | 600.570 ns | 4,493.50 ns |    7312 B |
| &#39;Strategy.Apply: RGA&#39;             | 2,185.93 ns | 631.810 ns | 945.663 ns | 1,751.00 ns |    3141 B |
| &#39;Strategy.Apply: CounterMap&#39;      |   952.17 ns |  64.804 ns |  94.989 ns |   920.00 ns |    1333 B |
| &#39;Strategy.Apply: LwwMap&#39;          |   818.18 ns |  65.473 ns |  93.900 ns |   791.50 ns |     997 B |
| &#39;Strategy.Apply: FwwMap&#39;          |   432.29 ns |  22.436 ns |  32.176 ns |   424.50 ns |     455 B |
| &#39;Strategy.Apply: MaxWinsMap&#39;      |   692.79 ns |  40.236 ns |  58.977 ns |   668.00 ns |     669 B |
| &#39;Strategy.Apply: MinWinsMap&#39;      |   134.04 ns |  10.553 ns |  15.135 ns |   129.00 ns |     256 B |
| &#39;Strategy.Apply: OrMap&#39;           | 1,426.62 ns |  23.102 ns |  33.863 ns | 1,421.00 ns |    2253 B |
| &#39;Strategy.Apply: Graph&#39;           |    73.73 ns |   3.055 ns |   4.181 ns |    73.00 ns |     175 B |
| &#39;Strategy.Apply: TwoPhaseGraph&#39;   |   436.00 ns |  29.522 ns |  43.272 ns |   426.00 ns |     573 B |
| &#39;Strategy.Apply: ReplicatedTree&#39;  |   506.00 ns |  43.472 ns |  63.721 ns |   485.50 ns |     893 B |
| &#39;Strategy.Apply: EpochBound&#39;      |   349.04 ns |   7.707 ns |  11.054 ns |   348.50 ns |     240 B |
| &#39;Strategy.Apply: ApprovalQuorum&#39;  |    81.56 ns |   6.137 ns |   8.604 ns |    80.00 ns |     128 B |


```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
Intel Core i9-10850K CPU 3.60GHz, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.201
  [Host] : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3

Job=MediumRun  Toolchain=InProcessNoEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
| Method                               | Mean        | Error     | StdDev    | Gen0   | Allocated |
|------------------------------------- |------------:|----------:|----------:|-------:|----------:|
| &#39;Strategy.Generate: LWW&#39;             |   118.27 ns |  0.905 ns |  1.327 ns | 0.0129 |     136 B |
| &#39;Strategy.Generate: FWW&#39;             |    42.89 ns |  0.530 ns |  0.777 ns | 0.0130 |     136 B |
| &#39;Strategy.Generate: Counter&#39;         |   148.78 ns |  1.756 ns |  2.628 ns | 0.0312 |     328 B |
| &#39;Strategy.Generate: GCounter&#39;        |   185.14 ns |  1.830 ns |  2.625 ns | 0.0312 |     328 B |
| &#39;Strategy.Generate: BoundedCounter&#39;  |   145.26 ns |  1.482 ns |  2.173 ns | 0.0312 |     328 B |
| &#39;Strategy.Generate: MaxWins&#39;         |   103.27 ns |  1.824 ns |  2.673 ns | 0.0175 |     184 B |
| &#39;Strategy.Generate: MinWins&#39;         |   101.45 ns |  0.773 ns |  1.157 ns | 0.0175 |     184 B |
| &#39;Strategy.Generate: AverageRegister&#39; |   103.57 ns |  0.973 ns |  1.396 ns | 0.0191 |     200 B |
| &#39;Strategy.Generate: GSet&#39;            |   354.26 ns |  2.428 ns |  3.634 ns | 0.0777 |     816 B |
| &#39;Strategy.Generate: TwoPhaseSet&#39;     |   629.44 ns |  6.868 ns | 10.279 ns | 0.1583 |    1664 B |
| &#39;Strategy.Generate: LwwSet&#39;          |   450.22 ns |  3.242 ns |  4.852 ns | 0.1206 |    1264 B |
| &#39;Strategy.Generate: FwwSet&#39;          |   444.16 ns |  1.907 ns |  2.796 ns | 0.1206 |    1264 B |
| &#39;Strategy.Generate: OrSet&#39;           | 1,013.64 ns |  5.295 ns |  7.423 ns | 0.2079 |    2192 B |
| &#39;Strategy.Generate: ArrayLcs&#39;        | 1,355.93 ns | 12.153 ns | 17.813 ns | 0.3128 |    3280 B |
| &#39;Strategy.Generate: FixedSizeArray&#39;  |   364.37 ns |  1.272 ns |  1.904 ns | 0.0577 |     608 B |
| &#39;Strategy.Generate: Lseq&#39;            |   345.35 ns |  5.409 ns |  8.096 ns | 0.0854 |     896 B |
| &#39;Strategy.Generate: VoteCounter&#39;     |   421.04 ns |  3.551 ns |  5.314 ns | 0.1030 |    1080 B |
| &#39;Strategy.Generate: StateMachine&#39;    |   162.32 ns |  2.487 ns |  3.566 ns | 0.0129 |     136 B |
| &#39;Strategy.Generate: PriorityQueue&#39;   | 4,974.79 ns | 35.726 ns | 53.473 ns | 0.8011 |    8416 B |
| &#39;Strategy.Generate: SortedSet&#39;       | 1,636.29 ns |  9.318 ns | 13.947 ns | 0.3223 |    3384 B |
| &#39;Strategy.Generate: RGA&#39;             |   826.11 ns |  8.409 ns | 12.325 ns | 0.2537 |    2656 B |
| &#39;Strategy.Generate: CounterMap&#39;      |   973.44 ns |  4.089 ns |  5.864 ns | 0.2823 |    2968 B |
| &#39;Strategy.Generate: LwwMap&#39;          | 1,228.52 ns |  9.232 ns | 13.818 ns | 0.2613 |    2744 B |
| &#39;Strategy.Generate: FwwMap&#39;          | 1,071.05 ns | 11.459 ns | 16.796 ns | 0.2575 |    2712 B |
| &#39;Strategy.Generate: MaxWinsMap&#39;      |   587.11 ns | 11.951 ns | 17.888 ns | 0.1497 |    1568 B |
| &#39;Strategy.Generate: MinWinsMap&#39;      |   581.11 ns |  2.572 ns |  3.606 ns | 0.1497 |    1568 B |
| &#39;Strategy.Generate: OrMap&#39;           | 1,477.33 ns | 13.073 ns | 18.749 ns | 0.3014 |    3152 B |
| &#39;Strategy.Generate: Graph&#39;           |   385.20 ns |  4.117 ns |  6.163 ns | 0.0863 |     904 B |
| &#39;Strategy.Generate: TwoPhaseGraph&#39;   |   396.22 ns |  2.434 ns |  3.568 ns | 0.1044 |    1096 B |
| &#39;Strategy.Generate: ReplicatedTree&#39;  |   566.85 ns |  3.034 ns |  4.447 ns | 0.1078 |    1128 B |
| &#39;Strategy.Generate: EpochBound&#39;      |   465.93 ns |  2.061 ns |  3.085 ns | 0.0753 |     792 B |
| &#39;Strategy.Generate: ApprovalQuorum&#39;  |   443.05 ns |  2.518 ns |  3.768 ns | 0.0749 |     784 B |


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
| &#39;Strategy.GenerateOp: LWW&#39;             |  54.45 ns | 0.139 ns | 0.195 ns |  54.39 ns |      - |         - |
| &#39;Strategy.GenerateOp: FWW&#39;             |  54.03 ns | 0.064 ns | 0.086 ns |  53.99 ns |      - |         - |
| &#39;Strategy.GenerateOp: Counter&#39;         |  76.51 ns | 0.448 ns | 0.642 ns |  76.46 ns | 0.0084 |      88 B |
| &#39;Strategy.GenerateOp: GCounter&#39;        |  99.75 ns | 0.757 ns | 1.133 ns |  99.35 ns | 0.0084 |      88 B |
| &#39;Strategy.GenerateOp: BoundedCounter&#39;  |  53.95 ns | 0.146 ns | 0.218 ns |  53.89 ns |      - |         - |
| &#39;Strategy.GenerateOp: MaxWins&#39;         |  54.14 ns | 0.112 ns | 0.164 ns |  54.12 ns |      - |         - |
| &#39;Strategy.GenerateOp: MinWins&#39;         |  56.06 ns | 1.086 ns | 1.558 ns |  56.87 ns |      - |         - |
| &#39;Strategy.GenerateOp: AverageRegister&#39; |  54.22 ns | 0.076 ns | 0.107 ns |  54.27 ns |      - |         - |
| &#39;Strategy.GenerateOp: GSet&#39;            |  79.49 ns | 0.108 ns | 0.155 ns |  79.54 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: TwoPhaseSet&#39;     |  56.05 ns | 0.212 ns | 0.317 ns |  55.92 ns |      - |         - |
| &#39;Strategy.GenerateOp: LwwSet&#39;          |  56.39 ns | 0.416 ns | 0.555 ns |  56.21 ns |      - |         - |
| &#39;Strategy.GenerateOp: FwwSet&#39;          |  56.03 ns | 0.123 ns | 0.169 ns |  56.06 ns |      - |         - |
| &#39;Strategy.GenerateOp: OrSet&#39;           | 149.67 ns | 1.469 ns | 2.106 ns | 148.68 ns | 0.0069 |      72 B |
| &#39;Strategy.GenerateOp: ArrayLcs&#39;        | 425.63 ns | 2.673 ns | 4.001 ns | 423.21 ns | 0.0293 |     312 B |
| &#39;Strategy.GenerateOp: FixedSizeArray&#39;  |  92.96 ns | 0.330 ns | 0.474 ns |  93.14 ns | 0.0053 |      56 B |
| &#39;Strategy.GenerateOp: Lseq&#39;            | 106.63 ns | 0.768 ns | 1.149 ns | 106.80 ns | 0.0107 |     112 B |
| &#39;Strategy.GenerateOp: VoteCounter&#39;     |  60.22 ns | 0.756 ns | 1.035 ns |  60.99 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: StateMachine&#39;    | 249.17 ns | 0.930 ns | 1.363 ns | 248.50 ns | 0.0082 |      88 B |
| &#39;Strategy.GenerateOp: PriorityQueue&#39;   |  82.31 ns | 0.500 ns | 0.717 ns |  82.75 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: SortedSet&#39;       |  61.96 ns | 0.145 ns | 0.207 ns |  61.93 ns | 0.0053 |      56 B |
| &#39;Strategy.GenerateOp: RGA&#39;             | 240.14 ns | 2.242 ns | 3.356 ns | 239.13 ns | 0.0580 |     608 B |
| &#39;Strategy.GenerateOp: CounterMap&#39;      |  60.09 ns | 0.534 ns | 0.783 ns |  60.31 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: LwwMap&#39;          |  60.93 ns | 0.159 ns | 0.238 ns |  60.86 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: FwwMap&#39;          |  68.31 ns | 0.403 ns | 0.578 ns |  68.46 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: MaxWinsMap&#39;      |  60.72 ns | 1.038 ns | 1.554 ns |  60.52 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: MinWinsMap&#39;      |  59.90 ns | 0.501 ns | 0.702 ns |  59.51 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: OrMap&#39;           | 166.98 ns | 1.648 ns | 2.363 ns | 167.37 ns | 0.0107 |     112 B |
| &#39;Strategy.GenerateOp: Graph&#39;           |  58.37 ns | 0.088 ns | 0.126 ns |  58.35 ns | 0.0023 |      24 B |
| &#39;Strategy.GenerateOp: TwoPhaseGraph&#39;   |  71.68 ns | 1.367 ns | 1.960 ns |  71.81 ns | 0.0023 |      24 B |
| &#39;Strategy.GenerateOp: ReplicatedTree&#39;  | 125.79 ns | 0.613 ns | 0.899 ns | 126.07 ns | 0.0053 |      56 B |
| &#39;Strategy.GenerateOp: EpochBound&#39;      | 329.62 ns | 1.751 ns | 2.397 ns | 329.85 ns | 0.0195 |     208 B |
| &#39;Strategy.GenerateOp: EpochClear&#39;      |  69.93 ns | 0.083 ns | 0.119 ns |  69.93 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: ApprovalQuorum&#39;  | 327.44 ns | 2.391 ns | 3.429 ns | 325.21 ns | 0.0250 |     264 B |


<!-- BENCHMARKS_END -->