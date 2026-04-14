# Version Policy

> Single source of versioning rules for all repository skill/policy files.

## Format

Version format: `X.Y.Z`.

## Version string rule

- Do not use the `v` prefix in version numbers (invalid: `v2.15.10.47`, valid: `2.15.10.47`).

## Source fields

- Primary project version source: `AssemblyVersion` (if this source is used by the project).
- If `Version`/`FileVersion` are present, they must be synchronized with `AssemblyVersion` according to repository rules.

## Usage rule

- `AGENTS.md`, `.codex/skills/*` must not duplicate detailed versioning rules.
- These files must reference `version_policy.md` as the single source of truth.
