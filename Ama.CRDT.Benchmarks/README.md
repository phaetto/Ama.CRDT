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
| Method            | Mean       | Error   | StdDev   | Gen0   | Allocated |
|------------------ |-----------:|--------:|---------:|-------:|----------:|
| ApplyPatchSimple  |   227.8 ns | 1.10 ns |  1.57 ns | 0.0465 |     488 B |
| ApplyPatchComplex | 1,018.0 ns | 8.56 ns | 12.55 ns | 0.1564 |    1648 B |


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
| GeneratePatchSimple  |   603.3 ns | 14.51 ns | 20.81 ns | 0.1240 |      - |   1.27 KB |
| GeneratePatchComplex | 3,297.9 ns | 27.11 ns | 38.00 ns | 0.5836 | 0.0038 |   5.98 KB |


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
| &#39;Strategy.Apply: LWW&#39;             |    394.08 ns |  28.476 ns |  38.014 ns |    381.50 ns |     225 B |
| &#39;Strategy.Apply: FWW&#39;             |    293.86 ns |  20.700 ns |  29.688 ns |    283.25 ns |     304 B |
| &#39;Strategy.Apply: Counter&#39;         |    262.54 ns |  20.849 ns |  29.902 ns |    262.50 ns |     208 B |
| &#39;Strategy.Apply: GCounter&#39;        |    298.07 ns |  23.393 ns |  35.014 ns |    291.50 ns |     255 B |
| &#39;Strategy.Apply: BoundedCounter&#39;  |     82.00 ns |   7.026 ns |   9.849 ns |     82.00 ns |     128 B |
| &#39;Strategy.Apply: MaxWins&#39;         |    260.43 ns |  13.461 ns |  19.305 ns |    262.50 ns |     269 B |
| &#39;Strategy.Apply: MinWins&#39;         |    252.15 ns |  11.571 ns |  16.221 ns |    255.00 ns |     232 B |
| &#39;Strategy.Apply: AverageRegister&#39; |    519.48 ns | 150.872 ns | 221.147 ns |    429.00 ns |     950 B |
| &#39;Strategy.Apply: GSet&#39;            |    502.20 ns | 101.913 ns | 152.539 ns |    446.00 ns |     546 B |
| &#39;Strategy.Apply: TwoPhaseSet&#39;     |    522.21 ns |  65.004 ns |  93.227 ns |    488.50 ns |     749 B |
| &#39;Strategy.Apply: LwwSet&#39;          |  1,367.31 ns | 207.822 ns | 304.623 ns |  1,222.00 ns |    2201 B |
| &#39;Strategy.Apply: FwwSet&#39;          |  1,414.11 ns | 259.180 ns | 371.709 ns |  1,627.50 ns |    2473 B |
| &#39;Strategy.Apply: OrSet&#39;           |  1,255.77 ns | 231.615 ns | 346.670 ns |  1,123.50 ns |    2601 B |
| &#39;Strategy.Apply: ArrayLcs&#39;        |    677.45 ns |  90.695 ns | 132.940 ns |    635.00 ns |     389 B |
| &#39;Strategy.Apply: FixedSizeArray&#39;  |    369.60 ns |  41.547 ns |  62.186 ns |    344.50 ns |     202 B |
| &#39;Strategy.Apply: Lseq&#39;            |    747.69 ns | 271.403 ns | 397.819 ns |    987.00 ns |     280 B |
| &#39;Strategy.Apply: VoteCounter&#39;     |    772.93 ns | 134.490 ns | 201.299 ns |    650.00 ns |     585 B |
| &#39;Strategy.Apply: StateMachine&#39;    |  4,269.33 ns | 157.953 ns | 236.416 ns |  4,292.00 ns |     865 B |
| &#39;Strategy.Apply: PriorityQueue&#39;   | 12,044.92 ns | 196.810 ns | 269.395 ns | 11,987.00 ns |   13006 B |
| &#39;Strategy.Apply: SortedSet&#39;       |  7,113.11 ns | 450.592 ns | 646.226 ns |  6,789.50 ns |    8819 B |
| &#39;Strategy.Apply: RGA&#39;             |  1,907.41 ns | 635.737 ns | 931.855 ns |  1,677.00 ns |    2537 B |
| &#39;Strategy.Apply: CounterMap&#39;      |    842.32 ns | 113.961 ns | 163.439 ns |    759.50 ns |    1161 B |
| &#39;Strategy.Apply: LwwMap&#39;          |    617.93 ns |  78.053 ns | 116.826 ns |    567.00 ns |     825 B |
| &#39;Strategy.Apply: FwwMap&#39;          |    607.68 ns |  28.514 ns |  40.894 ns |    596.00 ns |     985 B |
| &#39;Strategy.Apply: MaxWinsMap&#39;      |    469.33 ns |  31.516 ns |  44.181 ns |    450.00 ns |     433 B |
| &#39;Strategy.Apply: MinWinsMap&#39;      |    474.68 ns |  35.287 ns |  50.608 ns |    455.00 ns |     433 B |
| &#39;Strategy.Apply: OrMap&#39;           |  1,167.75 ns |  24.438 ns |  35.048 ns |  1,175.50 ns |    1857 B |
| &#39;Strategy.Apply: Graph&#39;           |    214.15 ns |  12.245 ns |  17.166 ns |    207.00 ns |     329 B |
| &#39;Strategy.Apply: TwoPhaseGraph&#39;   |    371.17 ns |  22.131 ns |  33.125 ns |    365.00 ns |     490 B |
| &#39;Strategy.Apply: ReplicatedTree&#39;  |    426.95 ns |  65.291 ns |  95.702 ns |    383.00 ns |     814 B |
| &#39;Strategy.Apply: EpochBound&#39;      |    274.98 ns |   9.892 ns |  14.187 ns |    275.75 ns |     218 B |
| &#39;Strategy.Apply: ApprovalQuorum&#39;  |     78.00 ns |   7.116 ns |  10.205 ns |     75.00 ns |     128 B |


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
| &#39;Strategy.Generate: LWW&#39;             |   120.64 ns |  1.589 ns |  2.378 ns | 0.0129 |     136 B |
| &#39;Strategy.Generate: FWW&#39;             |    99.99 ns |  0.851 ns |  1.248 ns | 0.0130 |     136 B |
| &#39;Strategy.Generate: Counter&#39;         |   142.39 ns |  1.735 ns |  2.543 ns | 0.0205 |     216 B |
| &#39;Strategy.Generate: GCounter&#39;        |   139.64 ns |  1.595 ns |  2.387 ns | 0.0205 |     216 B |
| &#39;Strategy.Generate: BoundedCounter&#39;  |   138.47 ns |  0.686 ns |  1.006 ns | 0.0205 |     216 B |
| &#39;Strategy.Generate: MaxWins&#39;         |   103.25 ns |  0.435 ns |  0.637 ns | 0.0175 |     184 B |
| &#39;Strategy.Generate: MinWins&#39;         |   125.69 ns |  0.578 ns |  0.865 ns | 0.0174 |     184 B |
| &#39;Strategy.Generate: AverageRegister&#39; |   102.19 ns |  0.712 ns |  1.044 ns | 0.0191 |     200 B |
| &#39;Strategy.Generate: GSet&#39;            |   348.32 ns |  2.428 ns |  3.559 ns | 0.0749 |     784 B |
| &#39;Strategy.Generate: TwoPhaseSet&#39;     |   621.61 ns |  5.932 ns |  8.695 ns | 0.1554 |    1632 B |
| &#39;Strategy.Generate: LwwSet&#39;          |   428.14 ns |  2.767 ns |  3.969 ns | 0.1178 |    1232 B |
| &#39;Strategy.Generate: FwwSet&#39;          |   429.55 ns |  3.977 ns |  5.952 ns | 0.1178 |    1232 B |
| &#39;Strategy.Generate: OrSet&#39;           |   980.70 ns |  4.569 ns |  6.697 ns | 0.2060 |    2160 B |
| &#39;Strategy.Generate: ArrayLcs&#39;        | 1,586.77 ns | 11.367 ns | 16.303 ns | 0.3719 |    3896 B |
| &#39;Strategy.Generate: FixedSizeArray&#39;  | 1,077.71 ns |  4.237 ns |  6.211 ns | 0.0801 |     840 B |
| &#39;Strategy.Generate: Lseq&#39;            |   336.77 ns |  2.180 ns |  3.195 ns | 0.0892 |     936 B |
| &#39;Strategy.Generate: VoteCounter&#39;     |   426.88 ns |  8.780 ns | 12.308 ns | 0.1030 |    1080 B |
| &#39;Strategy.Generate: StateMachine&#39;    | 1,715.89 ns | 10.111 ns | 14.820 ns | 0.0648 |     696 B |
| &#39;Strategy.Generate: PriorityQueue&#39;   | 8,178.31 ns | 46.707 ns | 69.909 ns | 0.9766 |   10232 B |
| &#39;Strategy.Generate: SortedSet&#39;       | 2,445.72 ns | 19.471 ns | 27.925 ns | 0.4845 |    5096 B |
| &#39;Strategy.Generate: RGA&#39;             |   911.52 ns |  8.978 ns | 13.160 ns | 0.2737 |    2864 B |
| &#39;Strategy.Generate: CounterMap&#39;      | 1,007.80 ns |  8.090 ns | 11.858 ns | 0.2537 |    2656 B |
| &#39;Strategy.Generate: LwwMap&#39;          | 1,169.28 ns |  9.153 ns | 13.416 ns | 0.2575 |    2712 B |
| &#39;Strategy.Generate: FwwMap&#39;          | 1,084.30 ns |  7.753 ns | 10.613 ns | 0.2460 |    2584 B |
| &#39;Strategy.Generate: MaxWinsMap&#39;      |   603.19 ns | 13.890 ns | 20.789 ns | 0.1469 |    1536 B |
| &#39;Strategy.Generate: MinWinsMap&#39;      |   580.61 ns | 13.588 ns | 20.339 ns | 0.1469 |    1536 B |
| &#39;Strategy.Generate: OrMap&#39;           | 1,376.70 ns |  8.043 ns | 12.039 ns | 0.2918 |    3056 B |
| &#39;Strategy.Generate: Graph&#39;           |   371.01 ns |  2.690 ns |  4.027 ns | 0.0863 |     904 B |
| &#39;Strategy.Generate: TwoPhaseGraph&#39;   |   414.92 ns |  3.968 ns |  5.816 ns | 0.1044 |    1096 B |
| &#39;Strategy.Generate: ReplicatedTree&#39;  |   529.67 ns |  4.858 ns |  6.968 ns | 0.1078 |    1128 B |
| &#39;Strategy.Generate: EpochBound&#39;      |   242.46 ns |  4.988 ns |  7.154 ns | 0.0587 |     616 B |
| &#39;Strategy.Generate: ApprovalQuorum&#39;  |   239.24 ns |  2.863 ns |  4.197 ns | 0.0577 |     608 B |


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
| &#39;Strategy.GenerateOp: LWW&#39;             |    54.52 ns |  0.522 ns |  0.781 ns |    54.47 ns |      - |         - |
| &#39;Strategy.GenerateOp: FWW&#39;             |    54.08 ns |  0.145 ns |  0.218 ns |    54.04 ns |      - |         - |
| &#39;Strategy.GenerateOp: Counter&#39;         |    70.26 ns |  0.358 ns |  0.536 ns |    70.21 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: GCounter&#39;        |    79.15 ns |  1.835 ns |  2.690 ns |    77.07 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: BoundedCounter&#39;  |    54.11 ns |  0.104 ns |  0.145 ns |    54.07 ns |      - |         - |
| &#39;Strategy.GenerateOp: MaxWins&#39;         |    54.03 ns |  0.076 ns |  0.114 ns |    54.03 ns |      - |         - |
| &#39;Strategy.GenerateOp: MinWins&#39;         |    54.40 ns |  0.079 ns |  0.114 ns |    54.36 ns |      - |         - |
| &#39;Strategy.GenerateOp: AverageRegister&#39; |    53.96 ns |  0.096 ns |  0.138 ns |    53.92 ns |      - |         - |
| &#39;Strategy.GenerateOp: GSet&#39;            |    63.50 ns |  0.112 ns |  0.163 ns |    63.54 ns |      - |         - |
| &#39;Strategy.GenerateOp: TwoPhaseSet&#39;     |    56.00 ns |  0.102 ns |  0.147 ns |    56.00 ns |      - |         - |
| &#39;Strategy.GenerateOp: LwwSet&#39;          |    55.97 ns |  0.093 ns |  0.137 ns |    55.90 ns |      - |         - |
| &#39;Strategy.GenerateOp: FwwSet&#39;          |    56.09 ns |  0.166 ns |  0.243 ns |    56.04 ns |      - |         - |
| &#39;Strategy.GenerateOp: OrSet&#39;           |   137.48 ns |  0.475 ns |  0.711 ns |   137.27 ns | 0.0038 |      40 B |
| &#39;Strategy.GenerateOp: ArrayLcs&#39;        |   354.86 ns |  1.431 ns |  2.097 ns |   354.28 ns | 0.0244 |     256 B |
| &#39;Strategy.GenerateOp: FixedSizeArray&#39;  |   750.71 ns |  2.150 ns |  3.014 ns |   751.81 ns | 0.0269 |     288 B |
| &#39;Strategy.GenerateOp: Lseq&#39;            |   105.85 ns |  0.542 ns |  0.812 ns |   105.95 ns | 0.0107 |     112 B |
| &#39;Strategy.GenerateOp: VoteCounter&#39;     |    59.42 ns |  0.334 ns |  0.490 ns |    59.25 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: StateMachine&#39;    | 1,702.67 ns | 13.344 ns | 19.559 ns | 1,699.57 ns | 0.0562 |     592 B |
| &#39;Strategy.GenerateOp: PriorityQueue&#39;   |    65.24 ns |  0.346 ns |  0.507 ns |    65.49 ns |      - |         - |
| &#39;Strategy.GenerateOp: SortedSet&#39;       |    61.88 ns |  0.276 ns |  0.413 ns |    61.86 ns | 0.0053 |      56 B |
| &#39;Strategy.GenerateOp: RGA&#39;             |   217.13 ns |  1.609 ns |  2.358 ns |   216.96 ns | 0.0443 |     464 B |
| &#39;Strategy.GenerateOp: CounterMap&#39;      |    59.57 ns |  0.334 ns |  0.489 ns |    59.36 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: LwwMap&#39;          |    61.03 ns |  0.397 ns |  0.569 ns |    60.79 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: FwwMap&#39;          |    69.80 ns |  0.237 ns |  0.341 ns |    69.84 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: MaxWinsMap&#39;      |    59.06 ns |  0.182 ns |  0.273 ns |    59.06 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: MinWinsMap&#39;      |    59.22 ns |  0.199 ns |  0.291 ns |    59.18 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: OrMap&#39;           |   151.83 ns |  0.204 ns |  0.279 ns |   151.91 ns | 0.0046 |      48 B |
| &#39;Strategy.GenerateOp: Graph&#39;           |    58.53 ns |  0.379 ns |  0.567 ns |    58.36 ns | 0.0023 |      24 B |
| &#39;Strategy.GenerateOp: TwoPhaseGraph&#39;   |    69.33 ns |  0.263 ns |  0.393 ns |    69.34 ns | 0.0023 |      24 B |
| &#39;Strategy.GenerateOp: ReplicatedTree&#39;  |   125.44 ns |  0.245 ns |  0.359 ns |   125.45 ns | 0.0053 |      56 B |
| &#39;Strategy.GenerateOp: EpochBound&#39;      |   114.48 ns |  0.339 ns |  0.496 ns |   114.42 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: EpochClear&#39;      |    70.42 ns |  0.338 ns |  0.463 ns |    70.29 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: ApprovalQuorum&#39;  |   132.90 ns |  0.825 ns |  1.209 ns |   133.01 ns | 0.0145 |     152 B |


<!-- BENCHMARKS_END -->