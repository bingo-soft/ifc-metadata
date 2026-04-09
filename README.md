# ifc-metadata

CLI tool for extracting IFC model metadata into JSON.

## What the tool does

Input: IFC file (`.ifc`)

Output: JSON document with:
- model-level metadata (`id`, `projectId`, `author`, `schema`, etc.)
- `metaObjects` map where key is IFC `GlobalId`
- for each object: name, type, parent, property set ids, material id, type id

## Architecture

Single executable with 3 functional blocks:

1. `Program` (`src/Program.cs`)
   - validates CLI arguments
   - resolves input/output files
   - handles process exit code

2. `MetadataExtractor` (`src/MetadataExtractor.cs`)
   - opens IFC model through `Xbim.Ifc.IfcStore`
   - extracts project metadata from IFC header
   - traverses model hierarchy recursively
   - extracts typed id, material link, property set ids

3. `IfcJsonHelper` (`src/IfcJsonHelper.cs`)
   - writes JSON via `Utf8JsonWriter`
   - serializes root fields and `metaObjects`

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

```bash
dotnet run --project src/ifc-metadata.csproj -- ./path/to/source.ifc
```

## Publish

Example for Linux x64:

```bash
dotnet publish ./src/ifc-metadata.csproj -c Release -r linux-x64 --self-contained false -o ./artifacts/ifc-metadata-linux-x64
```

## CLI contract

```bash
ifc-metadata <source.ifc> [target.json]
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

Run benchmark:

```bash
dotnet run --project benchmarks/ifc-metadata.Benchmarks.csproj -c Release
```

Benchmark contains 3 separate methods in `IfcFilePipelineBenchmark`:
- `Extract_Only`
- `Serialize_Only_From_Extracted_Metadata`
- `EndToEnd_Extract_And_Serialize`

Model path resolution order:
1. `IFC_BENCHMARK_FILE` environment variable
2. `./ifc/01_26_Slavyanka_4.ifc`
3. `./ifc/sample.ifc`
4. `sample.ifc` near benchmark executable

Rule:
- after test run, benchmark report must be generated;
- formalized in `benchmarks/benchmark_policy.md`.


Command for regular cycle (tests -> benchmarks -> report):

```bash
pwsh ./benchmarks/run-baseline.ps1 -IfcFilePath "ifc/01_26_Slavyanka_4.ifc"
```

What script does automatically:
- runs `dotnet test ifc-metadata.slnx -c Release`;
- runs 3 benchmark methods;
- saves timestamped CSV/MD snapshots to `benchmarks/results`;
- updates rolling snapshot in `benchmarks/results/latest`;
- keeps previous rolling snapshot in `benchmarks/results/previous`;
- creates comparison report `benchmarks/results/latest/IfcFilePipelineBenchmark-comparison.md`;
- compares with previous commit (`HEAD~1`) when previous report exists there; otherwise compares with previous local run;
- formats time and memory in comparison report by magnitude (`μs/ms/s`, `KB/MB/GB`);
- for commit-to-commit comparison, keep `benchmarks/results/latest/*` committed after each run.



## Repository structure

```text
.
├── src/
│   ├── Program.cs
│   ├── MetadataExtractor.cs
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
│   ├── run-baseline.ps1
│   ├── results/
│   │   ├── latest/
│   │   └── previous/
│   └── ifc-metadata.Benchmarks.csproj
└── ifc-metadata.slnx
```

## Known limitations

- No strict JSON schema file in repository (only documented contract).
- Processing errors are returned as generic process error (`exit 1`).
- Reflection is used for some IFC links (`IsTypedBy`, `Material`), which depends on model/runtime type shape.

