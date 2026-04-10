```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
AMD Ryzen 5 5600X 3.70GHz, 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.201
  [Host]            : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  Server_Concurrent : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  WS_Concurrent     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  WS_NonConcurrent  : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3

IterationCount=8  LaunchCount=1  WarmupCount=3  

```
| Method                         | Job               | Concurrent | Server | PreserveOrder | TelemetryEnabled | Mean       | Error    | StdDev   | Gen0       | Gen1       | Gen2      | Allocated |
|------------------------------- |------------------ |----------- |------- |-------------- |----------------- |-----------:|---------:|---------:|-----------:|-----------:|----------:|----------:|
| **EndToEnd_Extract_And_Serialize** | **Server_Concurrent** | **True**       | **True**   | **False**         | **False**            |   **917.6 ms** | **14.14 ms** |  **6.28 ms** | **15000.0000** |  **8000.0000** | **5000.0000** | **824.89 MB** |
| EndToEnd_Extract_And_Serialize | WS_Concurrent     | True       | False  | False         | False            | 1,213.7 ms | 51.33 ms | 26.85 ms | 47000.0000 | 18000.0000 | 3000.0000 |  824.9 MB |
| EndToEnd_Extract_And_Serialize | WS_NonConcurrent  | False      | False  | False         | False            | 1,312.7 ms | 95.22 ms | 49.80 ms |  6000.0000 |  3000.0000 | 1000.0000 | 824.87 MB |
| **EndToEnd_Extract_And_Serialize** | **Server_Concurrent** | **True**       | **True**   | **True**          | **False**            |   **876.5 ms** | **21.13 ms** |  **9.38 ms** | **14000.0000** |  **7000.0000** | **4000.0000** | **824.89 MB** |
| EndToEnd_Extract_And_Serialize | WS_Concurrent     | True       | False  | True          | False            | 1,200.9 ms | 51.09 ms | 26.72 ms | 45000.0000 | 16000.0000 | 1000.0000 |  824.9 MB |
| EndToEnd_Extract_And_Serialize | WS_NonConcurrent  | False      | False  | True          | False            | 1,260.4 ms | 42.18 ms | 22.06 ms |  7000.0000 |  3000.0000 | 1000.0000 | 824.89 MB |
