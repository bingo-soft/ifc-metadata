# IFC4 fast-path parity (single file)

Date: 2026-04-10

| File | PreserveOrder | Fast SHA256 | Fallback SHA256 | Equal |
|---|---|---|---|---|
| `ifc/fd920df9-bf02-419b-a5f6-c59d89ba60d8.ifc` | `true` | `76DDCC8B20B61A4F5DD51375C88C15352A45CBC63BBD2D1075681B7FF720F36B` | `76DDCC8B20B61A4F5DD51375C88C15352A45CBC63BBD2D1075681B7FF720F36B` | `true` |

Notes:
- Fallback was forced via environment variable `IFC_FORCE_FALLBACK=1`.
- Outputs compared by SHA256 of generated JSON files.
