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
| Method                         | Job               | Concurrent | Server | PreserveOrder | TelemetryEnabled | Mean       | Error     | StdDev   | Gen0       | Gen1       | Gen2      | Allocated |
|------------------------------- |------------------ |----------- |------- |-------------- |----------------- |-----------:|----------:|---------:|-----------:|-----------:|----------:|----------:|
| **EndToEnd_Extract_And_Serialize** | **Server_Concurrent** | **True**       | **True**   | **False**         | **False**            |   **919.7 ms** |  **32.81 ms** | **14.57 ms** | **15000.0000** |  **8000.0000** | **5000.0000** | **824.72 MB** |
| EndToEnd_Extract_And_Serialize | WS_Concurrent     | True       | False  | False         | False            | 1,265.5 ms | 114.04 ms | 59.65 ms | 47000.0000 | 18000.0000 | 3000.0000 | 824.73 MB |
| EndToEnd_Extract_And_Serialize | WS_NonConcurrent  | False      | False  | False         | False            | 1,377.5 ms |  96.95 ms | 50.71 ms |  6000.0000 |  3000.0000 | 1000.0000 |  824.7 MB |
| **EndToEnd_Extract_And_Serialize** | **Server_Concurrent** | **True**       | **True**   | **True**          | **False**            |   **904.8 ms** |  **11.98 ms** |  **6.27 ms** | **14000.0000** |  **7000.0000** | **4000.0000** | **824.74 MB** |
| EndToEnd_Extract_And_Serialize | WS_Concurrent     | True       | False  | True          | False            | 1,263.5 ms |  55.72 ms | 29.14 ms | 47000.0000 | 18000.0000 | 3000.0000 | 824.89 MB |
| EndToEnd_Extract_And_Serialize | WS_NonConcurrent  | False      | False  | True          | False            | 1,368.7 ms |  99.98 ms | 52.29 ms |  6000.0000 |  3000.0000 | 1000.0000 | 824.86 MB |
