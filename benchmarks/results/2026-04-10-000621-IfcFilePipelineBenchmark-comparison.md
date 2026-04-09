# Benchmark comparison report

Date: 2026-04-10 00:06:21
IFC file: D:\projects\bingosoft\ifc-metadata\ifc\01_26_Slavyanka_4.ifc

Comparison source: previous local run

| Method | Previous Mean | Current Mean | Delta Mean | Previous Allocated | Current Allocated | Delta Allocated |
|---|---:|---:|---:|---:|---:|---:|
| EndToEnd_Extract_And_Serialize [PreserveOrder=False, Job=Server_Concurrent] | 919.7 ms | 1.145 s | +24.50% | 824.72 MB | 824.71 MB | 0.00% |
| EndToEnd_Extract_And_Serialize [PreserveOrder=False, Job=WS_Concurrent] | 1.266 s | 1.639 s | +29.51% | 824.73 MB | 824.73 MB | 0.00% |
| EndToEnd_Extract_And_Serialize [PreserveOrder=False, Job=WS_NonConcurrent] | 1.378 s | 1.689 s | +22.61% | 824.7 MB | 824.7 MB | 0.00% |
| EndToEnd_Extract_And_Serialize [PreserveOrder=True, Job=Server_Concurrent] | 904.8 ms | 1.16 s | +28.21% | 824.74 MB | 824.74 MB | 0.00% |
| EndToEnd_Extract_And_Serialize [PreserveOrder=True, Job=WS_Concurrent] | 1.264 s | 1.645 s | +30.19% | 824.89 MB | 824.75 MB | -0.02% |
| EndToEnd_Extract_And_Serialize [PreserveOrder=True, Job=WS_NonConcurrent] | 1.369 s | 1.754 s | +28.15% | 824.86 MB | 824.73 MB | -0.02% |

## Final report order/no-order summary

Summary job: WS_Concurrent

| Metric | Value |
|---|---|
| Current ordered result | Mean: 1.645 s; Allocated: 824.75 MB |
| Current no-order result | Mean: 1.639 s; Allocated: 824.73 MB |
| Current no-order vs ordered difference | Mean: -0.36%; Allocated: 0.00% |
| Previous ordered result | Mean: 1.264 s; Allocated: 824.89 MB |
| Previous no-order result | Mean: 1.266 s; Allocated: 824.73 MB |
| Previous no-order vs ordered difference | Mean: +0.16%; Allocated: -0.02% |
| Ordered current vs previous difference | Mean: +30.19%; Allocated: -0.02% |
| No-order current vs previous difference | Mean: +29.51%; Allocated: 0.00% |
