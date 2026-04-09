# Changelog

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
