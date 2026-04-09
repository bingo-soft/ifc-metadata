# Benchmark policy

1. After each test run, benchmark report must be generated.
2. Standard command:

```bash
pwsh ./benchmarks/run-baseline.ps1 -IfcFilePath "ifc/01_26_Slavyanka_4.ifc"
```

3. Script responsibilities:
- run tests (`dotnet test ifc-metadata.slnx -c Release`);
- run all 3 methods of `IfcFilePipelineBenchmark`;
- store timestamped benchmark snapshots in `benchmarks/results`;
- update rolling snapshot in `benchmarks/results/latest`;
- move previous rolling snapshot to `benchmarks/results/previous`;
- generate comparison report;
- compare with previous commit (`HEAD~1`) if previous commit report exists, otherwise compare with previous local run.

4. To enable comparison with previous commit, `benchmarks/results/latest/*` must be committed every time after benchmark run.
5. Time and memory values in generated comparison report must be formatted by magnitude:
- time: `μs` / `ms` / `s` (if value reaches seconds, use seconds);
- memory: `KB` / `MB` / `GB` (if value reaches megabytes, use megabytes).
