# Stage 0 procedure: baseline + contract lock

## 1) Contract lock

- Контракт JSON зафиксирован в: `docs/json-contract-baseline.md`.
- Перед этапами A/B контракт не менять.

## 2) Dataset lock

1. Заполнить `benchmarks/dataset-manifest.md`.
2. Для каждого IFC-файла зафиксировать SHA256.
3. Подтвердить категории: `small`, `medium`, `large` (и `edge` опционально).

## 3) Baseline run

Запуск автоматизированного скрипта:

```powershell
pwsh -Command "./benchmarks/run-stage0-baseline.ps1 -Dataset @('small|ifc/455--wall--infiniteLoop--augmented.ifc','medium|ifc/01_26_Slavyanka_4.ifc','large|ifc/fbf851df-840f-49e3-9e74-3900cab65d14.ifc') -MeasuredRuns 1 -PreserveOrder $true"
pwsh -Command "./benchmarks/run-stage0-baseline.ps1 -Dataset @('small|ifc/455--wall--infiniteLoop--augmented.ifc','medium|ifc/01_26_Slavyanka_4.ifc','large|ifc/fbf851df-840f-49e3-9e74-3900cab65d14.ifc') -MeasuredRuns 1 -PreserveOrder $false"
```

Что делает скрипт:
- запускает CLI-экспорт с `--verbosity detailed` и заданным флагом `--preserve-order`;
- делает warm-up для каждого dataset;
- снимает метрики по сериям measured runs;
- сохраняет сырые логи;
- сохраняет golden JSON (run 1 для каждого dataset);
- считает SHA256 для golden JSON;
- строит aggregated summary (median/p95).

## 4) Output artifacts

Скрипт создает папку:

- `benchmarks/results/stage0/<timestamp>/`

Содержимое:
- `logs/*.log`
- `raw/*.csv`
- `golden/*.json`
- `summary.md`

## 5) Baseline report update

После запуска перенести агрегированные значения в:
- `benchmarks/baseline-results.md`

## 6) Acceptance for Stage 0

Stage 0 завершен, если:
1. `docs/json-contract-baseline.md` утвержден.
2. `benchmarks/dataset-manifest.md` заполнен (включая IFC SHA256).
3. Есть `summary.md` и golden JSON по всем обязательным dataset.
4. `benchmarks/baseline-results.md` заполнен.

## 7) Unknown schema policy (future)

Если появляется новая схема, не входящая в hot-path (например не IFC2x3/IFC4/IFC4x3):

1. **Не ломать экспорт:** использовать текущий универсальный pipeline (fallback).
2. **Контракт JSON не менять:** выход должен соответствовать `docs/json-contract-baseline.md`.
3. **Логировать факт схемы:** в отчете фиксировать значение `Schema:`.
4. **Добавить dataset в manifest:** новый файл + SHA256 + категория.
5. **Снять отдельный baseline-cycle:** минимум 1 warm-up + N measured runs.
6. **Решение о hot-path:**
   - если схема регулярно встречается и дает заметную долю нагрузки, планировать typed fast-path;
   - иначе оставлять на fallback.
7. **Критерий допуска в hot-path:** подтвержденная parity с fallback + KPI по ускорению/аллокациям.
