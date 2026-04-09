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
| **EndToEnd_Extract_And_Serialize** | **Server_Concurrent** | **True**       | **True**   | **False**         | **False**            |   **972.3 ms** | **41.37 ms** | **21.64 ms** | **14000.0000** |  **7000.0000** | **4000.0000** | **824.62 MB** |
| EndToEnd_Extract_And_Serialize | WS_Concurrent     | True       | False  | False         | False            | 1,259.8 ms | 68.74 ms | 35.95 ms | 47000.0000 | 18000.0000 | 3000.0000 | 824.63 MB |
| EndToEnd_Extract_And_Serialize | WS_NonConcurrent  | False      | False  | False         | False            | 1,358.8 ms | 78.19 ms | 40.90 ms |  6000.0000 |  3000.0000 | 1000.0000 |  824.6 MB |
| **EndToEnd_Extract_And_Serialize** | **Server_Concurrent** | **True**       | **True**   | **True**          | **False**            |   **904.2 ms** | **16.26 ms** |  **8.50 ms** | **14000.0000** |  **7000.0000** | **4000.0000** | **824.62 MB** |
| EndToEnd_Extract_And_Serialize | WS_Concurrent     | True       | False  | True          | False            | 1,251.8 ms | 64.41 ms | 33.69 ms | 45000.0000 | 16000.0000 | 1000.0000 | 824.77 MB |
| EndToEnd_Extract_And_Serialize | WS_NonConcurrent  | False      | False  | True          | False            | 1,330.1 ms | 34.77 ms | 18.18 ms |  7000.0000 |  3000.0000 | 1000.0000 | 824.62 MB |
