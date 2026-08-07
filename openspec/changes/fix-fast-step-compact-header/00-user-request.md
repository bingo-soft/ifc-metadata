# User Request

## Consolidated Request Formulation

The fast-step export path must handle valid IFC STEP files whose `HEADER;` marker shares a physical line with following header statements, such as `HEADER;FILE_DESCRIPTION(...)`. In the reported case, `StepHeaderReader` failed to recognize the compact header marker, consumed the rest of the reader while looking for a standalone `HEADER;` line, returned an empty schema, and left no `DATA;` content for `StepEntityScanner`, producing `Fast-step scan found 0 STEP entities`.

The intended behavior is for fast-step export to parse the compact header, leave the shared reader positioned after the header `ENDSEC;`, scan the following `DATA;` section normally, and emit JSON without falling back to xBIM. The scope is limited to STEP header section reading and regression coverage for scanner reader positioning; it does not change STEP entity syntax, JSON contract shape, CLI arguments, xBIM behavior, or output schema semantics.

Formulation confidence: `medium`.

Evidence:
- User-provided command failed with `Fast-step scan found 0 STEP entities`.
- Diagnostics showed `fast-step scan: header read complete schema=` followed by `entities=0`.
- The IFC file begins with compact header content: `HEADER;FILE_DESCRIPTION(...)`.
- Local validation after the fix exported the same IFC as `IFC4` with `2,525,230` STEP entities and `20,547` meta objects.

Assumptions and gaps:
- No external task tracker item was supplied.
- No configured task tracker MCP endpoint was available in this session, so exact lookup and recent-window discovery were skipped.
- The current branch is `fix`, treated as the active reconciliation branch for this narrow fix despite carrying no task number.

## Task Association

- Selected change kind: `fix`
- Selected primary task ref: `#0`
- Association source: `explicit-unknown`
- Association confidence: `medium`
- Exact lookup status: `not_applicable_no_nonzero_task_ref`
- Recent-window status: `skipped_provider_unavailable`
- Semantic fit status: `not_applicable`
- Title fit: `not_applicable`
- Description fit: `not_applicable`
- Requested outcome fit: `strong_local_request_and_diff_fit`
- Affected area fit: `strong_fast-step_header_reader`
- Mechanism/boundary fit: `strong_STEP_header_reader_and_scanner_positioning`
- Repository evidence fit: `strong`
- Conflict or mismatch notes: `none`
- Final decision: `explicit_no_task`
- Formulation confidence: `medium`
