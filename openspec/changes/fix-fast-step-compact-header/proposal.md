# Proposal

## Summary

Fix fast-step exports for valid STEP/IFC files whose `HEADER;` marker and header statements are written on the same physical line.

## Problem

`StepHeaderReader` previously read the header line-by-line and only entered the header section when a trimmed line was exactly `HEADER;`. Compact STEP files can legally write `HEADER;FILE_DESCRIPTION(...)` on the same line. For those files, the reader never recognized the header start, consumed the file to EOF, and left no content for entity scanning.

The resulting fast-step path reported an empty schema and zero STEP entities even though the `DATA;` section contained valid assignments.

## Scope

In scope:
- Token-based detection of `HEADER;` and header `ENDSEC;`.
- Preserve reader position so `StepEntityScanner.ScanWithHeader` can scan `DATA;`.
- Regression coverage for compact header parsing and scanner reader reuse.

Out of scope:
- JSON contract changes.
- CLI option changes.
- xBIM exporter behavior.
- Broad STEP lexer/entity parsing changes.
- Root changelog/shared documentation aggregation on this branch.

## Validation

- `dotnet test ifc-metadata.slnx` passed, 45 tests.
- `dotnet publish src\ifc-metadata.csproj -c Release -o src\bin\Release\net10.0\publish` completed.
- The reported fast-step command succeeded against `D:\data\ifc\1\XLDGDNrSZCMTdXakQfIDcAxCAUjgOuHf_0.ifc`.
