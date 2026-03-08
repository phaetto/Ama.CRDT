# Ama.CRDT.Benchmarks

These benchmarks test the individual CRDT strategy application, patch generation, and other performance metrics.

<!-- BENCHMARKS_START -->
```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.7840/25H2/2025Update/HudsonValley2)
Intel Core i9-10850K CPU 3.60GHz, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=MediumRun  Toolchain=InProcessNoEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
| Method            | Mean     | Error    | StdDev   | Gen0   | Allocated |
|------------------ |---------:|---------:|---------:|-------:|----------:|
| ApplyPatchSimple  | 199.0 ns |  1.33 ns |  1.95 ns | 0.0434 |     456 B |
| ApplyPatchComplex | 927.0 ns | 11.43 ns | 16.02 ns | 0.1469 |    1552 B |


```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.7840/25H2/2025Update/HudsonValley2)
Intel Core i9-10850K CPU 3.60GHz, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=MediumRun  Toolchain=InProcessNoEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
| Method               | Mean       | Error    | StdDev   | Gen0   | Gen1   | Allocated |
|--------------------- |-----------:|---------:|---------:|-------:|-------:|----------:|
| GeneratePatchSimple  |   517.3 ns |  9.10 ns | 13.06 ns | 0.1211 |      - |   1.24 KB |
| GeneratePatchComplex | 3,111.2 ns | 15.59 ns | 21.85 ns | 0.5760 | 0.0038 |   5.88 KB |


```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.7840/25H2/2025Update/HudsonValley2)
Intel Core i9-10850K CPU 3.60GHz, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=MediumRun  Toolchain=InProcessNoEmitToolchain  InvocationCount=1  
IterationCount=15  LaunchCount=2  UnrollFactor=1  
WarmupCount=10  

```
| Method                            | Mean         | Error      | StdDev     | Median       | Allocated |
|---------------------------------- |-------------:|-----------:|-----------:|-------------:|----------:|
| &#39;Strategy.Apply: LWW&#39;             |     18.64 ns |   2.157 ns |   3.094 ns |     18.00 ns |         - |
| &#39;Strategy.Apply: FWW&#39;             |    333.59 ns |   9.837 ns |  14.419 ns |    334.00 ns |     326 B |
| &#39;Strategy.Apply: Counter&#39;         |    243.37 ns |  12.056 ns |  16.900 ns |    239.00 ns |     200 B |
| &#39;Strategy.Apply: GCounter&#39;        |    260.79 ns |  12.202 ns |  17.885 ns |    257.00 ns |     200 B |
| &#39;Strategy.Apply: BoundedCounter&#39;  |     77.74 ns |   9.413 ns |  13.196 ns |     72.00 ns |     120 B |
| &#39;Strategy.Apply: MaxWins&#39;         |    260.71 ns |  35.542 ns |  50.973 ns |    233.00 ns |     244 B |
| &#39;Strategy.Apply: MinWins&#39;         |    249.11 ns |  12.362 ns |  17.729 ns |    246.00 ns |     224 B |
| &#39;Strategy.Apply: AverageRegister&#39; |    355.59 ns |  93.107 ns | 136.475 ns |    294.00 ns |     584 B |
| &#39;Strategy.Apply: GSet&#39;            |    355.72 ns |  72.970 ns | 106.958 ns |    293.00 ns |     534 B |
| &#39;Strategy.Apply: TwoPhaseSet&#39;     |    414.31 ns |  67.635 ns |  99.139 ns |    375.00 ns |     745 B |
| &#39;Strategy.Apply: LwwSet&#39;          |    953.17 ns | 180.638 ns | 270.371 ns |    822.00 ns |    1601 B |
| &#39;Strategy.Apply: FwwSet&#39;          |  1,297.60 ns | 249.915 ns | 374.060 ns |  1,301.50 ns |    2457 B |
| &#39;Strategy.Apply: OrSet&#39;           |  1,241.11 ns | 227.964 ns | 319.574 ns |  1,040.00 ns |    2585 B |
| &#39;Strategy.Apply: ArrayLcs&#39;        |    678.90 ns | 139.707 ns | 204.781 ns |    576.00 ns |     377 B |
| &#39;Strategy.Apply: FixedSizeArray&#39;  |     15.22 ns |   0.749 ns |   1.050 ns |     15.00 ns |         - |
| &#39;Strategy.Apply: Lseq&#39;            |    743.54 ns | 272.088 ns | 390.221 ns |    953.00 ns |     329 B |
| &#39;Strategy.Apply: VoteCounter&#39;     |     16.11 ns |   0.636 ns |   0.892 ns |     16.00 ns |         - |
| &#39;Strategy.Apply: StateMachine&#39;    |  3,754.76 ns | 147.567 ns | 216.301 ns |  3,697.00 ns |     860 B |
| &#39;Strategy.Apply: PriorityQueue&#39;   | 10,557.36 ns | 335.335 ns | 480.927 ns | 10,405.00 ns |   13297 B |
| &#39;Strategy.Apply: SortedSet&#39;       |  2,241.33 ns | 243.031 ns | 363.758 ns |  2,018.00 ns |    2243 B |
| &#39;Strategy.Apply: RGA&#39;             |  1,800.93 ns | 591.804 ns | 885.785 ns |  1,395.00 ns |    2521 B |
| &#39;Strategy.Apply: CounterMap&#39;      |    854.69 ns | 110.159 ns | 161.469 ns |    781.00 ns |    1145 B |
| &#39;Strategy.Apply: LwwMap&#39;          |    571.30 ns |  36.532 ns |  51.212 ns |    552.00 ns |     681 B |
| &#39;Strategy.Apply: FwwMap&#39;          |    630.00 ns |  85.419 ns | 119.746 ns |    576.00 ns |     969 B |
| &#39;Strategy.Apply: MaxWinsMap&#39;      |    462.46 ns |  20.911 ns |  28.623 ns |    453.50 ns |     417 B |
| &#39;Strategy.Apply: MinWinsMap&#39;      |    492.82 ns |  55.554 ns |  79.674 ns |    456.00 ns |     404 B |
| &#39;Strategy.Apply: OrMap&#39;           |  1,177.09 ns |  23.527 ns |  33.741 ns |  1,177.75 ns |    1841 B |
| &#39;Strategy.Apply: Graph&#39;           |    207.65 ns |   6.445 ns |   8.822 ns |    206.50 ns |     264 B |
| &#39;Strategy.Apply: TwoPhaseGraph&#39;   |    344.20 ns |  36.122 ns |  54.066 ns |    319.50 ns |     456 B |
| &#39;Strategy.Apply: ReplicatedTree&#39;  |    397.11 ns |  64.649 ns |  92.718 ns |    352.00 ns |     819 B |
| &#39;Strategy.Apply: EpochBound&#39;      |    256.00 ns |  11.366 ns |  16.660 ns |    250.00 ns |     176 B |
| &#39;Strategy.Apply: ApprovalQuorum&#39;  |     73.48 ns |   3.244 ns |   4.331 ns |     72.00 ns |     120 B |


```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.7840/25H2/2025Update/HudsonValley2)
Intel Core i9-10850K CPU 3.60GHz, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=MediumRun  Toolchain=InProcessNoEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
| Method                               | Mean        | Error     | StdDev    | Median      | Gen0   | Allocated |
|------------------------------------- |------------:|----------:|----------:|------------:|-------:|----------:|
| &#39;Strategy.Generate: LWW&#39;             |    46.64 ns |  0.518 ns |  0.775 ns |    46.30 ns | 0.0130 |     136 B |
| &#39;Strategy.Generate: FWW&#39;             |    93.92 ns |  0.238 ns |  0.334 ns |    93.92 ns | 0.0130 |     136 B |
| &#39;Strategy.Generate: Counter&#39;         |   137.66 ns |  0.633 ns |  0.867 ns |   137.62 ns | 0.0205 |     216 B |
| &#39;Strategy.Generate: GCounter&#39;        |   134.41 ns |  0.733 ns |  1.052 ns |   134.23 ns | 0.0205 |     216 B |
| &#39;Strategy.Generate: BoundedCounter&#39;  |   135.77 ns |  0.778 ns |  1.116 ns |   135.77 ns | 0.0205 |     216 B |
| &#39;Strategy.Generate: MaxWins&#39;         |   101.07 ns |  1.133 ns |  1.696 ns |   100.50 ns | 0.0175 |     184 B |
| &#39;Strategy.Generate: MinWins&#39;         |   101.02 ns |  0.963 ns |  1.381 ns |   101.32 ns | 0.0175 |     184 B |
| &#39;Strategy.Generate: AverageRegister&#39; |   105.20 ns |  0.735 ns |  1.100 ns |   104.86 ns | 0.0191 |     200 B |
| &#39;Strategy.Generate: GSet&#39;            |   367.58 ns |  2.174 ns |  3.187 ns |   367.88 ns | 0.0749 |     784 B |
| &#39;Strategy.Generate: TwoPhaseSet&#39;     |   633.99 ns |  3.973 ns |  5.698 ns |   633.07 ns | 0.1554 |    1632 B |
| &#39;Strategy.Generate: LwwSet&#39;          |   434.64 ns |  1.658 ns |  2.430 ns |   434.85 ns | 0.1178 |    1232 B |
| &#39;Strategy.Generate: FwwSet&#39;          |   468.37 ns |  1.372 ns |  1.923 ns |   468.30 ns | 0.1178 |    1232 B |
| &#39;Strategy.Generate: OrSet&#39;           |   985.95 ns |  6.137 ns |  9.186 ns |   986.69 ns | 0.2060 |    2160 B |
| &#39;Strategy.Generate: ArrayLcs&#39;        | 1,531.91 ns |  8.120 ns | 11.646 ns | 1,529.80 ns | 0.3719 |    3896 B |
| &#39;Strategy.Generate: FixedSizeArray&#39;  |   983.01 ns |  3.569 ns |  5.342 ns |   981.93 ns | 0.0801 |     840 B |
| &#39;Strategy.Generate: Lseq&#39;            |   343.11 ns |  7.114 ns | 10.648 ns |   337.65 ns | 0.0892 |     936 B |
| &#39;Strategy.Generate: VoteCounter&#39;     |   330.55 ns |  1.754 ns |  2.626 ns |   330.24 ns | 0.1001 |    1048 B |
| &#39;Strategy.Generate: StateMachine&#39;    | 1,544.77 ns |  7.539 ns | 11.284 ns | 1,544.52 ns | 0.0648 |     696 B |
| &#39;Strategy.Generate: PriorityQueue&#39;   | 8,059.38 ns | 37.142 ns | 53.269 ns | 8,072.39 ns | 0.9766 |   10232 B |
| &#39;Strategy.Generate: SortedSet&#39;       | 2,407.82 ns |  7.309 ns | 10.714 ns | 2,407.44 ns | 0.4807 |    5032 B |
| &#39;Strategy.Generate: RGA&#39;             |   939.34 ns | 17.592 ns | 26.331 ns |   932.28 ns | 0.2728 |    2864 B |
| &#39;Strategy.Generate: CounterMap&#39;      |   971.96 ns |  4.802 ns |  7.188 ns |   971.11 ns | 0.2537 |    2656 B |
| &#39;Strategy.Generate: LwwMap&#39;          | 1,156.08 ns | 12.479 ns | 18.292 ns | 1,147.96 ns | 0.2575 |    2712 B |
| &#39;Strategy.Generate: FwwMap&#39;          | 1,075.07 ns |  5.936 ns |  8.885 ns | 1,076.89 ns | 0.2460 |    2584 B |
| &#39;Strategy.Generate: MaxWinsMap&#39;      |   605.69 ns |  4.213 ns |  6.043 ns |   605.91 ns | 0.1469 |    1536 B |
| &#39;Strategy.Generate: MinWinsMap&#39;      |   583.52 ns |  1.922 ns |  2.877 ns |   583.49 ns | 0.1469 |    1536 B |
| &#39;Strategy.Generate: OrMap&#39;           | 1,356.64 ns |  5.629 ns |  8.250 ns | 1,356.07 ns | 0.2918 |    3056 B |
| &#39;Strategy.Generate: Graph&#39;           |   369.37 ns |  1.601 ns |  2.347 ns |   369.95 ns | 0.0863 |     904 B |
| &#39;Strategy.Generate: TwoPhaseGraph&#39;   |   431.50 ns |  2.535 ns |  3.715 ns |   431.65 ns | 0.1044 |    1096 B |
| &#39;Strategy.Generate: ReplicatedTree&#39;  |   528.43 ns |  1.987 ns |  2.912 ns |   527.77 ns | 0.1078 |    1128 B |
| &#39;Strategy.Generate: EpochBound&#39;      |   228.00 ns |  0.994 ns |  1.488 ns |   228.16 ns | 0.0558 |     584 B |
| &#39;Strategy.Generate: ApprovalQuorum&#39;  |   226.67 ns |  1.135 ns |  1.698 ns |   227.29 ns | 0.0551 |     576 B |


```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.7840/25H2/2025Update/HudsonValley2)
Intel Core i9-10850K CPU 3.60GHz, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=MediumRun  Toolchain=InProcessNoEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
| Method                                 | Mean        | Error    | StdDev    | Median      | Gen0   | Allocated |
|--------------------------------------- |------------:|---------:|----------:|------------:|-------:|----------:|
| &#39;Strategy.GenerateOp: LWW&#39;             |    53.83 ns | 0.191 ns |  0.280 ns |    53.86 ns |      - |         - |
| &#39;Strategy.GenerateOp: FWW&#39;             |    54.06 ns | 0.383 ns |  0.574 ns |    54.14 ns |      - |         - |
| &#39;Strategy.GenerateOp: Counter&#39;         |    69.17 ns | 0.165 ns |  0.247 ns |    69.12 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: GCounter&#39;        |    75.93 ns | 0.802 ns |  1.124 ns |    75.39 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: BoundedCounter&#39;  |    53.96 ns | 0.251 ns |  0.368 ns |    53.98 ns |      - |         - |
| &#39;Strategy.GenerateOp: MaxWins&#39;         |    53.70 ns | 0.112 ns |  0.161 ns |    53.72 ns |      - |         - |
| &#39;Strategy.GenerateOp: MinWins&#39;         |    54.11 ns | 0.096 ns |  0.144 ns |    54.09 ns |      - |         - |
| &#39;Strategy.GenerateOp: AverageRegister&#39; |    53.61 ns | 0.160 ns |  0.224 ns |    53.58 ns |      - |         - |
| &#39;Strategy.GenerateOp: GSet&#39;            |    63.49 ns | 0.582 ns |  0.872 ns |    63.50 ns |      - |         - |
| &#39;Strategy.GenerateOp: TwoPhaseSet&#39;     |    55.64 ns | 0.096 ns |  0.141 ns |    55.64 ns |      - |         - |
| &#39;Strategy.GenerateOp: LwwSet&#39;          |    56.24 ns | 0.362 ns |  0.542 ns |    56.36 ns |      - |         - |
| &#39;Strategy.GenerateOp: FwwSet&#39;          |    55.54 ns | 0.156 ns |  0.233 ns |    55.55 ns |      - |         - |
| &#39;Strategy.GenerateOp: OrSet&#39;           |   137.57 ns | 0.543 ns |  0.813 ns |   137.52 ns | 0.0038 |      40 B |
| &#39;Strategy.GenerateOp: ArrayLcs&#39;        |   355.21 ns | 1.216 ns |  1.820 ns |   355.46 ns | 0.0244 |     256 B |
| &#39;Strategy.GenerateOp: FixedSizeArray&#39;  |   739.60 ns | 2.505 ns |  3.750 ns |   739.59 ns | 0.0269 |     288 B |
| &#39;Strategy.GenerateOp: Lseq&#39;            |   102.08 ns | 0.496 ns |  0.727 ns |   102.01 ns | 0.0107 |     112 B |
| &#39;Strategy.GenerateOp: VoteCounter&#39;     |    58.38 ns | 0.185 ns |  0.278 ns |    58.34 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: StateMachine&#39;    | 1,660.84 ns | 7.764 ns | 11.134 ns | 1,663.56 ns | 0.0562 |     592 B |
| &#39;Strategy.GenerateOp: PriorityQueue&#39;   |    64.57 ns | 1.182 ns |  1.769 ns |    64.52 ns |      - |         - |
| &#39;Strategy.GenerateOp: SortedSet&#39;       |    61.68 ns | 0.234 ns |  0.343 ns |    61.80 ns | 0.0053 |      56 B |
| &#39;Strategy.GenerateOp: RGA&#39;             |   210.27 ns | 0.662 ns |  0.950 ns |   210.35 ns | 0.0443 |     464 B |
| &#39;Strategy.GenerateOp: CounterMap&#39;      |    58.24 ns | 0.136 ns |  0.204 ns |    58.23 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: LwwMap&#39;          |    60.02 ns | 0.381 ns |  0.571 ns |    59.93 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: FwwMap&#39;          |    59.80 ns | 0.142 ns |  0.208 ns |    59.84 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: MaxWinsMap&#39;      |    58.49 ns | 0.195 ns |  0.292 ns |    58.45 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: MinWinsMap&#39;      |    58.33 ns | 0.134 ns |  0.200 ns |    58.27 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: OrMap&#39;           |   151.04 ns | 2.036 ns |  2.985 ns |   152.66 ns | 0.0046 |      48 B |
| &#39;Strategy.GenerateOp: Graph&#39;           |    57.44 ns | 0.108 ns |  0.158 ns |    57.44 ns | 0.0023 |      24 B |
| &#39;Strategy.GenerateOp: TwoPhaseGraph&#39;   |    67.36 ns | 0.140 ns |  0.210 ns |    67.38 ns | 0.0023 |      24 B |
| &#39;Strategy.GenerateOp: ReplicatedTree&#39;  |   123.25 ns | 0.333 ns |  0.499 ns |   123.31 ns | 0.0053 |      56 B |
| &#39;Strategy.GenerateOp: EpochBound&#39;      |   117.44 ns | 2.349 ns |  3.516 ns |   117.30 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: EpochClear&#39;      |    69.70 ns | 0.290 ns |  0.425 ns |    69.56 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: ApprovalQuorum&#39;  |   122.84 ns | 0.567 ns |  0.849 ns |   123.07 ns | 0.0084 |      88 B |


<!-- BENCHMARKS_END -->