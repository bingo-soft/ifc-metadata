# Benchmark comparison report

Date: 2026-04-09 18:15:25
IFC file: D:\projects\bingosoft\ifc-metadata\ifc\01_26_Slavyanka_4.ifc

Comparison source: previous commit (HEAD~1)

| Method | Previous Mean | Current Mean | Delta Mean | Previous Allocated | Current Allocated | Delta Allocated |
|---|---:|---:|---:|---:|---:|---:|
| EndToEnd_Extract_And_Serialize [PreserveOrder=True] | 1.432 s | 1.54 s | +7.54% | 824.25 MB | 824.25 MB | 0.00% |
| EndToEnd_Extract_And_Serialize [PreserveOrder=False] | 1.368 s | 1.592 s | +16.37% | 824.2 MB | 824.25 MB | +0.01% |

## Final report order/no-order summary

| Metric | Value |
|---|---|
| Current ordered result | Mean: 1.54 s; Allocated: 824.25 MB |
| Current no-order result | Mean: 1.592 s; Allocated: 824.25 MB |
| Current no-order vs ordered difference | Mean: +3.38%; Allocated: 0.00% |
| Previous ordered result | Mean: 1.432 s; Allocated: 824.25 MB |
| Previous no-order result | Mean: 1.368 s; Allocated: 824.2 MB |
| Previous no-order vs ordered difference | Mean: -4.47%; Allocated: -0.01% |
| Ordered current vs previous difference | Mean: +7.54%; Allocated: 0.00% |
| No-order current vs previous difference | Mean: +16.37%; Allocated: +0.01% |

## Accessor telemetry artifacts

- IfcFilePipelineBenchmark-accessor-telemetry-PreserveOrder-False.md
- IfcFilePipelineBenchmark-accessor-telemetry-PreserveOrder-True.md
