# Benchmark comparison report

Date: 2026-04-10 16:37:54
IFC file: D:\projects\bingosoft\ifc-metadata\ifc\01_26_Slavyanka_4.ifc

Comparison source: previous local run

| Method | Previous Mean | Current Mean | Delta Mean | Previous Allocated | Current Allocated | Delta Allocated |
|---|---:|---:|---:|---:|---:|---:|
| EndToEnd_Extract_And_Serialize [PreserveOrder=False, Job=Server_Concurrent] | 972.3 ms | 1.161 s | +19.41% | 824.62 MB | 824.89 MB | +0.03% |
| EndToEnd_Extract_And_Serialize [PreserveOrder=False, Job=WS_Concurrent] | 1.26 s | 1.59 s | +26.21% | 824.63 MB | 824.9 MB | +0.03% |
| EndToEnd_Extract_And_Serialize [PreserveOrder=False, Job=WS_NonConcurrent] | 1.359 s | 1.773 s | +30.48% | 824.6 MB | 824.87 MB | +0.03% |
| EndToEnd_Extract_And_Serialize [PreserveOrder=True, Job=Server_Concurrent] | 904.2 ms | 1.162 s | +28.51% | 824.62 MB | 824.89 MB | +0.03% |
| EndToEnd_Extract_And_Serialize [PreserveOrder=True, Job=WS_Concurrent] | 1.252 s | 1.6 s | +27.82% | 824.77 MB | 824.9 MB | +0.02% |
| EndToEnd_Extract_And_Serialize [PreserveOrder=True, Job=WS_NonConcurrent] | 1.33 s | 1.797 s | +35.10% | 824.62 MB | 824.89 MB | +0.03% |

## Final report order/no-order summary

Summary job: WS_Concurrent

| Metric | Value |
|---|---|
| Current ordered result | Mean: 1.6 s; Allocated: 824.9 MB |
| Current no-order result | Mean: 1.59 s; Allocated: 824.9 MB |
| Current no-order vs ordered difference | Mean: -0.63%; Allocated: 0.00% |
| Previous ordered result | Mean: 1.252 s; Allocated: 824.77 MB |
| Previous no-order result | Mean: 1.26 s; Allocated: 824.63 MB |
| Previous no-order vs ordered difference | Mean: +0.64%; Allocated: -0.02% |
| Ordered current vs previous difference | Mean: +27.82%; Allocated: +0.02% |
| No-order current vs previous difference | Mean: +26.21%; Allocated: +0.03% |
