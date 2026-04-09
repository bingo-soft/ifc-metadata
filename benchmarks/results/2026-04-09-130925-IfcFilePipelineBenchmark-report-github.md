```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
AMD Ryzen 5 5600X 3.70GHz, 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3


```
| Method                                 | Mean           | Error        | StdDev       | Gen0       | Gen1       | Gen2      | Allocated    |
|--------------------------------------- |---------------:|-------------:|-------------:|-----------:|-----------:|----------:|-------------:|
| Extract_Only                           | 1,311,769.4 μs | 24,307.78 μs | 23,873.49 μs | 46000.0000 | 17000.0000 | 2000.0000 | 843682.21 KB |
| Serialize_Only_From_Extracted_Metadata |       292.8 μs |      5.85 μs |      8.38 μs |    22.4609 |    16.1133 |   16.1133 |    238.96 KB |
| EndToEnd_Extract_And_Serialize         | 1,305,452.7 μs | 25,776.43 μs | 28,650.43 μs | 46000.0000 | 17000.0000 | 2000.0000 |  843921.3 KB |
