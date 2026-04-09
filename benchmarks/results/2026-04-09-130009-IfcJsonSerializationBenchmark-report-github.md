```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
AMD Ryzen 5 5600X 3.70GHz, 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3


```
| Method                 | MetaObjectCount | Mean     | Error    | StdDev   | Gen0     | Gen1     | Gen2     | Allocated |
|----------------------- |---------------- |---------:|---------:|---------:|---------:|---------:|---------:|----------:|
| **Serialize_To_Json_File** | **100**             | **166.0 μs** |  **3.31 μs** |  **6.21 μs** |   **2.6855** |        **-** |        **-** |   **47.7 KB** |
| **Serialize_To_Json_File** | **1000**            | **552.0 μs** | **10.88 μs** | **11.64 μs** | **124.0234** | **124.0234** | **124.0234** | **506.48 KB** |
