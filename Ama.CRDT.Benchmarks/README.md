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
| ApplyPatchSimple  |   300.2 ns |  3.54 ns |  5.30 ns | 0.0772 |     808 B |
| ApplyPatchComplex | 1,296.6 ns | 17.44 ns | 25.56 ns | 0.2937 |    3088 B |


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
| GeneratePatchSimple  |   697.0 ns | 13.00 ns | 19.45 ns | 0.1640 |      - |   1.68 KB |
| GeneratePatchComplex | 3,118.3 ns | 23.63 ns | 35.36 ns | 0.5836 | 0.0038 |   5.99 KB |


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
| &#39;Strategy.Apply: LWW&#39;             |   431.80 ns |  28.174 ns |  42.169 ns |   419.00 ns |     317 B |
| &#39;Strategy.Apply: FWW&#39;             |   319.53 ns |  23.576 ns |  35.288 ns |   329.00 ns |     267 B |
| &#39;Strategy.Apply: Counter&#39;         |   380.80 ns |  60.297 ns |  90.250 ns |   338.50 ns |     541 B |
| &#39;Strategy.Apply: GCounter&#39;        |   469.23 ns |  27.846 ns |  41.678 ns |   453.50 ns |     538 B |
| &#39;Strategy.Apply: BoundedCounter&#39;  |    82.43 ns |   7.460 ns |  10.699 ns |    82.00 ns |     158 B |
| &#39;Strategy.Apply: MaxWins&#39;         |    79.41 ns |   6.378 ns |   8.941 ns |    77.00 ns |     128 B |
| &#39;Strategy.Apply: MinWins&#39;         |    92.38 ns |  11.380 ns |  16.681 ns |    93.00 ns |     128 B |
| &#39;Strategy.Apply: AverageRegister&#39; |   578.60 ns | 156.321 ns | 233.975 ns |   461.50 ns |     949 B |
| &#39;Strategy.Apply: GSet&#39;            |   569.23 ns |  94.503 ns | 141.448 ns |   492.50 ns |     720 B |
| &#39;Strategy.Apply: TwoPhaseSet&#39;     |   523.34 ns |  74.708 ns | 109.505 ns |   457.00 ns |     909 B |
| &#39;Strategy.Apply: LwwSet&#39;          | 1,531.91 ns | 286.741 ns | 411.235 ns | 1,271.25 ns |    2277 B |
| &#39;Strategy.Apply: FwwSet&#39;          | 1,335.41 ns | 180.055 ns | 263.922 ns | 1,208.00 ns |    1893 B |
| &#39;Strategy.Apply: OrSet&#39;           | 1,434.93 ns | 287.536 ns | 421.467 ns | 1,207.00 ns |    2786 B |
| &#39;Strategy.Apply: ArrayLcs&#39;        |   966.34 ns | 132.392 ns | 194.058 ns | 1,080.00 ns |     516 B |
| &#39;Strategy.Apply: FixedSizeArray&#39;  |   463.46 ns |  56.908 ns |  81.616 ns |   425.50 ns |     456 B |
| &#39;Strategy.Apply: Lseq&#39;            |   883.32 ns | 288.486 ns | 413.738 ns | 1,122.00 ns |     421 B |
| &#39;Strategy.Apply: VoteCounter&#39;     |   686.96 ns | 140.661 ns | 201.732 ns |   574.50 ns |     616 B |
| &#39;Strategy.Apply: StateMachine&#39;    |   627.43 ns |  43.766 ns |  65.506 ns |   594.50 ns |     485 B |
| &#39;Strategy.Apply: PriorityQueue&#39;   | 6,293.73 ns | 329.013 ns | 492.451 ns | 6,072.50 ns |   10248 B |
| &#39;Strategy.Apply: SortedSet&#39;       | 4,841.90 ns | 360.833 ns | 540.078 ns | 4,617.50 ns |    7101 B |
| &#39;Strategy.Apply: RGA&#39;             | 2,208.37 ns | 630.363 ns | 943.498 ns | 1,835.50 ns |    3183 B |
| &#39;Strategy.Apply: CounterMap&#39;      | 1,016.07 ns |  83.602 ns | 125.131 ns |   962.00 ns |    1333 B |
| &#39;Strategy.Apply: LwwMap&#39;          |   804.07 ns |  64.724 ns |  92.825 ns |   768.00 ns |     997 B |
| &#39;Strategy.Apply: FwwMap&#39;          |   477.60 ns |  35.537 ns |  53.190 ns |   475.00 ns |     452 B |
| &#39;Strategy.Apply: MaxWinsMap&#39;      |   748.41 ns |  62.827 ns |  92.091 ns |   710.00 ns |     669 B |
| &#39;Strategy.Apply: MinWinsMap&#39;      |   141.43 ns |  11.399 ns |  17.061 ns |   136.00 ns |     286 B |
| &#39;Strategy.Apply: OrMap&#39;           | 1,533.66 ns |  85.328 ns | 125.072 ns | 1,488.00 ns |    2293 B |
| &#39;Strategy.Apply: Graph&#39;           |    78.90 ns |   7.578 ns |  11.108 ns |    76.00 ns |     128 B |
| &#39;Strategy.Apply: TwoPhaseGraph&#39;   |   453.03 ns |  32.360 ns |  48.434 ns |   439.50 ns |     570 B |
| &#39;Strategy.Apply: ReplicatedTree&#39;  |   592.14 ns |  57.072 ns |  83.656 ns |   577.00 ns |     890 B |
| &#39;Strategy.Apply: EpochBound&#39;      | 1,852.63 ns |  96.116 ns | 134.741 ns | 1,796.00 ns |     349 B |
| &#39;Strategy.Apply: ApprovalQuorum&#39;  |    91.47 ns |  12.778 ns |  19.125 ns |    92.00 ns |     165 B |


```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
Intel Core i9-10850K CPU 3.60GHz, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.201
  [Host] : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3

Job=MediumRun  Toolchain=InProcessNoEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
| Method                               | Mean       | Error    | StdDev   | Median     | Gen0   | Allocated |
|------------------------------------- |-----------:|---------:|---------:|-----------:|-------:|----------:|
| &#39;Strategy.Generate: LWW&#39;             |   119.1 ns |  0.56 ns |  0.83 ns |   119.1 ns | 0.0129 |     136 B |
| &#39;Strategy.Generate: FWW&#39;             |   118.4 ns |  2.99 ns |  4.38 ns |   116.0 ns | 0.0130 |     136 B |
| &#39;Strategy.Generate: Counter&#39;         |   148.1 ns |  1.02 ns |  1.46 ns |   147.6 ns | 0.0312 |     328 B |
| &#39;Strategy.Generate: GCounter&#39;        |   188.7 ns |  2.38 ns |  3.41 ns |   188.3 ns | 0.0312 |     328 B |
| &#39;Strategy.Generate: BoundedCounter&#39;  |   147.1 ns |  0.52 ns |  0.78 ns |   147.1 ns | 0.0312 |     328 B |
| &#39;Strategy.Generate: MaxWins&#39;         |   104.9 ns |  1.06 ns |  1.55 ns |   104.4 ns | 0.0175 |     184 B |
| &#39;Strategy.Generate: MinWins&#39;         |   102.1 ns |  0.59 ns |  0.88 ns |   102.2 ns | 0.0175 |     184 B |
| &#39;Strategy.Generate: AverageRegister&#39; |   101.8 ns |  0.76 ns |  1.11 ns |   101.9 ns | 0.0191 |     200 B |
| &#39;Strategy.Generate: GSet&#39;            |   346.6 ns |  1.63 ns |  2.38 ns |   346.4 ns | 0.0777 |     816 B |
| &#39;Strategy.Generate: TwoPhaseSet&#39;     |   591.1 ns |  3.84 ns |  5.75 ns |   591.2 ns | 0.1583 |    1664 B |
| &#39;Strategy.Generate: LwwSet&#39;          |   443.0 ns |  3.07 ns |  4.59 ns |   441.8 ns | 0.1206 |    1264 B |
| &#39;Strategy.Generate: FwwSet&#39;          |   446.2 ns |  8.39 ns | 12.03 ns |   441.8 ns | 0.1206 |    1264 B |
| &#39;Strategy.Generate: OrSet&#39;           |   972.6 ns | 18.47 ns | 27.64 ns |   960.6 ns | 0.2079 |    2192 B |
| &#39;Strategy.Generate: ArrayLcs&#39;        | 1,347.3 ns | 14.25 ns | 20.89 ns | 1,342.7 ns | 0.3128 |    3280 B |
| &#39;Strategy.Generate: FixedSizeArray&#39;  |   382.5 ns |  1.56 ns |  2.29 ns |   382.4 ns | 0.0577 |     608 B |
| &#39;Strategy.Generate: Lseq&#39;            |   344.7 ns |  2.28 ns |  3.42 ns |   345.0 ns | 0.0854 |     896 B |
| &#39;Strategy.Generate: VoteCounter&#39;     |   416.8 ns |  3.05 ns |  4.47 ns |   418.0 ns | 0.1030 |    1080 B |
| &#39;Strategy.Generate: StateMachine&#39;    |   156.7 ns |  3.06 ns |  4.48 ns |   156.0 ns | 0.0129 |     136 B |
| &#39;Strategy.Generate: PriorityQueue&#39;   | 4,615.9 ns | 23.14 ns | 32.44 ns | 4,603.7 ns | 0.7706 |    8096 B |
| &#39;Strategy.Generate: SortedSet&#39;       | 1,675.0 ns | 16.35 ns | 24.47 ns | 1,669.1 ns | 0.3223 |    3384 B |
| &#39;Strategy.Generate: RGA&#39;             |   818.6 ns |  7.06 ns | 10.57 ns |   817.9 ns | 0.2537 |    2656 B |
| &#39;Strategy.Generate: CounterMap&#39;      |   966.8 ns |  6.44 ns |  9.64 ns |   962.6 ns | 0.2823 |    2968 B |
| &#39;Strategy.Generate: LwwMap&#39;          | 1,161.1 ns |  6.15 ns |  9.20 ns | 1,160.0 ns | 0.2613 |    2744 B |
| &#39;Strategy.Generate: FwwMap&#39;          | 1,045.6 ns |  9.17 ns | 13.44 ns | 1,043.9 ns | 0.2575 |    2712 B |
| &#39;Strategy.Generate: MaxWinsMap&#39;      |   590.1 ns |  5.32 ns |  7.63 ns |   591.0 ns | 0.1497 |    1568 B |
| &#39;Strategy.Generate: MinWinsMap&#39;      |   585.7 ns |  4.75 ns |  6.96 ns |   585.5 ns | 0.1497 |    1568 B |
| &#39;Strategy.Generate: OrMap&#39;           | 1,432.4 ns | 10.15 ns | 15.19 ns | 1,430.1 ns | 0.3014 |    3152 B |
| &#39;Strategy.Generate: Graph&#39;           |   381.2 ns |  3.76 ns |  5.63 ns |   378.9 ns | 0.0863 |     904 B |
| &#39;Strategy.Generate: TwoPhaseGraph&#39;   |   393.7 ns |  2.08 ns |  3.04 ns |   393.6 ns | 0.1044 |    1096 B |
| &#39;Strategy.Generate: ReplicatedTree&#39;  |   551.2 ns |  2.45 ns |  3.67 ns |   550.2 ns | 0.1078 |    1128 B |
| &#39;Strategy.Generate: EpochBound&#39;      |   517.1 ns |  5.10 ns |  7.64 ns |   516.7 ns | 0.0753 |     792 B |
| &#39;Strategy.Generate: ApprovalQuorum&#39;  |   449.8 ns |  3.88 ns |  5.80 ns |   450.5 ns | 0.0749 |     784 B |


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
| &#39;Strategy.GenerateOp: LWW&#39;             |  54.11 ns | 0.077 ns | 0.112 ns |  54.08 ns |      - |         - |
| &#39;Strategy.GenerateOp: FWW&#39;             |  53.98 ns | 0.095 ns | 0.143 ns |  54.01 ns |      - |         - |
| &#39;Strategy.GenerateOp: Counter&#39;         |  76.41 ns | 0.488 ns | 0.699 ns |  76.28 ns | 0.0084 |      88 B |
| &#39;Strategy.GenerateOp: GCounter&#39;        | 101.48 ns | 0.675 ns | 1.011 ns | 101.59 ns | 0.0084 |      88 B |
| &#39;Strategy.GenerateOp: BoundedCounter&#39;  |  54.57 ns | 0.401 ns | 0.601 ns |  54.42 ns |      - |         - |
| &#39;Strategy.GenerateOp: MaxWins&#39;         |  54.24 ns | 0.208 ns | 0.311 ns |  54.16 ns |      - |         - |
| &#39;Strategy.GenerateOp: MinWins&#39;         |  54.33 ns | 0.100 ns | 0.143 ns |  54.27 ns |      - |         - |
| &#39;Strategy.GenerateOp: AverageRegister&#39; |  54.12 ns | 0.252 ns | 0.370 ns |  53.96 ns |      - |         - |
| &#39;Strategy.GenerateOp: GSet&#39;            |  78.16 ns | 0.213 ns | 0.306 ns |  78.17 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: TwoPhaseSet&#39;     |  57.47 ns | 1.125 ns | 1.684 ns |  57.50 ns |      - |         - |
| &#39;Strategy.GenerateOp: LwwSet&#39;          |  56.13 ns | 0.154 ns | 0.225 ns |  56.26 ns |      - |         - |
| &#39;Strategy.GenerateOp: FwwSet&#39;          |  55.81 ns | 0.063 ns | 0.091 ns |  55.81 ns |      - |         - |
| &#39;Strategy.GenerateOp: OrSet&#39;           | 147.04 ns | 1.283 ns | 1.880 ns | 148.09 ns | 0.0069 |      72 B |
| &#39;Strategy.GenerateOp: ArrayLcs&#39;        | 426.82 ns | 1.070 ns | 1.568 ns | 426.65 ns | 0.0293 |     312 B |
| &#39;Strategy.GenerateOp: FixedSizeArray&#39;  |  94.58 ns | 0.772 ns | 1.131 ns |  94.92 ns | 0.0053 |      56 B |
| &#39;Strategy.GenerateOp: Lseq&#39;            | 106.47 ns | 1.205 ns | 1.803 ns | 106.15 ns | 0.0107 |     112 B |
| &#39;Strategy.GenerateOp: VoteCounter&#39;     |  59.15 ns | 0.184 ns | 0.276 ns |  59.11 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: StateMachine&#39;    | 252.10 ns | 0.622 ns | 0.912 ns | 252.14 ns | 0.0082 |      88 B |
| &#39;Strategy.GenerateOp: PriorityQueue&#39;   |  83.60 ns | 0.410 ns | 0.575 ns |  83.30 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: SortedSet&#39;       |  62.11 ns | 0.211 ns | 0.309 ns |  62.11 ns | 0.0053 |      56 B |
| &#39;Strategy.GenerateOp: RGA&#39;             | 240.06 ns | 5.237 ns | 7.838 ns | 239.73 ns | 0.0580 |     608 B |
| &#39;Strategy.GenerateOp: CounterMap&#39;      |  59.04 ns | 0.191 ns | 0.280 ns |  59.00 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: LwwMap&#39;          |  60.55 ns | 0.119 ns | 0.174 ns |  60.52 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: FwwMap&#39;          |  68.17 ns | 0.535 ns | 0.801 ns |  68.11 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: MaxWinsMap&#39;      |  59.30 ns | 0.485 ns | 0.726 ns |  58.96 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: MinWinsMap&#39;      |  59.79 ns | 0.623 ns | 0.932 ns |  59.38 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: OrMap&#39;           | 168.34 ns | 1.167 ns | 1.674 ns | 167.48 ns | 0.0107 |     112 B |
| &#39;Strategy.GenerateOp: Graph&#39;           |  58.45 ns | 0.169 ns | 0.232 ns |  58.42 ns | 0.0023 |      24 B |
| &#39;Strategy.GenerateOp: TwoPhaseGraph&#39;   |  68.41 ns | 0.137 ns | 0.196 ns |  68.49 ns | 0.0023 |      24 B |
| &#39;Strategy.GenerateOp: ReplicatedTree&#39;  | 124.39 ns | 0.253 ns | 0.371 ns | 124.40 ns | 0.0053 |      56 B |
| &#39;Strategy.GenerateOp: EpochBound&#39;      | 626.15 ns | 1.539 ns | 2.256 ns | 626.53 ns | 0.0244 |     264 B |
| &#39;Strategy.GenerateOp: EpochClear&#39;      | 348.44 ns | 1.154 ns | 1.727 ns | 348.06 ns | 0.0079 |      88 B |
| &#39;Strategy.GenerateOp: ApprovalQuorum&#39;  | 318.76 ns | 1.023 ns | 1.531 ns | 318.63 ns | 0.0189 |     200 B |


<!-- BENCHMARKS_END -->