# Stage 0 dataset manifest

Зафиксировано для текущего Stage 0.

## Dataset table

| Label | IFC Path | Schema | Category | IFC Size (bytes) | IFC SHA256 | Notes |
|---|---|---|---|---:|---|---|
| small | `ifc/455--wall--infiniteLoop--augmented.ifc` | `IFC2X3` | small | 1701860 | `C6ADDED2F899818443B27BFED664B6B9FBCB9448BE3E76728ECF5778CCC14AE1` | wall/infinite-loop augmented case |
| medium | `ifc/01_26_Slavyanka_4.ifc` | `IFC2X3` | medium | 35943115 | `C04D22CCB31EC3ABF7F12DBEC8CA0D2042D132E5238B66EF9F3200EACB41EF94` | default benchmark file |
| large | `ifc/fbf851df-840f-49e3-9e74-3900cab65d14.ifc` | `IFC2X3` | large | 89971601 | `8AC774168DC028ADA3266DF83078816A78483DE2FD73476BF45CDB110C4FEA18` | large production-like sample |
| edge (optional) | _not selected_ |  | edge |  |  | can be added in next baseline cycle |

## Category guidance

- small: ~1k–10k metaObjects
- medium: ~50k–200k metaObjects
- large: >500k metaObjects (или максимально доступный боевой файл)
- edge: нестандартные/грязные данные

## How to compute IFC SHA256 (PowerShell)

```powershell
Get-FileHash <path-to-ifc> -Algorithm SHA256
```
