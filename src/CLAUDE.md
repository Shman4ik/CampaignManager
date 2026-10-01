# CampaignManager 2.0 — решение `src/`

План, решения D1–D7 и архитектура — [docs/v2/README.md](../docs/v2/README.md). Здесь — то, что
нужно знать, трогая код 2.0. Знание конкретного модуля — в `CLAUDE.md` его папки.

## Проекты

| Проект | Что в нём | Ссылается на |
|---|---|---|
| `Core` | правила книги, документы | только BCL |
| `Contracts` | DTO, маршруты, интерфейсы API | `Core` |
| `ApiClient` | `HttpClient`-реализации интерфейсов | `Contracts` |
| `Data` | `CmDbContext`, миграции схемы `cm` | `Core` |
| `UI` | страницы, компоненты, Tailwind | `Core`, `Contracts` |
| `Web.Client` | хост WebAssembly: регистрирует `ApiClient` | `UI`, `ApiClient` |
| `Server` | API `/api/v1/…`, хост приложения | всё, кроме `ApiClient` |

Границы проверяет `tests/CampaignManager.Server.Tests/ArchitectureTests` по ссылкам собранных
сборок. Новый проект — туда же, в `AllowedOwnDependencies`.

## Правила

- **`Core`, `Contracts`, `ApiClient` — `IsAotCompatible` и `IsTrimmable`** (D4: на них же будет
  мобильное приложение). JSON — только через `ContractsJsonContext`: новый DTO добавляется туда
  атрибутом `[JsonSerializable]`, иначе клиент не сможет его прочитать. Рефлексию анализаторы не
  пропустят — не глушить их, а переписать без неё.
- **Маршрут — константа в `Contracts`** (`PlatformRoutes.Ping`): сервер маппит её, клиент по ней
  ходит, строка одна.
- **Модуль сервера** — пара `AddXxxModule` / `MapXxxApi` в папке модуля; `Program.cs` их только
  перечисляет. Эндпоинт вызывает прикладной сервис, сервис принимает и отдаёт DTO из `Contracts`.
- **Клиент нового модуля** — одна строка в `ApiClientServiceCollectionExtensions`.
- **Версии пакетов** — только в корневом `Directory.Packages.props`, в csproj без `Version`.
  Включено транзитивное закрепление: без него Npgsql тянул EF 10.0.4, а EF Design — 10.0.12, и
  сервер падал с CS1705. Новый пакет EF — закреплять там же.
- **Корневые `Directory.*.props` для проектов v1 выключены по имени** (`IsCampaignManagerV1`):
  Dockerfile v1 делает `COPY . .`, и центральные версии уронили бы его `PackageReference` с
  `Version`. Список уходит вместе с v1 в T3.3.

## Хост и страницы

- **WebAssembly без пререндера** (D1): `App.razor` в `Server` ставит
  `InteractiveWebAssemblyRenderMode(prerender: false)`. Сервер отдаёт оболочку с заглушкой
  «Загрузка…» (`#app-loading`), её прячет CSS, как только отрисовалась `.cm-shell`. Тест
  `App_page_is_not_prerendered` следит, чтобы пререндер не вернулся.
- **Статически сервер рендерит только `[ExcludeFromInteractiveRouting]`-страницы** — пока это
  `/Error`: страница ошибки не должна зависеть от того, загрузится ли клиент. Маршрут такой
  страницы `Router` из `UI` берёт у эндпоинта, хотя сама она живёт в сборке `Server`.
- **Ошибки и 404.** Страницы отвечают `/Error` и `/not-found`, API — ProblemDetails. Обработчики
  страниц стоят **в основном конвейере**, обработчики API — внутри них, в `UseWhen`. Наоборот не
  работает: повторный проход из ветки `UseWhen` заново эндпоинт не ищет, и неизвестный адрес
  получал пустой 404 вместо страницы.
- `/health` маппится всегда (в v1 ServiceDefaults маппил его только в Development). OpenAPI —
  встроенный `Microsoft.AspNetCore.OpenApi`, документ на `/openapi/v1.json`; без Swashbuckle.
- Культура WebAssembly — `ru-RU` при любом языке браузера (`Web.Client/Program.cs`), поэтому
  грузятся все данные ICU (`BlazorWebAssemblyLoadAllGlobalizationData`).

## Tailwind

Собирается в `UI` тем же MSBuild-таргетом, что в v1, со всеми оговорками из корневого
`CLAUDE.md`; результат — `UI/wwwroot/styles.css` (не в git), отдаётся как
`_content/CampaignManager.UI/styles.css`. Сканирует и `.cs` в `UI`, и `.razor` сервера.
Пока v3 без токенов — дизайн-систему и выбор v3/v4 делает T1.6.

## База

- Строка подключения сервера — `ConnectionStrings:DefaultConnection` в
  `Server/appsettings.Development.json` (в `.gitignore`; в worktree скопировать строку из
  `CampaignManager.Web/appsettings.Development.json` основного чекаута — это ветка Neon `dev`).
  **В прод не писать.**
- `dotnet ef` работает с `Data` напрямую, без `Program.cs` и Auth0 (`DesignTimeCmDbContextFactory`):
  `dotnet ef migrations add <Имя> --project src/CampaignManager.Data`. Строка — из `CM_DB`, без
  неё — `localhost:5432`.
- Журнал миграций — `cm.__ef_migrations_history`, не общий `public` v1.
- **Схема `cm`** — `Data/<Модуль>/` (сущности и `IEntityTypeConfiguration` рядом), спецификация —
  [SCHEMA.md](../docs/v2/SCHEMA.md). Первая миграция — `InitialCmSchema`; применена на ветке `dev`.
  Миграции не применяются при старте сервера: `dotnet ef database update --project src/CampaignManager.Data`
  с `CM_DB`.
- **Enum'ы колонок живут в `Core/<Модуль>`** (`CampaignStatus`, `CharacterKind`…) — их же возьмут
  `Contracts` и документы. В базе — текст с именем члена. Ни конвертер, ни CHECK руками не пишутся:
  `SchemaConventions.MapEnumsAsCheckedText` обходит модель и вешает на каждую enum-колонку
  `ck_<таблица>_<колонка>` со списком из самого enum (массив enum'ов — `eras <@ ARRAY[…]`).
  Новый член enum'а — новая миграция (меняется CHECK); переименовать член — значит переписать данные.
- **Значение по умолчанию** — `HasDbDefault`/`HasDbDefaultSql`: default есть в DDL (для сырого SQL
  переноса), но EF всегда пишет значение объекта. Голый `HasDefaultValue` делает «нулевое» значение
  незаданным: `volume = 0` молча стало бы 100, `count = 0` — 1. Свойство инициализируется тем же
  значением, что default в DDL.
- `created_at`/`updated_at` ставит `TimestampsInterceptor` по интерфейсам `ICreatedAt`/`IUpdatedAt`;
  руками не заполнять (явно заданный `CreatedAt` сохраняется — это для переноса). Время — `DateTimeOffset`
  в UTC: Npgsql не пишет в `timestamptz` значение с ненулевым смещением.
- **Конкурентность** — `uint Version` + `IsRowVersion()` = системная `xmin`, колонки в DDL нет. Есть у
  справочников (`CatalogEntry`), `music_tracks`, `scenarios`, `characters`, `encounters`. Устаревшая
  версия → `DbUpdateConcurrencyException` (API превратит в 409).
- Документы (`characters.sheet`, `creatures.statblock`, `scenario_creatures.statblock`,
  `encounters.state`) пока `JsonDocument`; типы из `Core` подставляет T1.7. `characters.name`/`occupation` —
  generated-колонки из `sheet.personal.*`, только для чтения.
- **Чего EF не умеет — в миграции SQL-ом**, и при следующих миграциях это не трогать: уникальные индексы
  `lower(name)` у справочников и составной FK `characters (campaign_id, owner_id) → campaign_members`
  с `ON DELETE SET NULL (campaign_id)` (в модели он `ClientNoAction`: EF обнулил бы и `owner_id`,
  а это нарушает `ck_characters_owner`). Частичные индексы, `NULLS NOT DISTINCT`, GIN и триграммы EF
  делает сам (`HasFilter`, `AreNullsDistinct(false)`, `HasMethod`/`HasOperators`).
- Нужен PostgreSQL 15+ (`SET NULL (колонка)`, `NULLS NOT DISTINCT`): Neon — 15, CI и локальные тесты — 17.
- Конвенция snake_case не считает цифру границей слова: `Auth0Sub` → `auth0sub`, поэтому у таких
  свойств имя колонки задано явно (`auth0_sub`).
- После правки модели: `dotnet ef migrations add <Имя> --project src/CampaignManager.Data`. Тест
  `Migrations_cover_the_model` падает, если модель ушла вперёд миграций.

## Тесты

- `Core.Tests` — правила книги; `Server.Tests` — API через `ApiClient` на `WebApplicationFactory`
  (окружение `Testing`, чтобы не подхватить `appsettings.Development.json`) и тест архитектуры.
- База тестов — `CM_TEST_DB` (D7); без неё тесты с базой пропускаются (`TestDatabase.SkipIfMissing`).
  Локально — одноразовый Postgres в `wslc`:

```bash
wslc run -d --rm --name cm-test-pg -p 55432:5432 -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=campaignmanager_test postgres:17
```

  и `CM_TEST_DB=Host=localhost;Port=55432;Database=campaignmanager_test;Username=postgres;Password=postgres`.
  Ветку Neon `dev` в `CM_TEST_DB` не подставлять: тестам нужна своя база, которую не жалко.
- Тесты схемы (`Server.Tests/Schema`) создают на сервере из `CM_TEST_DB` отдельную базу
  `cm_schema_<guid>`, поднимают её миграциями и удаляют после прогона (`SchemaDatabase`). Нужны права
  `CREATEDB` — у `postgres` в контейнере они есть.

## Запуск

`.claude/launch.json`, конфигурация `v2` — `https://localhost:8080`, тот же порт, что у v1 (он
разрешён в dev-приложении Auth0), поэтому v1 и v2 запускаются по очереди. `/dev/ping` —
сквозная проверка: страница → `ApiClient` → `GET /api/v1/ping` → `Data` → Postgres.
