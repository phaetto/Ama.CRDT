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
| ApplyPatchSimple  |   724.2 ns | 1.55 ns | 2.17 ns | 0.0200 |     216 B |
| ApplyPatchComplex | 1,974.5 ns | 5.52 ns | 8.25 ns | 0.0648 |     712 B |


```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.7840/25H2/2025Update/HudsonValley2)
Intel Core i9-10850K CPU 3.60GHz, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=MediumRun  Toolchain=InProcessNoEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
| Method               | Mean     | Error     | StdDev    | Gen0   | Allocated |
|--------------------- |---------:|----------:|----------:|-------:|----------:|
| GeneratePatchSimple  | 1.139 μs | 0.0076 μs | 0.0114 μs | 0.1106 |   1.15 KB |
| GeneratePatchComplex | 4.554 μs | 0.0236 μs | 0.0353 μs | 0.5341 |   5.48 KB |


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
| &#39;Strategy.Apply: LWW&#39;             |     18.41 ns |   1.846 ns |   2.706 ns |     18.00 ns |         - |
| &#39;Strategy.Apply: Counter&#39;         |    277.62 ns |  28.965 ns |  42.457 ns |    260.00 ns |     250 B |
| &#39;Strategy.Apply: GCounter&#39;        |    293.97 ns |  15.471 ns |  23.157 ns |    290.00 ns |     250 B |
| &#39;Strategy.Apply: BoundedCounter&#39;  |  1,177.33 ns |  42.954 ns |  60.215 ns |  1,162.00 ns |     570 B |
| &#39;Strategy.Apply: MaxWins&#39;         |    284.52 ns |  18.734 ns |  27.460 ns |    292.00 ns |     224 B |
| &#39;Strategy.Apply: MinWins&#39;         |    257.90 ns |  14.508 ns |  21.715 ns |    253.00 ns |     257 B |
| &#39;Strategy.Apply: AverageRegister&#39; |    297.46 ns |  26.898 ns |  36.819 ns |    290.00 ns |     634 B |
| &#39;Strategy.Apply: GSet&#39;            |    324.92 ns |  13.820 ns |  18.916 ns |    323.00 ns |     531 B |
| &#39;Strategy.Apply: TwoPhaseSet&#39;     |    407.67 ns |  43.215 ns |  64.682 ns |    384.50 ns |     738 B |
| &#39;Strategy.Apply: LwwSet&#39;          |    852.44 ns |  42.083 ns |  56.180 ns |    837.00 ns |    1594 B |
| &#39;Strategy.Apply: OrSet&#39;           |  1,035.93 ns |  62.575 ns |  87.722 ns |  1,008.00 ns |    2581 B |
| &#39;Strategy.Apply: ArrayLcs&#39;        |    725.26 ns | 149.364 ns | 209.387 ns |    595.00 ns |     373 B |
| &#39;Strategy.Apply: FixedSizeArray&#39;  |    286.17 ns |   4.562 ns |   5.932 ns |    288.00 ns |     194 B |
| &#39;Strategy.Apply: Lseq&#39;            |    382.56 ns |  53.861 ns |  75.506 ns |    365.00 ns |     312 B |
| &#39;Strategy.Apply: VoteCounter&#39;     |     17.81 ns |   1.611 ns |   2.258 ns |     17.50 ns |         - |
| &#39;Strategy.Apply: StateMachine&#39;    |  3,901.93 ns | 193.117 ns | 289.049 ns |  3,816.50 ns |     857 B |
| &#39;Strategy.Apply: PriorityQueue&#39;   | 11,287.15 ns |  70.471 ns |  96.461 ns | 11,269.00 ns |   14189 B |
| &#39;Strategy.Apply: SortedSet&#39;       |  1,967.57 ns |  68.509 ns |  98.254 ns |  1,938.50 ns |    2240 B |
| &#39;Strategy.Apply: RGA&#39;             |  1,249.33 ns | 248.361 ns | 348.168 ns |  1,114.00 ns |    2517 B |
| &#39;Strategy.Apply: CounterMap&#39;      |    757.54 ns |  15.147 ns |  20.733 ns |    756.00 ns |    1138 B |
| &#39;Strategy.Apply: LwwMap&#39;          |    558.52 ns |  38.861 ns |  54.477 ns |    536.00 ns |     677 B |
| &#39;Strategy.Apply: MaxWinsMap&#39;      |    461.89 ns |  20.186 ns |  28.298 ns |    454.00 ns |     404 B |
| &#39;Strategy.Apply: MinWinsMap&#39;      |    450.54 ns |   7.795 ns |  10.670 ns |    448.50 ns |     413 B |
| &#39;Strategy.Apply: OrMap&#39;           |    939.04 ns |  48.282 ns |  69.245 ns |    918.00 ns |    1834 B |
| &#39;Strategy.Apply: Graph&#39;           |    217.18 ns |  13.313 ns |  19.092 ns |    212.00 ns |     264 B |
| &#39;Strategy.Apply: TwoPhaseGraph&#39;   |    318.50 ns |   8.479 ns |  12.161 ns |    318.50 ns |     506 B |
| &#39;Strategy.Apply: ReplicatedTree&#39;  |    475.66 ns |  51.533 ns |  75.536 ns |    478.50 ns |     823 B |


```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.7840/25H2/2025Update/HudsonValley2)
Intel Core i9-10850K CPU 3.60GHz, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=MediumRun  Toolchain=InProcessNoEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
| Method                               | Mean        | Error     | StdDev    | Gen0   | Allocated |
|------------------------------------- |------------:|----------:|----------:|-------:|----------:|
| &#39;Strategy.Generate: LWW&#39;             |    46.47 ns |  0.399 ns |  0.597 ns | 0.0130 |     136 B |
| &#39;Strategy.Generate: Counter&#39;         |   147.04 ns |  1.271 ns |  1.902 ns | 0.0205 |     216 B |
| &#39;Strategy.Generate: GCounter&#39;        |   136.90 ns |  0.991 ns |  1.483 ns | 0.0205 |     216 B |
| &#39;Strategy.Generate: BoundedCounter&#39;  |   137.27 ns |  1.870 ns |  2.741 ns | 0.0205 |     216 B |
| &#39;Strategy.Generate: MaxWins&#39;         |   106.43 ns |  4.500 ns |  6.596 ns | 0.0175 |     184 B |
| &#39;Strategy.Generate: MinWins&#39;         |   107.38 ns |  1.183 ns |  1.771 ns | 0.0175 |     184 B |
| &#39;Strategy.Generate: AverageRegister&#39; |   113.30 ns |  0.883 ns |  1.294 ns | 0.0191 |     200 B |
| &#39;Strategy.Generate: GSet&#39;            |   354.36 ns |  1.370 ns |  2.050 ns | 0.0749 |     784 B |
| &#39;Strategy.Generate: TwoPhaseSet&#39;     |   576.30 ns |  5.919 ns |  8.859 ns | 0.1554 |    1632 B |
| &#39;Strategy.Generate: LwwSet&#39;          |   462.52 ns |  6.615 ns |  9.487 ns | 0.1178 |    1232 B |
| &#39;Strategy.Generate: OrSet&#39;           |   968.44 ns |  3.809 ns |  5.462 ns | 0.2060 |    2160 B |
| &#39;Strategy.Generate: ArrayLcs&#39;        | 1,564.29 ns |  6.954 ns | 10.408 ns | 0.3719 |    3896 B |
| &#39;Strategy.Generate: FixedSizeArray&#39;  | 1,166.94 ns |  3.067 ns |  4.199 ns | 0.0801 |     840 B |
| &#39;Strategy.Generate: Lseq&#39;            |   340.40 ns |  2.658 ns |  3.811 ns | 0.0892 |     936 B |
| &#39;Strategy.Generate: VoteCounter&#39;     |   341.93 ns |  2.551 ns |  3.818 ns | 0.1001 |    1048 B |
| &#39;Strategy.Generate: StateMachine&#39;    | 1,684.03 ns |  8.897 ns | 13.317 ns | 0.0648 |     696 B |
| &#39;Strategy.Generate: PriorityQueue&#39;   | 7,877.72 ns | 40.298 ns | 59.069 ns | 0.9766 |   10232 B |
| &#39;Strategy.Generate: SortedSet&#39;       | 2,452.65 ns |  8.301 ns | 11.905 ns | 0.4807 |    5032 B |
| &#39;Strategy.Generate: RGA&#39;             |   984.99 ns |  5.072 ns |  7.434 ns | 0.2728 |    2864 B |
| &#39;Strategy.Generate: CounterMap&#39;      | 1,001.93 ns |  3.825 ns |  5.725 ns | 0.2537 |    2656 B |
| &#39;Strategy.Generate: LwwMap&#39;          | 1,149.15 ns |  4.287 ns |  6.149 ns | 0.2575 |    2712 B |
| &#39;Strategy.Generate: MaxWinsMap&#39;      |   600.49 ns |  3.894 ns |  5.708 ns | 0.1469 |    1536 B |
| &#39;Strategy.Generate: MinWinsMap&#39;      |   611.82 ns |  2.310 ns |  3.386 ns | 0.1469 |    1536 B |
| &#39;Strategy.Generate: OrMap&#39;           | 1,377.63 ns |  6.438 ns |  9.636 ns | 0.2918 |    3056 B |
| &#39;Strategy.Generate: Graph&#39;           |   397.04 ns |  2.641 ns |  3.871 ns | 0.0863 |     904 B |
| &#39;Strategy.Generate: TwoPhaseGraph&#39;   |   401.42 ns |  1.874 ns |  2.687 ns | 0.1044 |    1096 B |
| &#39;Strategy.Generate: ReplicatedTree&#39;  |   542.48 ns |  1.701 ns |  2.493 ns | 0.1078 |    1128 B |


```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.7840/25H2/2025Update/HudsonValley2)
Intel Core i9-10850K CPU 3.60GHz, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=MediumRun  Toolchain=InProcessNoEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
| Method                                 | Mean        | Error     | StdDev    | Median      | Gen0   | Allocated |
|--------------------------------------- |------------:|----------:|----------:|------------:|-------:|----------:|
| &#39;Strategy.GenerateOp: LWW&#39;             |    55.51 ns |  0.733 ns |  1.096 ns |    55.82 ns |      - |         - |
| &#39;Strategy.GenerateOp: Counter&#39;         |    69.41 ns |  0.301 ns |  0.441 ns |    69.40 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: GCounter&#39;        |    80.71 ns |  0.991 ns |  1.453 ns |    80.35 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: BoundedCounter&#39;  |    55.39 ns |  0.977 ns |  1.432 ns |    55.49 ns |      - |         - |
| &#39;Strategy.GenerateOp: MaxWins&#39;         |    54.98 ns |  0.574 ns |  0.841 ns |    54.62 ns |      - |         - |
| &#39;Strategy.GenerateOp: MinWins&#39;         |    54.54 ns |  0.115 ns |  0.165 ns |    54.51 ns |      - |         - |
| &#39;Strategy.GenerateOp: AverageRegister&#39; |    54.67 ns |  0.448 ns |  0.613 ns |    54.54 ns |      - |         - |
| &#39;Strategy.GenerateOp: GSet&#39;            |    63.79 ns |  0.595 ns |  0.891 ns |    63.87 ns |      - |         - |
| &#39;Strategy.GenerateOp: TwoPhaseSet&#39;     |    56.81 ns |  0.376 ns |  0.551 ns |    56.58 ns |      - |         - |
| &#39;Strategy.GenerateOp: LwwSet&#39;          |    57.50 ns |  1.017 ns |  1.425 ns |    57.68 ns |      - |         - |
| &#39;Strategy.GenerateOp: OrSet&#39;           |   140.99 ns |  0.634 ns |  0.949 ns |   140.98 ns | 0.0038 |      40 B |
| &#39;Strategy.GenerateOp: ArrayLcs&#39;        |   360.61 ns |  1.392 ns |  1.951 ns |   360.20 ns | 0.0244 |     256 B |
| &#39;Strategy.GenerateOp: FixedSizeArray&#39;  |   753.52 ns |  2.701 ns |  4.043 ns |   753.76 ns | 0.0269 |     288 B |
| &#39;Strategy.GenerateOp: Lseq&#39;            |   105.65 ns |  0.564 ns |  0.809 ns |   105.47 ns | 0.0107 |     112 B |
| &#39;Strategy.GenerateOp: VoteCounter&#39;     |    60.90 ns |  0.795 ns |  1.141 ns |    60.27 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: StateMachine&#39;    | 1,656.39 ns | 13.096 ns | 19.602 ns | 1,646.77 ns | 0.0562 |     592 B |
| &#39;Strategy.GenerateOp: PriorityQueue&#39;   |    65.69 ns |  0.964 ns |  1.413 ns |    65.76 ns |      - |         - |
| &#39;Strategy.GenerateOp: SortedSet&#39;       |    63.50 ns |  0.967 ns |  1.387 ns |    63.07 ns | 0.0053 |      56 B |
| &#39;Strategy.GenerateOp: RGA&#39;             |   225.12 ns |  1.420 ns |  2.037 ns |   224.78 ns | 0.0443 |     464 B |
| &#39;Strategy.GenerateOp: CounterMap&#39;      |    60.76 ns |  1.013 ns |  1.517 ns |    60.00 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: LwwMap&#39;          |    61.66 ns |  0.583 ns |  0.778 ns |    62.05 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: MaxWinsMap&#39;      |    61.87 ns |  0.526 ns |  0.788 ns |    61.93 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: MinWinsMap&#39;      |    60.22 ns |  0.460 ns |  0.674 ns |    60.45 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: OrMap&#39;           |   154.96 ns |  0.269 ns |  0.376 ns |   154.95 ns | 0.0046 |      48 B |
| &#39;Strategy.GenerateOp: Graph&#39;           |    61.19 ns |  0.225 ns |  0.316 ns |    61.22 ns | 0.0023 |      24 B |
| &#39;Strategy.GenerateOp: TwoPhaseGraph&#39;   |    72.44 ns |  0.765 ns |  1.144 ns |    72.36 ns | 0.0023 |      24 B |
| &#39;Strategy.GenerateOp: ReplicatedTree&#39;  |   128.90 ns |  1.847 ns |  2.764 ns |   129.13 ns | 0.0053 |      56 B |


<!-- BENCHMARKS_END -->