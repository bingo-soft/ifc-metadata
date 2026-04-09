# Skill: NuGet Compliance Check and Push

## Purpose

Проверить проект на соответствие требованиям NuGet на основе `nuget_policy.md`.

- Режим проверки: анализ и отчёт.
- Режим push: анализ, отчёт и push в GitLab.
- Публикация в NuGet/ProGet этим skill **не выполняется**.
- В любом режиме проверка включает синхронизацию `.csproj` по `nuget_policy.md`.

## Scope

- `nuget_policy.md`
- `src/**/*.csproj`
- `Directory.Build.props` (если есть)
- `README.md`
- `CHANGELOG.md`
- `nuget.config`
- `.gitlab-ci.yml`

## Command triggers

- `проверь nuget`
  - режим: `read/validate/report`
  - допустим в любой ветке
- `проверь nuget и пуш`
  - режим: `read/validate/report/push`
  - допустим **только** в ветке `develop`

## Source of truth

- Для NuGet-метаданных значения для проверки и синхронизации берутся из `nuget_policy.md`.
- Для правил версионирования источник истины — `version_policy.md`.

Если `nuget_policy.md` отсутствует:
- статус: `NOT READY`
- в отчёте указывается необходимость создать policy-файл.

Если `version_policy.md` отсутствует:
- статус: `NOT READY`
- в отчёте указывается необходимость создать policy-файл версионирования.

## Synchronization rules

Перед финальной проверкой Codex выполняет синхронизацию в 3 шага:

1. **Проверить наличие параметра в `nuget_policy.md`**.
2. Если параметра нет в policy:
   - добавить параметр в `nuget_policy.md`;
   - если значение можно определить из проекта автоматически — заполнить его;
   - если автоматически определить нельзя — записать `<parameter_name>`.
3. После наличия параметра в policy:
   - задать/обновить соответствующее поле в `.csproj` по значению из policy.

Правило для файла проекта (обязательно в любом режиме):
- если параметра нет в `.csproj` — параметр создаётся (если значение в policy не placeholder);
- если параметр есть, но значение не совпадает с policy, и значение в policy не равно `<parameter_name>` — значение исправляется по policy.

Правило для placeholder-значений:
- значение вида `<parameter_name>` означает, что параметр должен заполнить пользователь;
- такие поля не переносятся в `.csproj`;
- такие поля помечаются как `FAIL` в отчёте до ручного заполнения.

Специальное правило для лицензии:
- если файл лицензии, указанный в policy (`package_license_file`), существует в репозитории — используется `PackageLicenseFile`;
- если файл лицензии отсутствует — используется `PackageLicenseExpression`;
- одновременно заполнять `PackageLicenseExpression` и `PackageLicenseFile` в `.csproj` нельзя.

## Required checks

Сверка `.csproj` с `nuget_policy.md` и `version_policy.md`:
- `PackageId`
- `Version` / `AssemblyVersion` (по `version_policy.md`)
- `Authors`
- `Description`
- `RepositoryUrl`
- `RepositoryType`
- `PackageProjectUrl`
- `PackageTags`
- `PackageReadmeFile`
- `PackageLicenseExpression` или `PackageLicenseFile` (по правилу наличия файла лицензии)
- `IncludeSymbols`
- `SymbolPackageFormat`

Для каждого поля применяется правило:
- если поле отсутствует в `.csproj`, но есть в policy и не placeholder — поле создаётся/заполняется;
- если поле отсутствует и в policy — сначала добавляется в policy по правилам синхронизации, затем переносится в `.csproj`.

Дополнительно:
- `README.md` существует и не пустой
- `CHANGELOG.md` содержит запись текущей версии
- `nuget.config` содержит feed из политики

## Report format

Итог:
- `NuGet Compliance: READY|NOT READY`
- список `PASS/FAIL` по каждому пункту
- `Mismatch with nuget_policy.md`
- `How to fix`
- `Push status: DONE|SKIPPED|FAILED`

## Push policy

Push выполняется только по явной команде пользователя и только если есть изменения.

Если перед push в GitLab есть изменённые файлы:
- сначала выполнить commit всех подготовленных изменений по commit policy;
- затем выполнять push.

Ограничение по ветке:
- команда `проверь nuget и пуш` разрешена только в `develop`;
- в любой другой ветке возвращается отказ с пояснением.

Commit message format:
- `<type>: <short summary>`
- пример: `chore: align csproj nuget metadata with policy`

## Recommended execution

- `проверь nuget` — для любой рабочей ветки и pre-check перед MR.
- `проверь nuget и пуш` — только для `develop`.
