# Benchmark comparison report

Date: 2026-04-10 00:38:53
IFC file: D:\projects\bingosoft\ifc-metadata\ifc\01_26_Slavyanka_4.ifc

Comparison source: previous local run

| Method | Previous Mean | Current Mean | Delta Mean | Previous Allocated | Current Allocated | Delta Allocated |
|---|---:|---:|---:|---:|---:|---:|
| EndToEnd_Extract_And_Serialize [PreserveOrder=False, Job=Server_Concurrent] | 1.145 s | 972.3 ms | -15.08% | 824.71 MB | 824.62 MB | -0.01% |
| EndToEnd_Extract_And_Serialize [PreserveOrder=False, Job=WS_Concurrent] | 1.639 s | 1.26 s | -23.14% | 824.73 MB | 824.63 MB | -0.01% |
| EndToEnd_Extract_And_Serialize [PreserveOrder=False, Job=WS_NonConcurrent] | 1.689 s | 1.359 s | -19.55% | 824.7 MB | 824.6 MB | -0.01% |
| EndToEnd_Extract_And_Serialize [PreserveOrder=True, Job=Server_Concurrent] | 1.16 s | 904.2 ms | -22.05% | 824.74 MB | 824.62 MB | -0.01% |
| EndToEnd_Extract_And_Serialize [PreserveOrder=True, Job=WS_Concurrent] | 1.645 s | 1.252 s | -23.90% | 824.75 MB | 824.77 MB | 0.00% |
| EndToEnd_Extract_And_Serialize [PreserveOrder=True, Job=WS_NonConcurrent] | 1.754 s | 1.33 s | -24.17% | 824.73 MB | 824.62 MB | -0.01% |

## Final report order/no-order summary

Summary job: WS_Concurrent

| Metric | Value |
|---|---|
| Current ordered result | Mean: 1.252 s; Allocated: 824.77 MB |
| Current no-order result | Mean: 1.26 s; Allocated: 824.63 MB |
| Current no-order vs ordered difference | Mean: +0.64%; Allocated: -0.02% |
| Previous ordered result | Mean: 1.645 s; Allocated: 824.75 MB |
| Previous no-order result | Mean: 1.639 s; Allocated: 824.73 MB |
| Previous no-order vs ordered difference | Mean: -0.36%; Allocated: 0.00% |
| Ordered current vs previous difference | Mean: -23.90%; Allocated: 0.00% |
| No-order current vs previous difference | Mean: -23.14%; Allocated: -0.01% |
