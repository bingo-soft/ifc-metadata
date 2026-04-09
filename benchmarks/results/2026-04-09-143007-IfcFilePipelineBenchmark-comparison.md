# Benchmark comparison report

Date: 2026-04-09 14:30:07
IFC file: D:\projects\bingosoft\ifc-metadata\ifc\01_26_Slavyanka_4.ifc

Comparison source: previous local run

| Method | Previous Mean | Current Mean | Delta Mean | Previous Allocated | Current Allocated | Delta Allocated |
|---|---:|---:|---:|---:|---:|---:|
| Serialize_Only_From_Extracted_Metadata | 294.9 μs | 266.8 μs | -9.53% | 238.94 KB | 238.96 KB | +0.01% |
| EndToEnd_Extract_And_Serialize | 1.316 s | 1.34 s | +1.82% | 824.142 MB | 824.252 MB | +0.01% |
| Extract_Only | 1.342 s | 1.355 s | +0.99% | 823.928 MB | 823.093 MB | -0.10% |
| Extract_Only_NoOrder | n/a | 1.325 s | n/a | n/a | 823.069 MB | n/a |
| EndToEnd_Extract_And_Serialize_NoOrder | n/a | 1.218 s | n/a | n/a | 824.198 MB | n/a |
