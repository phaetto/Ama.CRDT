```

BenchmarkDotNet v0.15.2, Windows 11 (10.0.26200.7840)
Intel Core i9-10850K CPU 3.60GHz, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.103
  [Host] : .NET 9.0.13 (9.0.1326.6317), X64 RyuJIT AVX2

Job=MediumRun  Toolchain=InProcessNoEmitToolchain  InvocationCount=1  
IterationCount=15  LaunchCount=2  UnrollFactor=1  
WarmupCount=10  

```
| Method                            | Mean         | Error      | StdDev     | Median       | Allocated |
|---------------------------------- |-------------:|-----------:|-----------:|-------------:|----------:|
| &#39;Strategy.Apply: LWW&#39;             |     18.52 ns |   1.729 ns |   2.424 ns |     18.00 ns |         - |
| &#39;Strategy.Apply: Counter&#39;         |    744.82 ns |  26.904 ns |  38.585 ns |    749.00 ns |     216 B |
| &#39;Strategy.Apply: GCounter&#39;        |    306.34 ns |  38.642 ns |  56.641 ns |    287.00 ns |     250 B |
| &#39;Strategy.Apply: BoundedCounter&#39;  |  1,408.04 ns |  72.988 ns | 102.319 ns |  1,399.00 ns |     576 B |
| &#39;Strategy.Apply: MaxWins&#39;         |    322.86 ns |  26.710 ns |  38.306 ns |    313.00 ns |     220 B |
| &#39;Strategy.Apply: MinWins&#39;         |    300.75 ns |  10.909 ns |  15.646 ns |    296.50 ns |     227 B |
| &#39;Strategy.Apply: AverageRegister&#39; |    468.79 ns | 118.103 ns | 173.113 ns |    386.00 ns |     652 B |
| &#39;Strategy.Apply: GSet&#39;            |    444.76 ns |  65.269 ns |  95.670 ns |    411.00 ns |     580 B |
| &#39;Strategy.Apply: TwoPhaseSet&#39;     |    504.81 ns |  33.283 ns |  45.559 ns |    497.50 ns |     745 B |
| &#39;Strategy.Apply: LwwSet&#39;          |  1,182.20 ns | 139.682 ns | 209.069 ns |  1,100.00 ns |    1664 B |
| &#39;Strategy.Apply: OrSet&#39;           |  1,412.07 ns | 179.727 ns | 269.007 ns |  1,315.50 ns |    2645 B |
| &#39;Strategy.Apply: ArrayLcs&#39;        |    809.84 ns | 167.584 ns | 240.344 ns |    715.25 ns |     312 B |
| &#39;Strategy.Apply: FixedSizeArray&#39;  |    326.33 ns |  28.442 ns |  39.872 ns |    310.00 ns |     130 B |
| &#39;Strategy.Apply: Lseq&#39;            |    617.13 ns | 287.556 ns | 430.400 ns |    435.50 ns |     315 B |
| &#39;Strategy.Apply: VoteCounter&#39;     |     23.70 ns |   3.256 ns |   4.564 ns |     24.00 ns |       3 B |
| &#39;Strategy.Apply: StateMachine&#39;    |  4,487.26 ns | 269.950 ns | 378.432 ns |  4,308.00 ns |     812 B |
| &#39;Strategy.Apply: PriorityQueue&#39;   | 13,990.29 ns | 110.228 ns | 158.085 ns | 13,995.00 ns |   14064 B |
| &#39;Strategy.Apply: SortedSet&#39;       |  2,796.27 ns | 241.734 ns | 361.816 ns |  2,784.00 ns |    2303 B |
| &#39;Strategy.Apply: RGA&#39;             |  1,618.10 ns | 587.867 ns | 861.688 ns |  1,238.00 ns |    2456 B |
| &#39;Strategy.Apply: CounterMap&#39;      |    857.92 ns |  21.029 ns |  28.785 ns |    851.25 ns |    1194 B |
| &#39;Strategy.Apply: LwwMap&#39;          |    658.68 ns |  40.595 ns |  58.221 ns |    639.00 ns |     744 B |
| &#39;Strategy.Apply: MaxWinsMap&#39;      |    534.21 ns |  32.765 ns |  46.991 ns |    518.50 ns |     352 B |
| &#39;Strategy.Apply: MinWinsMap&#39;      |    536.69 ns |  29.343 ns |  40.164 ns |    518.00 ns |     352 B |
| &#39;Strategy.Apply: OrMap&#39;           |  1,101.64 ns |  55.460 ns |  79.539 ns |  1,080.00 ns |    1904 B |
| &#39;Strategy.Apply: Graph&#39;           |    273.20 ns |  15.875 ns |  23.761 ns |    267.00 ns |     276 B |
| &#39;Strategy.Apply: TwoPhaseGraph&#39;   |    369.95 ns |  21.584 ns |  31.637 ns |    356.50 ns |     472 B |
| &#39;Strategy.Apply: ReplicatedTree&#39;  |    433.90 ns |  18.664 ns |  24.917 ns |    428.00 ns |     856 B |
