# Changelog

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
