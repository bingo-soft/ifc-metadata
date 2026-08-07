# Design

## Approach

Replace line-exact header section detection with token-based marker scanning:

- Read until `HEADER;` is found, regardless of physical line boundaries.
- Capture header content through the first non-string `ENDSEC;` marker.
- Preserve quoted string handling so semicolon-like text inside STEP strings does not end the header.
- Leave the shared `TextReader` positioned immediately after the header section so the existing entity scanner can consume `DATA;`.

This keeps the fix localized to `StepHeaderReader` and preserves the existing scanner and emitter contracts.

## Architecture Notes

The affected area is a compact CLI/library parser module. The fitting architecture lens is a utility-module lens: preserve API clarity, localized parser state, and existing module boundaries.

No dependency direction, storage model, event contract, runtime configuration, or JSON output boundary changes are introduced.

## Artifact Impact Matrix

- UL impact: `none`
- Architecture impact: `none`
- Event contract impact: `none`
- Docs impact: `none`
- Knowledge impact: `none`
- Changelog impact: `top_entry_required`

## Sync Reconciliation Notes

- Current branch: `fix`
- Sync mode: active branch reconciliation for manual/off-flow implementation.
- Package status: created from current branch reality after implementation checkpoint.
- Manual implementation provenance: developer-owned manual work adopted by Sync.
- Sync-adopted implementation commit: `e860d0a0d40c47da65f65e4b806a01cfb08633ec`
