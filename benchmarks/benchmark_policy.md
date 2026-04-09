# Benchmark policy

1. After each test run, benchmark report must be generated.
2. Standard command:

```bash
pwsh ./benchmarks/run-baseline.ps1 -IfcFilePath "ifc/01_26_Slavyanka_4.ifc"
```

3. Script responsibilities:
- run tests (`dotnet test ifc-metadata.slnx -c Release`);
- run only the overall benchmark (`IfcFilePipelineBenchmark.EndToEnd_Extract_And_Serialize`) by default;
- run GC matrix in one cycle for modes: `WS_Concurrent`, `WS_NonConcurrent`, `Server_Concurrent`;
- run order variants (`PreserveOrder=true/false`) for each GC mode;
- store timestamped benchmark snapshots in `benchmarks/results`;
- update rolling snapshot in `benchmarks/results/latest`;
- move previous rolling snapshot to `benchmarks/results/previous`;
- generate comparison report (`IfcFilePipelineBenchmark-comparison.md`);
- generate GC mode report (`IfcFilePipelineBenchmark-gc-comparison.md`) with deltas vs `WS_Concurrent`;
- compare with pre-commit baseline (`benchmarks/results/previous/*` from the last successful cycle); if unavailable, compare with previous commit (`HEAD~1`) when report exists there.

4. Additional benchmarks (`Extract_Only`, `Serialize_Only_From_Extracted_Metadata`, etc.) are diagnostic and must be run only on explicit request (`--detailed`).

5. Pre-commit baseline rule:
- after tests and benchmark run, current `benchmarks/results/latest/*` is the baseline for the next local run;
- before commit, this baseline must be updated by the same cycle (tests -> benchmarks -> report);
- the updated `benchmarks/results/latest/*` must be committed together with code changes.
6. Time and memory values in generated comparison report must be formatted by magnitude:
- time: `μs` / `ms` / `s` (if value reaches seconds, use seconds);
- memory: `KB` / `MB` / `GB` (if value reaches megabytes, use megabytes).

7. Final report must include order/no-order summary lines for baseline GC mode `WS_Concurrent`:
- current ordered result;
- current no-order result;
- current no-order vs ordered difference in percent;
- previous ordered result;
- previous no-order result;
- previous no-order vs ordered difference in percent;
- ordered current vs previous difference in percent;
- no-order current vs previous difference in percent.

8. GC report must include, for each `PreserveOrder` value:
- all three GC modes (`WS_Concurrent`, `WS_NonConcurrent`, `Server_Concurrent`);
- current Mean and Allocated;
- deltas vs `WS_Concurrent`;
- best mode by Mean and best mode by Allocated.

