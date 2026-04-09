# Benchmark comparison report

Date: 2026-04-09 21:36:17
IFC file: D:\projects\bingosoft\ifc-metadata\ifc\01_26_Slavyanka_4.ifc

Comparison source: previous commit (HEAD~1)

| Method | Previous Mean | Current Mean | Delta Mean | Previous Allocated | Current Allocated | Delta Allocated |
|---|---:|---:|---:|---:|---:|---:|
| EndToEnd_Extract_And_Serialize [PreserveOrder=False] | 1.592 s | 1.406 s | -11.68% | 824.25 MB | 824.39 MB | +0.02% |
| EndToEnd_Extract_And_Serialize [PreserveOrder=True] | 1.54 s | 1.485 s | -3.57% | 824.25 MB | 824.25 MB | 0.00% |

## Final report order/no-order summary

| Metric | Value |
|---|---|
| Current ordered result | Mean: 1.485 s; Allocated: 824.25 MB |
| Current no-order result | Mean: 1.406 s; Allocated: 824.39 MB |
| Current no-order vs ordered difference | Mean: -5.32%; Allocated: +0.02% |
| Previous ordered result | Mean: 1.54 s; Allocated: 824.25 MB |
| Previous no-order result | Mean: 1.592 s; Allocated: 824.25 MB |
| Previous no-order vs ordered difference | Mean: +3.38%; Allocated: 0.00% |
| Ordered current vs previous difference | Mean: -3.57%; Allocated: 0.00% |
| No-order current vs previous difference | Mean: -11.68%; Allocated: +0.02% |

## Accessor telemetry artifacts

- IfcFilePipelineBenchmark-accessor-telemetry-PreserveOrder-False.md
- IfcFilePipelineBenchmark-accessor-telemetry-PreserveOrder-True.md
