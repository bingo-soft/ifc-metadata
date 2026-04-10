# JSON contract baseline (Stage 0)

Документ фиксирует текущий контракт JSON до оптимизаций вариантов A/B.
Источник истины: `src/IfcStreamingJsonExporter.cs`.

## 1. Root object

Порядок полей в корне:

1. `id`
2. `projectId`
3. `author`
4. `createdAt`
5. `schema`
6. `creatingApplication`
7. `metaObjects`

### Поля корня

| Field | Type | Nullable | Source |
|---|---|---:|---|
| `id` | `string` | yes | `project.Name` |
| `projectId` | `string` | no | `project.GlobalId` |
| `author` | `string` | no | `Header.FileName.AuthorName` joined by `;` |
| `createdAt` | `string` | no | `Header.TimeStamp` |
| `schema` | `string` | no | `Header.SchemaVersion` |
| `creatingApplication` | `string` | yes | `Header.CreatingApplication` |
| `metaObjects` | `object` | no | keyed by object GlobalId |

Rules:
- `author` always exists as string. If no authors, value is empty string `""`.
- `metaObjects` is object/map, not array.

## 2. `metaObjects` contract

`metaObjects` is JSON object:
- key: IFC object id (`GlobalId`)
- value: meta-object payload

### Meta-object field order

1. `id`
2. `name`
3. `type`
4. `parent`
5. `properties`
6. `material_id`
7. `type_id`

### Meta-object fields

| Field | Type | Nullable | Source |
|---|---|---:|---|
| `id` | `string` | no | object `GlobalId` |
| `name` | `string` | yes | `objectDefinition.Name` |
| `type` | `string` | no | `IfcAccessors.GetRuntimeTypeName(...)` |
| `parent` | `string` | yes | parent object id in traversal |
| `properties` | `null \| string[]` | yes | pset GlobalIds |
| `material_id` | `string` | yes | `IfcAccessors.GetMaterialId(...)` |
| `type_id` | `string` | yes | `IfcAccessors.GetTypedId(...)` |

Rules for `properties`:
- `null` if object is `IIfcProject`.
- `null` if object is not `IIfcObject`.
- `null` if no valid property sets found.
- otherwise array of `IIfcPropertySet.GlobalId` values.

## 3. Hierarchy and dedup behavior

Traversal combines:
- `IsDecomposedBy -> RelatedObjects`
- for spatial elements: `ContainsElements -> RelatedElements`

Dedup behavior:
- same `GlobalId` can be visited multiple times during traversal.
- exporter writes a meta-object once.
- effective rule: "last occurrence wins" based on traversal sequence and internal counter decrement.

Parent rule under dedup:
- `parent` is taken from occurrence that actually gets serialized (the final remaining occurrence).

## 4. Preserve-order behavior

- `--preserve-order true` (default): child collections are sorted by `GlobalId` (`Ordinal`) before stack push.
- `--preserve-order false`: direct push order from IFC relations.

Contract note:
- JSON object member order for `metaObjects` follows traversal write order.
- For baseline parity checks, compare outputs under the same `preserve-order` mode.

## 5. Baseline parity criteria for future fast-path

For each validation IFC file:
1. root fields are present and in same order.
2. each meta-object contains the same fields in same order.
3. `metaObjects` key set is identical.
4. for each key, values are equal (`id/name/type/parent/properties/material_id/type_id`).
5. no additional/removed fields.

## 6. Reference sample

```json
{
  "id": "Project Name",
  "projectId": "3ir4vYruPFgwIKoil6nQwl",
  "author": "",
  "createdAt": "2022-09-07T11:00:15+03:00",
  "schema": "IFC2X3",
  "creatingApplication": "...",
  "metaObjects": {
    "3ir4vYruPFgwIKoil6nQwl": {
      "id": "3ir4vYruPFgwIKoil6nQwl",
      "name": "Project Name",
      "type": "IfcProject",
      "parent": null,
      "properties": null,
      "material_id": null,
      "type_id": null
    }
  }
}
```
