```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
AMD Ryzen 5 5600X 3.70GHz, 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3


```
| Method                                 | Mean           | Error        | StdDev       | Gen0       | Gen1       | Gen2      | Allocated    |
|--------------------------------------- |---------------:|-------------:|-------------:|-----------:|-----------:|----------:|-------------:|
| Extract_Only                           | 1,341,927.7 μs | 26,694.62 μs | 29,671.00 μs | 47000.0000 | 18000.0000 | 3000.0000 | 843702.62 KB |
| Serialize_Only_From_Extracted_Metadata |       294.9 μs |      5.80 μs |      9.53 μs |    22.4609 |    16.1133 |   16.1133 |    238.94 KB |
| EndToEnd_Extract_And_Serialize         | 1,316,079.1 μs | 25,445.48 μs | 35,671.01 μs | 46000.0000 | 17000.0000 | 2000.0000 | 843921.05 KB |
