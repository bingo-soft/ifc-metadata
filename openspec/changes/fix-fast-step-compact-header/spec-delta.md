# Spec Delta

## Fast-Step STEP Header Reading

### Fixed Behavior

Fast-step header reading must recognize the STEP `HEADER;` section marker even when it shares a physical line with following header statements.

### Acceptance Criteria

- Given a STEP/IFC file containing `HEADER;FILE_DESCRIPTION(...)`, header parsing reads `FILE_SCHEMA` normally.
- Given `ScanWithHeader` on a compact header file, entity scanning starts after the header `ENDSEC;` and indexes entities from the following `DATA;` section.
- Existing spaced `FILE_NAME (` and `FILE_SCHEMA (` header function support remains intact.
- Existing fast-step JSON output contract remains unchanged.
