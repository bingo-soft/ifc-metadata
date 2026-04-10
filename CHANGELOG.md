# Changelog

## [1.12.0] - 2026-04-10 12:13

### Summary
- Added CLI progress output mode control (`completed` / `remaining`) with integer percentage updates during streaming export.
- Updated executable artifact naming for publish outputs to `ifc-metadata-v2`.
- Documented production self-contained publish profile settings for `win-x64` and `linux-x64` with TieredPGO + ReadyToRun (+ composite enabled).

### Changed
- Updated `src/Program.cs`:
  - added argument parsing for `--progress [completed|remaining]`;
  - added progress reporter wiring for export lifecycle and usage/help text updates.
- Updated `src/IfcStreamingJsonExporter.cs`:
  - extended `Export(...)` signature with optional `Action<int,int>` progress callback;
  - added progress callback notifications for initial and per-metaobject states.
- Updated `src/ifc-metadata.csproj`:
  - set `<AssemblyName>ifc-metadata-v2</AssemblyName>`;
  - bumped version fields to `1.12.0`.
- Updated `README.md`:
  - documented `--progress` CLI option;
  - documented production self-contained publish command set and output executable names.

## [1.11.0] - 2026-04-10 00:39

### Summary
- Optimized streaming export by buffering traversal nodes and removing repeated IFC hierarchy traversal in `Export`.
- Reduced ordered-traversal allocation pressure by replacing per-node temporary `List<T>` collections with `ArrayPool<T>` buffers.
- Refreshed benchmark baseline artifacts after optimization run; latest cycle shows end-to-end acceleration across all tested GC/order modes.

### Changed
- Updated `src/IfcStreamingJsonExporter.cs`:
  - `Export` now buffers traversal nodes during object-id counting and reuses buffered nodes for JSON write phase;
  - added cached `ObjectId` in traversal node to avoid repeated `GlobalId` access in hot path;
  - replaced ordered child collection via `new List<IIfcObjectDefinition>()` with pooled arrays (`ArrayPool<IIfcObjectDefinition>`) and in-place sorting.
- Updated benchmark artifacts:
  - refreshed `benchmarks/results/latest/*` and `benchmarks/results/previous/*` benchmark snapshots;
  - added stamped snapshots `benchmarks/results/2026-04-10-003853-*`.

## [1.10.0] - 2026-04-10 00:06

### Summary
- Implemented GC A/B benchmark matrix for `Server/Workstation` and `Concurrent/Non-concurrent` modes in the default pipeline benchmark cycle.
- Extended baseline script reporting with GC-mode comparison report and stable case matching by `Method + PreserveOrder + Job`.
- Refreshed benchmark artifacts and pruned old stamped `IfcFilePipelineBenchmark-*` snapshots, keeping only the latest stamped set.

### Changed
- Updated `benchmarks/IfcFilePipelineBenchmark.cs`:
  - added explicit BenchmarkDotNet jobs: `WS_Concurrent`, `WS_NonConcurrent`, `Server_Concurrent`;
  - configured GC modes per job via `WithGcServer(...)` and `WithGcConcurrent(...)`.
- Updated `benchmarks/run-baseline.ps1`:
  - comparison keys now include `Job` to support GC matrix rows;
  - added `benchmarks/results/latest/IfcFilePipelineBenchmark-gc-comparison.md` generation;
  - order/no-order final summary is pinned to baseline job `WS_Concurrent`;
  - comparison source resolution now prefers `previous local run`, then falls back to `HEAD~1`.
- Updated documentation:
  - `benchmarks/benchmark_policy.md` with mandatory GC matrix/report rules;
  - `README.md` benchmark section with GC jobs and GC comparison report path.
- Updated benchmark artifacts:
  - refreshed `benchmarks/results/latest/*` and `benchmarks/results/previous/*`;
  - added stamped set `benchmarks/results/2026-04-10-000621-*`;
  - removed older stamped `benchmarks/results/*-IfcFilePipelineBenchmark-*` snapshots.

## [1.9.0] - 2026-04-09 23:34

### Summary
- Optimized IFC extraction hot paths in accessors and streaming traversal to reduce fallback overhead and transient allocations.
- Updated benchmark flow to run only baseline comparison for execution time and memory with telemetry disabled.
- Refreshed benchmark snapshots and comparison reports for the current baseline cycle.

### Changed
- Updated `src/IfcAccessors.cs`:
  - added runtime type-name cache and material id string creation via `string.Create`;
  - reduced delegate invocation overhead (`?.Invoke`) and converted recursive GlobalId extraction path to iterative loop;
  - added telemetry enable/disable switch for low-overhead runs.
- Updated `src/IfcStreamingJsonExporter.cs` and `src/MetadataExtractor.cs`:
  - reused cached runtime type names;
  - reduced intermediate allocations in hierarchy traversal when `PreserveOrder=false`;
  - removed repeated `GlobalId` conversions in writer path.
- Updated benchmark tooling:
  - `benchmarks/IfcFilePipelineBenchmark.cs` now runs with `TelemetryEnabled=false` only and keeps `MemoryDiagnoser` for time/memory baseline;
  - `benchmarks/run-baseline.ps1` generates comparison by time/memory without telemetry artifact processing;
  - refreshed `benchmarks/results/latest/*`, `benchmarks/results/previous/*`, and added stamped snapshots (`2026-04-09-233445-*`).
- Updated `README.md` benchmark workflow section to match telemetry-disabled baseline reporting.

## [1.8.0] - 2026-04-09 22:21

### Summary
- Added configurable output stream write settings in CLI for buffer sizing and write-through mode.
- Applied explicit output `FileStreamOptions` in streaming exporter for controlled create/write behavior.
- Updated documentation for new output write tuning options and defaults.

### Changed
- Updated `src/Program.cs`:
  - added CLI options `--output-buffer-kb N`, `--write-through`, `--no-write-through`;
  - passed output stream settings into exporter;
  - extended execution report and usage text with output stream configuration.
- Updated `src/IfcStreamingJsonExporter.cs`:
  - changed `Export(...)` signature to accept `outputFileBufferSize` and `writeThrough`;
  - replaced implicit file creation with explicit `FileStreamOptions` (`FileMode.Create`, `FileAccess.Write`, `FileShare.None`, configurable `BufferSize`, `WriteThrough/SequentialScan` selection);
  - set default output buffer to `512 KB` based on local measurement.
- Updated `README.md` with output file write options and updated CLI contract.

## [1.7.0] - 2026-04-09 21:51

### Summary
- Added CLI post-run verbosity report with execution, memory, output, and accessor telemetry metrics.
- Hardened `type_id` extraction correctness for IFC2x3 typed relations and covered it with dedicated tests.
- Updated benchmark snapshots and telemetry artifacts after the accessor/reporting changes.

### Added
- Added `tests/IfcAccessorsTypedIdTests.cs` with IFC2x3 assertions for typed-id resolution and known non-typed hot types.

### Changed
- Updated `src/Program.cs` to support `--verbosity [summary|detailed|none]` and print a post-export execution report.
- Updated `src/IfcStreamingJsonExporter.cs` to return `IfcExportReport` (`SchemaVersion`, `MetaObjectCount`) for CLI reporting.
- Updated `README.md` with the new verbosity option and report behavior.
- Refreshed benchmark artifacts in `benchmarks/results/latest`, `benchmarks/results/previous`, and added stamped snapshots (`2026-04-09-213617-*`).

### Fixed
- Fixed `TryExtractGlobalId` precedence in `src/IfcAccessors.cs` so `IIfcRelDefinesByType` resolves to `RelatingType.GlobalId` before generic `IIfcRoot` fallback.

## [1.6.0] - 2026-04-09 18:47

### Summary
- Implemented typed-id strategy cache in `IfcAccessors` to minimize fallback overhead on hot IFC2x3 runtime types.
- Added explicit fast-path handling for common telemetry candidates and fast-null handling for known non-typed objects.
- Refreshed benchmark/telemetry snapshots after optimization run.

### Changed
- Updated `src/IfcAccessors.cs`:
  - introduced `TypedIdStrategy` and per-type cache (`ConcurrentDictionary<Type, TypedIdStrategy>`);
  - added fast-null strategy for `IfcProject`, `IfcBuilding`, `IfcSite`, `IfcBuildingStorey`;
  - added direct typed-by fast path for `IfcRoof`, `IfcRailing`, `IfcStair`;
  - moved typed-id fallback into dedicated method and execute it only for `FallbackDelegate` strategy.
- Updated benchmark artifacts in `benchmarks/results/latest`, `benchmarks/results/previous`, and added stamped snapshots (`2026-04-09-184552-*`).

### Fixed
- Reduced `TypedId` fallback rate to `0.00%` for both `PreserveOrder=False` and `PreserveOrder=True` in latest telemetry reports.

## [1.5.0] - 2026-04-09 18:25

### Summary
- Introduced a centralized hybrid accessor layer for IFC hot paths with fast interface-based extraction and delegate-cached fallback.
- Added runtime telemetry for accessor fast/fallback hit tracking and fallback type distribution.
- Extended benchmark baseline workflow to export and persist accessor telemetry artifacts together with reports.

### Added
- Added `src/IfcAccessors.cs`:
  - centralized APIs: `GetTypedId`, `GetMaterialId`, `TryGetEntityLabel`, `TryExtractGlobalId`;
  - telemetry snapshot/reset API for benchmarks;
  - concurrent delegate cache for fallback property access.
- Added benchmark telemetry artifacts:
  - `benchmarks/results/latest/IfcFilePipelineBenchmark-accessor-telemetry-PreserveOrder-True.md`
  - `benchmarks/results/latest/IfcFilePipelineBenchmark-accessor-telemetry-PreserveOrder-False.md`
  - stamped telemetry snapshots in `benchmarks/results/2026-04-09-181524-*`.

### Changed
- Updated `src/IfcStreamingJsonExporter.cs` and `src/MetadataExtractor.cs` to use `IfcAccessors` instead of local reflection helper methods for `type_id` and `material_id` extraction.
- Updated `benchmarks/IfcFilePipelineBenchmark.cs` to reset accessor telemetry in setup and emit per-parameter telemetry markdown in cleanup.
- Updated `benchmarks/run-baseline.ps1` to:
  - set `IFC_BENCHMARK_TELEMETRY_DIR` for benchmark process;
  - copy telemetry artifacts into `latest`, rotate to `previous`, and store stamped snapshots;
  - include telemetry artifact list in the generated comparison report.
- Updated `src/ifc-metadata.csproj` to exclude `ifc-metadata.Generators/**/*.cs` from main project compile.
- Updated benchmark snapshots in `benchmarks/results/latest`, `benchmarks/results/previous`, and added stamped reports (`2026-04-09-181524-*`).

## [1.4.0] - 2026-04-09 16:27

### Summary
- Optimized extraction/serialization hot paths by removing LINQ-heavy traversals and intermediate allocations in streaming and extractor flows.
- Hardened benchmark baseline parsing for locale-specific and grouped numeric formats.
- Updated benchmark workflow policy to enforce pre-commit baseline refresh and comparison against it.

### Changed
- Updated `src/IfcStreamingJsonExporter.cs`:
  - replaced `SelectMany`/`OrderBy`/`ToArray` traversal logic with explicit loops and list sorting only when ordering is enabled;
  - removed intermediate `Metadata` object creation in write path;
  - switched properties emission to direct streaming write without temporary `string[]`.
- Updated `src/MetadataExtractor.cs`:
  - replaced LINQ in hierarchy/property/material extraction with explicit loops;
  - removed `AddRange`-based recursive accumulation in favor of in-place list fill;
  - optimized author join loop and project lookup without LINQ.
- Updated `benchmarks/run-baseline.ps1` to parse benchmark time/memory values robustly across grouped separators, locale variants, and `μs/µs` units.
- Updated `benchmarks/benchmark_policy.md` and `README.md` with pre-commit baseline rule (`tests -> benchmarks -> report`, commit refreshed `benchmarks/results/latest/*`).
- Updated benchmark snapshots in `benchmarks/results/latest`, `benchmarks/results/previous`, and added stamped report `2026-04-09-162651-*`.

### Fixed
- Fixed baseline report generation failures on benchmark values like `1,341,927.7 μs`.

## [1.3.0] - 2026-04-09 15:19

### Summary
- Switched CLI export path to a streaming IFC→JSON pipeline with configurable deterministic ordering.
- Reworked benchmark strategy to default on overall E2E measurement with order/no-order parameter comparison.
- Extended baseline reporting with mandatory final order/no-order summary metrics and improved unit parsing.

### Added
- Added `src/IfcStreamingJsonExporter.cs` for single-pass hierarchy traversal and direct JSON writing without building a full metadata list.
- Added `benchmarks/IfcFilePipelineDetailedBenchmark.cs` for diagnostic stage-level benchmarks available only on explicit `--detailed` request.

### Changed
- Updated `src/Program.cs` to support `--preserve-order true|false` / `--no-preserve-order` and use streaming export by default.
- Updated `benchmarks/IfcFilePipelineBenchmark.cs` to run only overall method with `[Params(true,false)]` for `PreserveOrder`.
- Updated `benchmarks/Program.cs` to select benchmark mode (`overall` by default, `--detailed` on demand).
- Updated `benchmarks/run-baseline.ps1` to:
  - parse time/memory values across `μs|ms|s` and `KB|MB|GB` formats;
  - keep parameter-aware method keys for comparison;
  - include required final order/no-order summary lines.
- Updated `benchmarks/benchmark_policy.md` and `README.md` to reflect benchmark execution scope and reporting requirements.
- Updated benchmark result snapshots in `benchmarks/results/latest`, `benchmarks/results/previous`, and new timestamped reports.

### Fixed
- Fixed baseline report generation failure when BenchmarkDotNet emits values in seconds (`s`) and megabytes (`MB`).

## [1.2.0] - 2026-04-09 13:57

### Summary
- Replaced interactive benchmark selection with deterministic pipeline benchmarks for IFC extraction/serialization baseline.
- Added automated baseline workflow that runs tests, stores benchmark artifacts, and generates history-based comparison reports.
- Formalized benchmark execution/reporting policy and aligned repository docs/process for reproducible tracking.

### Added
- Added `benchmarks/IfcBenchmarkSettings.cs` for IFC source file resolution with fallback order and explicit missing-file diagnostics.
- Added `benchmarks/IfcFilePipelineBenchmark.cs` with fixed stage methods: `Extract_Only`, `Serialize_Only_From_Extracted_Metadata`, `EndToEnd_Extract_And_Serialize`.
- Added `benchmarks/run-baseline.ps1` for tests+benchmarks orchestration, artifact snapshots (`latest`/`previous`), and comparison report generation.
- Added `benchmarks/benchmark_policy.md` with mandatory post-test benchmark reporting rules.

### Changed
- Updated `benchmarks/Program.cs` to run benchmark class directly via `BenchmarkRunner.Run<IfcFilePipelineBenchmark>()`.
- Updated `README.md` benchmark section for fixed methods, baseline command, comparison sources, and magnitude-based formatting rules.
- Updated `.gitignore` to ignore local IFC input files under `ifc/`.

### Removed
- Removed `benchmarks/IfcJsonHelperBenchmark.cs`.

## [1.1.1] - 2026-04-09 12:36

### Summary
- Fixed Visual Studio solution/project configuration mapping mismatch for the main project.
- Aligned project platform declarations to prevent `unknown project configuration mappings` errors when opening `.slnx`.

### Fixed
- Updated `src/ifc-metadata.csproj` platform and configuration conditions to support solution mappings consistently (`AnyCPU` and `x64`).

## [1.1.0] - 2026-04-09 12:26

### Summary
- Migrated the project stack to .NET 10 and aligned development artifacts to current tooling.
- Added automated tests and a benchmark project for JSON serialization behavior and performance.
- Standardized release documentation by introducing an English changelog and policy-driven structure.

### Added
- Added `tests/ifc-metadata.Tests.csproj` with xUnit tests for `IfcJsonHelper` JSON contract behavior.
- Added `benchmarks/ifc-metadata.Benchmarks.csproj` with BenchmarkDotNet benchmark for serialization throughput.
- Added `CHANGELOG.md` and release policy documents (`changelog_policy.md`, `version_policy.md`).

### Changed
- Migrated all projects (`src`, `tests`, `benchmarks`) to `net10.0`.
- Switched solution format to `ifc-metadata.slnx` and removed legacy `.sln` from active use.
- Updated `README.md` with architecture, API contract, stack, test/benchmark usage, and current solution commands.
- Updated package versions (`Xbim.Essentials`, `BenchmarkDotNet`, test SDK/tooling) to versions compatible with current setup.

## [1.0.0] - 2024-08-13 09:15

### Summary
- Reworked JSON output generation by introducing streaming serialization via `Utf8JsonWriter`.
- Simplified the export flow and updated user-facing documentation.

### Added
- Added `IfcJsonHelper` to centralize JSON writing logic.

### Changed
- Moved JSON creation logic from the main execution flow into a dedicated helper component.
- Updated `Program`, `MetadataExtractor`, and project settings to align with the new serialization pipeline.
- Updated `README.md`.

## [0.0.4] - 2024-08-02 12:13

### Summary
- Fixed IFC project type conversion to ensure correct processing of the model root element.

### Fixed
- Fixed incorrect conversion of `IIfcProject` type.

## [0.0.3] - 2024-08-02 10:14

### Summary
- Improved metadata extraction stability for models with incomplete PropertySet data.

### Fixed
- Fixed a `NullReferenceException` when a property is missing in a `PropertySet`.

## [0.0.2] - 2024-07-31 16:37

### Summary
- Simplified runtime targeting by moving to a single target framework.

### Changed
- Removed multi-targeting.
- Kept only `.NET 7` as the supported target.

## [0.0.1] - 2024-07-31 15:33

### Summary
- Introduced the initial CLI release for IFC metadata extraction.
- Established core domain models and the base JSON conversion pipeline.

### Added
- Added the initial project structure and CLI implementation.
- Added the `Metadata` model.
- Added compatibility with `netcore2.0` and `.NET 7` (at the time of release 0.0.1).

### Changed
- Replaced `Newtonsoft.Json` with `System.Text.Json`.
- Removed `dotnetstandard2.0` target.
