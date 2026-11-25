# Ama.CRDT.Benchmarks

These benchmarks test the individual CRDT strategy application, patch generation, and other performance metrics.

<!-- BENCHMARKS_START -->
```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9974)
Intel Core i9-10850K CPU 3.60GHz, 1 CPU, 20 logical and 10 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=MediumRun  Toolchain=InProcessNoEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
| Method            | Mean       | Error    | StdDev   | Gen0   | Allocated |
|------------------ |-----------:|---------:|---------:|-------:|----------:|
| ApplyPatchSimple  |   328.7 ns |  2.33 ns |  3.41 ns | 0.0677 |     712 B |
| ApplyPatchComplex | 1,219.4 ns | 11.66 ns | 17.09 ns | 0.2270 |    2392 B |


```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9974)
Intel Core i9-10850K CPU 3.60GHz, 1 CPU, 20 logical and 10 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=MediumRun  Toolchain=InProcessNoEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
| Method                                           | Categories                      | Mean             | Error          | StdDev         | Median           | Ratio     | RatioSD | Meta Overhead % | Meta/Data Ratio | Gen0     | Gen1    | Gen2    | Allocated | Alloc Ratio |
|------------------------------------------------- |-------------------------------- |-----------------:|---------------:|---------------:|-----------------:|----------:|--------:|----------------:|----------------:|---------:|--------:|--------:|----------:|------------:|
| &#39;DVV: 3 Replicas, 0 Dots (Linear)&#39;               | DVV Dimensions                  |        81.113 ns |      4.0854 ns |      6.1149 ns |        80.651 ns |      1.01 |    0.11 |               - |               - |   0.0206 |       - |       - |     216 B |        1.00 |
| &#39;DVV: 3 Replicas, 5 Dots/Replica&#39;                | DVV Dimensions                  |       710.281 ns |      2.8029 ns |      4.1953 ns |       709.326 ns |      8.80 |    0.65 |               - |               - |   0.0839 |       - |       - |     880 B |        4.07 |
| &#39;DVV: 10 Replicas, 0 Dots (Linear)&#39;              | DVV Dimensions                  |       473.034 ns |      9.4660 ns |     14.1682 ns |       476.694 ns |      5.86 |    0.47 |               - |               - |   0.0615 |       - |       - |     648 B |        3.00 |
| &#39;DVV: 10 Replicas, 10 Dots/Replica&#39;              | DVV Dimensions                  |     9,336.863 ns |     35.7200 ns |     53.4641 ns |     9,334.893 ns |    115.74 |    8.59 |               - |               - |   0.4272 |       - |       - |    4608 B |       21.33 |
| &#39;DVV: 100 Replicas, 0 Dots (Linear)&#39;             | DVV Dimensions                  |     3,994.470 ns |    108.4833 ns |    162.3728 ns |     3,967.126 ns |     49.52 |    4.17 |               - |               - |   0.9460 |  0.0381 |       - |    9928 B |       45.96 |
| &#39;DVV: 100 Replicas, 20 Dots/Replica&#39;             | DVV Dimensions                  |   129,616.143 ns |    221.4172 ns |    331.4069 ns |   129,618.408 ns |  1,606.78 |  118.98 |               - |               - |  13.1836 |  4.3945 |       - |  139729 B |      646.89 |
|                                                  |                                 |                  |                |                |                  |           |         |                 |                 |          |         |         |           |             |
| &#39;DataOnly: CollectionPoco (3x100 items)&#39;         | Doc: CollectionPoco (100 items) |     1,907.693 ns |     24.6505 ns |     36.8957 ns |     1,898.880 ns |      1.00 |    0.03 |     0.0% (Base) |    1.00x (Data) |   0.4807 |  0.0095 |       - |    5040 B |        1.00 |
| &#39;MetadataOnly: CollectionPoco (3x100 items)&#39;     | Doc: CollectionPoco (100 items) |    15,535.152 ns |    128.0832 ns |    191.7088 ns |    15,500.191 ns |      8.15 |    0.18 |          575.4% |           5.75x |   2.7466 |  0.4578 |       - |   29000 B |        5.75 |
| &#39;FullDoc: CollectionPoco (3x100 items)&#39;          | Doc: CollectionPoco (100 items) |    17,608.134 ns |    144.2132 ns |    215.8515 ns |    17,544.475 ns |      9.23 |    0.21 |          575.9% |           5.76x |   3.2349 |  0.7935 |       - |   34064 B |        6.76 |
| &#39;PackedJson: CollectionPoco (3x100 items)&#39;       | Doc: CollectionPoco (100 items) |   152,363.851 ns |    632.5688 ns |    907.2114 ns |   152,194.739 ns |     79.90 |    1.57 |         4551.2% |          45.51x |  22.2168 |  4.3945 |       - |  234418 B |       46.51 |
| &#39;PackedBrotli: CollectionPoco (3x100 items)&#39;     | Doc: CollectionPoco (100 items) |   199,183.465 ns |  1,430.0447 ns |  2,096.1412 ns |   198,199.243 ns |    104.45 |    2.23 |         4130.6% |          41.31x |  20.2637 |  1.7090 |       - |  213222 B |       42.31 |
|                                                  |                                 |                  |                |                |                  |           |         |                 |                 |          |         |         |           |             |
| &#39;DataOnly: CollectionPoco (3x25 items)&#39;          | Doc: CollectionPoco (25 items)  |       490.278 ns |      3.4106 ns |      4.9992 ns |       490.042 ns |      1.00 |    0.01 |     0.0% (Base) |    1.00x (Data) |   0.1583 |  0.0010 |       - |    1656 B |        1.00 |
| &#39;MetadataOnly: CollectionPoco (3x25 items)&#39;      | Doc: CollectionPoco (25 items)  |     4,285.432 ns |     20.5582 ns |     30.7705 ns |     4,290.398 ns |      8.74 |    0.11 |          503.4% |           5.03x |   0.7935 |  0.0381 |       - |    8336 B |        5.03 |
| &#39;FullDoc: CollectionPoco (3x25 items)&#39;           | Doc: CollectionPoco (25 items)  |     4,861.174 ns |     16.8494 ns |     25.2193 ns |     4,858.886 ns |      9.92 |    0.11 |          504.8% |           5.05x |   0.9537 |  0.0839 |       - |   10016 B |        6.05 |
| &#39;PackedJson: CollectionPoco (3x25 items)&#39;        | Doc: CollectionPoco (25 items)  |    39,788.043 ns |    136.0450 ns |    199.4130 ns |    39,834.143 ns |     81.16 |    0.91 |         3574.9% |          35.75x |   5.7983 |  0.4272 |       - |   60856 B |       36.75 |
| &#39;PackedBrotli: CollectionPoco (3x25 items)&#39;      | Doc: CollectionPoco (25 items)  |    57,339.364 ns |    678.4255 ns |    972.9777 ns |    56,932.718 ns |    116.96 |    2.28 |         3350.4% |          33.50x |   5.4321 |  0.1221 |       - |   57138 B |       34.50 |
|                                                  |                                 |                  |                |                |                  |           |         |                 |                 |          |         |         |           |             |
| &#39;DataOnly: CollectionPoco (3x5 items)&#39;           | Doc: CollectionPoco (5 items)   |       138.842 ns |      1.0803 ns |      1.5493 ns |       138.592 ns |      1.00 |    0.02 |     0.0% (Base) |    1.00x (Data) |   0.0687 |  0.0002 |       - |     720 B |        1.00 |
| &#39;MetadataOnly: CollectionPoco (3x5 items)&#39;       | Doc: CollectionPoco (5 items)   |     1,298.054 ns |      2.9182 ns |      4.3678 ns |     1,297.600 ns |      9.35 |    0.11 |          306.7% |           3.07x |   0.2098 |  0.0019 |       - |    2208 B |        3.07 |
| &#39;FullDoc: CollectionPoco (3x5 items)&#39;            | Doc: CollectionPoco (5 items)   |     1,477.978 ns |      4.2913 ns |      5.7288 ns |     1,477.131 ns |     10.65 |    0.12 |          310.0% |           3.10x |   0.2823 |  0.0057 |       - |    2952 B |        4.10 |
| &#39;PackedJson: CollectionPoco (3x5 items)&#39;         | Doc: CollectionPoco (5 items)   |     9,858.654 ns |     31.2593 ns |     45.8195 ns |     9,848.459 ns |     71.01 |    0.84 |         1908.9% |          19.09x |   1.3733 |  0.0305 |       - |   14464 B |       20.09 |
| &#39;PackedBrotli: CollectionPoco (3x5 items)&#39;       | Doc: CollectionPoco (5 items)   |    18,059.860 ns |    164.2198 ns |    245.7966 ns |    17,994.194 ns |    130.09 |    2.25 |         1944.6% |          19.45x |   1.4038 |       - |       - |   14721 B |       20.45 |
|                                                  |                                 |                  |                |                |                  |           |         |                 |                 |          |         |         |           |             |
| &#39;DataOnly: ComplexPoco (5 nested items)&#39;         | Doc: ComplexPoco (5 items)      |        51.642 ns |      0.6706 ns |      1.0037 ns |        52.007 ns |      1.00 |    0.03 |     0.0% (Base) |    1.00x (Data) |   0.0413 |  0.0001 |       - |     432 B |        1.00 |
| &#39;MetadataOnly: ComplexPoco (5 nested items)&#39;     | Doc: ComplexPoco (5 items)      |     3,197.965 ns |     36.8046 ns |     55.0873 ns |     3,168.187 ns |     61.95 |    1.59 |         1163.0% |          11.63x |   0.4768 |  0.0038 |       - |    5024 B |       11.63 |
| &#39;FullDoc: ComplexPoco (5 nested items)&#39;          | Doc: ComplexPoco (5 items)      |     3,253.025 ns |     23.2873 ns |     34.1342 ns |     3,258.082 ns |     63.01 |    1.38 |         1163.0% |          11.63x |   0.5188 |  0.0076 |       - |    5456 B |       12.63 |
| &#39;PackedJson: ComplexPoco (5 nested items)&#39;       | Doc: ComplexPoco (5 items)      |     6,367.257 ns |     62.0791 ns |     92.9170 ns |     6,323.890 ns |    123.34 |    2.96 |         2229.6% |          22.30x |   0.9613 |  0.0153 |       - |   10064 B |       23.30 |
| &#39;PackedBrotli: ComplexPoco (5 nested items)&#39;     | Doc: ComplexPoco (5 items)      |    12,511.042 ns |     51.6864 ns |     74.1271 ns |    12,521.672 ns |    242.35 |    4.88 |         1970.4% |          19.70x |   0.8545 |       - |       - |    8944 B |       20.70 |
|                                                  |                                 |                  |                |                |                  |           |         |                 |                 |          |         |         |           |             |
| &#39;DataOnly: ComplexPoco (50 nested items)&#39;        | Doc: ComplexPoco (50 items)     |       296.461 ns |      2.4991 ns |      3.5841 ns |       296.937 ns |      1.00 |    0.02 |     0.0% (Base) |    1.00x (Data) |   0.2131 |  0.0029 |       - |    2232 B |        1.00 |
| &#39;MetadataOnly: ComplexPoco (50 nested items)&#39;    | Doc: ComplexPoco (50 items)     |    17,997.267 ns |     74.9295 ns |    109.8307 ns |    18,003.583 ns |     60.72 |    0.82 |         1457.7% |          14.58x |   3.0823 |  0.1526 |       - |   32536 B |       14.58 |
| &#39;FullDoc: ComplexPoco (50 nested items)&#39;         | Doc: ComplexPoco (50 items)     |    18,512.077 ns |    121.2889 ns |    181.5395 ns |    18,461.705 ns |     62.45 |    0.97 |         1457.7% |          14.58x |   3.2959 |  0.2747 |       - |   34768 B |       15.58 |
| &#39;PackedJson: ComplexPoco (50 nested items)&#39;      | Doc: ComplexPoco (50 items)     |    39,508.708 ns |    286.3842 ns |    419.7782 ns |    39,380.029 ns |    133.29 |    2.13 |         2834.1% |          28.34x |   6.2256 |  0.7324 |       - |   65488 B |       29.34 |
| &#39;PackedBrotli: ComplexPoco (50 nested items)&#39;    | Doc: ComplexPoco (50 items)     |    52,984.463 ns |    386.1661 ns |    541.3510 ns |    52,797.736 ns |    178.75 |    2.81 |         2175.7% |          21.76x |   4.8218 |  0.1221 |       - |   50793 B |       22.76 |
|                                                  |                                 |                  |                |                |                  |           |         |                 |                 |          |         |         |           |             |
| &#39;DataOnly: SimplePoco (3 props)&#39;                 | Doc: SimplePoco                 |         3.989 ns |      0.1092 ns |      0.1634 ns |         4.017 ns |      1.00 |    0.06 |     0.0% (Base) |    1.00x (Data) |   0.0046 |       - |       - |      48 B |        1.00 |
| &#39;MetadataOnly: SimplePoco (3 props)&#39;             | Doc: SimplePoco                 |       247.249 ns |      1.6234 ns |      2.3283 ns |       246.558 ns |     62.09 |    2.60 |         1133.3% |          11.33x |   0.0520 |       - |       - |     544 B |       11.33 |
| &#39;FullDoc: SimplePoco (Data + Metadata)&#39;          | Doc: SimplePoco                 |       251.069 ns |      2.4957 ns |      3.6581 ns |       249.860 ns |     63.05 |    2.73 |         1133.3% |          11.33x |   0.0563 |       - |       - |     592 B |       12.33 |
| &#39;PackedJson: SimplePoco (Data + JSON Bytes)&#39;     | Doc: SimplePoco                 |       878.684 ns |      8.7032 ns |     13.0266 ns |       878.511 ns |    220.66 |    9.56 |         2400.0% |          24.00x |   0.1144 |       - |       - |    1200 B |       25.00 |
| &#39;PackedBrotli: SimplePoco (Data + Brotli Bytes)&#39; | Doc: SimplePoco                 |     4,584.679 ns |     36.7983 ns |     53.9385 ns |     4,558.412 ns |  1,151.33 |   48.80 |         3100.0% |          31.00x |   0.1450 |       - |       - |    1536 B |       32.00 |
|                                                  |                                 |                  |                |                |                  |           |         |                 |                 |          |         |         |           |             |
| &#39;DataOnly: WidePoco (21 props)&#39;                  | Doc: WidePoco                   |        14.190 ns |      0.1550 ns |      0.2272 ns |        14.217 ns |      1.00 |    0.02 |     0.0% (Base) |    1.00x (Data) |   0.0168 |       - |       - |     176 B |        1.00 |
| &#39;MetadataOnly: WidePoco (21 props)&#39;              | Doc: WidePoco                   |     1,621.883 ns |     24.1185 ns |     36.0994 ns |     1,624.335 ns |    114.32 |    3.09 |         1086.4% |          10.86x |   0.1812 |       - |       - |    1912 B |       10.86 |
| &#39;FullDoc: WidePoco (Data + Metadata)&#39;            | Doc: WidePoco                   |     1,617.285 ns |      8.7764 ns |     13.1362 ns |     1,616.749 ns |    114.00 |    2.02 |         1086.4% |          10.86x |   0.1984 |       - |       - |    2088 B |       11.86 |
| &#39;PackedJson: WidePoco (Data + JSON Bytes)&#39;       | Doc: WidePoco                   |     3,726.345 ns |     12.4526 ns |     18.2528 ns |     3,724.783 ns |    262.66 |    4.35 |         3381.8% |          33.82x |   0.5836 |  0.0038 |       - |    6128 B |       34.82 |
| &#39;PackedBrotli: WidePoco (Data + Brotli Bytes)&#39;   | Doc: WidePoco                   |     8,265.196 ns |     77.2951 ns |    115.6916 ns |     8,215.311 ns |    582.59 |   12.23 |         2945.5% |          29.45x |   0.5035 |       - |       - |    5360 B |       30.45 |
|                                                  |                                 |                  |                |                |                  |           |         |                 |                 |          |         |         |           |             |
| &#39;Shards: 1 Shard x 5 Replicas (0 Dots)&#39;          | Shard Map Dimensions            |       214.439 ns |      2.4623 ns |      3.6092 ns |       213.375 ns |      1.00 |    0.02 |               - |               - |   0.0503 |       - |       - |     528 B |        1.00 |
| &#39;Shards: 10 Shards x 5 Replicas (0 Dots)&#39;        | Shard Map Dimensions            |     2,347.868 ns |      7.1893 ns |     10.5379 ns |     2,349.492 ns |     10.95 |    0.19 |               - |               - |   0.4082 |  0.0076 |       - |    4272 B |        8.09 |
| &#39;Shards: 10 Shards x 25 Replicas (5 Dots)&#39;       | Shard Map Dimensions            |   147,460.146 ns |  1,127.0095 ns |  1,686.8550 ns |   146,713.586 ns |    687.84 |   13.73 |               - |               - |   7.8125 |  1.9531 |       - |   83634 B |      158.40 |
| &#39;Shards: 100 Shards x 5 Replicas (0 Dots)&#39;       | Shard Map Dimensions            |    23,468.228 ns |    123.0549 ns |    184.1828 ns |    23,453.659 ns |    109.47 |    1.99 |               - |               - |   4.4556 |  0.7324 |       - |   46672 B |       88.39 |
| &#39;Shards: 100 Shards x 50 Replicas (10 Dots)&#39;     | Shard Map Dimensions            | 4,227,603.529 ns | 35,516.7043 ns | 52,059.9289 ns | 4,208,970.312 ns | 19,720.06 |  403.47 |               - |               - | 257.8125 | 85.9375 | 15.6250 | 2589222 B |    4,903.83 |


```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9974)
Intel Core i9-10850K CPU 3.60GHz, 1 CPU, 20 logical and 10 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=MediumRun  Toolchain=InProcessNoEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
| Method               | Mean       | Error    | StdDev   | Gen0   | Gen1   | Allocated |
|--------------------- |-----------:|---------:|---------:|-------:|-------:|----------:|
| GeneratePatchSimple  |   705.7 ns |  8.99 ns | 13.46 ns | 0.1631 |      - |   1.67 KB |
| GeneratePatchComplex | 3,420.7 ns | 13.35 ns | 19.98 ns | 0.5913 | 0.0038 |   6.06 KB |


```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9974)
Intel Core i9-10850K CPU 3.60GHz, 1 CPU, 20 logical and 10 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=MediumRun  Toolchain=InProcessNoEmitToolchain  InvocationCount=1  
IterationCount=15  LaunchCount=2  UnrollFactor=1  
WarmupCount=10  

```
| Method                            | Mean        | Error      | StdDev     | Median      | Allocated |
|---------------------------------- |------------:|-----------:|-----------:|------------:|----------:|
| &#39;Strategy.Apply: LWW&#39;             |   323.00 ns |  52.008 ns |  74.588 ns |   287.50 ns |     218 B |
| &#39;Strategy.Apply: FWW&#39;             |   310.13 ns |  25.839 ns |  38.675 ns |   301.50 ns |     208 B |
| &#39;Strategy.Apply: Counter&#39;         |   526.68 ns |  77.874 ns | 111.685 ns |   480.00 ns |     646 B |
| &#39;Strategy.Apply: GCounter&#39;        |   545.55 ns |  74.484 ns | 109.178 ns |   493.00 ns |     582 B |
| &#39;Strategy.Apply: BoundedCounter&#39;  |    79.93 ns |   6.496 ns |   9.106 ns |    78.00 ns |     152 B |
| &#39;Strategy.Apply: MaxWins&#39;         |    76.44 ns |   3.074 ns |   4.104 ns |    77.00 ns |     152 B |
| &#39;Strategy.Apply: MinWins&#39;         |    82.00 ns |   6.922 ns |   9.475 ns |    79.25 ns |     152 B |
| &#39;Strategy.Apply: AverageRegister&#39; |   640.57 ns | 199.579 ns | 286.230 ns |   478.00 ns |     918 B |
| &#39;Strategy.Apply: GSet&#39;            |   536.57 ns |  97.157 ns | 145.420 ns |   465.50 ns |     566 B |
| &#39;Strategy.Apply: TwoPhaseSet&#39;     |   423.24 ns |  68.499 ns | 100.404 ns |   382.00 ns |     438 B |
| &#39;Strategy.Apply: LwwSet&#39;          | 1,378.10 ns | 281.877 ns | 413.172 ns | 1,027.00 ns |    1054 B |
| &#39;Strategy.Apply: FwwSet&#39;          |   890.21 ns | 146.627 ns | 210.288 ns |   801.50 ns |     798 B |
| &#39;Strategy.Apply: OrSet&#39;           | 1,109.00 ns | 277.949 ns | 416.021 ns |   890.00 ns |    1342 B |
| &#39;Strategy.Apply: ArrayLcs&#39;        |   812.90 ns | 126.560 ns | 185.510 ns |   732.00 ns |     374 B |
| &#39;Strategy.Apply: FixedSizeArray&#39;  |   459.17 ns |  47.151 ns |  69.113 ns |   437.00 ns |     272 B |
| &#39;Strategy.Apply: Lseq&#39;            |   802.77 ns | 270.203 ns | 369.857 ns |   517.00 ns |     454 B |
| &#39;Strategy.Apply: VoteCounter&#39;     |   709.64 ns | 150.540 ns | 215.900 ns |   591.00 ns |     510 B |
| &#39;Strategy.Apply: StateMachine&#39;    |   567.22 ns |  13.783 ns |  18.400 ns |   565.00 ns |     278 B |
| &#39;Strategy.Apply: PriorityQueue&#39;   | 6,398.30 ns | 652.150 ns | 914.223 ns | 5,852.00 ns |    8590 B |
| &#39;Strategy.Apply: SortedSet&#39;       | 3,487.10 ns | 387.452 ns | 567.922 ns | 3,037.00 ns |    4462 B |
| &#39;Strategy.Apply: RGA&#39;             | 2,157.00 ns | 647.152 ns | 968.626 ns | 1,525.00 ns |    3494 B |
| &#39;Strategy.Apply: CounterMap&#39;      |   794.00 ns |  14.605 ns |  19.498 ns |   789.00 ns |     734 B |
| &#39;Strategy.Apply: LwwMap&#39;          |   791.97 ns |  74.064 ns | 110.855 ns |   752.50 ns |     590 B |
| &#39;Strategy.Apply: FwwMap&#39;          |   469.89 ns |  23.586 ns |  33.827 ns |   461.50 ns |     438 B |
| &#39;Strategy.Apply: MaxWinsMap&#39;      |   617.82 ns |  40.734 ns |  58.419 ns |   595.50 ns |     398 B |
| &#39;Strategy.Apply: MinWinsMap&#39;      |   133.96 ns |   5.169 ns |   7.246 ns |   135.00 ns |     304 B |
| &#39;Strategy.Apply: OrMap&#39;           | 1,110.31 ns | 116.908 ns | 171.362 ns | 1,040.00 ns |    1390 B |
| &#39;Strategy.Apply: Graph&#39;           |    74.76 ns |   2.271 ns |   3.032 ns |    74.00 ns |     152 B |
| &#39;Strategy.Apply: TwoPhaseGraph&#39;   |   377.85 ns |   8.802 ns |  12.049 ns |   374.50 ns |     344 B |
| &#39;Strategy.Apply: ReplicatedTree&#39;  |   480.54 ns |  56.505 ns |  81.038 ns |   442.00 ns |     622 B |
| &#39;Strategy.Apply: EpochBound&#39;      | 1,631.11 ns |  18.308 ns |  25.666 ns | 1,624.00 ns |     278 B |
| &#39;Strategy.Apply: ApprovalQuorum&#39;  |    76.92 ns |   2.605 ns |   3.566 ns |    76.50 ns |     152 B |


```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9974)
Intel Core i9-10850K CPU 3.60GHz, 1 CPU, 20 logical and 10 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=MediumRun  Toolchain=InProcessNoEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
| Method                               | Mean       | Error    | StdDev   | Median     | Gen0   | Allocated |
|------------------------------------- |-----------:|---------:|---------:|-----------:|-------:|----------:|
| &#39;Strategy.Generate: LWW&#39;             |   130.7 ns |  0.41 ns |  0.62 ns |   130.6 ns | 0.0143 |     152 B |
| &#39;Strategy.Generate: FWW&#39;             |   128.7 ns |  1.14 ns |  1.71 ns |   127.7 ns | 0.0143 |     152 B |
| &#39;Strategy.Generate: Counter&#39;         |   151.1 ns |  0.54 ns |  0.79 ns |   151.2 ns | 0.0329 |     344 B |
| &#39;Strategy.Generate: GCounter&#39;        |   183.6 ns |  1.53 ns |  2.29 ns |   182.6 ns | 0.0329 |     344 B |
| &#39;Strategy.Generate: BoundedCounter&#39;  |   153.4 ns |  1.32 ns |  1.93 ns |   154.4 ns | 0.0329 |     344 B |
| &#39;Strategy.Generate: MaxWins&#39;         |   114.3 ns |  0.91 ns |  1.34 ns |   114.7 ns | 0.0191 |     200 B |
| &#39;Strategy.Generate: MinWins&#39;         |   111.2 ns |  0.33 ns |  0.50 ns |   111.2 ns | 0.0191 |     200 B |
| &#39;Strategy.Generate: AverageRegister&#39; |   112.1 ns |  1.77 ns |  2.59 ns |   112.3 ns | 0.0206 |     216 B |
| &#39;Strategy.Generate: GSet&#39;            |   374.1 ns |  1.21 ns |  1.77 ns |   374.4 ns | 0.0677 |     712 B |
| &#39;Strategy.Generate: TwoPhaseSet&#39;     |   623.9 ns |  3.30 ns |  4.94 ns |   624.3 ns | 0.1059 |    1112 B |
| &#39;Strategy.Generate: LwwSet&#39;          |   366.0 ns |  2.54 ns |  3.81 ns |   366.1 ns | 0.0558 |     584 B |
| &#39;Strategy.Generate: FwwSet&#39;          |   373.4 ns |  1.21 ns |  1.77 ns |   373.5 ns | 0.0558 |     584 B |
| &#39;Strategy.Generate: OrSet&#39;           |   913.9 ns |  3.17 ns |  4.65 ns |   914.6 ns | 0.1364 |    1432 B |
| &#39;Strategy.Generate: ArrayLcs&#39;        |   905.8 ns |  3.18 ns |  4.56 ns |   906.2 ns | 0.1125 |    1184 B |
| &#39;Strategy.Generate: FixedSizeArray&#39;  |   408.0 ns |  3.04 ns |  4.55 ns |   406.6 ns | 0.0596 |     624 B |
| &#39;Strategy.Generate: Lseq&#39;            |   273.5 ns |  0.72 ns |  1.08 ns |   273.6 ns | 0.0443 |     464 B |
| &#39;Strategy.Generate: VoteCounter&#39;     |   428.9 ns |  5.36 ns |  7.86 ns |   426.9 ns | 0.1044 |    1096 B |
| &#39;Strategy.Generate: StateMachine&#39;    |   166.2 ns |  2.09 ns |  3.06 ns |   164.7 ns | 0.0143 |     152 B |
| &#39;Strategy.Generate: PriorityQueue&#39;   | 4,847.3 ns | 41.31 ns | 61.83 ns | 4,830.1 ns | 0.7401 |    7776 B |
| &#39;Strategy.Generate: SortedSet&#39;       | 1,787.9 ns |  5.60 ns |  8.39 ns | 1,786.8 ns | 0.3376 |    3536 B |
| &#39;Strategy.Generate: RGA&#39;             |   564.1 ns |  3.97 ns |  5.94 ns |   564.1 ns | 0.1163 |    1224 B |
| &#39;Strategy.Generate: CounterMap&#39;      |   794.7 ns |  2.39 ns |  3.58 ns |   795.0 ns | 0.1392 |    1456 B |
| &#39;Strategy.Generate: LwwMap&#39;          |   952.4 ns |  7.73 ns | 10.84 ns |   947.8 ns | 0.1478 |    1552 B |
| &#39;Strategy.Generate: FwwMap&#39;          |   840.9 ns |  2.43 ns |  3.64 ns |   841.0 ns | 0.1450 |    1520 B |
| &#39;Strategy.Generate: MaxWinsMap&#39;      |   489.2 ns |  1.37 ns |  2.05 ns |   489.0 ns | 0.0849 |     888 B |
| &#39;Strategy.Generate: MinWinsMap&#39;      |   494.8 ns |  5.26 ns |  7.88 ns |   492.7 ns | 0.0849 |     888 B |
| &#39;Strategy.Generate: OrMap&#39;           | 1,198.3 ns |  5.03 ns |  7.37 ns | 1,196.8 ns | 0.1831 |    1928 B |
| &#39;Strategy.Generate: Graph&#39;           |   401.4 ns |  2.96 ns |  4.44 ns |   401.3 ns | 0.0968 |    1016 B |
| &#39;Strategy.Generate: TwoPhaseGraph&#39;   |   441.1 ns |  3.17 ns |  4.65 ns |   440.0 ns | 0.1245 |    1304 B |
| &#39;Strategy.Generate: ReplicatedTree&#39;  |   552.0 ns |  1.53 ns |  2.29 ns |   552.2 ns | 0.1087 |    1144 B |
| &#39;Strategy.Generate: EpochBound&#39;      |   747.9 ns |  1.81 ns |  2.71 ns |   747.9 ns | 0.0820 |     864 B |
| &#39;Strategy.Generate: ApprovalQuorum&#39;  |   427.0 ns |  2.59 ns |  3.79 ns |   426.6 ns | 0.0763 |     800 B |


```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9974)
Intel Core i9-10850K CPU 3.60GHz, 1 CPU, 20 logical and 10 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=MediumRun  Toolchain=InProcessNoEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
| Method                                 | Mean      | Error    | StdDev   | Median    | Gen0   | Allocated |
|--------------------------------------- |----------:|---------:|---------:|----------:|-------:|----------:|
| &#39;Strategy.GenerateOp: LWW&#39;             |  54.11 ns | 0.031 ns | 0.043 ns |  54.11 ns |      - |         - |
| &#39;Strategy.GenerateOp: FWW&#39;             |  54.58 ns | 0.251 ns | 0.376 ns |  54.66 ns |      - |         - |
| &#39;Strategy.GenerateOp: Counter&#39;         |  76.91 ns | 0.547 ns | 0.818 ns |  76.61 ns | 0.0084 |      88 B |
| &#39;Strategy.GenerateOp: GCounter&#39;        | 103.64 ns | 0.248 ns | 0.364 ns | 103.65 ns | 0.0084 |      88 B |
| &#39;Strategy.GenerateOp: BoundedCounter&#39;  |  54.69 ns | 0.431 ns | 0.646 ns |  54.38 ns |      - |         - |
| &#39;Strategy.GenerateOp: MaxWins&#39;         |  54.16 ns | 0.039 ns | 0.054 ns |  54.16 ns |      - |         - |
| &#39;Strategy.GenerateOp: MinWins&#39;         |  54.75 ns | 0.106 ns | 0.155 ns |  54.69 ns |      - |         - |
| &#39;Strategy.GenerateOp: AverageRegister&#39; |  54.30 ns | 0.238 ns | 0.356 ns |  54.16 ns |      - |         - |
| &#39;Strategy.GenerateOp: GSet&#39;            |  84.47 ns | 0.307 ns | 0.450 ns |  84.54 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: TwoPhaseSet&#39;     |  54.16 ns | 0.087 ns | 0.121 ns |  54.16 ns |      - |         - |
| &#39;Strategy.GenerateOp: LwwSet&#39;          |  54.55 ns | 0.369 ns | 0.529 ns |  54.55 ns |      - |         - |
| &#39;Strategy.GenerateOp: FwwSet&#39;          |  54.90 ns | 0.332 ns | 0.477 ns |  55.19 ns |      - |         - |
| &#39;Strategy.GenerateOp: OrSet&#39;           | 147.00 ns | 0.170 ns | 0.227 ns | 146.96 ns | 0.0069 |      72 B |
| &#39;Strategy.GenerateOp: ArrayLcs&#39;        | 394.54 ns | 1.987 ns | 2.912 ns | 395.51 ns | 0.0201 |     216 B |
| &#39;Strategy.GenerateOp: FixedSizeArray&#39;  |  95.33 ns | 0.402 ns | 0.589 ns |  95.15 ns | 0.0053 |      56 B |
| &#39;Strategy.GenerateOp: Lseq&#39;            | 117.27 ns | 1.084 ns | 1.447 ns | 118.23 ns | 0.0107 |     112 B |
| &#39;Strategy.GenerateOp: VoteCounter&#39;     |  59.51 ns | 0.060 ns | 0.090 ns |  59.50 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: StateMachine&#39;    | 226.65 ns | 0.226 ns | 0.324 ns | 226.70 ns |      - |         - |
| &#39;Strategy.GenerateOp: PriorityQueue&#39;   |  86.22 ns | 1.332 ns | 1.993 ns |  86.24 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: SortedSet&#39;       |  61.92 ns | 0.203 ns | 0.303 ns |  61.87 ns | 0.0053 |      56 B |
| &#39;Strategy.GenerateOp: RGA&#39;             | 265.95 ns | 1.501 ns | 2.200 ns | 265.77 ns | 0.0580 |     608 B |
| &#39;Strategy.GenerateOp: CounterMap&#39;      |  59.49 ns | 0.251 ns | 0.376 ns |  59.46 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: LwwMap&#39;          |  60.10 ns | 0.325 ns | 0.487 ns |  60.22 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: FwwMap&#39;          |  60.64 ns | 0.775 ns | 1.160 ns |  59.90 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: MaxWinsMap&#39;      |  59.14 ns | 0.072 ns | 0.098 ns |  59.16 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: MinWinsMap&#39;      |  59.64 ns | 0.319 ns | 0.468 ns |  59.56 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: OrMap&#39;           | 175.45 ns | 0.926 ns | 1.357 ns | 174.83 ns | 0.0107 |     112 B |
| &#39;Strategy.GenerateOp: Graph&#39;           |  59.19 ns | 1.098 ns | 1.539 ns |  57.93 ns | 0.0023 |      24 B |
| &#39;Strategy.GenerateOp: TwoPhaseGraph&#39;   |  80.86 ns | 0.406 ns | 0.556 ns |  80.86 ns | 0.0023 |      24 B |
| &#39;Strategy.GenerateOp: ReplicatedTree&#39;  | 136.35 ns | 0.449 ns | 0.672 ns | 136.09 ns | 0.0053 |      56 B |
| &#39;Strategy.GenerateOp: EpochBound&#39;      | 583.08 ns | 3.469 ns | 5.192 ns | 582.68 ns | 0.0140 |     152 B |
| &#39;Strategy.GenerateOp: EpochClear&#39;      | 358.44 ns | 1.789 ns | 2.678 ns | 357.16 ns | 0.0079 |      88 B |
| &#39;Strategy.GenerateOp: ApprovalQuorum&#39;  | 278.73 ns | 2.300 ns | 3.224 ns | 276.85 ns | 0.0082 |      88 B |


<!-- BENCHMARKS_END -->