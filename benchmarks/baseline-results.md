# Stage 0 baseline results

Документ заполняется после запуска baseline-цикла.

## Run configuration

- Build: `Release`
- Command mode: CLI (`src/ifc-metadata.csproj`)
- Preserve order baselines: `true` and `false`
- Verbosity: `detailed`
- Runs per dataset: `N = 1` (без warm-up)
- Warm-up runs: `1`
- Machine/OS: local workstation (not fixed in this cycle)
- CPU/RAM: not фиксировалось в этом цикле
- Date (preserve-order=true): `2026-04-10 13:10:05`
- Date (preserve-order=false): `2026-04-10 13:37:27`
- Stage0 summary artifact (true): `benchmarks/results/stage0/2026-04-10-130936/summary.md`
- Stage0 summary artifact (false): `benchmarks/results/stage0/2026-04-10-133659/summary.md`

## Aggregated metrics (preserve-order=true)

| Label | Schema | MetaObjects | Elapsed median (ms) | Elapsed p95 (ms) | Output size (bytes) | Managed delta median (bytes) | Working set median (bytes) | Peak working set median (bytes) | Golden JSON SHA256 |
|---|---|---:|---:|---:|---:|---:|---:|---:|---|
| small | IFC2X3 | 4 | 512.74 | 512.74 | 1256 | 40863007 | 102739476 | 102739476 | 1FAE26E3134CC61F2DCC6C923788171D908E9124D9E7B0DD7EE807364CCF16AB |
| medium | IFC2X3 | 220 | 2073.87 | 2073.87 | 111301 | 256985006 | 400629432 | 400629432 | 891B08E254A951DD1273905507E2C3BC4B90B91C925BD0828304A321365A6CAA |
| large | IFC2X3 | 2616 | 7139.35 | 7139.35 | 1299379 | 605479240 | 765512909 | 765512909 | 94DCE05F3C9EAA2AAAF26602F9D9A3694A77A05611B67D157D70F066C7E732AD |
| edge (optional) | not run |  |  |  |  |  |  |  |  |

## Aggregated metrics (preserve-order=false)

| Label | Schema | MetaObjects | Elapsed median (ms) | Elapsed p95 (ms) | Output size (bytes) | Managed delta median (bytes) | Working set median (bytes) | Peak working set median (bytes) | Golden JSON SHA256 |
|---|---|---:|---:|---:|---:|---:|---:|---:|---|
| small | IFC2X3 | 4 | 512.65 | 512.65 | 1256 | 40936407 | 102341018 | 102341018 | 1FAE26E3134CC61F2DCC6C923788171D908E9124D9E7B0DD7EE807364CCF16AB |
| medium | IFC2X3 | 220 | 1985.27 | 1985.27 | 111301 | 259449160 | 403691274 | 403691274 | A27F8E71E5190F9AAD2A45EFF27137BA54EDAF3C8177E367E1DBBF37B0F4DD39 |
| large | IFC2X3 | 2616 | 6975.64 | 6975.64 | 1299379 | 522599793 | 781472236 | 781472236 | 370F36C92E037EFF2A6C40568351DDC2E7A81F12091A46641594E13A5E1693C3 |
| edge (optional) | not run |  |  |  |  |  |  |  |  |

## Raw logs

Ссылки на сырые логи запусков:
- `benchmarks/results/stage0/2026-04-10-130936/logs/...` (preserve-order=true)
- `benchmarks/results/stage0/2026-04-10-133659/logs/...` (preserve-order=false)

## Golden outputs

Ссылки на эталонные JSON:
- `benchmarks/results/stage0/2026-04-10-130936/golden/*.json` (preserve-order=true)
- `benchmarks/results/stage0/2026-04-10-133659/golden/*.json` (preserve-order=false)

## Notes

- Все сравнения после оптимизаций A/B выполняются против этих baseline-значений и golden JSON.
- Если изменился входной IFC-файл (SHA256), baseline нужно переснять.
