# IFC schema inventory (`ifc/` folder)

Инвентаризация выполнена командой:

```powershell
pwsh ./benchmarks/list-ifc-schemas.ps1
```

## Files

| IFC file | Detected schema |
|---|---|
| `01_26_Slavyanka_4.ifc` | `IFC2X3` |
| `024_値�-姃�2100.350_倞.ifc` | `IFC4` |
| `1.ifc` | `IFC2X3` |
| `1001--reinforcingBar--segfault.ifc` | `IFC2X3` |
| `1034醎�00_巶_�.ifc` | `IFC4` |
| `455--wall--infiniteLoop--augmented.ifc` | `IFC2X3` |
| `64551f74-80d4-4fb2-8dc8-a66581ec86fc.ifc` | `IFC2X3` |
| `剺� 撫忪-劗妾ī (1).ifc` | `IFC4X3_ADD2` |
| `憿瓲� ぅ €磬喈� 1802.ifc` | `IFC2X3` |
| `ea9541e6-7fb5-4d37-acae-7c5114d88612.ifc` | `IFC2X2_FINAL` |
| `fbf851df-840f-49e3-9e74-3900cab65d14.ifc` | `IFC2X3` |
| `fd920df9-bf02-419b-a5f6-c59d89ba60d8.ifc` | `IFC4` |
| `L13-401-戔喈ㄢカ灬猗甠唺-€�.ifc` | `IFC2X3` |
| `L13-446-埆歙獱颻78-€�.ifc` | `IFC2X3` |

## Schema counts

| Schema | File count |
|---|---:|
| `IFC2X2_FINAL` | 1 |
| `IFC2X3` | 9 |
| `IFC4` | 3 |
| `IFC4X3_ADD2` | 1 |

## Notes

- В консоли часть имен файлов отображается с искаженной кодировкой. На определение схем это не влияет.
- Для fast-path приоритетными остаются `IFC2X3` и `IFC4`; `IFC4X3_ADD2` уже присутствует в датасете и может быть добавлен в отдельный baseline-cycle.
