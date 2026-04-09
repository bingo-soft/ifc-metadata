```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
AMD Ryzen 5 5600X 3.70GHz, 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3


```
| Method                         | PreserveOrder | Mean    | Error    | StdDev   | Gen0       | Gen1       | Gen2      | Allocated |
|------------------------------- |-------------- |--------:|---------:|---------:|-----------:|-----------:|----------:|----------:|
| **EndToEnd_Extract_And_Serialize** | **False**         | **1.410 s** | **0.0278 s** | **0.0380 s** | **46000.0000** | **17000.0000** | **2000.0000** | **824.14 MB** |
| **EndToEnd_Extract_And_Serialize** | **True**          | **1.411 s** | **0.0281 s** | **0.0345 s** | **46000.0000** | **17000.0000** | **2000.0000** | **824.13 MB** |
