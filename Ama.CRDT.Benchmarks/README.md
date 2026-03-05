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
| ApplyPatchSimple  |   710.1 ns | 2.01 ns | 2.82 ns | 0.0200 |     216 B |
| ApplyPatchComplex | 1,958.8 ns | 6.23 ns | 8.93 ns | 0.0648 |     712 B |


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
| GeneratePatchSimple  | 1.080 μs | 0.0040 μs | 0.0059 μs | 0.1106 |   1.15 KB |
| GeneratePatchComplex | 4.259 μs | 0.0180 μs | 0.0258 μs | 0.5341 |   5.48 KB |


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
| &#39;Strategy.Apply: LWW&#39;             |     19.68 ns |   1.666 ns |   2.389 ns |     19.00 ns |         - |
| &#39;Strategy.Apply: FWW&#39;             |    226.54 ns |   7.396 ns |  10.124 ns |    226.50 ns |     296 B |
| &#39;Strategy.Apply: Counter&#39;         |    264.44 ns |  14.789 ns |  20.731 ns |    255.00 ns |     203 B |
| &#39;Strategy.Apply: GCounter&#39;        |    271.11 ns |   9.056 ns |  12.696 ns |    269.00 ns |     200 B |
| &#39;Strategy.Apply: BoundedCounter&#39;  |  1,206.12 ns |  16.460 ns |  22.531 ns |  1,198.00 ns |     589 B |
| &#39;Strategy.Apply: MaxWins&#39;         |    241.30 ns |  10.459 ns |  14.662 ns |    237.00 ns |     224 B |
| &#39;Strategy.Apply: MinWins&#39;         |    246.31 ns |   6.821 ns |   9.337 ns |    244.50 ns |     274 B |
| &#39;Strategy.Apply: AverageRegister&#39; |    304.41 ns |  18.392 ns |  25.784 ns |    307.00 ns |     620 B |
| &#39;Strategy.Apply: GSet&#39;            |    322.32 ns |  14.035 ns |  18.737 ns |    320.00 ns |     554 B |
| &#39;Strategy.Apply: TwoPhaseSet&#39;     |    405.79 ns |  28.709 ns |  41.174 ns |    393.50 ns |     731 B |
| &#39;Strategy.Apply: LwwSet&#39;          |    851.11 ns |  21.308 ns |  30.559 ns |    853.50 ns |    1581 B |
| &#39;Strategy.Apply: FwwSet&#39;          |  1,325.86 ns | 349.450 ns | 512.219 ns |    991.00 ns |    2437 B |
| &#39;Strategy.Apply: OrSet&#39;           |  1,145.92 ns |  94.293 ns | 125.878 ns |  1,120.00 ns |    2578 B |
| &#39;Strategy.Apply: ArrayLcs&#39;        |    626.18 ns |  24.759 ns |  35.509 ns |    618.50 ns |     373 B |
| &#39;Strategy.Apply: FixedSizeArray&#39;  |    298.29 ns |  15.034 ns |  21.561 ns |    294.50 ns |     180 B |
| &#39;Strategy.Apply: Lseq&#39;            |    489.61 ns | 212.826 ns | 305.228 ns |    332.00 ns |     322 B |
| &#39;Strategy.Apply: VoteCounter&#39;     |     19.77 ns |   2.624 ns |   3.928 ns |     19.00 ns |         - |
| &#39;Strategy.Apply: StateMachine&#39;    |  4,005.55 ns | 189.973 ns | 278.460 ns |  4,059.00 ns |     857 B |
| &#39;Strategy.Apply: PriorityQueue&#39;   | 12,658.64 ns | 172.368 ns | 247.205 ns | 12,562.50 ns |   14189 B |
| &#39;Strategy.Apply: SortedSet&#39;       |  2,005.72 ns |  62.933 ns |  84.013 ns |  1,983.00 ns |    2233 B |
| &#39;Strategy.Apply: RGA&#39;             |  1,254.11 ns | 239.147 ns | 342.978 ns |  1,120.00 ns |    2517 B |
| &#39;Strategy.Apply: CounterMap&#39;      |    752.67 ns |  16.354 ns |  22.926 ns |    747.00 ns |    1141 B |
| &#39;Strategy.Apply: LwwMap&#39;          |    545.19 ns |  20.558 ns |  28.140 ns |    535.00 ns |     677 B |
| &#39;Strategy.Apply: FwwMap&#39;          |    577.60 ns |  16.631 ns |  22.202 ns |    573.00 ns |     965 B |
| &#39;Strategy.Apply: MaxWinsMap&#39;      |    455.35 ns |  14.007 ns |  19.173 ns |    449.50 ns |     413 B |
| &#39;Strategy.Apply: MinWinsMap&#39;      |    443.39 ns |   7.873 ns |  11.292 ns |    442.00 ns |     413 B |
| &#39;Strategy.Apply: OrMap&#39;           |  1,158.22 ns | 114.938 ns | 168.475 ns |  1,142.00 ns |    1834 B |
| &#39;Strategy.Apply: Graph&#39;           |    216.15 ns |   7.473 ns |  10.476 ns |    215.00 ns |     264 B |
| &#39;Strategy.Apply: TwoPhaseGraph&#39;   |    333.19 ns |  10.360 ns |  14.523 ns |    331.00 ns |     509 B |
| &#39;Strategy.Apply: ReplicatedTree&#39;  |    368.22 ns |  15.502 ns |  21.731 ns |    362.00 ns |     776 B |


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
| &#39;Strategy.Generate: LWW&#39;             |    45.46 ns |  0.455 ns |  0.680 ns | 0.0130 |     136 B |
| &#39;Strategy.Generate: FWW&#39;             |    95.80 ns |  0.392 ns |  0.575 ns | 0.0130 |     136 B |
| &#39;Strategy.Generate: Counter&#39;         |   131.59 ns |  1.043 ns |  1.461 ns | 0.0205 |     216 B |
| &#39;Strategy.Generate: GCounter&#39;        |   132.86 ns |  0.541 ns |  0.776 ns | 0.0205 |     216 B |
| &#39;Strategy.Generate: BoundedCounter&#39;  |   131.36 ns |  0.835 ns |  1.197 ns | 0.0205 |     216 B |
| &#39;Strategy.Generate: MaxWins&#39;         |    99.06 ns |  0.342 ns |  0.511 ns | 0.0175 |     184 B |
| &#39;Strategy.Generate: MinWins&#39;         |   101.32 ns |  0.492 ns |  0.736 ns | 0.0175 |     184 B |
| &#39;Strategy.Generate: AverageRegister&#39; |   101.62 ns |  1.328 ns |  1.987 ns | 0.0191 |     200 B |
| &#39;Strategy.Generate: GSet&#39;            |   318.24 ns |  4.876 ns |  7.298 ns | 0.0749 |     784 B |
| &#39;Strategy.Generate: TwoPhaseSet&#39;     |   578.49 ns |  3.327 ns |  4.877 ns | 0.1554 |    1632 B |
| &#39;Strategy.Generate: LwwSet&#39;          |   427.64 ns |  4.771 ns |  6.843 ns | 0.1178 |    1232 B |
| &#39;Strategy.Generate: FwwSet&#39;          |   434.17 ns |  2.686 ns |  3.937 ns | 0.1178 |    1232 B |
| &#39;Strategy.Generate: OrSet&#39;           |   908.45 ns |  7.975 ns | 11.936 ns | 0.2060 |    2160 B |
| &#39;Strategy.Generate: ArrayLcs&#39;        | 1,569.99 ns |  5.272 ns |  7.561 ns | 0.3719 |    3896 B |
| &#39;Strategy.Generate: FixedSizeArray&#39;  | 1,126.00 ns |  3.223 ns |  4.824 ns | 0.0801 |     840 B |
| &#39;Strategy.Generate: Lseq&#39;            |   318.66 ns |  1.656 ns |  2.428 ns | 0.0892 |     936 B |
| &#39;Strategy.Generate: VoteCounter&#39;     |   318.53 ns |  2.322 ns |  3.404 ns | 0.1001 |    1048 B |
| &#39;Strategy.Generate: StateMachine&#39;    | 1,588.30 ns |  9.526 ns | 14.259 ns | 0.0648 |     696 B |
| &#39;Strategy.Generate: PriorityQueue&#39;   | 8,015.18 ns | 26.101 ns | 38.258 ns | 0.9766 |   10232 B |
| &#39;Strategy.Generate: SortedSet&#39;       | 2,361.42 ns |  5.914 ns |  8.482 ns | 0.4807 |    5032 B |
| &#39;Strategy.Generate: RGA&#39;             |   869.66 ns |  4.574 ns |  6.705 ns | 0.2737 |    2864 B |
| &#39;Strategy.Generate: CounterMap&#39;      |   961.76 ns | 10.775 ns | 16.127 ns | 0.2537 |    2656 B |
| &#39;Strategy.Generate: LwwMap&#39;          | 1,126.81 ns |  4.781 ns |  7.007 ns | 0.2575 |    2712 B |
| &#39;Strategy.Generate: FwwMap&#39;          | 1,017.42 ns |  6.276 ns |  9.200 ns | 0.2460 |    2584 B |
| &#39;Strategy.Generate: MaxWinsMap&#39;      |   558.91 ns |  1.990 ns |  2.854 ns | 0.1469 |    1536 B |
| &#39;Strategy.Generate: MinWinsMap&#39;      |   566.70 ns |  3.895 ns |  5.710 ns | 0.1469 |    1536 B |
| &#39;Strategy.Generate: OrMap&#39;           | 1,318.93 ns |  6.257 ns |  9.365 ns | 0.2918 |    3056 B |
| &#39;Strategy.Generate: Graph&#39;           |   366.09 ns |  4.295 ns |  6.428 ns | 0.0863 |     904 B |
| &#39;Strategy.Generate: TwoPhaseGraph&#39;   |   384.52 ns |  1.494 ns |  2.236 ns | 0.1044 |    1096 B |
| &#39;Strategy.Generate: ReplicatedTree&#39;  |   523.81 ns |  2.155 ns |  3.226 ns | 0.1078 |    1128 B |


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
| &#39;Strategy.GenerateOp: LWW&#39;             |    53.91 ns | 0.075 ns |  0.107 ns |    53.90 ns |      - |         - |
| &#39;Strategy.GenerateOp: FWW&#39;             |    53.77 ns | 0.064 ns |  0.096 ns |    53.77 ns |      - |         - |
| &#39;Strategy.GenerateOp: Counter&#39;         |    67.17 ns | 0.150 ns |  0.219 ns |    67.22 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: GCounter&#39;        |    77.63 ns | 0.817 ns |  1.224 ns |    77.59 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: BoundedCounter&#39;  |    53.93 ns | 0.083 ns |  0.125 ns |    53.93 ns |      - |         - |
| &#39;Strategy.GenerateOp: MaxWins&#39;         |    53.85 ns | 0.072 ns |  0.101 ns |    53.86 ns |      - |         - |
| &#39;Strategy.GenerateOp: MinWins&#39;         |    54.55 ns | 0.078 ns |  0.115 ns |    54.58 ns |      - |         - |
| &#39;Strategy.GenerateOp: AverageRegister&#39; |    53.80 ns | 0.061 ns |  0.086 ns |    53.82 ns |      - |         - |
| &#39;Strategy.GenerateOp: GSet&#39;            |    64.40 ns | 1.523 ns |  2.279 ns |    64.39 ns |      - |         - |
| &#39;Strategy.GenerateOp: TwoPhaseSet&#39;     |    56.03 ns | 0.229 ns |  0.336 ns |    55.86 ns |      - |         - |
| &#39;Strategy.GenerateOp: LwwSet&#39;          |    55.87 ns | 0.080 ns |  0.118 ns |    55.86 ns |      - |         - |
| &#39;Strategy.GenerateOp: FwwSet&#39;          |    56.56 ns | 0.379 ns |  0.531 ns |    56.82 ns |      - |         - |
| &#39;Strategy.GenerateOp: OrSet&#39;           |   135.20 ns | 1.448 ns |  2.123 ns |   133.49 ns | 0.0038 |      40 B |
| &#39;Strategy.GenerateOp: ArrayLcs&#39;        |   355.84 ns | 1.659 ns |  2.483 ns |   355.82 ns | 0.0244 |     256 B |
| &#39;Strategy.GenerateOp: FixedSizeArray&#39;  |   752.09 ns | 2.835 ns |  4.066 ns |   752.02 ns | 0.0269 |     288 B |
| &#39;Strategy.GenerateOp: Lseq&#39;            |   103.70 ns | 0.646 ns |  0.966 ns |   103.29 ns | 0.0107 |     112 B |
| &#39;Strategy.GenerateOp: VoteCounter&#39;     |    58.72 ns | 0.102 ns |  0.150 ns |    58.73 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: StateMachine&#39;    | 1,627.87 ns | 7.622 ns | 11.408 ns | 1,625.18 ns | 0.0562 |     592 B |
| &#39;Strategy.GenerateOp: PriorityQueue&#39;   |    63.67 ns | 0.346 ns |  0.485 ns |    63.50 ns |      - |         - |
| &#39;Strategy.GenerateOp: SortedSet&#39;       |    61.13 ns | 0.114 ns |  0.167 ns |    61.12 ns | 0.0053 |      56 B |
| &#39;Strategy.GenerateOp: RGA&#39;             |   209.47 ns | 0.633 ns |  0.928 ns |   209.49 ns | 0.0443 |     464 B |
| &#39;Strategy.GenerateOp: CounterMap&#39;      |    59.18 ns | 0.432 ns |  0.647 ns |    59.15 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: LwwMap&#39;          |    61.46 ns | 0.839 ns |  1.230 ns |    60.61 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: FwwMap&#39;          |    61.93 ns | 0.091 ns |  0.130 ns |    61.95 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: MaxWinsMap&#39;      |    58.63 ns | 0.114 ns |  0.155 ns |    58.60 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: MinWinsMap&#39;      |    59.27 ns | 0.512 ns |  0.766 ns |    59.27 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: OrMap&#39;           |   149.37 ns | 1.771 ns |  2.483 ns |   147.65 ns | 0.0046 |      48 B |
| &#39;Strategy.GenerateOp: Graph&#39;           |    57.98 ns | 0.123 ns |  0.181 ns |    57.99 ns | 0.0023 |      24 B |
| &#39;Strategy.GenerateOp: TwoPhaseGraph&#39;   |    68.71 ns | 1.060 ns |  1.586 ns |    68.73 ns | 0.0023 |      24 B |
| &#39;Strategy.GenerateOp: ReplicatedTree&#39;  |   126.20 ns | 0.527 ns |  0.756 ns |   126.22 ns | 0.0053 |      56 B |


<!-- BENCHMARKS_END -->