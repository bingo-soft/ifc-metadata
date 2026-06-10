# Changelog Policy

> Single source of rules for creating and updating `CHANGELOG.md`.

## Scope

- File: `CHANGELOG.md`
- Format: **Keep a Changelog** (without the `Unreleased` section)

## Entry order

- Changelog history is maintained **top to bottom**.
- The newest version is added **at the top of the version list** (immediately after the `# Changelog` header).

## Version header format

Each version entry must use the following header format:

- `## [X.Y.Z.T] - YYYY-MM-DD HH:mm`

Where:
- `X.Y.Z.T` — version according to `version_policy.md`;
- `YYYY-MM-DD HH:mm` — entry creation date and time (repository local time or team-agreed time).

## Keep a Changelog sections

Allowed sections inside a version entry:
- `### Added`
- `### Changed`
- `### Fixed`
- `### Removed`
- `### Security`

Sections are added only when there are actual changes of the corresponding type.

## Mandatory summary rule

In every version entry, a high-level change summary must be added **as the first section**:

- секция: `### Summary`
- 1-3 bullet points describing the release intent at product/feature level.

Example:
- `### Summary`
- `- Improved NuGet packaging stability and metadata synchronization.`

## Noise filtering

Do not add trivial/technical-noise entries to `CHANGELOG.md`, for example:
- "updated changelog";
- "fixed formatting" (if it does not affect behavior/contract);
- "reordered lines/comments without functional impact".

Record only meaningful changes for users/developers of the library.

## Source of truth

- Content and structure rules: `changelog_policy.md`
- Version rules: `version_policy.md`
