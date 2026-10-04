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
- **Статически сервер рендерит только `[ExcludeFromInteractiveRouting]`-страницы** — `/Error` (страница ошибки не
  должна зависеть от того, загрузится ли клиент) и второй экран раздатки `/scenarios/{id}/handouts/{handoutId}` (T2.5b:
  телевизор у стола без WebAssembly, [Server/Scenarios/CLAUDE.md](CampaignManager.Server/Scenarios/CLAUDE.md)). Статус 404
  такая страница не ставит: .NET 10 отдаёт его конвейеру `/not-found`, и тело заменяется. Маршрут такой
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
  запрос) — кроме Development: там без них доступен только тестовый вход (см. «Проверка под ролями»). Значения dev-приложения — в `Server/appsettings.Development.json` основного чекаута (в worktree копировать скриптом
  целиком, не печатая).

## UI и Tailwind

Дизайн-система, UI-кит и оболочка — [UI/CLAUDE.md](CampaignManager.UI/CLAUDE.md); живой пример
всего кита — `/dev/ui` (только Development); как строить и проверять экран — [docs/design-system.md](../docs/design-system.md)
(«Композиция», «Ревью по скриншоту»), порядок работы — скилл `campaign-manager-design`. Коротко:

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
  `Server/appsettings.Development.json` (в `.gitignore`; в worktree — копия этого файла из основного чекаута,
  ветка Neon `dev`).
  **В прод не писать.**
- `dotnet ef` работает с `Data` напрямую, без `Program.cs` и Auth0 (`DesignTimeCmDbContextFactory`):
  `dotnet ef migrations add <Имя> --project src/CampaignManager.Data`. Строка — из `CM_DB`, без
  неё — `localhost:5432`.
- Журнал миграций — `cm.__ef_migrations_history`, не общий `public` v1.
- **Данные `cm` на ветке `dev` — перенос v1** (`tools/CampaignManager.Migrate`, T1.3; как запускать —
  [его CLAUDE.md](../tools/CampaignManager.Migrate/CLAUDE.md)). Перезалить: сбросить ветку от `main`, накатить
  миграции, перенос с `--reset`. Id строк v1 сохранены — ссылки на листы и сценарии v1 ведут туда же.
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
- `Migrate.Tests` — перенос v1 → v2: листы v1 (обезличенные) и прогон целиком на своей базе из `CM_TEST_DB`.
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
| `Campaigns` | кампании, участники и псевдонимы, журнал встреч, главная (`/api/v1/home`) | [Server/Campaigns/CLAUDE.md](CampaignManager.Server/Campaigns/CLAUDE.md), [UI/Campaigns/CLAUDE.md](CampaignManager.UI/Campaigns/CLAUDE.md) |
| `Encounters` | сцены — бой и погоня: начать, состояние с `If-Match`, завершить; ядро — `Core/Encounters`, итоги в лист — через API листа | [Server/Encounters/CLAUDE.md](CampaignManager.Server/Encounters/CLAUDE.md), [UI/Encounters/CLAUDE.md](CampaignManager.UI/Encounters/CLAUDE.md) |
| `Characters` | лист сыщика: чтение, запись документа, портрета и статуса с `If-Match`, соседи по столу, сыщики кампании для ширмы | [Server/Characters/CLAUDE.md](CampaignManager.Server/Characters/CLAUDE.md), [UI/Characters/CLAUDE.md](CampaignManager.UI/Characters/CLAUDE.md) |
| `Catalogs` | семь справочников одним сервисом: список с ETag, правка с `If-Match`, импорт и экспорт JSON, синхронизация с правилами, журнал правок | [Server/Catalogs/CLAUDE.md](CampaignManager.Server/Catalogs/CLAUDE.md), [UI/Catalogs/CLAUDE.md](CampaignManager.UI/Catalogs/CLAUDE.md) |
| `Music` | фонотека — справочник на том же сервисе; закреплённые настроения, пул сцены; плеер в шапке и панели | [Server/Music/CLAUDE.md](CampaignManager.Server/Music/CLAUDE.md), [UI/Music/CLAUDE.md](CampaignManager.UI/Music/CLAUDE.md) |
| `Profile` | личный кабинет: имя и псевдонимы, заявка на Хранителя, настройки (`/api/v1/profile`) | [Server/Profile/CLAUDE.md](CampaignManager.Server/Profile/CLAUDE.md), [UI/Profile/CLAUDE.md](CampaignManager.UI/Profile/CLAUDE.md) |
| `Admin` | пользователи и роли, заявки на Хранителя (`/api/v1/admin`); страница сирот файлов | [Server/Admin/CLAUDE.md](CampaignManager.Server/Admin/CLAUDE.md), [UI/Admin/CLAUDE.md](CampaignManager.UI/Admin/CLAUDE.md) |
| `Scenarios` | библиотека сценариев, рабочее место одним запросом, части по строке (локации, проверки, факты, раздатки, твари, предметы), состав НПС и прегены; режим игры: прохождения для проверок, музыка локации, раздатка для показа и второй экран; прохождения и ваншоты (`RunService`: играть в кампании, объявить ваншот, анонс, бронь прегена копией листа) | [Server/Scenarios/CLAUDE.md](CampaignManager.Server/Scenarios/CLAUDE.md), [UI/Scenarios/CLAUDE.md](CampaignManager.UI/Scenarios/CLAUDE.md) |

**Отказ по смыслу — один механизм на все модули: `Platform/ApiProblemException`** (400 `invalid`, 409 `conflict`/`duplicate`/
`in-use`/`stale`, 428 `version-required`); `DbUpdateConcurrencyException` (устаревший `xmin`) обработчик тоже превращает в 409
`stale`. Текст для человека — в `detail`, его UI показывает как есть; код — расширение ProblemDetails `code`
(`Contracts/Platform/ApiProblemCodes`); клиент, которому он нужен, читает ответ через `ApiResponses` с `withCode: true` и
получает `ApiException` (наследник `HttpRequestException`). Своих исключений-отказов модулям не заводить (так было у
кампаний — `CampaignRejectedException`, сведён сюда в T2.9). Отказ по правам — `AccessDeniedException` (Access), не он.
Ошибка чтения тела запроса (неверное значение enum, не тот тип, битый JSON) — тоже 400 `invalid` с текстом «поле «era»…»:
`ApiProblemExceptionHandler` ловит `BadHttpRequestException` и `JsonException`, а `PlatformModule` включает
`RouteHandlerOptions.ThrowOnBadRequest` во всех окружениях (по умолчанию его бросает только Development — там было 500, #194).
Перехватчики `SaveChanges` модулей — `ISaveChangesInterceptor` в DI, `AddCmData` их подключает.

Настройки MinIO (`Minio:*`) в `appsettings.json` не лежат. Ветка Neon `dev` (и beta-стенд на ней) смотрит
в бакет **`campaign-manager-dev`**: туда перенос (T1.3) копирует объекты v1, на которые ссылаются строки
`files` ветки `dev`; боевой `campain-manager` (v1 и прод) разработка не трогает. Локально — `Minio:BucketName`
в `Server/appsettings.Development.json`. Без настроек сервер стартует, а файлы отвечают ошибкой с именем
недостающей настройки.

## Проверка под ролями

Страницу под Гостем, Игроком, Хранителем и Админом агент проверяет в браузере **тестовым входом** —
только в окружении Development (`dotnet run` берёт его из `launchSettings.json`), без Auth0 и на
любом порту:

```
https://localhost:<порт>/dev/login?as=keeper&returnUrl=/scenarios
```

- `as` — `player`, `keeper` или `admin`; пользователь — `dev-<роль>@cm.test`, его строка в `cm.users`
  заводится тем же кодом, что при входе через Auth0, и получает эту роль. `&email=…` — войти конкретным
  адресом (его роль тоже станет `as`). Гость — просто без входа или после выхода.
- Сменить роль — снова `/dev/login?as=…` (кука перезаписывается); выйти — обычный `/account/logout`
  или «Выйти» в меню, в Auth0 такая сессия не ходит.
- На `/login` в Development есть те же кнопки «Войти как …».
- **Несколько серверов параллельно — можно.** Куки привязаны к хосту, а не к порту, поэтому в
  Development к имени каждой куки приложения дописан порт сервера (`.CampaignManager.Auth.8083`,
  `.CampaignManager.LastLogin.8083`, корреляция и nonce OIDC, antiforgery): вход на 8083 не выбивает
  сессию на 8084. Порт берётся из `--urls`/`ASPNETCORE_URLS`/`launchSettings.json` (первый https) —
  без него имена прежние, о чём сервер пишет в лог при старте. Вне Development имена не меняются.
- Вне Development (`Testing`, `Production`, `Beta`) адреса нет — 404; это держит тест `DevLoginTests`.
- Подробности — [Server/Identity/CLAUDE.md](CampaignManager.Server/Identity/CLAUDE.md), «Тестовый вход».

## Запуск

`.claude/launch.json`, конфигурация `v2` — `https://localhost:8080`, тот же порт, что у v1 (он
разрешён в dev-приложении Auth0), поэтому v1 и v2 запускаются по очереди. Второй экземпляр без
входа — `dotnet run --project src/CampaignManager.Server --no-build --urls https://localhost:8081`.
`/dev/ping` —
сквозная проверка: страница → `ApiClient` → `GET /api/v1/ping` → `Data` → Postgres.

## Памятка исполнителя

Для сессии, которая делает задачу 2.0 в своём worktree параллельно с другими. Порядок работы
оркестратора и шаблон промта — [docs/v2/ORCHESTRATOR.md](../docs/v2/ORCHESTRATOR.md).

- **Конфиг.** Скопировать **целиком** `src/CampaignManager.Server/appsettings.Development.json`
  основного чекаута (`X:\source\CampaignManager`) в тот же путь worktree — скриптом, не печатая: там
  строка ветки Neon `dev`, Auth0 dev и ключ MinIO только на `campaign-manager-dev`.
- **Порт и контейнер** выдаёт оркестратор (свой на задачу). Сервер —
  `dotnet run --project src/CampaignManager.Server --no-build --no-launch-profile --urls https://localhost:<порт>`
  с `ASPNETCORE_ENVIRONMENT=Development`, лог — в файл с уникальным именем (не общий `server.log`).
  Тестовый Postgres — `wslc run -d --rm --name <контейнер> -p <порт>:5432 …` (команда — «Тесты»).
  Windows резервирует диапазоны портов, и они меняются (было 54369–54468 и 55339–55438): при
  `WSAEACCES` взять соседний свободный и написать об этом в отчёте.
- **Данные.** Ветка Neon `dev` — общая: на ней beta и перенос (`--reset` стирает всё, что завели
  агенты). Проверять на своей копии: `pg_dump --schema=cm` с `dev` (только чтение) в свой контейнер.
  Писать в `dev` — только миграции и перенос, если задача этого требует. Прод (`main`) не трогать никак.
  Копию обычно готовит оркестратор (проверка прав исполнителя `pg_dump` с `dev` отклоняет): дамп
  `pg_dump -Fc --schema=cm --no-owner --no-privileges` из контейнера `postgres:17`, перед `pg_restore` в
  пустой базе — `create extension citext; create extension pg_trgm;`, иначе без `cm.users` и индексов.
- **Вход под ролями** — `/dev/login?as=…` (раздел выше). Браузер — своя вкладка (`preview_start` с
  `url`, `tabId` явно); чужие вкладки, процессы и контейнеры не трогать; в конце погасить свои и
  сбросить вьюпорт.
- **Проверка в браузере** — по сценарию задачи на 1194×696 (ландшафт iPad Pro 11", корневой `CLAUDE.md`, «Целевые
  устройства»), плюс 834×1056 для страниц Хранителя и 393×651 для страниц игрока. **Каждая задетая страница —
  скриншот и чек-лист «Ревью по скриншоту»** ([docs/design-system.md](../docs/design-system.md)): замеры
  `scrollWidth` и высоты тап-целей ловят поломки, но не уродство (главную гостя они пропустили дважды). Итог по
  чек-листу — в «Проверке» описания PR. Поведение компонентов — bUnit-тестами.
- **Тесты.** В работе — `dotnet test --filter …` по своему модулю; полный прогон
  (`dotnet test -v q` с `CM_TEST_DB`) — один раз перед PR. `obj/` с битой ref-сборкой (CS0009) —
  удалить `obj` и `bin` этого проекта.
- **Коммитить по ходу работы**, после каждого слоя: worktree с незакоммиченной работой может пропасть.
- **Общие документы не править.** Отметку «Сделано» в `docs/v2/TASKS.md`, паритет в
  `docs/v2/README.md` и сводные таблицы этого файла ставит оркестратор после слияния — по разделу
  «Для TASKS/README» в описании PR. Исполнитель обновляет только CLAUDE.md своего модуля и
  `rules-findings.md`, если нашёл расхождение с книгой.
- **Правила книги** — Книга Хранителя 7e на русском: `X:\Knowledge\CallOfCthulhu\` (`01-07.md` —
  создание, игровая система, бой, погоня; `08-10.md` — рассудок и магия). Искать Grep, целиком не
  читать, текст в репозиторий не копировать (D5). Расхождение v1 с книгой — верить книге и записать в
  `docs/v2/rules-findings.md`; противоречие внутри перевода — вопрос владельцу.
- **Обязательные правила продукта:** любой бросок можно вписать (`RollInput`/`DiceInput`); время —
  в поясе браузера (`LocalTime`); числа в `style` — инвариантной культурой; «одна копия формулы» в Core.

## Деплой и beta-стенд

**Прод — https://cthulhu.dmnet.dev — на 2.0 с 2026-10-04** (T3.2, [CUTOVER.md](../docs/v2/CUTOVER.md)): namespace
`campaign-manager`, тот же SealedSecret `campaign-manager-env`, что был у v1 (2.0 читает те же ключи), Neon `main`, бакет
`campain-manager`. **Beta — https://beta.cthulhu.dmnet.dev** — тот же образ на Neon `dev` (T0.4). Один push катит обоих:
проверять до слияния — на своей копии `dev`, бета уже не «до прода».

- **Выкатка.** Push в `master`, задевший `src/**`, `Directory.*.props`, `global.json`, `NuGet.config` или сам
  воркфлоу (и ручной `workflow_dispatch`) → `.github/workflows/v2-deploy.yml` делает
  `dotnet publish` прямо на раннере (в `artifacts/v2`, без анализаторов — их уже прогнал CI), упаковывает
  его стадией `prebuilt` из `src/Dockerfile` в `ghcr.io/shman4ik/campaign-manager-v2:{0.2.N, latest}` и коммитит тег в приватный
  `Shman4ik/dmnet-gitops`, `workloads/campaign-manager/kustomization.yaml` и `workloads/campaign-manager-beta/kustomization.yaml` (ключ `GITOPS_DEPLOY_KEY`,
  тот же, что у v1). Argo CD (Application `campaign-manager-beta`, namespace `campaign-manager-beta`)
  катит его за ~90 с. Старые версии пакета чистятся, последние 10 остаются.
- **Образ** собирается из корня: `wslc build -f src/Dockerfile -t campaign-manager-v2 .` (цель по
  умолчанию, `final`, публикует сама внутри образа; `prebuilt` — для деплоя, ей нужен готовый `artifacts/v2`). Внутри — только
  `src/`; Node скопирован в SDK-стадию ради Tailwind (MSBuild-таргет `UI` сам делает `npm ci`).
  Рантайм — `aspnet:10.0-noble-chiseled-extra`, порт 8080, без shell. Проверка локально:
  `wslc run --rm -p 58080:8080 --env-file <файл> campaign-manager-v2` и `curl localhost:58080/health`.
- **Окружение — `Production`**, отдельного `Beta` нет: стенд должен вести себя как будущий прод
  (страница ошибки, HSTS, без отладки WebAssembly), а страницы `/dev/*` — обычные страницы UI и
  открываются в любом окружении. `appsettings.<Env>.json` всё равно в `.gitignore`, так что настройки —
  только переменными окружения.
- **Прокси.** TLS снимает Traefik, поэтому в Deployment `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`:
  без него `UseForwardedHeaders` доверяет только loopback, и `redirect_uri` в Auth0 уходит с `http://`.
  Пробы — `httpGet /health` (без проверки базы: холодный старт Neon не должен убивать под).
- **Секреты** — SealedSecret `campaign-manager-beta-env` в dmnet-gitops (зашифрован ключом кластера под
  namespace `campaign-manager-beta`): `ConnectionStrings__DefaultConnection` — **ветка Neon `dev`**,
  `Authentication__Auth0__{Domain,ClientId,ClientSecret}` — **dev-приложение Auth0** (в нём разрешены
  `https://beta.cthulhu.dmnet.dev/signin-oidc` и `/signout-callback-oidc`). Значения копируются
  скриптом из `src/CampaignManager.Server/appsettings.Development.json` основного чекаута и шифруются
  `kubeseal --raw` на VPS, не попадая ни в вывод, ни в git. Добавить ключ, не расшифровывая остальные:

  ```bash
  printf '%s' "$VALUE" | ssh vps 'KUBECONFIG=/etc/rancher/k3s/k3s.yaml kubeseal --raw \
    --namespace campaign-manager-beta --name campaign-manager-beta-env --from-file=/dev/stdin'
  ```

  и вывод — строкой под `encryptedData` в `workloads/campaign-manager-beta/campaign-manager-beta-env.sealed.yaml`
  (PR в dmnet-gitops). Несекретное (`Minio__Endpoint`, `Minio__BucketName`…) — в `env` Deployment.
- **Файлы** — бакет `campaign-manager-dev` на `s3.dmnet.dev` (заводит T1.3), не боевой `campain-manager`.
  `Minio__AccessKey`/`Minio__SecretKey` в секрет ещё не положены: до них сервер работает, а запросы к
  файлам отвечают ошибкой с именем недостающей настройки. Ключ — отдельный пользователь MinIO с правами
  только на этот бакет, а не корневой.
- **Базы.** Прод — ветка Neon `main` (настоящие данные), бета — `dev`, та же, что у локальной разработки (её можно
  сбросить от `main`: `neonctl branches reset dev --parent` — после этого в `dev` сразу прод-данные, схема `cm` уже есть).
  **Миграции сервер не применяет**, и выкатка тоже. Кто добавил миграцию, тот накатывает её **до слияния** своего PR
  **на обе ветки — `dev` и `main`**: выкатка идёт на прод и бету одновременно, и без миграции прод упадёт на первом
  запросе к новой таблице. Миграция должна быть совместимой с кодом до неё (под старой версии живёт до замены):

  ```bash
  CM_DB="<строка ветки dev из appsettings.Development.json>" dotnet ef database update --project src/CampaignManager.Data
  CM_DB="<строка main — из секрета прода, не печатая>" dotnet ef database update --project src/CampaignManager.Data
  ```

  Строку `main` взять из секрета прода в переменную, не выводя (`kubectl get secret campaign-manager-env` на VPS через
  `ssh vps`, ключ `ConnectionStrings__DefaultConnection`, base64). Перенос T1.3 на `main` больше не запускать: он стирает
  данные игры (`--reset`).
- **Откат** — revert коммита `campaign-manager 0.2.N` в dmnet-gitops (или ручной `newTag` на прежнюю версию, PR в
  dmnet-gitops): Argo вернёт прежний образ прода и беты. Миграцию откат не отменяет. Откат прода на v1 — revert
  dmnet-gitops#3: образ v1 хранится в ghcr, схемы `games`/`identity` живы, пока их не снимет владелец (код v1 — в git
  до T3.3, PR удаления).
- **Кто войдёт.** Белый список (`Authorization:AllowedEmails`/`AllowedDomains`) не задан, как и у прода:
  войти может любой с подтверждённой почтой и станет игроком на данных `dev`. Первые админы —
  `Authorization__AdminEmails__0` в секрет тем же `kubeseal --raw`, если роли из переноса не хватит.
