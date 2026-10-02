# CampaignManager 2.0 — решение `src/`

План, решения D1–D7 и архитектура — [docs/v2/README.md](../docs/v2/README.md). Здесь — то, что
нужно знать, трогая код 2.0. Знание конкретного модуля — в `CLAUDE.md` его папки.

## Проекты

| Проект | Что в нём | Ссылается на |
|---|---|---|
| `Core` | правила книги, документы ([CLAUDE.md](CampaignManager.Core/CLAUDE.md)) | только BCL |
| `Contracts` | DTO, маршруты, интерфейсы API | `Core` |
| `ApiClient` | `HttpClient`-реализации интерфейсов | `Contracts` |
| `Data` | `CmDbContext`, миграции схемы `cm` | `Core` |
| `UI` | страницы, UI-кит, оболочка, Tailwind ([CLAUDE.md](CampaignManager.UI/CLAUDE.md)) | `Core`, `Contracts` |
| `Web.Client` | хост WebAssembly: регистрирует `ApiClient`, кит UI, `AuthenticationStateProvider`; индикатор связи на каждом `HttpClient` | `UI`, `ApiClient` |
| `Server` | API `/api/v1/…`, хост приложения | всё, кроме `ApiClient` |

Границы проверяет `tests/CampaignManager.Server.Tests/ArchitectureTests` по ссылкам собранных
сборок. Новый проект — туда же, в `AllowedOwnDependencies`.

## Правила

- **`Core`, `Contracts`, `ApiClient` — `IsAotCompatible` и `IsTrimmable`** (D4: на них же будет
  мобильное приложение). JSON — только через source-generated контексты: DTO — `ContractsJsonContext`,
  документы Core — `CmJsonContext`; новый тип добавляется туда атрибутом `[JsonSerializable]`, иначе
  клиент не сможет его прочитать. Рефлексию анализаторы не
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

## Вход и права

Подробно — [Server/Identity/CLAUDE.md](CampaignManager.Server/Identity/CLAUDE.md) (Auth0, куки,
JWT, автовход) и [Server/Access/CLAUDE.md](CampaignManager.Server/Access/CLAUDE.md) (таблица прав).
Что нужно знать любому модулю:

- **Эндпоинты API закрыты:** группа модуля — `.RequireAuthorization()` или
  `.RequireAuthorization(Policies.Keeper|Admin)`. Открытый эндпоинт роняет тест
  `Every_api_endpoint_requires_authorization`.
- **Каждый метод записи сервиса зовёт `AccessPolicy` и `Demand`**; флаги `canEdit`/`canDelete` в DTO —
  из неё же. Не виден объект — 404, виден, но нельзя — 403. Своих проверок ролей и членства в
  сервисах не писать: новое правило — метод `AccessPolicy` и строка в таблицу Access.
- Кто делает запрос — `CurrentUser` (id, роль из `cm.users`). `IHttpContextAccessor`, claims и почту
  в сервисах не читать.
- **Страницы UI — `[Authorize(Policy = Policies.Keeper)]`, не `Roles`**: сервер проверяет атрибут
  страницы при прямой загрузке, а роли в куке нет.
- Сервер без `Authentication:Auth0:Domain`/`ClientId` не стартует (в v1 это был 500 на каждый
  запрос). Значения dev-приложения — в `Server/appsettings.Development.json` (копируются скриптом
  из `CampaignManager.Web/appsettings.Development.json` основного чекаута вместе со строкой подключения).

## UI и Tailwind

Дизайн-система, UI-кит и оболочка — [UI/CLAUDE.md](CampaignManager.UI/CLAUDE.md); живой пример
всего кита — `/dev/ui` (только Development). Коротко:

- **Tailwind v4**, токены — `@theme` в `UI/Styles/theme.css`, конфига `tailwind.config.js` нет.
  Палитры только наши: `gray-*`, `slate-*`, `info-*` и прочих стандартных в сборке нет.
- Собирается MSBuild-таргетом в `UI` (все оговорки v1 из корневого `CLAUDE.md` в силе) через
  `npm ci` по `UI/package-lock.json` — нужен Node; результат `UI/wwwroot/styles.css` (в репозиторий
  не кладётся) отдаётся как `_content/CampaignManager.UI/styles.css`.
- Классы `cm-*` — в слое `components`: утилита перебивает их без «!». Изолированный
  `*.razor.css` — внутри `@layer components { }`, иначе он бьёт утилиты.
- Страница — `PageHeader` + `<div class="cm-page">`; диалог — `Modal`/`DialogService`;
  сообщение — `Alert`/`ToastService`; загрузка — `AsyncContent`; бросок — `RollInput`.
- Шрифты и Font Awesome — свои, из `UI/wwwroot`; внешних CDN нет.

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
  `encounters.state`) в `Data` — `JsonDocument`, и так и остаются: читать `CmJson.ReadSheet(sheet, sheetVersion)`
  (апкастер видит версию из соседней колонки — конвертер EF её не видит), писать — присваивать
  `CmJson.Write(документ)` вместе с `*_version = CurrentVersion`. Правка графа на месте без присваивания
  EF не заметит. Типы и правила — `Core`, см. [Core/CLAUDE.md](CampaignManager.Core/CLAUDE.md).
  `characters.name`/`occupation` — generated-колонки из `sheet.personal.*`, только для чтения.
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

- `UI.Tests` — UI-кит на bUnit (поведение компонентов и служб; вёрстку проверяет браузер).
- `Core.Tests` — правила книги (тесты T0.2, перенесённые на `Core`; кости — `ScriptedDice`, страница книги —
  `[Trait("page", …)]`, находка — `[Trait("finding", "F-…")]`); `Server.Tests` — API через `ApiClient` на `WebApplicationFactory`
  (окружение `Testing`, чтобы не подхватить `appsettings.Development.json`) и тест архитектуры.
- База тестов — `CM_TEST_DB` (D7); без неё тесты с базой пропускаются (`TestDatabase.SkipIfMissing`).
  Локально — одноразовый Postgres в `wslc`:

```bash
wslc run -d --rm --name cm-test-pg -p 55432:5432 -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=campaignmanager_test postgres:17
```

  и `CM_TEST_DB=Host=localhost;Port=55432;Database=campaignmanager_test;Username=postgres;Password=postgres`.
  Ветку Neon `dev` в `CM_TEST_DB` не подставлять: тестам нужна своя база, которую не жалко.
- Порт для `-p` бывает занят не процессом, а резервом Windows (Hyper-V/WinNAT): `wslc` отвечает
  `WSAEACCES`. Свободен ли порт — `netsh int ipv4 show excludedportrange protocol=tcp`.
- Тесты адаптера MinIO — по `CM_TEST_MINIO=endpoint;accessKey;secretKey` (MinIO в `wslc`, см.
  [Server/Files/CLAUDE.md](CampaignManager.Server/Files/CLAUDE.md)); без переменной пропускаются, в CI
  MinIO нет — тесты API модуля файлов идут на хранилище в памяти.
- `CmApp` подставляет пустышки Auth0, эфемерные ключи Data Protection и схему `TestAuth`: запрос с
  заголовком `X-Test-UserId` (или `X-Test-Sub` + `X-Test-Email`) — вошедший пользователь
  (`client.As(userId)`), без заголовков — настоящий аноним. Метаданные Auth0 заданы руками: вход и
  выход дают редирект на Auth0 без сети. Тестам API с базой нужна схема `cm` — наследник `CmApp`
  со своей `SchemaDatabase` (пример — `IdentityApp`).
- Тесты схемы (`Server.Tests/Schema`) создают на сервере из `CM_TEST_DB` отдельную базу
  `cm_schema_<guid>`, поднимают её миграциями и удаляют после прогона (`SchemaDatabase`). Нужны права
  `CREATEDB` — у `postgres` в контейнере они есть.

## Модули сервера

| Модуль | Что | Знание |
|---|---|---|
| `Platform` | база, JSON, ошибки, health, OpenAPI, хост WebAssembly | этот файл |
| `Identity` | вход через Auth0 (кука и JWT), автовход, `cm.users`, `/api/v1/me` | [Server/Identity/CLAUDE.md](CampaignManager.Server/Identity/CLAUDE.md) |
| `Access` | `CurrentUser`, `AccessPolicy`, политики `[Authorize]` | [Server/Access/CLAUDE.md](CampaignManager.Server/Access/CLAUDE.md) |
| `Files` | `cm.files` поверх MinIO: загрузка, отдача с Range, сироты | [Server/Files/CLAUDE.md](CampaignManager.Server/Files/CLAUDE.md) |

Настройки MinIO (`Minio:*`) в `appsettings.json` не лежат: у v1 dev и прод — один бакет. Без них
сервер стартует, а файлы отвечают ошибкой с именем недостающей настройки.

## Запуск

`.claude/launch.json`, конфигурация `v2` — `https://localhost:8080`, тот же порт, что у v1 (он
разрешён в dev-приложении Auth0), поэтому v1 и v2 запускаются по очереди. Второй экземпляр без
входа — `dotnet run --project src/CampaignManager.Server --no-build --urls https://localhost:8081`.
`/dev/ping` —
сквозная проверка: страница → `ApiClient` → `GET /api/v1/ping` → `Data` → Postgres.
