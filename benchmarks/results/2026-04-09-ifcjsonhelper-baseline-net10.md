# IfcJsonHelper benchmark baseline (.NET 10)

Date: 2026-04-09  
Command:

```bash
dotnet run --project benchmarks/ifc-metadata.Benchmarks.csproj -c Release
```

Environment:
- BenchmarkDotNet 0.15.8
- OS: Windows 11 (10.0.26200.8037)
- CPU: AMD Ryzen 5 5600X
- Runtime: .NET 10.0.5, X64 RyuJIT
- GC: Concurrent Workstation

Results:

| Method                 | MetaObjectCount | Mean     | Error    | StdDev   | Gen0     | Gen1     | Gen2     | Allocated |
|----------------------- |---------------- |---------:|---------:|---------:|---------:|---------:|---------:|----------:|
| Serialize_To_Json_File | 100             | 177.6 us |  3.52 us |  5.98 us |   2.6855 |        - |        - |   47.7 KB |
| Serialize_To_Json_File | 1000            | 717.6 us | 14.20 us | 29.94 us | 124.0234 | 124.0234 | 124.0234 | 506.48 KB |

Notes for comparison:
- This baseline measures only JSON serialization (`IfcJsonHelper.ToJson`) on synthetic in-memory metadata.
- It does not include IFC parsing (`IfcStore.Open` + extraction).
- Baseline file should be preserved and compared against future optimization runs.
