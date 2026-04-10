# GC mode comparison report

Date: 2026-04-10 18:06:38
IFC file: D:\projects\bingosoft\ifc-metadata\ifc\01_26_Slavyanka_4.ifc

## PreserveOrder=False

| GC Mode | Mean | Allocated | Delta Mean vs WS_Concurrent | Delta Allocated vs WS_Concurrent |
|---|---:|---:|---:|---:|
| WS_Concurrent | 1.214 s | 824.9 MB | 0.00% | 0.00% |
| WS_NonConcurrent | 1.313 s | 824.87 MB | +8.16% | 0.00% |
| Server_Concurrent | 917.6 ms | 824.89 MB | -24.40% | 0.00% |

Best Mean: Server_Concurrent (917.6 ms)
Best Allocated: WS_NonConcurrent (824.87 MB)

## PreserveOrder=True

| GC Mode | Mean | Allocated | Delta Mean vs WS_Concurrent | Delta Allocated vs WS_Concurrent |
|---|---:|---:|---:|---:|
| WS_Concurrent | 1.201 s | 824.9 MB | 0.00% | 0.00% |
| WS_NonConcurrent | 1.26 s | 824.89 MB | +4.95% | 0.00% |
| Server_Concurrent | 876.5 ms | 824.89 MB | -27.01% | 0.00% |

Best Mean: Server_Concurrent (876.5 ms)
Best Allocated: Server_Concurrent (824.89 MB)

