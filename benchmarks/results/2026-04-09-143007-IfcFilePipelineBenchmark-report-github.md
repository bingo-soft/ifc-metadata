```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
AMD Ryzen 5 5600X 3.70GHz, 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3


```
| Method                                 | Mean           | Error        | StdDev       | Gen0       | Gen1       | Gen2      | Allocated    |
|--------------------------------------- |---------------:|-------------:|-------------:|-----------:|-----------:|----------:|-------------:|
| Extract_Only                           | 1,355,199.6 μs | 26,282.87 μs | 35,976.29 μs | 46000.0000 | 17000.0000 | 2000.0000 | 842847.35 KB |
| Serialize_Only_From_Extracted_Metadata |       266.8 μs |      5.32 μs |      7.45 μs |    22.4609 |    16.1133 |   16.1133 |    238.96 KB |
| EndToEnd_Extract_And_Serialize         | 1,340,074.8 μs | 26,026.57 μs | 69,018.76 μs | 46000.0000 | 17000.0000 | 2000.0000 | 844033.84 KB |
| Extract_Only_NoOrder                   | 1,324,738.1 μs | 26,091.35 μs | 30,046.83 μs | 46000.0000 | 17000.0000 | 2000.0000 | 842822.46 KB |
| EndToEnd_Extract_And_Serialize_NoOrder | 1,218,027.5 μs | 23,343.90 μs | 23,972.47 μs | 46000.0000 | 17000.0000 | 2000.0000 | 843978.45 KB |
