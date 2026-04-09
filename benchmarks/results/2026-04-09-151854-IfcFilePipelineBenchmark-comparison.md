# Benchmark comparison report

Date: 2026-04-09 15:18:54
IFC file: D:\projects\bingosoft\ifc-metadata\ifc\01_26_Slavyanka_4.ifc

Comparison source: previous local run

| Method | Previous Mean | Current Mean | Delta Mean | Previous Allocated | Current Allocated | Delta Allocated |
|---|---:|---:|---:|---:|---:|---:|
| EndToEnd_Extract_And_Serialize [PreserveOrder=True] | 1.344 s | 1.432 s | +6.55% | 824.25 MB | 824.25 MB | 0.00% |
| EndToEnd_Extract_And_Serialize [PreserveOrder=False] | 1.409 s | 1.368 s | -2.91% | 824.2 MB | 824.2 MB | 0.00% |

## Final report order/no-order summary

| Metric | Value |
|---|---|
| Current ordered result | Mean: 1.432 s; Allocated: 824.25 MB |
| Current no-order result | Mean: 1.368 s; Allocated: 824.2 MB |
| Current no-order vs ordered difference | Mean: -4.47%; Allocated: -0.01% |
| Previous ordered result | Mean: 1.344 s; Allocated: 824.25 MB |
| Previous no-order result | Mean: 1.409 s; Allocated: 824.2 MB |
| Previous no-order vs ordered difference | Mean: +4.84%; Allocated: -0.01% |
| Ordered current vs previous difference | Mean: +6.55%; Allocated: 0.00% |
| No-order current vs previous difference | Mean: -2.91%; Allocated: 0.00% |
