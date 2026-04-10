# Benchmark comparison report

Date: 2026-04-10 18:06:38
IFC file: D:\projects\bingosoft\ifc-metadata\ifc\01_26_Slavyanka_4.ifc

Comparison source: previous local run

| Method | Previous Mean | Current Mean | Delta Mean | Previous Allocated | Current Allocated | Delta Allocated |
|---|---:|---:|---:|---:|---:|---:|
| EndToEnd_Extract_And_Serialize [PreserveOrder=False, Job=Server_Concurrent] | 1.161 s | 917.6 ms | -20.96% | 824.89 MB | 824.89 MB | 0.00% |
| EndToEnd_Extract_And_Serialize [PreserveOrder=False, Job=WS_Concurrent] | 1.59 s | 1.214 s | -23.67% | 824.9 MB | 824.9 MB | 0.00% |
| EndToEnd_Extract_And_Serialize [PreserveOrder=False, Job=WS_NonConcurrent] | 1.773 s | 1.313 s | -25.96% | 824.87 MB | 824.87 MB | 0.00% |
| EndToEnd_Extract_And_Serialize [PreserveOrder=True, Job=Server_Concurrent] | 1.162 s | 876.5 ms | -24.57% | 824.89 MB | 824.89 MB | 0.00% |
| EndToEnd_Extract_And_Serialize [PreserveOrder=True, Job=WS_Concurrent] | 1.6 s | 1.201 s | -24.94% | 824.9 MB | 824.9 MB | 0.00% |
| EndToEnd_Extract_And_Serialize [PreserveOrder=True, Job=WS_NonConcurrent] | 1.797 s | 1.26 s | -29.86% | 824.89 MB | 824.89 MB | 0.00% |

## Final report order/no-order summary

Summary job: WS_Concurrent

| Metric | Value |
|---|---|
| Current ordered result | Mean: 1.201 s; Allocated: 824.9 MB |
| Current no-order result | Mean: 1.214 s; Allocated: 824.9 MB |
| Current no-order vs ordered difference | Mean: +1.07%; Allocated: 0.00% |
| Previous ordered result | Mean: 1.6 s; Allocated: 824.9 MB |
| Previous no-order result | Mean: 1.59 s; Allocated: 824.9 MB |
| Previous no-order vs ordered difference | Mean: -0.63%; Allocated: 0.00% |
| Ordered current vs previous difference | Mean: -24.94%; Allocated: 0.00% |
| No-order current vs previous difference | Mean: -23.67%; Allocated: 0.00% |
