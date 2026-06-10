# ifc-metadata

CLI tool for exporting IFC metadata from `.ifc` into JSON.
Current project version: `1.25.0`.

## Purpose

Input:
- IFC file (`.ifc`).

Output:
- root metadata: `id`, `projectId`, `author`, `createdAt`, `schema`, `creatingApplication`;
- `metaObjects` as map (`GlobalId -> object`);
- each object contains: `id`, `name`, `type`, `parent`, `properties`, `material_id`, `type_id`.

## Quick start

Build:

```bash
dotnet build ifc-metadata.slnx -c Release
```

Run:

```bash
dotnet run --project src/ifc-metadata.csproj -- ./path/to/source.ifc ./path/to/target.json
```

If `target.json` is omitted, the file is created next to the input IFC with the name `source.json`.

## CLI

```bash
ifc-metadata <source.ifc> [target.json] [--preserve-order true|false] [--no-preserve-order] [--engine xbim|fast-step] [--verbosity [summary|detailed|timing|none]] [--progress [none|completed|remaining]] [--output-buffer-kb N] [--write-through|--no-write-through]
```

### Options

- `--engine xbim|fast-step`
  - default: `xbim`;
  - for `fast-step`, router falls back to `xbim` on schema-read failure, unsupported schema, or fast-step runtime failure;
  - current fast-step schema families: `IFC2X2*`, `IFC2X3*`, `IFC4`, `IFC4X3*`.

- `--preserve-order true|false` and `--no-preserve-order`
  - default: `true`.

- `--verbosity [summary|detailed|timing|none]`
  - default: `none`;
  - `summary|detailed|normal|verbose` => full execution report + accessor telemetry;
  - `timing|time` => elapsed time only;
  - `quiet|none` => no post-run report.

- `--progress [none|completed|remaining]`
  - default: `completed`;
  - alias: `off` = `none`.

- `--output-buffer-kb N`
  - default: `512` KB.

- `--write-through` / `--no-write-through`
  - default: `--no-write-through`.

### Exit codes

- `0` - success.
- `1` - argument error, missing input file, or runtime error.

## Architecture

1. `Program` (`src/Program.cs`)
   - CLI parsing;
   - execution/reporting/progress;
   - exit codes.

2. `IfcEngineRouter` (`src/IfcEngineRouter.cs`)
   - engine selection: `xbim` or `fast-step`;
   - `fast-step` fallback to `xbim`;
   - diagnostics: requested/effective engine and fallback reason.

3. `IfcStreamingJsonExporter` (`src/IfcStreamingJsonExporter.cs`)
   - baseline xBIM exporter;
   - streaming JSON writer;
   - dedup rule: `last occurrence wins`.

4. `FastStepJsonExporter` + `src/FastStep/*`
   - STEP scan/index + direct JSON emit;
   - parity with baseline contract.

5. `IfcAccessors` (`src/IfcAccessors.cs`)
   - fast accessors for `type_id`, `material_id`, `GlobalId`, `EntityLabel`;
   - fast/fallback telemetry.

## Publish

Single-file self-contained publish:

```bash
dotnet publish ./src/ifc-metadata.csproj -c Release -r win-x64
dotnet publish ./src/ifc-metadata.csproj -c Release -r linux-x64
```

Artifacts:
- Windows: `ifc-metadata-v2.exe`
- Linux: `ifc-metadata-v2`

## Tests

```bash
dotnet test ifc-metadata.slnx -c Release
```

## Benchmarks

Normal mode:

```bash
dotnet run --project benchmarks/ifc-metadata.Benchmarks.csproj -c Release
```

Detailed mode:

```bash
dotnet run --project benchmarks/ifc-metadata.Benchmarks.csproj -c Release -- --detailed
```

Baseline cycle:

```bash
pwsh ./benchmarks/run-baseline.ps1 -IfcFilePath "ifc/01_26_Slavyanka_4.ifc"
```

## Limitations

- No formal JSON Schema file (contract is documented in markdown).
- All runtime failures currently map to `exit 1`.
- Rare runtime types can still hit accessor fallback paths; monitor through telemetry/benchmarks.

## Documentation index

- release evolution: `docs/changes-since-1.0.0.md`
- JSON contract baseline: `docs/json-contract-baseline.md`
- repository publication scope: `docs/repository-publication-scope.md`
- reusable project prompt: `docs/reuse-prompt-for-similar-project.md`
- benchmark process policy: `benchmarks/benchmark_policy.md`
