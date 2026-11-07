# Practitioner Directory — сервис справочника врачей

Backend-сервис для хранения и управления справочником врачей: REST CRUD, чтение в формате **HL7 FHIR R5**,
асинхронный импорт через брокер сообщений, кэширование и полнотекстовый поиск.

## Стек

| Компонент | Технология |
|---|---|
| API | ASP.NET Core 8 Web API, Swagger (Swashbuckle) |
| БД | PostgreSQL 16 (EF Core + Npgsql) |
| Брокер сообщений | Redis Streams (consumer group) |
| Кэш | Redis |
| Развёртывание | Docker / docker-compose |

## Быстрый старт

Требуется только Docker.

```bash
docker compose up --build
```

Поднимутся 4 контейнера: `postgres`, `redis`, `api` (порт **8080**) и `consumer`.
При первом запуске `api` применяет **EF Core миграции**: `InitialCreate` создаёт схему, `SeedPractitioners` загружает 25 тестовых врачей. Повторные запуски ничего не дублируют (EF хранит применённые миграции в `__EFMigrationsHistory`).

* Swagger UI: http://localhost:8080/swagger
* Health: http://localhost:8080/health
* Остановка: `docker compose down` (с удалением данных: `docker compose down -v`)

### Локальный запуск без Docker для приложения

```bash
docker compose up -d postgres redis                                   # только инфраструктура
dotnet run --project src/Practitioners.Api                            # API (настройки в appsettings.json)
dotnet run --project src/Practitioners.Api -- --AppMode=consumer      # consumer — во втором терминале
```

## Структура проекта

```
src/Practitioners.Api
├── Controllers/    REST (PractitionersController), FHIR (FhirPractitionerController), producer (ImportController)
├── Services/       PractitionerService (бизнес-логика), Redis-кэш
├── Fhir/           FHIR-модели и маппинг
├── Messaging/      ImportPublisher (producer), ImportConsumer (BackgroundService), события
├── Data/           DbContext, применение миграций при старте
├── Migrations/     EF Core миграции: схема, тестовые данные, snapshot модели
└── Domain/         сущности
```

Один образ — два режима: `AppMode=api` (по умолчанию) и `AppMode=consumer`.

## Модель данных `Practitioner`

`id, firstName, lastName, phone, speciality, placeOfWork, email, isActive` + служебные поля:
`created, updated, version` (оптимистичная блокировка / ETag), `isDeleted` (soft delete), `sourceEventId`
(последнее событие импорта). Для полнотекстового поиска в БД есть generated-колонка `tsvector` + GIN-индекс.

## REST API (`/api/practitioners`)

| Метод | URL | Описание |
|---|---|---|
| POST | `/api/practitioners` | Создание (201, `Location`, `ETag`). Заголовок `Idempotency-Key` — безопасный повтор |
| GET | `/api/practitioners/{id}` | Получение по id |
| GET | `/api/practitioners` | Поиск: `q, firstName, lastName, speciality, placeOfWork, email, phone, isActive, sort, page, pageSize` |
| PUT | `/api/practitioners/{id}` | Изменение. `If-Match: W/"<version>"` — оптимистичная блокировка (412 при конфликте) |
| DELETE | `/api/practitioners/{id}` | Удаление (soft delete), идемпотентно — всегда 204 |
| POST | `/api/import/practitioners` | Публикация событий импорта в брокер (202 Accepted) |

Примеры:

```bash
# создать (с ключом идемпотентности — повтор вернёт ту же запись)
curl -X POST http://localhost:8080/api/practitioners \
  -H "Content-Type: application/json" -H "Idempotency-Key: 6f2b1c7e-demo-1" \
  -d '{"firstName":"Пётр","lastName":"Иванов","phone":"+7 (999) 123-45-67","speciality":"Терапевт","placeOfWork":"Поликлиника №1","email":"p.ivanov@clinic.example","isActive":true}'

# поиск с фильтрами и пагинацией
curl "http://localhost:8080/api/practitioners?speciality=кардиолог&isActive=true&page=1&pageSize=10"

# полнотекстовый (префиксный) поиск по firstName + lastName
curl "http://localhost:8080/api/practitioners?q=иван%20пет"

# изменить с проверкой версии
curl -X PUT http://localhost:8080/api/practitioners/<id> -H "Content-Type: application/json" -H 'If-Match: W/"1"' \
  -d '{"firstName":"Пётр","lastName":"Иванов","speciality":"Кардиолог","isActive":true}'

# удалить
curl -X DELETE http://localhost:8080/api/practitioners/<id>
```

## FHIR (`/fhir/Practitioner`) — HL7 FHIR R5

Реализованы **read** и **search** (https://hl7.org/fhir/R5/http.html), `Content-Type: application/fhir+json`.

* `GET /fhir/Practitioner/{id}` — ресурс `Practitioner`; заголовки `ETag` (`W/"version"`), `Last-Modified`, поддержка `If-None-Match` (304);
  `404` / `410 Gone` (удалён) возвращаются как `OperationOutcome`.
* `GET /fhir/Practitioner?...` — `Bundle` типа `searchset` с `total`, `link` (`self`/`next`/`previous`) и `entry`.

Параметры поиска: `_id`, `name` (полнотекстовый префиксный по given+family), `family`, `given`, `active`,
`email`, `phone`, `speciality`, `_count` (1–100, по умолчанию 20), `_offset`, `_sort` (`family`, `given`, `_lastUpdated`, `-` для убывания).

Маппинг: `firstName → name[0].given`, `lastName → name[0].family`, `phone/email → telecom`, `speciality → qualification[0].code.text`,
`isActive → active`, `version/updated → meta.versionId/lastUpdated`. Места работы в базовом ресурсе `Practitioner` нет (оно в `PractitionerRole`),
поэтому `placeOfWork` отдаётся как extension.

```bash
curl -H "Accept: application/fhir+json" "http://localhost:8080/fhir/Practitioner?name=иван&_count=5&_sort=family"
curl -H "Accept: application/fhir+json" http://localhost:8080/fhir/Practitioner/<id>
```

## Асинхронный импорт и consumer

Producer (`POST /api/import/practitioners`) кладёт события в Redis Stream `practitioners:import`.
Consumer (контейнер `consumer`, consumer group `practitioners-consumers`) читает их и применяет к БД.

Запуск consumer:

* Docker: уже запущен сервисом `consumer` (`docker compose logs -f consumer`);
* локально: `dotnet run --project src/Practitioners.Api -- --AppMode=consumer`;
* масштабирование: `docker compose up -d --scale consumer=3` (отключите `Consumer__Name` или задайте уникальные имена).

Формат события:

```json
[
  { "eventId": "evt-001", "type": "upsert", "occurredAt": "2026-11-04T10:00:00Z",
    "payload": { "id": "11111111-1111-1111-1111-111111111111", "firstName": "Леонид", "lastName": "Тестов",
                 "speciality": "Хирург", "placeOfWork": "Клиника", "email": "l.testov@clinic.example", "isActive": true } },
  { "eventId": "evt-003", "type": "delete", "payload": { "id": "11111111-1111-1111-1111-111111111111" } }
]
```

```bash
curl -X POST http://localhost:8080/api/import/practitioners -H "Content-Type: application/json" -d @events.json
```

## Идемпотентность и консистентность

* **POST**: `Idempotency-Key` → таблица `idempotency_keys`; запись врача и ключ сохраняются в одной транзакции,
  гонка параллельных запросов разруливается уникальным ключом.
* **PUT**: полная замена = идемпотентна; `version` — concurrency token (`UPDATE ... WHERE version = @v`), `If-Match` → 412.
  Без `If-Match` конкурентная запись всё равно не теряется молча (409).
* **DELETE**: атомарный soft delete, повтор безопасен.
* **Импорт**: доставка at-least-once, обработка идемпотентна — в одной транзакции пишется `processed_events(eventId)`
  и изменяются данные; дубль события (тот же `eventId`) пропускается. Устаревшие события (`occurredAt` старше `updated`) не затирают свежие данные.
  Невалидные сообщения уходят в dead-letter stream `practitioners:import:dlq`; при временной ошибке сообщение остаётся в pending и перечитывается.

## Кэширование

Redis, fail-open (при недоступности Redis сервис работает напрямую с БД):

* `GET by id` — ключ на запись, сбрасывается при изменении/удалении;
* поиск — ключ содержит глобальную «версию списков» (`prac:list:ver`), которая увеличивается при любой записи
  (в т.ч. из consumer'а), поэтому устаревшие выдачи не отдаются; TTL — 5 минут.

## Полнотекстовый поиск

PostgreSQL `tsvector` (generated column по `firstName + lastName`, конфигурация `simple`) + GIN-индекс.
Запрос `q=иван пет` превращается в префиксный `tsquery` `иван:* & пет:*`.

## Миграции EF Core

Схема и тестовые данные полностью управляются миграциями (`src/Practitioners.Api/Migrations`):

| Миграция | Что делает |
|---|---|
| `InitialCreate` | таблицы `practitioners`, `processed_events`, `idempotency_keys`; generated-колонка `tsvector`, GIN-индекс, индексы по `LastName`, `Speciality`, `IsActive` |
| `SeedPractitioners` | data-миграция: `InsertData` с 25 тестовыми врачами (фиксированные Guid — `Down` их удаляет) |

`api` применяет их при старте (`Database.MigrateAsync()`), consumer схему не трогает и ждёт её появления.

Работа с миграциями вручную (локальная Postgres на `localhost:5432`, строка подключения — в `Data/AppDbContextFactory.cs`
или переменная `ConnectionStrings__Postgres`):

```bash
dotnet tool restore                                              # ставит dotnet-ef из .config/dotnet-tools.json
dotnet ef migrations add <Name> --project src/Practitioners.Api  # новая миграция после изменения модели
dotnet ef database update --project src/Practitioners.Api        # применить
dotnet ef database update InitialCreate --project src/Practitioners.Api  # откат к миграции (данные seed удалятся)
dotnet ef migrations script --project src/Practitioners.Api -o migrate.sql  # SQL-скрипт
```

## Postman

Коллекция: `postman/Practitioners.postman_collection.json` (переменная `baseUrl`, id созданного врача подставляется автоматически).
Содержит CRUD, FHIR и импорт через брокер.

## Допущения и что можно улучшить

* Миграции применяются при старте приложения — для одного экземпляра API это удобно; в проде их обычно выполняют отдельным шагом деплоя (`dotnet ef migrations script` / migration bundle).
* Аутентификации нет; в проде — авторизация и rate limiting.
* Для масштабирования consumer'ов стоит добавить reclaim «зависших» сообщений упавших consumer'ов (`XAUTOCLAIM`) и счётчик попыток перед отправкой в DLQ.
* Добавить unit/integration-тесты (Testcontainers).
