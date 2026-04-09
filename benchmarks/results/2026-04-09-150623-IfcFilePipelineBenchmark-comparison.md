# Benchmark comparison report

Date: 2026-04-09 15:06:23
IFC file: D:\projects\bingosoft\ifc-metadata\ifc\01_26_Slavyanka_4.ifc

Comparison source: previous local run

| Method | Previous Mean | Current Mean | Delta Mean | Previous Allocated | Current Allocated | Delta Allocated |
|---|---:|---:|---:|---:|---:|---:|
| EndToEnd_Extract_And_Serialize [AnalyzeLaunchVariance=False, EvaluateOverhead=Default, MaxAbsoluteError=Default, MaxRelativeError=Default, MinInvokeCount=Default, MinIterationTime=Default, OutlierMode=Default, Affinity=111111111111, EnvironmentVariables=Empty, Jit=RyuJit, LargeAddressAware=Default, Platform=X64, PowerPlanMode=8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c, Runtime=.NET 10.0, AllowVeryLargeObjects=False, Concurrent=True, CpuGroups=False, Force=True, HeapAffinitizeMask=Default, HeapCount=Default, NoAffinitize=False, RetainVm=False, Server=False, Arguments=Default, BuildConfiguration=Default, Clock=Default, EngineFactory=Default, NuGetReferences=Default, Toolchain=Default, IsMutator=Default, InvocationCount=Default, IterationCount=Default, IterationTime=Default, LaunchCount=Default, MaxIterationCount=Default, MaxWarmupIterationCount=Default, MemoryRandomization=Default, MinIterationCount=Default, MinWarmupIterationCount=Default, RunStrategy=Default, UnrollFactor=16, WarmupCount=Default, PreserveOrder=False] | 1.229 s | 1.409 s | +14.65% | 824.2 MB | 824.2 MB | 0.00% |
| EndToEnd_Extract_And_Serialize [AnalyzeLaunchVariance=False, EvaluateOverhead=Default, MaxAbsoluteError=Default, MaxRelativeError=Default, MinInvokeCount=Default, MinIterationTime=Default, OutlierMode=Default, Affinity=111111111111, EnvironmentVariables=Empty, Jit=RyuJit, LargeAddressAware=Default, Platform=X64, PowerPlanMode=8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c, Runtime=.NET 10.0, AllowVeryLargeObjects=False, Concurrent=True, CpuGroups=False, Force=True, HeapAffinitizeMask=Default, HeapCount=Default, NoAffinitize=False, RetainVm=False, Server=False, Arguments=Default, BuildConfiguration=Default, Clock=Default, EngineFactory=Default, NuGetReferences=Default, Toolchain=Default, IsMutator=Default, InvocationCount=Default, IterationCount=Default, IterationTime=Default, LaunchCount=Default, MaxIterationCount=Default, MaxWarmupIterationCount=Default, MemoryRandomization=Default, MinIterationCount=Default, MinWarmupIterationCount=Default, RunStrategy=Default, UnrollFactor=16, WarmupCount=Default, PreserveOrder=True] | 1.22 s | 1.344 s | +10.16% | 824.25 MB | 824.25 MB | 0.00% |

## Final report order/no-order summary

| Metric | Value |
|---|---|
| Current ordered result | Mean: 1.344 s; Allocated: 824.25 MB |
| Current no-order result | Mean: 1.409 s; Allocated: 824.2 MB |
| Current no-order vs ordered difference | Mean: +4.84%; Allocated: -0.01% |
| Previous ordered result | Mean: 1.22 s; Allocated: 824.25 MB |
| Previous no-order result | Mean: 1.229 s; Allocated: 824.2 MB |
| Previous no-order vs ordered difference | Mean: +0.74%; Allocated: -0.01% |
| Ordered current vs previous difference | Mean: +10.16%; Allocated: 0.00% |
| No-order current vs previous difference | Mean: +14.65%; Allocated: 0.00% |
