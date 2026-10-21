# Ama.CRDT.Benchmarks

These benchmarks test the individual CRDT strategy application, patch generation, and other performance metrics.

<!-- BENCHMARKS_START -->
```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
Intel Core i9-10850K CPU 3.60GHz, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=MediumRun  Toolchain=InProcessNoEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
| Method            | Mean       | Error     | StdDev    | Median     | Gen0   | Allocated |
|------------------ |-----------:|----------:|----------:|-----------:|-------:|----------:|
| ApplyPatchSimple  |   439.4 ns |  36.42 ns |  52.23 ns |   416.6 ns | 0.0753 |     792 B |
| ApplyPatchComplex | 1,484.9 ns | 136.69 ns | 204.59 ns | 1,360.5 ns | 0.2480 |    2608 B |


```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
Intel Core i9-10850K CPU 3.60GHz, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=MediumRun  Toolchain=InProcessNoEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
| Method               | Mean       | Error    | StdDev   | Gen0   | Gen1   | Allocated |
|--------------------- |-----------:|---------:|---------:|-------:|-------:|----------:|
| GeneratePatchSimple  |   763.8 ns | 11.43 ns | 16.39 ns | 0.1659 |      - |    1.7 KB |
| GeneratePatchComplex | 3,522.6 ns | 33.57 ns | 45.95 ns | 0.6027 | 0.0038 |   6.18 KB |


```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
Intel Core i9-10850K CPU 3.60GHz, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=MediumRun  Toolchain=InProcessNoEmitToolchain  InvocationCount=1  
IterationCount=15  LaunchCount=2  UnrollFactor=1  
WarmupCount=10  

```
| Method                            | Mean        | Error      | StdDev    | Median      | Allocated |
|---------------------------------- |------------:|-----------:|----------:|------------:|----------:|
| &#39;Strategy.Apply: LWW&#39;             |   433.85 ns |  22.787 ns |  34.11 ns |   439.75 ns |     274 B |
| &#39;Strategy.Apply: FWW&#39;             |   324.37 ns |  27.270 ns |  38.23 ns |   332.00 ns |     250 B |
| &#39;Strategy.Apply: Counter&#39;         |   531.30 ns |  68.259 ns | 102.17 ns |   482.50 ns |     767 B |
| &#39;Strategy.Apply: GCounter&#39;        |   607.41 ns |  98.450 ns | 144.31 ns |   540.00 ns |     705 B |
| &#39;Strategy.Apply: BoundedCounter&#39;  |    97.55 ns |  19.434 ns |  28.49 ns |    91.00 ns |     152 B |
| &#39;Strategy.Apply: MaxWins&#39;         |    90.44 ns |   7.334 ns |  10.28 ns |    90.00 ns |     152 B |
| &#39;Strategy.Apply: MinWins&#39;         |    98.33 ns |  12.187 ns |  18.24 ns |    95.50 ns |     152 B |
| &#39;Strategy.Apply: AverageRegister&#39; |   603.10 ns | 163.021 ns | 244.00 ns |   467.50 ns |     993 B |
| &#39;Strategy.Apply: GSet&#39;            |   563.50 ns |  95.982 ns | 143.66 ns |   494.50 ns |     705 B |
| &#39;Strategy.Apply: TwoPhaseSet&#39;     |   530.76 ns |  85.277 ns | 125.00 ns |   475.50 ns |     913 B |
| &#39;Strategy.Apply: LwwSet&#39;          | 1,493.23 ns | 279.570 ns | 418.45 ns | 1,267.50 ns |    2249 B |
| &#39;Strategy.Apply: FwwSet&#39;          | 1,510.23 ns | 140.200 ns | 209.85 ns | 1,587.00 ns |    1865 B |
| &#39;Strategy.Apply: OrSet&#39;           | 1,397.76 ns | 258.846 ns | 379.41 ns | 1,185.00 ns |    2777 B |
| &#39;Strategy.Apply: ArrayLcs&#39;        |   972.00 ns | 154.682 ns | 226.73 ns | 1,090.00 ns |     457 B |
| &#39;Strategy.Apply: FixedSizeArray&#39;  |   440.93 ns |  53.272 ns |  79.73 ns |   411.50 ns |     306 B |
| &#39;Strategy.Apply: Lseq&#39;            |   896.50 ns | 253.047 ns | 378.75 ns |   876.00 ns |     385 B |
| &#39;Strategy.Apply: VoteCounter&#39;     |   677.50 ns | 135.797 ns | 203.25 ns |   591.00 ns |     617 B |
| &#39;Strategy.Apply: StateMachine&#39;    |   577.79 ns |  31.559 ns |  45.26 ns |   575.00 ns |     351 B |
| &#39;Strategy.Apply: PriorityQueue&#39;   | 6,326.64 ns | 533.203 ns | 764.70 ns | 6,063.00 ns |    9857 B |
| &#39;Strategy.Apply: SortedSet&#39;       | 4,737.90 ns | 394.660 ns | 590.71 ns | 4,463.00 ns |    7073 B |
| &#39;Strategy.Apply: RGA&#39;             | 2,192.81 ns | 561.240 ns | 786.78 ns | 2,054.50 ns |    3113 B |
| &#39;Strategy.Apply: CounterMap&#39;      |   988.62 ns |  82.604 ns | 121.08 ns |   942.00 ns |    1241 B |
| &#39;Strategy.Apply: LwwMap&#39;          |   882.45 ns |  87.047 ns | 130.29 ns |   854.25 ns |     921 B |
| &#39;Strategy.Apply: FwwMap&#39;          |   602.11 ns | 101.783 ns | 145.97 ns |   565.50 ns |     422 B |
| &#39;Strategy.Apply: MaxWinsMap&#39;      |   731.78 ns |  37.102 ns |  52.01 ns |   717.50 ns |     577 B |
| &#39;Strategy.Apply: MinWinsMap&#39;      |   158.24 ns |   7.869 ns |  11.03 ns |   159.50 ns |     304 B |
| &#39;Strategy.Apply: OrMap&#39;           | 1,463.36 ns |  55.714 ns |  79.90 ns | 1,440.50 ns |    2297 B |
| &#39;Strategy.Apply: Graph&#39;           |    87.57 ns |   7.710 ns |  11.30 ns |    87.00 ns |     152 B |
| &#39;Strategy.Apply: TwoPhaseGraph&#39;   |   440.77 ns |  27.887 ns |  41.74 ns |   434.00 ns |     529 B |
| &#39;Strategy.Apply: ReplicatedTree&#39;  |   523.93 ns |  83.933 ns | 123.03 ns |   478.00 ns |     865 B |
| &#39;Strategy.Apply: EpochBound&#39;      | 1,707.65 ns |  19.133 ns |  26.19 ns | 1,703.00 ns |     297 B |
| &#39;Strategy.Apply: ApprovalQuorum&#39;  |    98.10 ns |   7.403 ns |  10.85 ns |    96.00 ns |     152 B |


```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
Intel Core i9-10850K CPU 3.60GHz, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=MediumRun  Toolchain=InProcessNoEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
| Method                               | Mean       | Error    | StdDev   | Median     | Gen0   | Allocated |
|------------------------------------- |-----------:|---------:|---------:|-----------:|-------:|----------:|
| &#39;Strategy.Generate: LWW&#39;             |   132.3 ns |  1.32 ns |  1.94 ns |   131.9 ns | 0.0143 |     152 B |
| &#39;Strategy.Generate: FWW&#39;             |   132.3 ns |  1.21 ns |  1.73 ns |   132.7 ns | 0.0143 |     152 B |
| &#39;Strategy.Generate: Counter&#39;         |   158.5 ns |  1.39 ns |  2.03 ns |   159.1 ns | 0.0329 |     344 B |
| &#39;Strategy.Generate: GCounter&#39;        |   201.1 ns |  1.02 ns |  1.46 ns |   200.9 ns | 0.0329 |     344 B |
| &#39;Strategy.Generate: BoundedCounter&#39;  |   160.0 ns |  2.67 ns |  3.65 ns |   159.2 ns | 0.0329 |     344 B |
| &#39;Strategy.Generate: MaxWins&#39;         |   117.7 ns |  0.53 ns |  0.77 ns |   117.7 ns | 0.0191 |     200 B |
| &#39;Strategy.Generate: MinWins&#39;         |   118.5 ns |  0.59 ns |  0.86 ns |   118.4 ns | 0.0191 |     200 B |
| &#39;Strategy.Generate: AverageRegister&#39; |   117.8 ns |  1.71 ns |  2.51 ns |   117.0 ns | 0.0205 |     216 B |
| &#39;Strategy.Generate: GSet&#39;            |   385.1 ns |  3.30 ns |  4.94 ns |   386.0 ns | 0.0792 |     832 B |
| &#39;Strategy.Generate: TwoPhaseSet&#39;     |   638.7 ns |  6.85 ns | 10.05 ns |   638.3 ns | 0.1602 |    1680 B |
| &#39;Strategy.Generate: LwwSet&#39;          |   490.9 ns |  5.13 ns |  7.52 ns |   491.8 ns | 0.1221 |    1280 B |
| &#39;Strategy.Generate: FwwSet&#39;          |   486.0 ns |  5.56 ns |  7.79 ns |   488.9 ns | 0.1221 |    1280 B |
| &#39;Strategy.Generate: OrSet&#39;           | 1,077.4 ns |  8.12 ns | 11.90 ns | 1,082.4 ns | 0.2098 |    2208 B |
| &#39;Strategy.Generate: ArrayLcs&#39;        | 1,523.8 ns | 25.07 ns | 35.95 ns | 1,520.3 ns | 0.3147 |    3296 B |
| &#39;Strategy.Generate: FixedSizeArray&#39;  |   453.6 ns | 18.27 ns | 27.35 ns |   463.3 ns | 0.0591 |     624 B |
| &#39;Strategy.Generate: Lseq&#39;            |   358.3 ns |  5.95 ns |  8.91 ns |   357.2 ns | 0.0868 |     912 B |
| &#39;Strategy.Generate: VoteCounter&#39;     |   427.6 ns |  3.31 ns |  4.95 ns |   427.5 ns | 0.1044 |    1096 B |
| &#39;Strategy.Generate: StateMachine&#39;    |   172.5 ns |  2.82 ns |  3.95 ns |   172.0 ns | 0.0143 |     152 B |
| &#39;Strategy.Generate: PriorityQueue&#39;   | 4,687.5 ns | 29.50 ns | 43.25 ns | 4,664.9 ns | 0.7629 |    7984 B |
| &#39;Strategy.Generate: SortedSet&#39;       | 1,751.7 ns |  8.83 ns | 13.21 ns | 1,751.6 ns | 0.3433 |    3592 B |
| &#39;Strategy.Generate: RGA&#39;             |   865.5 ns |  8.11 ns | 12.14 ns |   864.0 ns | 0.2546 |    2672 B |
| &#39;Strategy.Generate: CounterMap&#39;      | 1,000.3 ns |  4.21 ns |  6.17 ns | 1,000.2 ns | 0.2842 |    2984 B |
| &#39;Strategy.Generate: LwwMap&#39;          | 1,221.8 ns |  8.23 ns | 12.32 ns | 1,219.7 ns | 0.2632 |    2760 B |
| &#39;Strategy.Generate: FwwMap&#39;          | 1,072.4 ns | 11.00 ns | 16.46 ns | 1,066.5 ns | 0.2594 |    2728 B |
| &#39;Strategy.Generate: MaxWinsMap&#39;      |   628.3 ns |  3.78 ns |  5.66 ns |   628.1 ns | 0.1507 |    1584 B |
| &#39;Strategy.Generate: MinWinsMap&#39;      |   629.2 ns |  4.29 ns |  6.42 ns |   628.8 ns | 0.1507 |    1584 B |
| &#39;Strategy.Generate: OrMap&#39;           | 1,469.0 ns |  4.49 ns |  6.44 ns | 1,469.3 ns | 0.3052 |    3200 B |
| &#39;Strategy.Generate: Graph&#39;           |   400.5 ns |  3.50 ns |  5.01 ns |   399.3 ns | 0.0877 |     920 B |
| &#39;Strategy.Generate: TwoPhaseGraph&#39;   |   419.8 ns |  3.35 ns |  4.90 ns |   418.7 ns | 0.1063 |    1112 B |
| &#39;Strategy.Generate: ReplicatedTree&#39;  |   540.4 ns |  2.22 ns |  3.12 ns |   540.9 ns | 0.1087 |    1144 B |
| &#39;Strategy.Generate: EpochBound&#39;      |   494.0 ns |  2.88 ns |  4.12 ns |   492.5 ns | 0.0801 |     840 B |
| &#39;Strategy.Generate: ApprovalQuorum&#39;  |   429.4 ns |  3.09 ns |  4.43 ns |   430.7 ns | 0.0792 |     832 B |


```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
Intel Core i9-10850K CPU 3.60GHz, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=MediumRun  Toolchain=InProcessNoEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
| Method                                 | Mean      | Error    | StdDev   | Median    | Gen0   | Allocated |
|--------------------------------------- |----------:|---------:|---------:|----------:|-------:|----------:|
| &#39;Strategy.GenerateOp: LWW&#39;             |  54.33 ns | 0.085 ns | 0.119 ns |  54.29 ns |      - |         - |
| &#39;Strategy.GenerateOp: FWW&#39;             |  54.44 ns | 0.348 ns | 0.487 ns |  54.24 ns |      - |         - |
| &#39;Strategy.GenerateOp: Counter&#39;         |  78.76 ns | 0.379 ns | 0.556 ns |  78.88 ns | 0.0084 |      88 B |
| &#39;Strategy.GenerateOp: GCounter&#39;        | 109.17 ns | 1.133 ns | 1.624 ns | 108.94 ns | 0.0084 |      88 B |
| &#39;Strategy.GenerateOp: BoundedCounter&#39;  |  55.27 ns | 1.124 ns | 1.648 ns |  54.22 ns |      - |         - |
| &#39;Strategy.GenerateOp: MaxWins&#39;         |  54.62 ns | 0.297 ns | 0.435 ns |  54.39 ns |      - |         - |
| &#39;Strategy.GenerateOp: MinWins&#39;         |  54.98 ns | 0.264 ns | 0.386 ns |  55.01 ns |      - |         - |
| &#39;Strategy.GenerateOp: AverageRegister&#39; |  54.59 ns | 0.526 ns | 0.772 ns |  54.16 ns |      - |         - |
| &#39;Strategy.GenerateOp: GSet&#39;            |  88.09 ns | 1.123 ns | 1.610 ns |  88.23 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: TwoPhaseSet&#39;     |  54.32 ns | 0.029 ns | 0.038 ns |  54.32 ns |      - |         - |
| &#39;Strategy.GenerateOp: LwwSet&#39;          |  54.37 ns | 0.128 ns | 0.179 ns |  54.29 ns |      - |         - |
| &#39;Strategy.GenerateOp: FwwSet&#39;          |  54.51 ns | 0.242 ns | 0.346 ns |  54.34 ns |      - |         - |
| &#39;Strategy.GenerateOp: OrSet&#39;           | 148.76 ns | 1.160 ns | 1.700 ns | 150.06 ns | 0.0069 |      72 B |
| &#39;Strategy.GenerateOp: ArrayLcs&#39;        | 424.18 ns | 1.092 ns | 1.601 ns | 424.73 ns | 0.0232 |     248 B |
| &#39;Strategy.GenerateOp: FixedSizeArray&#39;  |  99.98 ns | 0.660 ns | 0.988 ns |  99.96 ns | 0.0053 |      56 B |
| &#39;Strategy.GenerateOp: Lseq&#39;            | 123.95 ns | 0.433 ns | 0.635 ns | 124.06 ns | 0.0107 |     112 B |
| &#39;Strategy.GenerateOp: VoteCounter&#39;     |  60.07 ns | 0.386 ns | 0.554 ns |  60.04 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: StateMachine&#39;    | 241.05 ns | 0.818 ns | 1.200 ns | 240.81 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: PriorityQueue&#39;   |  81.67 ns | 0.935 ns | 1.399 ns |  81.58 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: SortedSet&#39;       |  62.37 ns | 0.256 ns | 0.383 ns |  62.30 ns | 0.0053 |      56 B |
| &#39;Strategy.GenerateOp: RGA&#39;             | 251.65 ns | 0.715 ns | 1.070 ns | 251.41 ns | 0.0580 |     608 B |
| &#39;Strategy.GenerateOp: CounterMap&#39;      |  59.59 ns | 0.370 ns | 0.543 ns |  59.31 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: LwwMap&#39;          |  59.95 ns | 0.726 ns | 1.041 ns |  59.73 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: FwwMap&#39;          |  58.88 ns | 0.402 ns | 0.564 ns |  59.12 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: MaxWinsMap&#39;      |  59.86 ns | 0.261 ns | 0.383 ns |  60.04 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: MinWinsMap&#39;      |  59.31 ns | 0.187 ns | 0.268 ns |  59.32 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: OrMap&#39;           | 169.45 ns | 1.853 ns | 2.657 ns | 168.25 ns | 0.0107 |     112 B |
| &#39;Strategy.GenerateOp: Graph&#39;           |  58.47 ns | 0.348 ns | 0.511 ns |  58.20 ns | 0.0023 |      24 B |
| &#39;Strategy.GenerateOp: TwoPhaseGraph&#39;   |  82.92 ns | 0.595 ns | 0.891 ns |  82.71 ns | 0.0023 |      24 B |
| &#39;Strategy.GenerateOp: ReplicatedTree&#39;  | 135.71 ns | 0.331 ns | 0.475 ns | 135.71 ns | 0.0053 |      56 B |
| &#39;Strategy.GenerateOp: EpochBound&#39;      | 599.30 ns | 2.120 ns | 3.107 ns | 598.12 ns | 0.0171 |     184 B |
| &#39;Strategy.GenerateOp: EpochClear&#39;      | 355.01 ns | 0.255 ns | 0.358 ns | 354.99 ns | 0.0079 |      88 B |
| &#39;Strategy.GenerateOp: ApprovalQuorum&#39;  | 280.39 ns | 2.025 ns | 2.968 ns | 279.66 ns | 0.0113 |     120 B |


<!-- BENCHMARKS_END -->