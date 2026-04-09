# GC mode comparison report

Date: 2026-04-10 00:06:21
IFC file: D:\projects\bingosoft\ifc-metadata\ifc\01_26_Slavyanka_4.ifc

## PreserveOrder=False

| GC Mode | Mean | Allocated | Delta Mean vs WS_Concurrent | Delta Allocated vs WS_Concurrent |
|---|---:|---:|---:|---:|
| WS_Concurrent | 1.639 s | 824.73 MB | 0.00% | 0.00% |
| WS_NonConcurrent | 1.689 s | 824.7 MB | +3.05% | 0.00% |
| Server_Concurrent | 1.145 s | 824.71 MB | -30.14% | 0.00% |

Best Mean: Server_Concurrent (1.145 s)
Best Allocated: WS_NonConcurrent (824.7 MB)

## PreserveOrder=True

| GC Mode | Mean | Allocated | Delta Mean vs WS_Concurrent | Delta Allocated vs WS_Concurrent |
|---|---:|---:|---:|---:|
| WS_Concurrent | 1.645 s | 824.75 MB | 0.00% | 0.00% |
| WS_NonConcurrent | 1.754 s | 824.73 MB | +6.63% | 0.00% |
| Server_Concurrent | 1.16 s | 824.74 MB | -29.48% | 0.00% |

Best Mean: Server_Concurrent (1.16 s)
Best Allocated: WS_NonConcurrent (824.73 MB)

