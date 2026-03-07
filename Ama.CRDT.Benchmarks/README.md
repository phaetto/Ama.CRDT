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
| Method            | Mean       | Error   | StdDev  | Gen0   | Allocated |
|------------------ |-----------:|--------:|--------:|-------:|----------:|
| ApplyPatchSimple  |   284.7 ns | 1.50 ns | 2.24 ns | 0.0434 |     456 B |
| ApplyPatchComplex | 1,187.4 ns | 6.00 ns | 8.22 ns | 0.1469 |    1552 B |


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
| GeneratePatchSimple  |   485.4 ns |  8.66 ns | 12.96 ns | 0.1202 |      - |   1.23 KB |
| GeneratePatchComplex | 3,012.8 ns | 24.73 ns | 37.01 ns | 0.5722 | 0.0038 |   5.88 KB |


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
| &#39;Strategy.Apply: LWW&#39;             |     16.64 ns |   1.601 ns |   2.297 ns |     16.00 ns |         - |
| &#39;Strategy.Apply: FWW&#39;             |    307.75 ns |   5.532 ns |   7.933 ns |    308.50 ns |     299 B |
| &#39;Strategy.Apply: Counter&#39;         |    264.83 ns |  26.058 ns |  38.195 ns |    248.00 ns |     250 B |
| &#39;Strategy.Apply: GCounter&#39;        |    273.93 ns |  19.754 ns |  28.331 ns |    265.50 ns |     200 B |
| &#39;Strategy.Apply: BoundedCounter&#39;  |     74.33 ns |   6.933 ns |   9.719 ns |     70.00 ns |     120 B |
| &#39;Strategy.Apply: MaxWins&#39;         |    247.93 ns |  13.363 ns |  18.733 ns |    242.00 ns |     277 B |
| &#39;Strategy.Apply: MinWins&#39;         |    239.96 ns |  16.212 ns |  23.251 ns |    230.00 ns |     278 B |
| &#39;Strategy.Apply: AverageRegister&#39; |    380.84 ns | 110.584 ns | 162.093 ns |    280.00 ns |     637 B |
| &#39;Strategy.Apply: GSet&#39;            |    399.38 ns |  93.105 ns | 136.472 ns |    317.00 ns |     534 B |
| &#39;Strategy.Apply: TwoPhaseSet&#39;     |    394.00 ns |  29.873 ns |  40.890 ns |    376.00 ns |     745 B |
| &#39;Strategy.Apply: LwwSet&#39;          |  1,024.93 ns | 235.554 ns | 345.271 ns |    829.00 ns |    1601 B |
| &#39;Strategy.Apply: FwwSet&#39;          |  1,488.59 ns | 246.697 ns | 361.605 ns |  1,644.00 ns |    2437 B |
| &#39;Strategy.Apply: OrSet&#39;           |  1,350.23 ns | 252.333 ns | 377.681 ns |  1,126.50 ns |    2582 B |
| &#39;Strategy.Apply: ArrayLcs&#39;        |    705.93 ns | 151.484 ns | 226.734 ns |    609.00 ns |     363 B |
| &#39;Strategy.Apply: FixedSizeArray&#39;  |    310.20 ns |  31.294 ns |  46.840 ns |    287.00 ns |     198 B |
| &#39;Strategy.Apply: Lseq&#39;            |    724.72 ns | 271.601 ns | 398.109 ns |    974.00 ns |     312 B |
| &#39;Strategy.Apply: VoteCounter&#39;     |     16.93 ns |   1.529 ns |   2.288 ns |     16.00 ns |      20 B |
| &#39;Strategy.Apply: StateMachine&#39;    |  3,890.15 ns | 192.354 ns | 287.907 ns |  3,942.50 ns |     860 B |
| &#39;Strategy.Apply: PriorityQueue&#39;   | 11,622.50 ns | 502.477 ns | 720.638 ns | 11,290.00 ns |   13297 B |
| &#39;Strategy.Apply: SortedSet&#39;       |  2,312.45 ns | 321.823 ns | 461.548 ns |  2,003.75 ns |    2243 B |
| &#39;Strategy.Apply: RGA&#39;             |  1,839.76 ns | 604.621 ns | 886.246 ns |  1,181.00 ns |    2507 B |
| &#39;Strategy.Apply: CounterMap&#39;      |    882.60 ns | 123.088 ns | 184.233 ns |    797.00 ns |    1142 B |
| &#39;Strategy.Apply: LwwMap&#39;          |    610.75 ns |  62.147 ns |  89.129 ns |    585.00 ns |     681 B |
| &#39;Strategy.Apply: FwwMap&#39;          |    644.61 ns |  67.522 ns |  96.838 ns |    621.00 ns |     966 B |
| &#39;Strategy.Apply: MaxWinsMap&#39;      |    504.78 ns |  43.836 ns |  64.254 ns |    483.50 ns |     417 B |
| &#39;Strategy.Apply: MinWinsMap&#39;      |    483.82 ns |  31.732 ns |  45.510 ns |    466.50 ns |     417 B |
| &#39;Strategy.Apply: OrMap&#39;           |  1,205.82 ns |  30.671 ns |  43.987 ns |  1,209.00 ns |    1837 B |
| &#39;Strategy.Apply: Graph&#39;           |    234.90 ns |  21.946 ns |  32.168 ns |    230.00 ns |     308 B |
| &#39;Strategy.Apply: TwoPhaseGraph&#39;   |    332.43 ns |  17.221 ns |  24.697 ns |    327.00 ns |     510 B |
| &#39;Strategy.Apply: ReplicatedTree&#39;  |    436.45 ns |  79.436 ns | 113.924 ns |    389.00 ns |     833 B |
| &#39;Strategy.Apply: EpochBound&#39;      |    285.07 ns |  29.802 ns |  43.683 ns |    270.00 ns |     189 B |
| &#39;Strategy.Apply: ApprovalQuorum&#39;  |     76.04 ns |   5.800 ns |   8.319 ns |     73.25 ns |     120 B |


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
| &#39;Strategy.Generate: LWW&#39;             |    47.97 ns |  1.479 ns |  2.213 ns |    46.50 ns | 0.0130 |     136 B |
| &#39;Strategy.Generate: FWW&#39;             |    96.51 ns |  0.385 ns |  0.576 ns |    96.70 ns | 0.0130 |     136 B |
| &#39;Strategy.Generate: Counter&#39;         |   132.91 ns |  0.911 ns |  1.364 ns |   132.59 ns | 0.0205 |     216 B |
| &#39;Strategy.Generate: GCounter&#39;        |   134.07 ns |  0.474 ns |  0.694 ns |   134.01 ns | 0.0205 |     216 B |
| &#39;Strategy.Generate: BoundedCounter&#39;  |   136.35 ns |  2.322 ns |  3.404 ns |   136.48 ns | 0.0205 |     216 B |
| &#39;Strategy.Generate: MaxWins&#39;         |   101.22 ns |  1.594 ns |  2.336 ns |   101.35 ns | 0.0175 |     184 B |
| &#39;Strategy.Generate: MinWins&#39;         |   102.99 ns |  0.925 ns |  1.384 ns |   103.25 ns | 0.0175 |     184 B |
| &#39;Strategy.Generate: AverageRegister&#39; |   104.79 ns |  1.867 ns |  2.795 ns |   105.27 ns | 0.0191 |     200 B |
| &#39;Strategy.Generate: GSet&#39;            |   379.59 ns |  2.572 ns |  3.849 ns |   378.91 ns | 0.0749 |     784 B |
| &#39;Strategy.Generate: TwoPhaseSet&#39;     |   658.83 ns |  3.630 ns |  5.321 ns |   659.50 ns | 0.1554 |    1632 B |
| &#39;Strategy.Generate: LwwSet&#39;          |   428.98 ns |  2.860 ns |  4.193 ns |   428.28 ns | 0.1178 |    1232 B |
| &#39;Strategy.Generate: FwwSet&#39;          |   431.68 ns |  4.248 ns |  5.955 ns |   429.21 ns | 0.1178 |    1232 B |
| &#39;Strategy.Generate: OrSet&#39;           | 1,005.25 ns |  4.808 ns |  7.196 ns | 1,005.46 ns | 0.2060 |    2160 B |
| &#39;Strategy.Generate: ArrayLcs&#39;        | 1,636.03 ns | 19.924 ns | 29.821 ns | 1,631.89 ns | 0.3719 |    3896 B |
| &#39;Strategy.Generate: FixedSizeArray&#39;  | 1,151.92 ns |  6.822 ns | 10.211 ns | 1,152.13 ns | 0.0801 |     840 B |
| &#39;Strategy.Generate: Lseq&#39;            |   340.85 ns |  3.192 ns |  4.777 ns |   340.17 ns | 0.0892 |     936 B |
| &#39;Strategy.Generate: VoteCounter&#39;     |   334.72 ns |  6.020 ns |  8.824 ns |   334.89 ns | 0.1001 |    1048 B |
| &#39;Strategy.Generate: StateMachine&#39;    | 1,583.92 ns | 10.804 ns | 16.171 ns | 1,579.66 ns | 0.0648 |     696 B |
| &#39;Strategy.Generate: PriorityQueue&#39;   | 8,258.81 ns | 50.842 ns | 72.916 ns | 8,268.51 ns | 0.9766 |   10232 B |
| &#39;Strategy.Generate: SortedSet&#39;       | 2,494.02 ns | 14.833 ns | 22.201 ns | 2,496.44 ns | 0.4807 |    5032 B |
| &#39;Strategy.Generate: RGA&#39;             |   928.24 ns |  6.725 ns |  9.645 ns |   929.10 ns | 0.2737 |    2864 B |
| &#39;Strategy.Generate: CounterMap&#39;      |   952.93 ns |  8.781 ns | 13.144 ns |   954.28 ns | 0.2537 |    2656 B |
| &#39;Strategy.Generate: LwwMap&#39;          | 1,109.16 ns |  8.755 ns | 12.833 ns | 1,111.85 ns | 0.2575 |    2712 B |
| &#39;Strategy.Generate: FwwMap&#39;          | 1,048.47 ns |  5.215 ns |  7.644 ns | 1,049.43 ns | 0.2460 |    2584 B |
| &#39;Strategy.Generate: MaxWinsMap&#39;      |   562.82 ns |  6.213 ns |  9.300 ns |   561.36 ns | 0.1469 |    1536 B |
| &#39;Strategy.Generate: MinWinsMap&#39;      |   575.99 ns |  2.792 ns |  3.914 ns |   575.89 ns | 0.1469 |    1536 B |
| &#39;Strategy.Generate: OrMap&#39;           | 1,316.30 ns |  8.368 ns | 12.266 ns | 1,317.15 ns | 0.2918 |    3056 B |
| &#39;Strategy.Generate: Graph&#39;           |   368.47 ns |  4.512 ns |  6.753 ns |   365.36 ns | 0.0863 |     904 B |
| &#39;Strategy.Generate: TwoPhaseGraph&#39;   |   403.70 ns |  3.749 ns |  5.495 ns |   405.05 ns | 0.1044 |    1096 B |
| &#39;Strategy.Generate: ReplicatedTree&#39;  |   551.13 ns |  4.383 ns |  6.560 ns |   550.72 ns | 0.1078 |    1128 B |
| &#39;Strategy.Generate: EpochBound&#39;      |   235.51 ns |  2.163 ns |  3.170 ns |   235.54 ns | 0.0558 |     584 B |
| &#39;Strategy.Generate: ApprovalQuorum&#39;  |   228.21 ns |  1.489 ns |  2.135 ns |   227.85 ns | 0.0551 |     576 B |


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
| &#39;Strategy.GenerateOp: LWW&#39;             |    54.55 ns | 0.408 ns |  0.586 ns |    54.64 ns |      - |         - |
| &#39;Strategy.GenerateOp: FWW&#39;             |    53.87 ns | 0.063 ns |  0.093 ns |    53.86 ns |      - |         - |
| &#39;Strategy.GenerateOp: Counter&#39;         |    68.34 ns | 0.488 ns |  0.715 ns |    68.19 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: GCounter&#39;        |    75.70 ns | 0.438 ns |  0.656 ns |    75.56 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: BoundedCounter&#39;  |    54.26 ns | 0.403 ns |  0.578 ns |    53.91 ns |      - |         - |
| &#39;Strategy.GenerateOp: MaxWins&#39;         |    53.75 ns | 0.084 ns |  0.123 ns |    53.77 ns |      - |         - |
| &#39;Strategy.GenerateOp: MinWins&#39;         |    54.55 ns | 0.048 ns |  0.072 ns |    54.53 ns |      - |         - |
| &#39;Strategy.GenerateOp: AverageRegister&#39; |    54.46 ns | 0.455 ns |  0.608 ns |    54.85 ns |      - |         - |
| &#39;Strategy.GenerateOp: GSet&#39;            |    64.47 ns | 1.028 ns |  1.539 ns |    63.89 ns |      - |         - |
| &#39;Strategy.GenerateOp: TwoPhaseSet&#39;     |    57.51 ns | 1.152 ns |  1.688 ns |    56.09 ns |      - |         - |
| &#39;Strategy.GenerateOp: LwwSet&#39;          |    55.82 ns | 0.119 ns |  0.163 ns |    55.78 ns |      - |         - |
| &#39;Strategy.GenerateOp: FwwSet&#39;          |    55.82 ns | 0.124 ns |  0.186 ns |    55.83 ns |      - |         - |
| &#39;Strategy.GenerateOp: OrSet&#39;           |   137.09 ns | 0.492 ns |  0.705 ns |   136.81 ns | 0.0038 |      40 B |
| &#39;Strategy.GenerateOp: ArrayLcs&#39;        |   357.87 ns | 1.904 ns |  2.791 ns |   358.62 ns | 0.0244 |     256 B |
| &#39;Strategy.GenerateOp: FixedSizeArray&#39;  |   783.82 ns | 4.662 ns |  6.978 ns |   781.95 ns | 0.0269 |     288 B |
| &#39;Strategy.GenerateOp: Lseq&#39;            |   102.58 ns | 0.679 ns |  0.995 ns |   102.49 ns | 0.0107 |     112 B |
| &#39;Strategy.GenerateOp: VoteCounter&#39;     |    59.15 ns | 0.156 ns |  0.229 ns |    59.19 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: StateMachine&#39;    | 1,624.14 ns | 7.597 ns | 10.895 ns | 1,625.00 ns | 0.0562 |     592 B |
| &#39;Strategy.GenerateOp: PriorityQueue&#39;   |    64.12 ns | 1.235 ns |  1.771 ns |    63.99 ns |      - |         - |
| &#39;Strategy.GenerateOp: SortedSet&#39;       |    61.11 ns | 0.197 ns |  0.277 ns |    61.10 ns | 0.0053 |      56 B |
| &#39;Strategy.GenerateOp: RGA&#39;             |   212.93 ns | 2.256 ns |  3.235 ns |   212.58 ns | 0.0443 |     464 B |
| &#39;Strategy.GenerateOp: CounterMap&#39;      |    60.22 ns | 0.325 ns |  0.486 ns |    60.36 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: LwwMap&#39;          |    60.57 ns | 0.180 ns |  0.270 ns |    60.65 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: FwwMap&#39;          |    60.83 ns | 0.245 ns |  0.366 ns |    60.89 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: MaxWinsMap&#39;      |    60.67 ns | 0.757 ns |  1.086 ns |    61.25 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: MinWinsMap&#39;      |    59.15 ns | 0.173 ns |  0.260 ns |    59.13 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: OrMap&#39;           |   152.01 ns | 0.383 ns |  0.562 ns |   152.04 ns | 0.0046 |      48 B |
| &#39;Strategy.GenerateOp: Graph&#39;           |    57.90 ns | 0.175 ns |  0.262 ns |    57.86 ns | 0.0023 |      24 B |
| &#39;Strategy.GenerateOp: TwoPhaseGraph&#39;   |    67.27 ns | 0.300 ns |  0.439 ns |    67.16 ns | 0.0023 |      24 B |
| &#39;Strategy.GenerateOp: ReplicatedTree&#39;  |   124.16 ns | 0.323 ns |  0.463 ns |   124.16 ns | 0.0053 |      56 B |
| &#39;Strategy.GenerateOp: EpochBound&#39;      |   123.94 ns | 1.084 ns |  1.623 ns |   124.10 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: EpochClear&#39;      |    71.07 ns | 1.073 ns |  1.539 ns |    70.93 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: ApprovalQuorum&#39;  |   113.39 ns | 1.368 ns |  1.962 ns |   113.44 ns | 0.0023 |      24 B |


<!-- BENCHMARKS_END -->