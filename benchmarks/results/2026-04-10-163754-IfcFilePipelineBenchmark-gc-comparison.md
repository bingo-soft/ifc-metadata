# GC mode comparison report

Date: 2026-04-10 16:37:54
IFC file: D:\projects\bingosoft\ifc-metadata\ifc\01_26_Slavyanka_4.ifc

## PreserveOrder=False

| GC Mode | Mean | Allocated | Delta Mean vs WS_Concurrent | Delta Allocated vs WS_Concurrent |
|---|---:|---:|---:|---:|
| WS_Concurrent | 1.59 s | 824.9 MB | 0.00% | 0.00% |
| WS_NonConcurrent | 1.773 s | 824.87 MB | +11.51% | 0.00% |
| Server_Concurrent | 1.161 s | 824.89 MB | -26.98% | 0.00% |

Best Mean: Server_Concurrent (1.161 s)
Best Allocated: WS_NonConcurrent (824.87 MB)

## PreserveOrder=True

| GC Mode | Mean | Allocated | Delta Mean vs WS_Concurrent | Delta Allocated vs WS_Concurrent |
|---|---:|---:|---:|---:|
| WS_Concurrent | 1.6 s | 824.9 MB | 0.00% | 0.00% |
| WS_NonConcurrent | 1.797 s | 824.89 MB | +12.31% | 0.00% |
| Server_Concurrent | 1.162 s | 824.89 MB | -27.38% | 0.00% |

Best Mean: Server_Concurrent (1.162 s)
Best Allocated: Server_Concurrent (824.89 MB)

