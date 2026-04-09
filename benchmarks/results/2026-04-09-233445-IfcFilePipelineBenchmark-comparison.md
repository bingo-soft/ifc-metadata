# Benchmark comparison report

Date: 2026-04-09 23:34:46
IFC file: D:\projects\bingosoft\ifc-metadata\ifc\01_26_Slavyanka_4.ifc

Comparison source: previous commit (HEAD~1)

| Method | Previous Mean | Current Mean | Delta Mean | Previous Allocated | Current Allocated | Delta Allocated |
|---|---:|---:|---:|---:|---:|---:|
| EndToEnd_Extract_And_Serialize [PreserveOrder=False] | 1.406 s | 1.452 s | +3.27% | 824.39 MB | 824.73 MB | +0.04% |
| EndToEnd_Extract_And_Serialize [PreserveOrder=True] | 1.485 s | 1.556 s | +4.78% | 824.25 MB | 824.75 MB | +0.06% |

## Final report order/no-order summary

| Metric | Value |
|---|---|
| Current ordered result | Mean: 1.556 s; Allocated: 824.75 MB |
| Current no-order result | Mean: 1.452 s; Allocated: 824.73 MB |
| Current no-order vs ordered difference | Mean: -6.68%; Allocated: 0.00% |
| Previous ordered result | Mean: 1.485 s; Allocated: 824.25 MB |
| Previous no-order result | Mean: 1.406 s; Allocated: 824.39 MB |
| Previous no-order vs ordered difference | Mean: -5.32%; Allocated: +0.02% |
| Ordered current vs previous difference | Mean: +4.78%; Allocated: +0.06% |
| No-order current vs previous difference | Mean: +3.27%; Allocated: +0.04% |
