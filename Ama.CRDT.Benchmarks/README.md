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
| Method            | Mean     | Error    | StdDev   | Gen0   | Allocated |
|------------------ |---------:|---------:|---------:|-------:|----------:|
| ApplyPatchSimple  | 226.0 ns |  1.80 ns |  2.69 ns | 0.0465 |     488 B |
| ApplyPatchComplex | 964.7 ns | 10.09 ns | 14.46 ns | 0.1564 |    1648 B |


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
| GeneratePatchSimple  |   581.8 ns |  7.14 ns | 10.47 ns | 0.1240 |      - |   1.27 KB |
| GeneratePatchComplex | 3,243.4 ns | 33.16 ns | 49.63 ns | 0.5836 | 0.0038 |   5.98 KB |


```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
Intel Core i9-10850K CPU 3.60GHz, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.201
  [Host] : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3

Job=MediumRun  Toolchain=InProcessNoEmitToolchain  InvocationCount=1  
IterationCount=15  LaunchCount=2  UnrollFactor=1  
WarmupCount=10  

```
| Method                            | Mean         | Error      | StdDev     | Median       | Allocated |
|---------------------------------- |-------------:|-----------:|-----------:|-------------:|----------:|
| &#39;Strategy.Apply: LWW&#39;             |    380.55 ns |  12.210 ns |  17.898 ns |    376.00 ns |     168 B |
| &#39;Strategy.Apply: FWW&#39;             |     16.66 ns |   1.780 ns |   2.609 ns |     16.00 ns |         - |
| &#39;Strategy.Apply: Counter&#39;         |    243.00 ns |  20.110 ns |  28.841 ns |    232.50 ns |     208 B |
| &#39;Strategy.Apply: GCounter&#39;        |    276.23 ns |  13.922 ns |  19.056 ns |    273.50 ns |     208 B |
| &#39;Strategy.Apply: BoundedCounter&#39;  |     77.69 ns |   3.798 ns |   5.198 ns |     76.00 ns |     128 B |
| &#39;Strategy.Apply: MaxWins&#39;         |    250.93 ns |  16.239 ns |  22.765 ns |    241.00 ns |     232 B |
| &#39;Strategy.Apply: MinWins&#39;         |    242.04 ns |   9.489 ns |  12.667 ns |    237.00 ns |     232 B |
| &#39;Strategy.Apply: AverageRegister&#39; |    528.69 ns | 157.580 ns | 230.979 ns |    391.00 ns |     977 B |
| &#39;Strategy.Apply: GSet&#39;            |    491.79 ns | 111.681 ns | 163.701 ns |    416.00 ns |     553 B |
| &#39;Strategy.Apply: TwoPhaseSet&#39;     |    429.18 ns |  51.403 ns |  73.721 ns |    397.50 ns |     825 B |
| &#39;Strategy.Apply: LwwSet&#39;          |  1,394.04 ns | 266.395 ns | 382.056 ns |  1,182.50 ns |    2249 B |
| &#39;Strategy.Apply: FwwSet&#39;          |  1,234.59 ns | 236.804 ns | 331.967 ns |  1,453.00 ns |    1865 B |
| &#39;Strategy.Apply: OrSet&#39;           |  1,218.67 ns | 247.297 ns | 370.142 ns |  1,033.50 ns |    2761 B |
| &#39;Strategy.Apply: ArrayLcs&#39;        |    686.33 ns | 119.530 ns | 178.907 ns |    609.00 ns |     393 B |
| &#39;Strategy.Apply: FixedSizeArray&#39;  |    382.43 ns |  50.717 ns |  72.736 ns |    348.50 ns |     195 B |
| &#39;Strategy.Apply: Lseq&#39;            |    745.07 ns | 286.906 ns | 429.428 ns |    479.50 ns |     323 B |
| &#39;Strategy.Apply: VoteCounter&#39;     |    720.14 ns | 110.423 ns | 161.857 ns |    640.00 ns |     585 B |
| &#39;Strategy.Apply: StateMachine&#39;    |  4,119.45 ns | 155.223 ns | 227.524 ns |  4,082.00 ns |     868 B |
| &#39;Strategy.Apply: PriorityQueue&#39;   | 12,042.96 ns | 135.180 ns | 189.503 ns | 11,988.00 ns |   12929 B |
| &#39;Strategy.Apply: SortedSet&#39;       |  7,592.17 ns | 584.220 ns | 856.342 ns |  7,067.00 ns |    8867 B |
| &#39;Strategy.Apply: RGA&#39;             |  1,862.03 ns | 594.965 ns | 872.093 ns |  1,735.00 ns |    2969 B |
| &#39;Strategy.Apply: CounterMap&#39;      |    825.55 ns | 103.399 ns | 151.561 ns |    766.00 ns |    1161 B |
| &#39;Strategy.Apply: LwwMap&#39;          |    621.24 ns |  46.309 ns |  67.879 ns |    600.00 ns |     825 B |
| &#39;Strategy.Apply: FwwMap&#39;          |    340.11 ns |  34.860 ns |  48.869 ns |    320.00 ns |     373 B |
| &#39;Strategy.Apply: MaxWinsMap&#39;      |    475.54 ns |  37.454 ns |  51.268 ns |    455.00 ns |     433 B |
| &#39;Strategy.Apply: MinWinsMap&#39;      |    467.34 ns |  22.711 ns |  33.290 ns |    456.00 ns |     433 B |
| &#39;Strategy.Apply: OrMap&#39;           |  1,156.76 ns |  18.165 ns |  26.627 ns |  1,152.00 ns |    2017 B |
| &#39;Strategy.Apply: Graph&#39;           |    219.48 ns |  11.201 ns |  15.702 ns |    218.00 ns |     272 B |
| &#39;Strategy.Apply: TwoPhaseGraph&#39;   |    351.71 ns |  13.810 ns |  19.806 ns |    347.50 ns |     521 B |
| &#39;Strategy.Apply: ReplicatedTree&#39;  |    416.10 ns |  59.307 ns |  88.768 ns |    377.00 ns |     814 B |
| &#39;Strategy.Apply: EpochBound&#39;      |    255.76 ns |   7.526 ns |  10.047 ns |    260.00 ns |     184 B |
| &#39;Strategy.Apply: ApprovalQuorum&#39;  |     76.59 ns |   5.956 ns |   8.349 ns |     74.00 ns |     128 B |


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
| &#39;Strategy.Generate: LWW&#39;             |   119.71 ns |  0.549 ns |  0.804 ns | 0.0129 |     136 B |
| &#39;Strategy.Generate: FWW&#39;             |    43.38 ns |  0.453 ns |  0.678 ns | 0.0130 |     136 B |
| &#39;Strategy.Generate: Counter&#39;         |   141.42 ns |  1.155 ns |  1.728 ns | 0.0205 |     216 B |
| &#39;Strategy.Generate: GCounter&#39;        |   142.28 ns |  0.719 ns |  1.031 ns | 0.0205 |     216 B |
| &#39;Strategy.Generate: BoundedCounter&#39;  |   139.20 ns |  1.989 ns |  2.853 ns | 0.0205 |     216 B |
| &#39;Strategy.Generate: MaxWins&#39;         |   101.63 ns |  0.560 ns |  0.803 ns | 0.0175 |     184 B |
| &#39;Strategy.Generate: MinWins&#39;         |   102.62 ns |  1.421 ns |  2.083 ns | 0.0175 |     184 B |
| &#39;Strategy.Generate: AverageRegister&#39; |   104.85 ns |  0.923 ns |  1.354 ns | 0.0191 |     200 B |
| &#39;Strategy.Generate: GSet&#39;            |   358.75 ns |  3.170 ns |  4.646 ns | 0.0749 |     784 B |
| &#39;Strategy.Generate: TwoPhaseSet&#39;     |   644.83 ns | 14.211 ns | 20.382 ns | 0.1554 |    1632 B |
| &#39;Strategy.Generate: LwwSet&#39;          |   441.15 ns |  5.147 ns |  7.705 ns | 0.1178 |    1232 B |
| &#39;Strategy.Generate: FwwSet&#39;          |   437.18 ns |  4.183 ns |  5.999 ns | 0.1178 |    1232 B |
| &#39;Strategy.Generate: OrSet&#39;           | 1,038.91 ns | 11.903 ns | 17.071 ns | 0.2060 |    2160 B |
| &#39;Strategy.Generate: ArrayLcs&#39;        | 1,574.39 ns | 13.102 ns | 19.205 ns | 0.3719 |    3896 B |
| &#39;Strategy.Generate: FixedSizeArray&#39;  | 1,177.61 ns |  5.949 ns |  8.340 ns | 0.0801 |     840 B |
| &#39;Strategy.Generate: Lseq&#39;            |   338.32 ns |  1.586 ns |  2.117 ns | 0.0892 |     936 B |
| &#39;Strategy.Generate: VoteCounter&#39;     |   422.24 ns |  2.209 ns |  3.097 ns | 0.1030 |    1080 B |
| &#39;Strategy.Generate: StateMachine&#39;    | 1,722.99 ns | 10.300 ns | 15.098 ns | 0.0648 |     696 B |
| &#39;Strategy.Generate: PriorityQueue&#39;   | 8,327.95 ns | 50.061 ns | 74.929 ns | 0.9766 |   10232 B |
| &#39;Strategy.Generate: SortedSet&#39;       | 2,495.94 ns | 23.306 ns | 32.672 ns | 0.4845 |    5096 B |
| &#39;Strategy.Generate: RGA&#39;             |   947.29 ns | 11.421 ns | 17.095 ns | 0.2918 |    3056 B |
| &#39;Strategy.Generate: CounterMap&#39;      |   927.54 ns | 11.968 ns | 17.912 ns | 0.2537 |    2656 B |
| &#39;Strategy.Generate: LwwMap&#39;          | 1,188.66 ns | 12.540 ns | 18.770 ns | 0.2575 |    2712 B |
| &#39;Strategy.Generate: FwwMap&#39;          |   990.63 ns |  5.148 ns |  7.047 ns | 0.2556 |    2680 B |
| &#39;Strategy.Generate: MaxWinsMap&#39;      |   553.11 ns |  5.763 ns |  8.448 ns | 0.1469 |    1536 B |
| &#39;Strategy.Generate: MinWinsMap&#39;      |   547.70 ns |  1.825 ns |  2.436 ns | 0.1469 |    1536 B |
| &#39;Strategy.Generate: OrMap&#39;           | 1,417.05 ns | 16.351 ns | 23.967 ns | 0.2918 |    3056 B |
| &#39;Strategy.Generate: Graph&#39;           |   379.37 ns |  4.160 ns |  6.227 ns | 0.0863 |     904 B |
| &#39;Strategy.Generate: TwoPhaseGraph&#39;   |   430.01 ns |  3.543 ns |  5.082 ns | 0.1044 |    1096 B |
| &#39;Strategy.Generate: ReplicatedTree&#39;  |   540.11 ns |  2.786 ns |  3.906 ns | 0.1078 |    1128 B |
| &#39;Strategy.Generate: EpochBound&#39;      |   239.33 ns |  2.973 ns |  4.449 ns | 0.0589 |     616 B |
| &#39;Strategy.Generate: ApprovalQuorum&#39;  |   234.03 ns |  1.864 ns |  2.790 ns | 0.0579 |     608 B |


```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
Intel Core i9-10850K CPU 3.60GHz, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.201
  [Host] : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3

Job=MediumRun  Toolchain=InProcessNoEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
| Method                                 | Mean        | Error     | StdDev    | Median      | Gen0   | Allocated |
|--------------------------------------- |------------:|----------:|----------:|------------:|-------:|----------:|
| &#39;Strategy.GenerateOp: LWW&#39;             |    54.10 ns |  0.221 ns |  0.331 ns |    54.06 ns |      - |         - |
| &#39;Strategy.GenerateOp: FWW&#39;             |    55.01 ns |  0.370 ns |  0.553 ns |    55.03 ns |      - |         - |
| &#39;Strategy.GenerateOp: Counter&#39;         |    72.08 ns |  1.161 ns |  1.738 ns |    71.49 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: GCounter&#39;        |    78.85 ns |  0.944 ns |  1.354 ns |    78.89 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: BoundedCounter&#39;  |    53.96 ns |  0.101 ns |  0.151 ns |    53.96 ns |      - |         - |
| &#39;Strategy.GenerateOp: MaxWins&#39;         |    56.32 ns |  1.846 ns |  2.763 ns |    55.60 ns |      - |         - |
| &#39;Strategy.GenerateOp: MinWins&#39;         |    54.37 ns |  0.119 ns |  0.178 ns |    54.33 ns |      - |         - |
| &#39;Strategy.GenerateOp: AverageRegister&#39; |    55.70 ns |  1.216 ns |  1.819 ns |    55.64 ns |      - |         - |
| &#39;Strategy.GenerateOp: GSet&#39;            |    63.84 ns |  0.360 ns |  0.538 ns |    64.10 ns |      - |         - |
| &#39;Strategy.GenerateOp: TwoPhaseSet&#39;     |    55.64 ns |  0.112 ns |  0.165 ns |    55.63 ns |      - |         - |
| &#39;Strategy.GenerateOp: LwwSet&#39;          |    55.91 ns |  0.199 ns |  0.285 ns |    55.86 ns |      - |         - |
| &#39;Strategy.GenerateOp: FwwSet&#39;          |    55.74 ns |  0.118 ns |  0.177 ns |    55.75 ns |      - |         - |
| &#39;Strategy.GenerateOp: OrSet&#39;           |   136.91 ns |  0.830 ns |  1.243 ns |   136.76 ns | 0.0038 |      40 B |
| &#39;Strategy.GenerateOp: ArrayLcs&#39;        |   354.81 ns |  0.846 ns |  1.158 ns |   354.91 ns | 0.0244 |     256 B |
| &#39;Strategy.GenerateOp: FixedSizeArray&#39;  |   727.92 ns |  3.121 ns |  4.575 ns |   728.79 ns | 0.0269 |     288 B |
| &#39;Strategy.GenerateOp: Lseq&#39;            |   104.39 ns |  0.453 ns |  0.635 ns |   104.33 ns | 0.0107 |     112 B |
| &#39;Strategy.GenerateOp: VoteCounter&#39;     |    60.39 ns |  1.004 ns |  1.502 ns |    60.29 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: StateMachine&#39;    | 1,727.88 ns | 10.571 ns | 15.161 ns | 1,726.88 ns | 0.0562 |     592 B |
| &#39;Strategy.GenerateOp: PriorityQueue&#39;   |    64.70 ns |  1.041 ns |  1.558 ns |    64.57 ns |      - |         - |
| &#39;Strategy.GenerateOp: SortedSet&#39;       |    62.08 ns |  0.805 ns |  1.204 ns |    62.09 ns | 0.0053 |      56 B |
| &#39;Strategy.GenerateOp: RGA&#39;             |   231.52 ns |  1.844 ns |  2.703 ns |   230.64 ns | 0.0580 |     608 B |
| &#39;Strategy.GenerateOp: CounterMap&#39;      |    58.98 ns |  0.191 ns |  0.280 ns |    58.93 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: LwwMap&#39;          |    60.35 ns |  0.219 ns |  0.328 ns |    60.33 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: FwwMap&#39;          |    67.60 ns |  0.529 ns |  0.775 ns |    68.02 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: MaxWinsMap&#39;      |    58.90 ns |  0.165 ns |  0.247 ns |    58.84 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: MinWinsMap&#39;      |    59.11 ns |  0.178 ns |  0.267 ns |    59.16 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: OrMap&#39;           |   155.53 ns |  0.972 ns |  1.455 ns |   155.42 ns | 0.0046 |      48 B |
| &#39;Strategy.GenerateOp: Graph&#39;           |    58.76 ns |  0.473 ns |  0.693 ns |    59.19 ns | 0.0023 |      24 B |
| &#39;Strategy.GenerateOp: TwoPhaseGraph&#39;   |    69.20 ns |  0.251 ns |  0.369 ns |    69.14 ns | 0.0023 |      24 B |
| &#39;Strategy.GenerateOp: ReplicatedTree&#39;  |   125.90 ns |  1.003 ns |  1.502 ns |   125.93 ns | 0.0053 |      56 B |
| &#39;Strategy.GenerateOp: EpochBound&#39;      |   117.25 ns |  1.195 ns |  1.751 ns |   118.10 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: EpochClear&#39;      |    70.72 ns |  0.620 ns |  0.928 ns |    70.69 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: ApprovalQuorum&#39;  |   131.13 ns |  0.988 ns |  1.479 ns |   131.08 ns | 0.0145 |     152 B |


<!-- BENCHMARKS_END -->