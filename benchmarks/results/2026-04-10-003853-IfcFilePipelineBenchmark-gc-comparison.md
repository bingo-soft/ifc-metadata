# GC mode comparison report

Date: 2026-04-10 00:38:53
IFC file: D:\projects\bingosoft\ifc-metadata\ifc\01_26_Slavyanka_4.ifc

## PreserveOrder=False

| GC Mode | Mean | Allocated | Delta Mean vs WS_Concurrent | Delta Allocated vs WS_Concurrent |
|---|---:|---:|---:|---:|
| WS_Concurrent | 1.26 s | 824.63 MB | 0.00% | 0.00% |
| WS_NonConcurrent | 1.359 s | 824.6 MB | +7.86% | 0.00% |
| Server_Concurrent | 972.3 ms | 824.62 MB | -22.82% | 0.00% |

Best Mean: Server_Concurrent (972.3 ms)
Best Allocated: WS_NonConcurrent (824.6 MB)

## PreserveOrder=True

| GC Mode | Mean | Allocated | Delta Mean vs WS_Concurrent | Delta Allocated vs WS_Concurrent |
|---|---:|---:|---:|---:|
| WS_Concurrent | 1.252 s | 824.77 MB | 0.00% | 0.00% |
| WS_NonConcurrent | 1.33 s | 824.62 MB | +6.25% | -0.02% |
| Server_Concurrent | 904.2 ms | 824.62 MB | -27.77% | -0.02% |

Best Mean: Server_Concurrent (904.2 ms)
Best Allocated: WS_NonConcurrent (824.62 MB)

