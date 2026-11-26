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
| Method            | Mean       | Error   | StdDev   | Gen0   | Allocated |
|------------------ |-----------:|--------:|---------:|-------:|----------:|
| ApplyPatchSimple  |   314.9 ns | 2.59 ns |  3.71 ns | 0.0677 |     712 B |
| ApplyPatchComplex | 1,076.2 ns | 9.28 ns | 13.89 ns | 0.2155 |    2272 B |


```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9974)
Intel Core i9-10850K CPU 3.60GHz, 1 CPU, 20 logical and 10 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=MediumRun  Toolchain=InProcessNoEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
| Method                                                                      | Categories                      | Mean             | Error          | StdDev         | Median           | Ratio     | RatioSD | Meta Overhead % | Meta/Data Ratio | Gen0     | Gen1    | Gen2    | Allocated | Alloc Ratio |
|---------------------------------------------------------------------------- |-------------------------------- |-----------------:|---------------:|---------------:|-----------------:|----------:|--------:|----------------:|----------------:|---------:|--------:|--------:|----------:|------------:|
| &#39;DVV: 3 Replicas, 0 Dots (Linear)&#39;                                          | DVV Dimensions                  |        79.567 ns |      2.4303 ns |      3.3266 ns |        78.948 ns |      1.00 |    0.06 |               - |               - |   0.0206 |       - |       - |     216 B |        1.00 |
| &#39;DVV: 3 Replicas, 5 Dots/Replica&#39;                                           | DVV Dimensions                  |       763.676 ns |      4.5051 ns |      6.4611 ns |       762.109 ns |      9.61 |    0.40 |               - |               - |   0.0839 |       - |       - |     880 B |        4.07 |
| &#39;DVV: 10 Replicas, 0 Dots (Linear)&#39;                                         | DVV Dimensions                  |       467.723 ns |      7.5676 ns |     11.0924 ns |       473.937 ns |      5.89 |    0.28 |               - |               - |   0.0615 |       - |       - |     648 B |        3.00 |
| &#39;DVV: 10 Replicas, 10 Dots/Replica&#39;                                         | DVV Dimensions                  |    10,155.084 ns |     39.3526 ns |     58.9011 ns |    10,163.802 ns |    127.84 |    5.30 |               - |               - |   0.4272 |       - |       - |    4608 B |       21.33 |
| &#39;DVV: 100 Replicas, 0 Dots (Linear)&#39;                                        | DVV Dimensions                  |     3,971.366 ns |     50.5512 ns |     70.8657 ns |     3,963.197 ns |     50.00 |    2.23 |               - |               - |   0.9460 |  0.0381 |       - |    9928 B |       45.96 |
| &#39;DVV: 100 Replicas, 20 Dots/Replica&#39;                                        | DVV Dimensions                  |   130,992.729 ns |  1,085.0384 ns |  1,590.4353 ns |   130,630.420 ns |  1,649.09 |   70.54 |               - |               - |  13.1836 |  4.3945 |       - |  139729 B |      646.89 |
|                                                                             |                                 |                  |                |                |                  |           |         |                 |                 |          |         |         |           |             |
| &#39;DataOnly: CollectionPoco (3x100 items)&#39;                                    | Doc: CollectionPoco (100 items) |     1,847.885 ns |     29.9773 ns |     44.8686 ns |     1,846.437 ns |      1.00 |    0.03 |     0.0% (Base) |    1.00x (Data) |   0.4807 |  0.0095 |       - |    5040 B |        1.00 |
| &#39;MetadataOnly: CollectionPoco (3x100 items)&#39;                                | Doc: CollectionPoco (100 items) |    15,185.398 ns |    166.1440 ns |    248.6765 ns |    15,120.745 ns |      8.22 |    0.24 |          579.8% |           5.80x |   2.7924 |  0.4578 |       - |   29224 B |        5.80 |
| &#39;FullDoc: CollectionPoco (3x100 items)&#39;                                     | Doc: CollectionPoco (100 items) |    17,389.049 ns |    128.0743 ns |    191.6956 ns |    17,360.680 ns |      9.42 |    0.25 |          580.3% |           5.80x |   3.2654 |  0.7629 |       - |   34288 B |        6.80 |
| &#39;PackedJson: CollectionPoco (3x100 items)&#39;                                  | Doc: CollectionPoco (100 items) |   147,469.431 ns |  1,096.6412 ns |  1,641.4010 ns |   147,238.733 ns |     79.85 |    2.10 |         4551.2% |          45.51x |  22.2168 |  4.3945 |       - |  234418 B |       46.51 |
| &#39;PackedBrotli: CollectionPoco (3x100 items)&#39;                                | Doc: CollectionPoco (100 items) |   195,740.574 ns |  2,222.4929 ns |  3,257.7016 ns |   195,615.308 ns |    105.99 |    3.07 |         4132.8% |          41.33x |  20.2637 |  1.7090 |       - |  213334 B |       42.33 |
|                                                                             |                                 |                  |                |                |                  |           |         |                 |                 |          |         |         |           |             |
| &#39;DataOnly: CollectionPoco (3x25 items)&#39;                                     | Doc: CollectionPoco (25 items)  |       490.682 ns |      6.3788 ns |      9.5474 ns |       492.858 ns |      1.00 |    0.03 |     0.0% (Base) |    1.00x (Data) |   0.1583 |  0.0010 |       - |    1656 B |        1.00 |
| &#39;MetadataOnly: CollectionPoco (3x25 items)&#39;                                 | Doc: CollectionPoco (25 items)  |     4,307.107 ns |     79.6082 ns |    116.6886 ns |     4,265.736 ns |      8.78 |    0.29 |          516.9% |           5.17x |   0.8163 |  0.0381 |       - |    8560 B |        5.17 |
| &#39;FullDoc: CollectionPoco (3x25 items)&#39;                                      | Doc: CollectionPoco (25 items)  |     4,760.742 ns |     27.5740 ns |     39.5458 ns |     4,764.510 ns |      9.71 |    0.20 |          518.4% |           5.18x |   0.9766 |  0.0839 |       - |   10240 B |        6.18 |
| &#39;PackedJson: CollectionPoco (3x25 items)&#39;                                   | Doc: CollectionPoco (25 items)  |    38,897.520 ns |    274.8332 ns |    402.8470 ns |    38,837.305 ns |     79.30 |    1.72 |         3574.9% |          35.75x |   5.7983 |  0.4272 |       - |   60856 B |       36.75 |
| &#39;PackedBrotli: CollectionPoco (3x25 items)&#39;                                 | Doc: CollectionPoco (25 items)  |    55,134.090 ns |    321.7247 ns |    481.5424 ns |    55,225.092 ns |    112.40 |    2.36 |         3352.3% |          33.52x |   5.4321 |  0.1831 |       - |   57170 B |       34.52 |
|                                                                             |                                 |                  |                |                |                  |           |         |                 |                 |          |         |         |           |             |
| &#39;DataOnly: CollectionPoco (3x5 items)&#39;                                      | Doc: CollectionPoco (5 items)   |       136.067 ns |      2.6300 ns |      3.6869 ns |       134.384 ns |      1.00 |    0.04 |     0.0% (Base) |    1.00x (Data) |   0.0687 |  0.0002 |       - |     720 B |        1.00 |
| &#39;MetadataOnly: CollectionPoco (3x5 items)&#39;                                  | Doc: CollectionPoco (5 items)   |     1,326.065 ns |     15.2224 ns |     22.3128 ns |     1,320.333 ns |      9.75 |    0.30 |          337.8% |           3.38x |   0.2308 |  0.0019 |       - |    2432 B |        3.38 |
| &#39;FullDoc: CollectionPoco (3x5 items)&#39;                                       | Doc: CollectionPoco (5 items)   |     1,512.355 ns |     15.3116 ns |     22.4436 ns |     1,509.810 ns |     11.12 |    0.34 |          341.1% |           3.41x |   0.3033 |  0.0076 |       - |    3176 B |        4.41 |
| &#39;PackedJson: CollectionPoco (3x5 items)&#39;                                    | Doc: CollectionPoco (5 items)   |    10,048.711 ns |    103.8185 ns |    145.5390 ns |     9,990.396 ns |     73.90 |    2.22 |         1908.9% |          19.09x |   1.3733 |  0.0305 |       - |   14464 B |       20.09 |
| &#39;PackedBrotli: CollectionPoco (3x5 items)&#39;                                  | Doc: CollectionPoco (5 items)   |    18,301.583 ns |    181.0235 ns |    270.9475 ns |    18,273.692 ns |    134.60 |    4.06 |         1943.5% |          19.43x |   1.4038 |       - |       - |   14713 B |       20.43 |
|                                                                             |                                 |                  |                |                |                  |           |         |                 |                 |          |         |         |           |             |
| &#39;DataOnly: ComplexPoco (5 nested items)&#39;                                    | Doc: ComplexPoco (5 items)      |        53.112 ns |      0.9748 ns |      1.3665 ns |        52.812 ns |      1.00 |    0.04 |     0.0% (Base) |    1.00x (Data) |   0.0413 |  0.0001 |       - |     432 B |        1.00 |
| &#39;MetadataOnly: ComplexPoco (5 nested items)&#39;                                | Doc: ComplexPoco (5 items)      |     3,191.770 ns |     13.5358 ns |     19.4126 ns |     3,192.060 ns |     60.13 |    1.50 |         1227.8% |          12.28x |   0.5035 |  0.0038 |       - |    5304 B |       12.28 |
| &#39;FullDoc: ComplexPoco (5 nested items)&#39;                                     | Doc: ComplexPoco (5 items)      |     3,274.839 ns |     19.6062 ns |     28.1186 ns |     3,267.315 ns |     61.70 |    1.58 |         1227.8% |          12.28x |   0.5455 |  0.0114 |       - |    5736 B |       13.28 |
| &#39;PackedJson: ComplexPoco (5 nested items)&#39;                                  | Doc: ComplexPoco (5 items)      |     6,277.580 ns |     71.0151 ns |    104.0931 ns |     6,246.409 ns |    118.27 |    3.45 |         2229.6% |          22.30x |   0.9613 |  0.0153 |       - |   10064 B |       23.30 |
| &#39;PackedBrotli: ComplexPoco (5 nested items)&#39;                                | Doc: ComplexPoco (5 items)      |    12,526.958 ns |     74.4047 ns |    111.3655 ns |    12,528.027 ns |    236.00 |    6.07 |         1970.4% |          19.70x |   0.8545 |       - |       - |    8944 B |       20.70 |
|                                                                             |                                 |                  |                |                |                  |           |         |                 |                 |          |         |         |           |             |
| &#39;DataOnly: ComplexPoco (50 nested items)&#39;                                   | Doc: ComplexPoco (50 items)     |       297.586 ns |      8.8436 ns |     12.1052 ns |       300.653 ns |      1.00 |    0.06 |     0.0% (Base) |    1.00x (Data) |   0.2131 |  0.0029 |       - |    2232 B |        1.00 |
| &#39;MetadataOnly: ComplexPoco (50 nested items)&#39;                               | Doc: ComplexPoco (50 items)     |    17,013.264 ns |    117.7190 ns |    165.0257 ns |    16,995.804 ns |     57.26 |    2.37 |         1470.3% |          14.70x |   3.1128 |  0.1526 |       - |   32816 B |       14.70 |
| &#39;FullDoc: ComplexPoco (50 nested items)&#39;                                    | Doc: ComplexPoco (50 items)     |    17,356.124 ns |    160.6191 ns |    235.4334 ns |    17,348.276 ns |     58.42 |    2.48 |         1470.3% |          14.70x |   3.3264 |  0.2747 |       - |   35048 B |       15.70 |
| &#39;PackedJson: ComplexPoco (50 nested items)&#39;                                 | Doc: ComplexPoco (50 items)     |    39,677.774 ns |    232.1247 ns |    340.2454 ns |    39,637.238 ns |    133.55 |    5.49 |         2834.1% |          28.34x |   6.2256 |  0.7324 |       - |   65488 B |       29.34 |
| &#39;PackedBrotli: ComplexPoco (50 nested items)&#39;                               | Doc: ComplexPoco (50 items)     |    53,398.255 ns |    278.7302 ns |    399.7465 ns |    53,414.090 ns |    179.73 |    7.35 |         2175.7% |          21.76x |   4.8218 |  0.1221 |       - |   50793 B |       22.76 |
|                                                                             |                                 |                  |                |                |                  |           |         |                 |                 |          |         |         |           |             |
| &#39;DataOnly: SimplePoco (3 props)&#39;                                            | Doc: SimplePoco                 |         3.891 ns |      0.0913 ns |      0.1280 ns |         3.871 ns |      1.00 |    0.05 |     0.0% (Base) |    1.00x (Data) |   0.0046 |       - |       - |      48 B |        1.00 |
| &#39;MetadataOnly: SimplePoco (3 props)&#39;                                        | Doc: SimplePoco                 |       252.740 ns |      2.3598 ns |      3.3844 ns |       252.331 ns |     65.02 |    2.25 |         1483.3% |          14.83x |   0.0677 |       - |       - |     712 B |       14.83 |
| &#39;FullDoc: SimplePoco (Data + Metadata)&#39;                                     | Doc: SimplePoco                 |       255.965 ns |      1.6220 ns |      2.4277 ns |       256.002 ns |     65.85 |    2.19 |         1483.3% |          14.83x |   0.0725 |       - |       - |     760 B |       15.83 |
| &#39;PackedJson: SimplePoco (Data + JSON Bytes)&#39;                                | Doc: SimplePoco                 |       851.512 ns |      1.8116 ns |      2.6554 ns |       851.534 ns |    219.06 |    7.03 |         2400.0% |          24.00x |   0.1144 |       - |       - |    1200 B |       25.00 |
| &#39;PackedBrotli: SimplePoco (Data + Brotli Bytes)&#39;                            | Doc: SimplePoco                 |     4,496.512 ns |     16.4684 ns |     24.6492 ns |     4,498.111 ns |  1,156.78 |   37.49 |         3100.0% |          31.00x |   0.1450 |       - |       - |    1536 B |       32.00 |
|                                                                             |                                 |                  |                |                |                  |           |         |                 |                 |          |         |         |           |             |
| &#39;DataOnly: WidePoco (21 props)&#39;                                             | Doc: WidePoco                   |        15.869 ns |      1.6002 ns |      2.3456 ns |        17.691 ns |      1.02 |    0.21 |     0.0% (Base) |    1.00x (Data) |   0.0168 |       - |       - |     176 B |        1.00 |
| &#39;MetadataOnly: WidePoco (21 props)&#39;                                         | Doc: WidePoco                   |     1,617.435 ns |      6.3987 ns |      9.5773 ns |     1,614.239 ns |    104.14 |   15.31 |         1754.5% |          17.55x |   0.2937 |       - |       - |    3088 B |       17.55 |
| &#39;FullDoc: WidePoco (Data + Metadata)&#39;                                       | Doc: WidePoco                   |     1,645.227 ns |      8.4872 ns |     12.4404 ns |     1,645.987 ns |    105.93 |   15.58 |         1754.5% |          17.55x |   0.3109 |  0.0019 |       - |    3264 B |       18.55 |
| &#39;PackedJson: WidePoco (Data + JSON Bytes)&#39;                                  | Doc: WidePoco                   |     3,739.547 ns |     19.5146 ns |     29.2085 ns |     3,737.229 ns |    240.78 |   35.42 |         3381.8% |          33.82x |   0.5836 |  0.0038 |       - |    6128 B |       34.82 |
| &#39;PackedBrotli: WidePoco (Data + Brotli Bytes)&#39;                              | Doc: WidePoco                   |     8,211.832 ns |     35.3498 ns |     51.8153 ns |     8,226.593 ns |    528.74 |   77.74 |         2945.5% |          29.45x |   0.5035 |       - |       - |    5360 B |       30.45 |
|                                                                             |                                 |                  |                |                |                  |           |         |                 |                 |          |         |         |           |             |
| &#39;ReplicaScope: Bare (ReplicaContext only)&#39;                                  | Replica Scope Overhead          |       142.942 ns |      2.2621 ns |      3.2443 ns |       141.589 ns |      1.00 |    0.03 |               - |               - |   0.0329 |       - |       - |     344 B |        1.00 |
| &#39;ReplicaScope: Core Sync Services (Patcher, Applicator, Merger, MetaMgr)&#39;   | Replica Scope Overhead          |       590.083 ns |      4.5226 ns |      6.7692 ns |       591.837 ns |      4.13 |    0.10 |               - |               - |   0.0792 |       - |       - |     832 B |        2.42 |
| &#39;ReplicaScope: Async Pipeline (AsyncPatcher, AsyncApplicator, AsyncMerger)&#39; | Replica Scope Overhead          |       828.178 ns |     10.3446 ns |     15.1630 ns |       823.852 ns |      5.80 |    0.17 |               - |               - |   0.1621 |       - |       - |    1704 B |        4.95 |
|                                                                             |                                 |                  |                |                |                  |           |         |                 |                 |          |         |         |           |             |
| &#39;Shards: 1 Shard x 5 Replicas (0 Dots)&#39;                                     | Shard Map Dimensions            |       216.784 ns |      1.7352 ns |      2.5972 ns |       216.680 ns |      1.00 |    0.02 |               - |               - |   0.0503 |       - |       - |     528 B |        1.00 |
| &#39;Shards: 10 Shards x 5 Replicas (0 Dots)&#39;                                   | Shard Map Dimensions            |     2,399.404 ns |     22.9991 ns |     34.4240 ns |     2,385.754 ns |     11.07 |    0.20 |               - |               - |   0.4082 |  0.0076 |       - |    4272 B |        8.09 |
| &#39;Shards: 10 Shards x 25 Replicas (5 Dots)&#39;                                  | Shard Map Dimensions            |   155,892.943 ns |    242.9453 ns |    356.1060 ns |   155,867.798 ns |    719.22 |    8.62 |               - |               - |   7.8125 |  1.9531 |       - |   83634 B |      158.40 |
| &#39;Shards: 100 Shards x 5 Replicas (0 Dots)&#39;                                  | Shard Map Dimensions            |    23,980.001 ns |    238.7666 ns |    357.3747 ns |    23,946.790 ns |    110.63 |    2.08 |               - |               - |   4.4556 |  0.7324 |       - |   46672 B |       88.39 |
| &#39;Shards: 100 Shards x 50 Replicas (10 Dots)&#39;                                | Shard Map Dimensions            | 4,438,869.477 ns | 49,820.1850 ns | 73,025.7872 ns | 4,445,109.375 ns | 20,478.82 |  409.75 |               - |               - | 257.8125 | 85.9375 | 15.6250 | 2589221 B |    4,903.83 |


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
| GeneratePatchSimple  |   734.4 ns |  4.78 ns |  7.16 ns | 0.1659 |      - |    1.7 KB |
| GeneratePatchComplex | 3,488.7 ns | 26.30 ns | 38.55 ns | 0.5989 | 0.0038 |   6.13 KB |


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
| &#39;Strategy.Apply: LWW&#39;             |   319.12 ns |  53.409 ns |  76.598 ns |   275.75 ns |     216 B |
| &#39;Strategy.Apply: FWW&#39;             |   313.05 ns |  34.521 ns |  49.510 ns |   283.25 ns |     216 B |
| &#39;Strategy.Apply: Counter&#39;         |   495.13 ns |  66.597 ns |  99.679 ns |   442.00 ns |     654 B |
| &#39;Strategy.Apply: GCounter&#39;        |   557.23 ns |  80.169 ns | 109.737 ns |   477.50 ns |     590 B |
| &#39;Strategy.Apply: BoundedCounter&#39;  |    79.39 ns |  12.481 ns |  17.900 ns |    72.00 ns |     160 B |
| &#39;Strategy.Apply: MaxWins&#39;         |    69.15 ns |   3.786 ns |   5.182 ns |    67.50 ns |     160 B |
| &#39;Strategy.Apply: MinWins&#39;         |    76.21 ns |   8.477 ns |  12.157 ns |    71.00 ns |     160 B |
| &#39;Strategy.Apply: AverageRegister&#39; |   701.93 ns | 181.833 ns | 254.905 ns |   865.00 ns |     926 B |
| &#39;Strategy.Apply: GSet&#39;            |   513.39 ns | 101.250 ns | 145.210 ns |   419.50 ns |     496 B |
| &#39;Strategy.Apply: TwoPhaseSet&#39;     |   421.52 ns |  91.288 ns | 133.809 ns |   348.00 ns |     389 B |
| &#39;Strategy.Apply: LwwSet&#39;          | 1,208.48 ns | 251.211 ns | 352.162 ns |   960.00 ns |    1070 B |
| &#39;Strategy.Apply: FwwSet&#39;          |   906.93 ns | 174.077 ns | 260.551 ns |   755.50 ns |     814 B |
| &#39;Strategy.Apply: OrSet&#39;           | 1,102.21 ns | 288.137 ns | 422.348 ns |   836.00 ns |    1358 B |
| &#39;Strategy.Apply: ArrayLcs&#39;        |   867.00 ns | 150.191 ns | 220.148 ns |   739.00 ns |     390 B |
| &#39;Strategy.Apply: FixedSizeArray&#39;  |   390.03 ns |  49.580 ns |  74.210 ns |   352.00 ns |     253 B |
| &#39;Strategy.Apply: Lseq&#39;            |   850.52 ns | 282.152 ns | 413.575 ns | 1,129.00 ns |     462 B |
| &#39;Strategy.Apply: VoteCounter&#39;     |   653.80 ns | 115.442 ns | 172.788 ns |   558.50 ns |     518 B |
| &#39;Strategy.Apply: StateMachine&#39;    |   554.14 ns |  51.389 ns |  73.700 ns |   509.00 ns |     286 B |
| &#39;Strategy.Apply: PriorityQueue&#39;   | 6,160.59 ns | 529.736 ns | 742.616 ns | 5,688.00 ns |    8614 B |
| &#39;Strategy.Apply: SortedSet&#39;       | 3,498.55 ns | 262.930 ns | 385.399 ns | 3,284.00 ns |    4478 B |
| &#39;Strategy.Apply: RGA&#39;             | 2,151.77 ns | 633.406 ns | 948.052 ns | 1,719.50 ns |    3510 B |
| &#39;Strategy.Apply: CounterMap&#39;      |   817.04 ns |  64.773 ns |  90.803 ns |   771.00 ns |     750 B |
| &#39;Strategy.Apply: LwwMap&#39;          |   746.71 ns |  86.028 ns | 123.379 ns |   679.50 ns |     606 B |
| &#39;Strategy.Apply: FwwMap&#39;          |   441.22 ns |  21.862 ns |  30.647 ns |   429.00 ns |     376 B |
| &#39;Strategy.Apply: MaxWinsMap&#39;      |   584.00 ns |  42.404 ns |  62.155 ns |   548.00 ns |     414 B |
| &#39;Strategy.Apply: MinWinsMap&#39;      |   133.25 ns |  12.026 ns |  17.247 ns |   128.50 ns |     320 B |
| &#39;Strategy.Apply: OrMap&#39;           | 1,174.83 ns | 171.374 ns | 256.505 ns | 1,030.00 ns |    1406 B |
| &#39;Strategy.Apply: Graph&#39;           |    71.23 ns |   2.421 ns |   3.314 ns |    71.00 ns |     160 B |
| &#39;Strategy.Apply: TwoPhaseGraph&#39;   |   402.71 ns |  39.242 ns |  56.280 ns |   383.00 ns |     388 B |
| &#39;Strategy.Apply: ReplicatedTree&#39;  |   453.39 ns |  37.335 ns |  53.545 ns |   426.00 ns |     620 B |
| &#39;Strategy.Apply: EpochBound&#39;      | 1,616.62 ns |  15.687 ns |  22.994 ns | 1,610.00 ns |     286 B |
| &#39;Strategy.Apply: ApprovalQuorum&#39;  |    73.23 ns |   3.046 ns |   4.169 ns |    73.00 ns |     160 B |


```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9974)
Intel Core i9-10850K CPU 3.60GHz, 1 CPU, 20 logical and 10 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=MediumRun  Toolchain=InProcessNoEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
| Method                               | Mean       | Error    | StdDev   | Gen0   | Allocated |
|------------------------------------- |-----------:|---------:|---------:|-------:|----------:|
| &#39;Strategy.Generate: LWW&#39;             |   125.9 ns |  0.94 ns |  1.41 ns | 0.0153 |     160 B |
| &#39;Strategy.Generate: FWW&#39;             |   127.9 ns |  1.41 ns |  2.07 ns | 0.0153 |     160 B |
| &#39;Strategy.Generate: Counter&#39;         |   166.6 ns |  0.83 ns |  1.24 ns | 0.0336 |     352 B |
| &#39;Strategy.Generate: GCounter&#39;        |   186.6 ns |  1.26 ns |  1.88 ns | 0.0336 |     352 B |
| &#39;Strategy.Generate: BoundedCounter&#39;  |   150.2 ns |  1.92 ns |  2.81 ns | 0.0336 |     352 B |
| &#39;Strategy.Generate: MaxWins&#39;         |   110.3 ns |  1.02 ns |  1.53 ns | 0.0198 |     208 B |
| &#39;Strategy.Generate: MinWins&#39;         |   110.9 ns |  0.74 ns |  1.10 ns | 0.0198 |     208 B |
| &#39;Strategy.Generate: AverageRegister&#39; |   109.4 ns |  1.84 ns |  2.75 ns | 0.0213 |     224 B |
| &#39;Strategy.Generate: GSet&#39;            |   350.1 ns |  2.10 ns |  3.07 ns | 0.0687 |     720 B |
| &#39;Strategy.Generate: TwoPhaseSet&#39;     |   580.1 ns |  1.83 ns |  2.74 ns | 0.1068 |    1120 B |
| &#39;Strategy.Generate: LwwSet&#39;          |   353.4 ns |  2.08 ns |  3.12 ns | 0.0563 |     592 B |
| &#39;Strategy.Generate: FwwSet&#39;          |   353.5 ns |  1.65 ns |  2.42 ns | 0.0563 |     592 B |
| &#39;Strategy.Generate: OrSet&#39;           |   954.4 ns |  4.47 ns |  6.41 ns | 0.1373 |    1440 B |
| &#39;Strategy.Generate: ArrayLcs&#39;        |   955.6 ns |  2.75 ns |  4.12 ns | 0.1125 |    1192 B |
| &#39;Strategy.Generate: FixedSizeArray&#39;  |   388.1 ns |  4.58 ns |  6.86 ns | 0.0601 |     632 B |
| &#39;Strategy.Generate: Lseq&#39;            |   267.1 ns |  2.46 ns |  3.67 ns | 0.0448 |     472 B |
| &#39;Strategy.Generate: VoteCounter&#39;     |   413.3 ns |  2.61 ns |  3.75 ns | 0.1054 |    1104 B |
| &#39;Strategy.Generate: StateMachine&#39;    |   150.3 ns |  0.66 ns |  0.97 ns | 0.0153 |     160 B |
| &#39;Strategy.Generate: PriorityQueue&#39;   | 4,714.8 ns | 44.01 ns | 65.87 ns | 0.7401 |    7784 B |
| &#39;Strategy.Generate: SortedSet&#39;       | 1,718.4 ns | 18.13 ns | 27.13 ns | 0.3376 |    3544 B |
| &#39;Strategy.Generate: RGA&#39;             |   541.0 ns |  5.84 ns |  8.74 ns | 0.1173 |    1232 B |
| &#39;Strategy.Generate: CounterMap&#39;      |   760.9 ns |  6.96 ns | 10.42 ns | 0.1392 |    1464 B |
| &#39;Strategy.Generate: LwwMap&#39;          |   950.3 ns |  8.20 ns | 12.27 ns | 0.1488 |    1560 B |
| &#39;Strategy.Generate: FwwMap&#39;          |   836.2 ns |  7.83 ns | 11.22 ns | 0.1459 |    1528 B |
| &#39;Strategy.Generate: MaxWinsMap&#39;      |   464.7 ns |  2.06 ns |  3.02 ns | 0.0854 |     896 B |
| &#39;Strategy.Generate: MinWinsMap&#39;      |   463.1 ns |  5.39 ns |  7.91 ns | 0.0854 |     896 B |
| &#39;Strategy.Generate: OrMap&#39;           | 1,178.8 ns |  9.65 ns | 14.15 ns | 0.1850 |    1952 B |
| &#39;Strategy.Generate: Graph&#39;           |   384.1 ns |  4.85 ns |  7.26 ns | 0.0978 |    1024 B |
| &#39;Strategy.Generate: TwoPhaseGraph&#39;   |   417.5 ns |  4.03 ns |  5.66 ns | 0.1254 |    1312 B |
| &#39;Strategy.Generate: ReplicatedTree&#39;  |   534.0 ns |  3.54 ns |  5.19 ns | 0.1097 |    1152 B |
| &#39;Strategy.Generate: EpochBound&#39;      |   673.6 ns |  3.94 ns |  5.90 ns | 0.0839 |     880 B |
| &#39;Strategy.Generate: ApprovalQuorum&#39;  |   401.8 ns |  5.11 ns |  7.64 ns | 0.0777 |     816 B |


```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9974)
Intel Core i9-10850K CPU 3.60GHz, 1 CPU, 20 logical and 10 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=MediumRun  Toolchain=InProcessNoEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
| Method                                 | Mean      | Error    | StdDev   | Gen0   | Allocated |
|--------------------------------------- |----------:|---------:|---------:|-------:|----------:|
| &#39;Strategy.GenerateOp: LWW&#39;             |  53.84 ns | 0.171 ns | 0.251 ns |      - |         - |
| &#39;Strategy.GenerateOp: FWW&#39;             |  53.99 ns | 0.143 ns | 0.214 ns |      - |         - |
| &#39;Strategy.GenerateOp: Counter&#39;         |  77.97 ns | 0.642 ns | 0.920 ns | 0.0084 |      88 B |
| &#39;Strategy.GenerateOp: GCounter&#39;        | 103.72 ns | 0.371 ns | 0.532 ns | 0.0084 |      88 B |
| &#39;Strategy.GenerateOp: BoundedCounter&#39;  |  54.38 ns | 0.150 ns | 0.225 ns |      - |         - |
| &#39;Strategy.GenerateOp: MaxWins&#39;         |  54.05 ns | 0.362 ns | 0.531 ns |      - |         - |
| &#39;Strategy.GenerateOp: MinWins&#39;         |  54.20 ns | 0.089 ns | 0.131 ns |      - |         - |
| &#39;Strategy.GenerateOp: AverageRegister&#39; |  54.46 ns | 0.283 ns | 0.414 ns |      - |         - |
| &#39;Strategy.GenerateOp: GSet&#39;            |  81.33 ns | 1.175 ns | 1.759 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: TwoPhaseSet&#39;     |  54.24 ns | 0.301 ns | 0.422 ns |      - |         - |
| &#39;Strategy.GenerateOp: LwwSet&#39;          |  53.99 ns | 0.123 ns | 0.184 ns |      - |         - |
| &#39;Strategy.GenerateOp: FwwSet&#39;          |  53.93 ns | 0.201 ns | 0.295 ns |      - |         - |
| &#39;Strategy.GenerateOp: OrSet&#39;           | 141.51 ns | 0.568 ns | 0.814 ns | 0.0069 |      72 B |
| &#39;Strategy.GenerateOp: ArrayLcs&#39;        | 380.01 ns | 1.664 ns | 2.439 ns | 0.0201 |     216 B |
| &#39;Strategy.GenerateOp: FixedSizeArray&#39;  |  94.03 ns | 0.727 ns | 1.043 ns | 0.0053 |      56 B |
| &#39;Strategy.GenerateOp: Lseq&#39;            | 114.65 ns | 0.876 ns | 1.284 ns | 0.0107 |     112 B |
| &#39;Strategy.GenerateOp: VoteCounter&#39;     |  58.83 ns | 0.223 ns | 0.326 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: StateMachine&#39;    | 202.10 ns | 0.700 ns | 1.048 ns |      - |         - |
| &#39;Strategy.GenerateOp: PriorityQueue&#39;   |  83.25 ns | 0.815 ns | 1.195 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: SortedSet&#39;       |  60.64 ns | 0.371 ns | 0.532 ns | 0.0053 |      56 B |
| &#39;Strategy.GenerateOp: RGA&#39;             | 255.23 ns | 1.057 ns | 1.515 ns | 0.0580 |     608 B |
| &#39;Strategy.GenerateOp: CounterMap&#39;      |  58.52 ns | 0.204 ns | 0.305 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: LwwMap&#39;          |  58.56 ns | 0.323 ns | 0.473 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: FwwMap&#39;          |  60.06 ns | 1.009 ns | 1.511 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: MaxWinsMap&#39;      |  58.57 ns | 0.310 ns | 0.444 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: MinWinsMap&#39;      |  59.58 ns | 0.406 ns | 0.607 ns | 0.0031 |      32 B |
| &#39;Strategy.GenerateOp: OrMap&#39;           | 172.79 ns | 0.993 ns | 1.455 ns | 0.0107 |     112 B |
| &#39;Strategy.GenerateOp: Graph&#39;           |  58.21 ns | 0.350 ns | 0.513 ns | 0.0023 |      24 B |
| &#39;Strategy.GenerateOp: TwoPhaseGraph&#39;   |  89.67 ns | 1.706 ns | 2.447 ns | 0.0023 |      24 B |
| &#39;Strategy.GenerateOp: ReplicatedTree&#39;  | 136.15 ns | 0.498 ns | 0.745 ns | 0.0053 |      56 B |
| &#39;Strategy.GenerateOp: EpochBound&#39;      | 548.00 ns | 1.282 ns | 1.839 ns | 0.0140 |     152 B |
| &#39;Strategy.GenerateOp: EpochClear&#39;      | 356.23 ns | 1.163 ns | 1.669 ns | 0.0079 |      88 B |
| &#39;Strategy.GenerateOp: ApprovalQuorum&#39;  | 251.75 ns | 2.350 ns | 3.518 ns | 0.0137 |     144 B |


<!-- BENCHMARKS_END -->