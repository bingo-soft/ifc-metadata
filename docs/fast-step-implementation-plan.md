# План реализации fast-step архитектуры (с сохранением JSON-контракта)

## 0. Цель и ограничение

**Цель:** радикально снизить время и память при экспорте IFC -> JSON.

**Жесткое ограничение:** результирующий JSON должен быть эквивалентен текущему контракту.
Допускается только изменение порядка данных там, где порядок незначим (например, порядок ключей map при выключенном preserve-order).

Источник контракта:
- `docs/json-contract-baseline.md`
- текущий exporter pipeline в `src/IfcStreamingExportUtilities.cs`

---

## 1. Что уже есть и что тормозит

Текущая реализация:
- открывает модель через `IfcStore.Open(...)`;
- проходит `IIfc*`-объекты;
- строит промежуточный IR (`IfcExportIr`);
- затем пишет JSON через `Utf8JsonWriter`.

Основная стоимость:
1. загрузка IFC в тяжелую объектную модель xBIM;
2. навигация по связям через интерфейсы;
3. лишние аллокации и косвенный доступ к данным.

---

## 2. Целевая архитектура

Добавить второй движок: `fast-step` (двухпроходный стриминговый parser STEP) без построения полной IFC object model.

### 2.1 Компоненты

1. `StepLexer`
   - потоковое чтение IFC (`Stream`/`PipeReader`);
   - токенизация STEP-записей (`#123=IFC...(...)`).

2. `EntityScanner` (Pass 1)
   - выделение только нужных сущностей/полей;
   - построение компактных индексов.

3. `RelationIndexer`
   - индексы связей для parent/properties/material/type;
   - разрешение коллизий и дубликатов по правилам текущего контракта.

4. `JsonEmitter` (Pass 2)
   - прямой вывод через `Utf8JsonWriter`;
   - без DOM и без массивов объектов в памяти.

5. `FastStepEngine`
   - orchestration двух проходов;
   - возврат `IfcExportReport` и telemetry fast-path.

6. `EngineRouter`
   - выбор engine: `xbim` (текущий) или `fast-step` через CLI flag.

### 2.2 Внутренние структуры (data-oriented)

Хранить данные в SoA-форме:
- `EntityId[]`
- `TypeCode[]` (int/enum)
- `ParentRef[]`
- `MaterialRef[]`
- `TypeRef[]`
- `PsetRange[]` + единый буфер ссылок
- string pool / intern table

Правило: в hot path избегать materialization строк, использовать коды и offset-ы.

---

## 3. Совместимость JSON (обязательный контракт)

### 3.1 Что должно совпасть

1. Root-поля и их значения.
2. Поля каждого `metaObjects[*]` и их значения.
3. Правила null/array для `properties`.
4. Дедуп: «last occurrence wins».
5. Parent определяется из финального вхождения объекта.

### 3.2 Что может отличаться

- порядок ключей в `metaObjects`, если это не влияет на семантику и режим не требует строгого порядка;
- порядок элементов там, где в контракте не зафиксирован deterministic order.

Для `--preserve-order true` поведение должно оставаться детерминированным и сопоставимым с baseline.

---

## 4. Пошаговый план внедрения

## Этап 1. Подготовка контрактных тестов (до fast-step)

Изменения:
- добавить integration/parity тесты: `xbim` vs будущий `fast-step`;
- утилиту нормализованного JSON-сравнения (игнор порядка незначимых узлов);
- зафиксировать golden outputs на наборе IFC-файлов.

Критерий готовности:
- тесты описывают все правила из `docs/json-contract-baseline.md`.

## Этап 2. CLI и маршрутизация движков

Изменения:
- новый флаг: `--engine xbim|fast-step` (default: `xbim`);
- роутер движка в `Program`/`IfcStreamingJsonExporter`.

Критерий готовности:
- поведение `xbim` не меняется;
- новый движок подключается без влияния на старый путь.

## Этап 3. Pass 1: streaming scanner + индексы

Изменения:
- лексер STEP (table-driven state machine);
- parser известных типов (`IFCPROJECT`, `IFCRELAGGREGATES`, `IFCRELCONTAINEDINSPATIALSTRUCTURE`, `IFCRELDEFINESBYPROPERTIES`, `IFCRELASSOCIATESMATERIAL`, `IFCRELDEFINESBYTYPE`, `IFCPROPERTYSET`);
- fallback tokenizer для неизвестных сущностей (без падений);
- компактные индексы + string pool.

Критерий готовности:
- из Pass 1 можно получить полный набор данных для сборки JSON-контракта.

## Этап 4. Pass 2: JSON emitter без IR/DOM

Изменения:
- прямой вывод root + `metaObjects` в `Utf8JsonWriter`;
- резолв ссылок parent/properties/material/type из индексов;
- сохранение семантики dedup («last occurrence wins»).

Критерий готовности:
- `--engine fast-step` проходит parity тесты с `xbim` на baseline датасете.

## Этап 5. Оптимизация hot path

Изменения:
- `Span<T>`/`ReadOnlySpan<T>`;
- `ArrayPool<T>` для временных буферов;
- удаление LINQ/boxing/reflection из циклов;
- precomputed hash/type dispatch для entity names.

Критерий готовности:
- измеримое снижение alloc и времени относительно этапа 4.

## Этап 6. Параллельный pipeline (опционально, после паритета)

Изменения:
- producer/consumer/writer через bounded `Channel<T>`;
- backpressure и лимиты памяти;
- режим включается флагом, default single-thread.

Критерий готовности:
- нет деградации контракта;
- выигрыши подтверждены на medium/large IFC.

## Этап 7. Режим измерений

Изменения:
- `--measure` с выводом wall/cpu/gc/alloc/working set;
- метрики вынести за пределы default hot path.

Критерий готовности:
- в обычном режиме отсутствует заметный telemetry overhead.

---

## 5. Матрица рисков

1. **Риск:** несовпадение JSON в edge-cases IFC.
   - Митигировать: golden/parity тесты + сравнение с baseline exporter.

2. **Риск:** сложность STEP-парсера.
   - Митигировать: сначала ограниченный набор сущностей + надежный fallback.

3. **Риск:** регрессия в preserve-order.
   - Митигировать: отдельные тесты для `--preserve-order true/false`.

4. **Риск:** рост сложности поддержки двух engines.
   - Митигировать: единый контрактный слой и общие тесты на оба пути.

---

## 6. Критерии завершения инициативы

1. `--engine fast-step` выдает JSON, эквивалентный baseline-контракту.
2. Паритет подтвержден на контрольном наборе IFC (small/medium/large + разные схемы).
3. Производительность лучше `xbim` по времени и памяти на целевых датасетах.
4. `xbim` путь сохранен как fallback и эталон корректности.

---

## 7. Минимальный набор модулей для первой поставки

- `src/FastStep/StepLexer.cs`
- `src/FastStep/StepEntityScanner.cs`
- `src/FastStep/FastStepIndexes.cs`
- `src/FastStep/FastStepJsonEmitter.cs`
- `src/FastStep/FastStepExporter.cs`
- `src/IfcEngineRouter.cs` (или расширение `IfcStreamingJsonExporter`)
- `tests/FastStepParityTests.cs`

Это даст управляемый переход: сначала корректность и паритет, затем агрессивные оптимизации.