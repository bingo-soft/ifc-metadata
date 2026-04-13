# ifc-metadata

CLI tool for extracting IFC model metadata into JSON.

## What the tool does

Input: IFC file (`.ifc`)

Output: JSON document with:
- model-level metadata (`id`, `projectId`, `author`, `schema`, etc.)
- `metaObjects` map where key is IFC `GlobalId`
- for each object: name, type, parent, property set ids, material id, type id

## Architecture

Single executable with engine routing and core functional blocks:

1. `Program` (`src/Program.cs`)
   - validates CLI arguments
   - resolves input/output files
   - handles process exit code
   - accepts optional `--preserve-order true|false` (default: `true`)
   - accepts optional `--engine xbim|fast-step` (default: `xbim`)
   - accepts optional `--verbosity` with modes `detailed|timing|none`
   - accepts optional `--progress [none|completed|remaining]` and prints integer progress to console (or disables it)
   - accepts optional output file tuning flags: `--output-buffer-kb N`, `--write-through|--no-write-through`



2. `IfcEngineRouter` + engines (`src/IfcEngineRouter.cs`, `src/IfcExportEngine.cs`)
   - routes export flow to `xbim` or `fast-step` engine;
   - for `--engine fast-step` uses fallback to `xbim` on schema read error / unsupported schema / fast-step runtime error.


3. `IfcStreamingJsonExporter` (`src/IfcStreamingJsonExporter.cs`)
   - opens IFC model through `Xbim.Ifc.IfcStore`
   - performs streaming extraction + serialization in one pipeline
   - does not build intermediate full `List<Metadata>` for CLI export
   - applies duplicate key handling with "last occurrence wins"

4. `FastStepJsonExporter` + `src/FastStep/*`
   - parses STEP entities in a lightweight scan/index pass
   - reads IFC header metadata from STEP header
   - emits JSON directly from fast-step indexes
   - preserves baseline contract semantics (including dedup/last occurrence wins)

5. `MetadataExtractor` (`src/MetadataExtractor.cs`)
   - keeps extraction model for non-streaming/internal scenarios

6. `IfcAccessors` (`src/IfcAccessors.cs`)
   - centralizes `type_id` / `material_id` / `GlobalId` / `EntityLabel` access
   - uses fast interface-based path first and cached delegate fallback for uncommon runtime shapes
   - exposes telemetry counters for benchmark analysis

7. `IfcJsonHelper` (`src/IfcJsonHelper.cs`)
   - writes JSON via `Utf8JsonWriter` from prepared metadata model
   - used in tests/benchmarks for serialization contract coverage



Data shape for one model object is defined by `Metadata` (`src/Metadata.cs`).

## Technology stack

- .NET 10 (`net10.0`)

- C#
- `Xbim.Essentials` (`6.0.587`) for IFC parsing

- `System.Text.Json` for serialization
- xUnit for unit tests


- BenchmarkDotNet for performance benchmark

## Build and run

### Prerequisites

- .NET SDK 10.0+



### Build

```bash
dotnet build ifc-metadata.slnx -c Release
```

### Run from source

```bash
dotnet run --project src/ifc-metadata.csproj -- ./path/to/source.ifc ./path/to/target.json
```

If output path is not passed, target defaults to source name with `.json` extension in the same directory.

Order option:
- `--preserve-order true` (default) — deterministic ordered traversal in output.
- `--preserve-order false` or `--no-preserve-order` — no order enforcement.

Engine option:
- `--engine xbim` (default) — existing exporter through xBIM object model.
- `--engine fast-step` — STEP-based scan + direct JSON emission path.
  - supported schema families for fast-step routing: `IFC2X2*`, `IFC2X3*`, `IFC4`, `IFC4X3*`;
  - otherwise export falls back to `xbim`.


Verbosity option:
- `--verbosity` (or `--verbosity detailed|summary`) — print post-run execution report to console (includes accessor telemetry and engine execution diagnostics: requested/effective engine, fast-step attempts/success, fallback count/reason, detected schema).

- `--verbosity timing` (or `--verbosity time`) — print only elapsed execution time; accessor telemetry is disabled.
- `--verbosity none` — disable report output.

Progress option:
- `--progress completed` (default) — show completed percentage.
- `--progress remaining` — show remaining percentage.
- `--progress none` (or `--progress off`) — disable progress output.

Output file write options:
- `--output-buffer-kb N` — set output stream buffer size in KB (default: `512`).
- `--write-through` — enable `FileOptions.WriteThrough` for output stream.
- `--no-write-through` — explicitly disable write-through (default behavior).



```bash
dotnet run --project src/ifc-metadata.csproj -- ./path/to/source.ifc
```

## Publish

Self-contained single-file publish with maximum size reduction (trim + compression):

```bash
dotnet publish ./src/ifc-metadata.csproj -c Release -r win-x64

dotnet publish ./src/ifc-metadata.csproj -c Release -r linux-x64
```

Trimmer stabilization checklist:
- `docs/trimmer-checklist.md`


Published executable names:
- Windows: `ifc-metadata-v2.exe`
- Linux: `ifc-metadata-v2`


## CLI contract

```bash
ifc-metadata <source.ifc> [target.json] [--preserve-order true|false] [--engine xbim|fast-step] [--verbosity [summary|detailed|timing|none]] [--progress [none|completed|remaining]] [--output-buffer-kb N] [--write-through|--no-write-through]


```

Exit codes:
- `0` — success
- `1` — invalid arguments, missing input file, or processing error

## JSON output schema

`metaObjects` is an object/map, not an array.

```json
{
  "id": "301-16-17-37",
  "projectId": "3ir4vYruPFgwIKoil6nQwl",
  "author": "",
  "createdAt": "2022-09-07T11:00:15+03:00",
  "schema": "IFC2X3",
  "creatingApplication": "23.0.20.21 - Exporter 23.1.0.0 - Alternate UI 23.1.0.0",
  "metaObjects": {
    "3ir4vYruPFgwIKoil6nQwl": {
      "id": "3ir4vYruPFgwIKoil6nQwl",
      "name": "301-16-17-37",
      "type": "IfcProject",
      "parent": null,
      "properties": null,
      "material_id": null,
      "type_id": null
    }
  }
}
```

Fields of each object in `metaObjects`:
- `id: string`
- `name: string | null`
- `type: string`
- `parent: string | null`
- `properties: string[] | null`
- `material_id: string | null`
- `type_id: string | null`

## Tests

Test project: `tests/ifc-metadata.Tests.csproj`

Run all tests:

```bash
dotnet test ifc-metadata.slnx -c Release
```

Current test coverage includes JSON serialization contract checks for `IfcJsonHelper`:
- root fields
- `metaObjects` map structure
- `properties` null/array behavior

## Benchmark

Benchmark project: `benchmarks/ifc-metadata.Benchmarks.csproj`

Run benchmark (default overall mode):

```bash
dotnet run --project benchmarks/ifc-metadata.Benchmarks.csproj -c Release
```

Run diagnostic benchmarks on demand:

```bash
dotnet run --project benchmarks/ifc-metadata.Benchmarks.csproj -c Release -- --detailed
```

Default benchmark contains only overall method in `IfcFilePipelineBenchmark`:
- `EndToEnd_Extract_And_Serialize` with `[Params(PreserveOrder=true|false)]`
- GC matrix jobs: `WS_Concurrent`, `WS_NonConcurrent`, `Server_Concurrent`


Diagnostic benchmarks are available only on explicit request (`--detailed`):
- `Extract_Only`
- `Serialize_Only_From_Extracted_Metadata`
- `EndToEnd_Extract_And_Serialize` (detailed class)

Model path resolution order:
1. `IFC_BENCHMARK_FILE` environment variable
2. `./ifc/01_26_Slavyanka_4.ifc`
3. `./ifc/sample.ifc`
4. `sample.ifc` near benchmark executable

Rule:
- after test run, benchmark report must be generated;
- before commit, the current `benchmarks/results/latest/*` must be refreshed by this cycle and treated as pre-commit baseline;
- next local run is compared with this baseline (`benchmarks/results/previous/*`);
- formalized in `benchmarks/benchmark_policy.md`.



Command for regular cycle (tests -> benchmarks -> report):

```bash
pwsh ./benchmarks/run-baseline.ps1 -IfcFilePath "ifc/01_26_Slavyanka_4.ifc"
```

What script does automatically:
- runs `dotnet test ifc-metadata.slnx -c Release`;
- runs only overall benchmark by default for `PreserveOrder=true|false` and GC jobs `WS_Concurrent` / `WS_NonConcurrent` / `Server_Concurrent`;
- saves timestamped CSV/MD snapshots to `benchmarks/results`;
- updates rolling snapshot in `benchmarks/results/latest`;
- keeps previous rolling snapshot in `benchmarks/results/previous`;
- creates run-to-run comparison report `benchmarks/results/latest/IfcFilePipelineBenchmark-comparison.md`;
- creates GC mode comparison report `benchmarks/results/latest/IfcFilePipelineBenchmark-gc-comparison.md` (deltas vs `WS_Concurrent`);
- compares with pre-commit baseline from previous local cycle (`benchmarks/results/previous/*`); if unavailable, compares with previous commit (`HEAD~1`) when report exists there;
- formats time and memory in comparison report by magnitude (`μs/ms/s`, `KB/MB/GB`);
- for commit-to-commit comparison, keep `benchmarks/results/latest/*` committed after each run.

Fast-step parity batch command (xbim vs fast-step contract check on multiple IFC files):

```bash
pwsh ./benchmarks/run-fast-step-parity.ps1 -IfcDirectory "ifc" -OutputReportPath "benchmarks/results/latest/fast-step-parity-report.md"
```






## Repository structure

```text
.
├── src/
│   ├── Program.cs
│   ├── IfcStreamingJsonExporter.cs
│   ├── MetadataExtractor.cs
│   ├── IfcAccessors.cs
│   ├── IfcJsonHelper.cs
│   ├── Metadata.cs
│   └── ifc-metadata.csproj
├── tests/
│   ├── IfcJsonHelperTests.cs
│   └── ifc-metadata.Tests.csproj
├── benchmarks/
│   ├── Program.cs
│   ├── IfcBenchmarkSettings.cs
│   ├── IfcFilePipelineBenchmark.cs
│   ├── IfcFilePipelineDetailedBenchmark.cs
│   ├── run-baseline.ps1
│   ├── run-fast-step-parity.ps1
│   ├── results/
│   │   ├── latest/
│   │   └── previous/
│   └── ifc-metadata.Benchmarks.csproj
└── ifc-metadata.slnx
```

## Known limitations

- No strict JSON schema file in repository (only documented contract).
- Processing errors are returned as generic process error (`exit 1`).
- Accessor fallback still uses runtime delegate access for uncommon IFC type shapes, so telemetry-guided tuning may be needed for hot models.

