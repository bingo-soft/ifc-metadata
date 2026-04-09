```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
AMD Ryzen 5 5600X 3.70GHz, 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3


```
| Method                         | PreserveOrder | TelemetryEnabled | Mean    | Error    | StdDev   | Gen0       | Gen1       | Gen2      | Allocated |
|------------------------------- |-------------- |----------------- |--------:|---------:|---------:|-----------:|-----------:|----------:|----------:|
| **EndToEnd_Extract_And_Serialize** | **False**         | **False**            | **1.452 s** | **0.0288 s** | **0.0724 s** | **47000.0000** | **18000.0000** | **3000.0000** | **824.73 MB** |
| **EndToEnd_Extract_And_Serialize** | **True**          | **False**            | **1.556 s** | **0.0307 s** | **0.0699 s** | **47000.0000** | **18000.0000** | **3000.0000** | **824.75 MB** |
