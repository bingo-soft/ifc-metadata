# Benchmark comparison report

Date: 2026-04-09 18:45:52
IFC file: D:\projects\bingosoft\ifc-metadata\ifc\01_26_Slavyanka_4.ifc

Comparison source: previous commit (HEAD~1)

| Method | Previous Mean | Current Mean | Delta Mean | Previous Allocated | Current Allocated | Delta Allocated |
|---|---:|---:|---:|---:|---:|---:|
| EndToEnd_Extract_And_Serialize [PreserveOrder=True] | 1.411 s | 1.256 s | -10.99% | 824.13 MB | 824.25 MB | +0.01% |
| EndToEnd_Extract_And_Serialize [PreserveOrder=False] | 1.41 s | 1.23 s | -12.77% | 824.14 MB | 824.39 MB | +0.03% |

## Final report order/no-order summary

| Metric | Value |
|---|---|
| Current ordered result | Mean: 1.256 s; Allocated: 824.25 MB |
| Current no-order result | Mean: 1.23 s; Allocated: 824.39 MB |
| Current no-order vs ordered difference | Mean: -2.07%; Allocated: +0.02% |
| Previous ordered result | Mean: 1.411 s; Allocated: 824.13 MB |
| Previous no-order result | Mean: 1.41 s; Allocated: 824.14 MB |
| Previous no-order vs ordered difference | Mean: -0.07%; Allocated: 0.00% |
| Ordered current vs previous difference | Mean: -10.99%; Allocated: +0.01% |
| No-order current vs previous difference | Mean: -12.77%; Allocated: +0.03% |

## Accessor telemetry artifacts

- IfcFilePipelineBenchmark-accessor-telemetry-PreserveOrder-False.md
- IfcFilePipelineBenchmark-accessor-telemetry-PreserveOrder-True.md
