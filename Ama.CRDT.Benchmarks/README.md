```

BenchmarkDotNet v0.15.2, Windows 10 (10.0.19045.6216/22H2/2022Update)
Intel Core i9-10850K CPU 3.60GHz, 1 CPU, 20 logical and 10 physical cores
.NET SDK 9.0.304
  [Host] : .NET 9.0.8 (9.0.825.36511), X64 RyuJIT AVX2

Job=MediumRun  Toolchain=InProcessNoEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
| Method                           | Mean      | Error     | StdDev    | Gen0   | Gen1   | Allocated |
|--------------------------------- |----------:|----------:|----------:|-------:|-------:|----------:|
| &#39;GeneratePatch: LWW&#39;             | 41.449 μs | 0.3033 μs | 0.4350 μs | 3.1738 |      - |  32.89 KB |
| &#39;GeneratePatch: Counter&#39;         | 41.022 μs | 0.1527 μs | 0.2189 μs | 3.1738 |      - |  32.92 KB |
| &#39;GeneratePatch: GCounter&#39;        | 41.831 μs | 0.3985 μs | 0.5715 μs | 3.1738 |      - |  32.92 KB |
| &#39;GeneratePatch: BoundedCounter&#39;  | 42.227 μs | 0.2931 μs | 0.4387 μs | 3.1738 |      - |  32.92 KB |
| &#39;GeneratePatch: MaxWins&#39;         | 41.896 μs | 0.4044 μs | 0.5800 μs | 3.1738 |      - |  32.89 KB |
| &#39;GeneratePatch: MinWins&#39;         | 42.336 μs | 0.3590 μs | 0.5373 μs | 3.1738 |      - |  32.89 KB |
| &#39;GeneratePatch: AverageRegister&#39; | 42.331 μs | 0.2782 μs | 0.4165 μs | 3.1738 |      - |  32.89 KB |
| &#39;GeneratePatch: GSet&#39;            | 41.528 μs | 0.1813 μs | 0.2658 μs | 3.2349 |      - |  33.25 KB |
| &#39;GeneratePatch: TwoPhaseSet&#39;     | 42.032 μs | 0.2583 μs | 0.3866 μs | 3.1738 |      - |  32.64 KB |
| &#39;GeneratePatch: LwwSet&#39;          | 41.474 μs | 0.1587 μs | 0.2376 μs | 3.1738 |      - |  32.76 KB |
| &#39;GeneratePatch: OrSet&#39;           | 42.979 μs | 0.3179 μs | 0.4660 μs | 3.2349 |      - |  33.15 KB |
| &#39;GeneratePatch: ArrayLcs&#39;        | 44.689 μs | 0.3405 μs | 0.4991 μs | 3.5400 |      - |   36.4 KB |
| &#39;GeneratePatch: FixedSizeArray&#39;  | 42.042 μs | 0.3024 μs | 0.4526 μs | 3.1738 |      - |  32.89 KB |
| &#39;GeneratePatch: Lseq&#39;            | 41.912 μs | 0.1862 μs | 0.2730 μs | 3.2959 |      - |  34.07 KB |
| &#39;GeneratePatch: VoteCounter&#39;     | 42.454 μs | 0.3955 μs | 0.5671 μs | 3.1738 |      - |  32.94 KB |
| &#39;GeneratePatch: StateMachine&#39;    | 43.829 μs | 0.0946 μs | 0.1387 μs | 3.2349 |      - |  33.42 KB |
| &#39;GeneratePatch: ExclusiveLock&#39;   | 42.157 μs | 0.2470 μs | 0.3620 μs | 3.1738 |      - |  32.92 KB |
| &#39;GeneratePatch: PriorityQueue&#39;   | 41.979 μs | 0.1811 μs | 0.2655 μs | 3.1738 |      - |  32.78 KB |
| &#39;GeneratePatch: SortedSet&#39;       | 41.008 μs | 0.0927 μs | 0.1329 μs | 3.2349 |      - |   33.2 KB |
| &#39;ApplyPatch: LWW&#39;                |  3.665 μs | 0.0216 μs | 0.0316 μs | 0.4654 | 0.0076 |   4.76 KB |
| &#39;ApplyPatch: Counter&#39;            |  4.242 μs | 0.0297 μs | 0.0426 μs | 0.5951 | 0.0076 |   6.14 KB |
| &#39;ApplyPatch: GCounter&#39;           |  4.417 μs | 0.0374 μs | 0.0548 μs | 0.5951 | 0.0076 |   6.14 KB |
| &#39;ApplyPatch: BoundedCounter&#39;     |  5.951 μs | 0.0484 μs | 0.0725 μs | 0.6485 | 0.0076 |   6.69 KB |
| &#39;ApplyPatch: MaxWins&#39;            |  3.879 μs | 0.1150 μs | 0.1721 μs | 0.4539 | 0.0038 |   4.65 KB |
| &#39;ApplyPatch: MinWins&#39;            |  5.103 μs | 0.1257 μs | 0.1882 μs | 0.4501 |      - |   4.65 KB |
| &#39;ApplyPatch: AverageRegister&#39;    |  5.606 μs | 0.0537 μs | 0.0786 μs | 0.5035 | 0.0076 |   5.16 KB |
| &#39;ApplyPatch: GSet&#39;               |  5.225 μs | 0.0982 μs | 0.1469 μs | 0.4883 | 0.0076 |   5.03 KB |
| &#39;ApplyPatch: TwoPhaseSet&#39;        |  6.102 μs | 0.1655 μs | 0.2477 μs | 0.5493 | 0.0076 |   5.65 KB |
| &#39;ApplyPatch: LwwSet&#39;             | 10.904 μs | 0.3932 μs | 0.5763 μs | 0.9613 | 0.0153 |   9.83 KB |
| &#39;ApplyPatch: OrSet&#39;              |  9.052 μs | 0.6607 μs | 0.9476 μs | 0.8698 | 0.0153 |   8.97 KB |
| &#39;ApplyPatch: ArrayLcs&#39;           |  9.571 μs | 0.1635 μs | 0.2346 μs | 0.9308 | 0.0153 |   9.54 KB |
| &#39;ApplyPatch: FixedSizeArray&#39;     |  6.119 μs | 0.0889 μs | 0.1187 μs | 0.5951 | 0.0076 |   6.12 KB |
| &#39;ApplyPatch: Lseq&#39;               |  5.068 μs | 0.0856 μs | 0.1281 μs | 0.4883 | 0.0076 |   5.01 KB |
| &#39;ApplyPatch: VoteCounter&#39;        |  5.587 μs | 0.1038 μs | 0.1488 μs | 0.5035 | 0.0076 |   5.18 KB |
| &#39;ApplyPatch: StateMachine&#39;       |  9.225 μs | 0.3007 μs | 0.4407 μs | 0.5341 |      - |   5.59 KB |
| &#39;ApplyPatch: ExclusiveLock&#39;      |  8.308 μs | 0.1973 μs | 0.2953 μs | 0.6866 |      - |    7.1 KB |
| &#39;ApplyPatch: PriorityQueue&#39;      | 29.436 μs | 0.6613 μs | 0.9694 μs | 1.8921 | 0.0305 |  19.44 KB |
| &#39;ApplyPatch: SortedSet&#39;          | 12.015 μs | 0.3742 μs | 0.5367 μs | 0.9308 | 0.0153 |   9.66 KB |
