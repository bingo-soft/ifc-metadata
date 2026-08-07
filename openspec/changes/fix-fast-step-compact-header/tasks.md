# Tasks

## Implementation

- [x] Update `StepHeaderReader` to detect `HEADER;` token-wise instead of requiring a standalone trimmed line.
- [x] Stop header capture at the first non-string header `ENDSEC;` marker.
- [x] Add header-reader regression coverage for compact `HEADER;FILE_DESCRIPTION(...)` input.
- [x] Add scanner regression coverage proving `ScanWithHeader` leaves the reader positioned for `DATA;`.

## Validation

- [x] `dotnet test ifc-metadata.slnx` passed, 45 tests.
- [x] `dotnet publish src\ifc-metadata.csproj -c Release -o src\bin\Release\net10.0\publish` completed.
- [x] Reported fast-step export command succeeded with schema `IFC4`, `2,525,230` scanned STEP entities, and `20,547` emitted meta objects.

## Sync Adoption Evidence

- Sync-adopted implementation commit: `e860d0a0d40c47da65f65e4b806a01cfb08633ec`
- Implementation provenance: developer-owned manual work
- Agent Flow origin: `sync-governance-adoption`
- Workflow lifecycle: `manual-synced`
- Pre-Sync implementation patch fingerprint: `ef034f1c2114f0567ef0252da4380a5cdc08a824`
- Skipped paths: none
- Residual risk: low; compact header parsing is covered by unit tests and the reported IFC export was rerun successfully.

## Task Association Evidence

- Selected change kind: `fix`
- Selected primary task ref: `#0`
- Association source: `explicit-unknown`
- Association confidence: `medium`
- Exact lookup status: `not_applicable_no_nonzero_task_ref`
- Recent-window status: `skipped_provider_unavailable`
- Semantic fit status: `not_applicable`
- Final decision: `explicit_no_task`
- Formulation confidence: `medium`

## Lifecycle Hygiene Before Release

- [x] No stale merged OpenSpec lifecycle leftovers require cleanup before Release.

## Required Artifact Follow-Ups Before Release

- [ ] Release: update the root `CHANGELOG.md` top/current release entry from this fix because changelog impact is `top_entry_required`.
