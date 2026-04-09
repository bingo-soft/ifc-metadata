```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
AMD Ryzen 5 5600X 3.70GHz, 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3


```
| Method                                 | Mean           | Error        | StdDev       | Gen0       | Gen1       | Gen2      | Allocated    |
|--------------------------------------- |---------------:|-------------:|-------------:|-----------:|-----------:|----------:|-------------:|
| Extract_Only                           | 1,324,484.5 μs | 25,736.01 μs | 30,636.89 μs | 46000.0000 | 17000.0000 | 2000.0000 | 843684.45 KB |
| Serialize_Only_From_Extracted_Metadata |       294.0 μs |      5.38 μs |      9.14 μs |    22.4609 |    16.1133 |   16.1133 |    238.96 KB |
| EndToEnd_Extract_And_Serialize         | 1,216,809.4 μs | 23,916.62 μs | 36,523.20 μs | 46000.0000 | 17000.0000 | 2000.0000 | 843921.05 KB |
