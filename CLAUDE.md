# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

CampaignManager is a tabletop RPG (Call of Cthulhu 7e) management system built with .NET 10 Blazor Server. It manages
campaigns, characters, scenarios, and game assets (creatures, items, weapons, spells, skills). Uses PostgreSQL with
Entity Framework Core, Auth0 (OpenID Connect) authentication, and .NET Aspire for orchestration.

## Development Commands

```bash
# Run the web application (preferred)
dotnet run --project CampaignManager.Web

# Run via .NET Aspire AppHost
dotnet run --project CampaignManager.AppHost

# Build
dotnet build

# Database migrations (two separate contexts)
dotnet ef migrations add <Name> --project CampaignManager.Web --context AppDbContext
dotnet ef migrations add <Name> --project CampaignManager.Web --context AppIdentityDbContext
dotnet ef database update --project CampaignManager.Web --context AppDbContext
dotnet ef database update --project CampaignManager.Web --context AppIdentityDbContext
```

История миграций `AppDb` схлопнута в одну `20260910142150_InitialCreate` — сорок шесть
прежних миграций занимали 34 664 строки, две трети всего C# в проекте. Схема приложения
при этом не менялась, кроме одного намеренного удаления: вместе с выпиленной
LLM-валидацией персонажа ушла таблица `LlmKnowledgeEntries`.

Единственная существующая база на новый журнал **уже переведена**, разовые скрипты переноса
удалены за ненадобностью. Новой базе ничего не нужно: обычный `database update` создаст схему
из `InitialCreate`.

- **Журнал миграций общий у обеих моделей.** `AppDbContext` и `AppIdentityDbContext` пишут
  в одну `public."__EFMigrationsHistory"` — отдельной таблицы у схемы `identity` нет. Значит
  «удалить из журнала всё, кроме своей миграции» — всегда ошибка: заодно уходит строка
  `20250314173627_InitialMigration`, и следующий `database update --context AppIdentityDbContext`
  пытается создать `AspNetRoles` заново и падает с `42P07: relation already exists`. Схема при
  этом цела, чинится возвратом строки в журнал. На этом ровно один раз и обожглись при переносе.
- Трогая журнал руками, проверяй потом **оба** контекста, а не только `AppDbContext`.
- Миграции нигде не применяются автоматически — ни в `Program.cs`, ни в Dockerfile, ни в CI,
  так что момент накатывания выбирается вручную.
- Прежние миграции содержали только `UPDATE` существующих строк (backfill'ы оружия и
  бестиария), без `InsertData`, поэтому на новой базе схлопывание ничего не теряет.

**Tailwind CSS** is built automatically by the `Tailwind` MSBuild target in the csproj:
- Debug: `npx tailwindcss@3 -i ./Styles/tailwind.css -o ./wwwroot/styles.css`
- Release: same with `--minify`

`wwwroot/styles.css` is a build artifact and is **not** tracked in git — it is regenerated on
every build, so the deployed CSS always matches the current markup. Two things keep that working,
don't undo either:
- The target is hooked `BeforeTargets="ResolveProjectStaticWebAssets"`, and it adds the file to
  `@(Content)` itself. MSBuild expands the `wwwroot/**` glob at evaluation time, so a file created
  during the build is invisible to static web assets — without the explicit `Content Include` the
  build and the deploy both succeed and the site serves 404 for `/styles.css`.
- Building therefore requires `npx` (and network access on the first run). The Dockerfile copies
  Node into the SDK stage for exactly this reason.

**No test projects exist** in this solution.

## Pull requests — только стеком

Работу, в которой больше одного смыслового изменения, выкладываем **стеком**, а не одним
большим PR и не пачкой параллельных PR от `master`. Стек — нативные
[stacked pull requests](https://docs.github.com/en/pull-requests/how-tos/stacked-pull-requests)
GitHub (public preview): каждый слой — своя ветка и свой PR поверх слоя ниже, GitHub рисует
карту стека в каждом PR и умеет мёржить его частями. Управляет им расширение `gh stack`
(`gh extension install github/gh-stack`, нужен `gh` ≥ 2.90 — на машине владельца уже стоит).

- **Один слой — одна тема** (фиксы → дизайн → фичи → рефакторинг), чтобы каждый ревьюился и
  мёржился отдельно. Правило, которое должно попасть в `master` первым, кладут в нижний слой.
- Новый стек: `gh stack init --base master <ветка1> <ветка2> …` — существующие ветки
  подхватываются, отсутствующие создаются; следующий слой — `gh stack add <ветка>`; выложить —
  `gh stack submit --auto`. Без `--auto` открывается интерактивный редактор, а у агента
  терминал не интерактивный. Состояние — `gh stack view`.
- Ветки с уже открытыми PR: `gh stack init` с их именами, затем `gh stack submit --auto` —
  PR найдутся сами и свяжутся в стек. Дорастить существующий стек готовыми ветками —
  `gh stack link <номер стека> <ветка|PR> …` (из стека он ничего не удаляет).
- Правка слоя: `gh stack checkout`/`gh stack bottom` → коммит в его ветку →
  `gh stack rebase --upstack` (каскадный ребейз всего, что выше) → `gh stack push`.
  Слои руками по одному не перебазировать и merge-коммитов не делать: `gh stack` требует
  линейной истории.
- Мёрж — снизу вверх: `gh stack merge <PR>` сливает все PR до выбранного включительно одной
  операцией, оставшиеся выше GitHub сам перенацелит на `master`. После — `gh stack sync --prune`.
- Слои можно делать параллельными агентами в отдельных worktree, но каждый стартует от ветки
  слоя **ниже**, а в стек их ставит тот, кто собирает стек.
- CI на PR в репозитории нет: перед `submit` каждый слой собирается (`dotnet build`, 0
  предупреждений) и проверяется в браузере на iPad-вьюпортах, итог — в описании PR.

## Работа в контейнере Claude Code on the web

В удалённом контейнере .NET SDK по умолчанию нет, а `dot.net` / `builds.dotnet.microsoft.com`
закрыты egress-политикой (`curl` получает 403 от прокси) — скрипт `dotnet-install.sh` там не качается.
Ставить надо из репозитория Ubuntu, где .NET 10 уже есть:

```bash
apt-get update                                  # без этого dotnet-sdk-10.0 не виден
DEBIAN_FRONTEND=noninteractive apt-get install -y dotnet-sdk-10.0
dotnet --version                                # 10.0.111 на noble-updates
```

`apt-get update` ругается на недоступные PPA (deadsnakes, ondrej) — это не мешает,
нужные индексы (`archive.ubuntu.com`, `packages.microsoft.com`) забираются.
`nuget.org` доступен, поэтому `dotnet restore` и `dotnet tool install` работают.

Дальше всё как обычно, с двумя оговорками:

```bash
dotnet build                                    # Tailwind собирается тем же таргетом, npx доступен

# EF: design-time поднимает Program.cs целиком, поэтому без строки подключения
# падает с «Value cannot be null. (Parameter 'Host')» — достаточно любой валидной
dotnet tool install --global dotnet-ef && export PATH="$PATH:/root/.dotnet/tools"
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=campaignmanager;Username=postgres;Password=postgres"
dotnet ef migrations has-pending-model-changes --project CampaignManager.Web --context AppDbContext
```

PostgreSQL 16 в контейнере уже установлен, только не запущен — на нём можно прогнать миграции
на живой базе и проверить перенос данных:

```bash
service postgresql start
su postgres -c "psql -c \"ALTER USER postgres PASSWORD 'postgres';\""
su postgres -c "createdb campaignmanager"
dotnet ef database update --project CampaignManager.Web --context AppDbContext
dotnet ef database update --project CampaignManager.Web --context AppIdentityDbContext
```

Запуск приложения: без настроек Auth0 **каждый** запрос отдаёт 500 (`ArgumentException` про
пустой `ClientId` из middleware аутентификации, ещё до роутинга) — это не поломка кода, а пустая
конфигурация. Хватает пустышек — метаданные Auth0 запрашиваются только при входе:

```bash
export Authentication__Auth0__Domain=dummy.auth0.com
export Authentication__Auth0__ClientId=dummy-client-id
export Authentication__Auth0__ClientSecret=dummy-client-secret
dotnet CampaignManager.Web/bin/Debug/net10.0/CampaignManager.Web.dll --urls http://127.0.0.1:5199
```

`dotnet run` берёт URL из `launchSettings.json` (https://localhost:8080) и игнорирует
`ASPNETCORE_URLS`, поэтому для смоук-теста удобнее запускать собранную dll с `--urls`.
Страницы под `[Authorize]` без залогиненного пользователя отдают 302 на логин — это
ожидаемо; для проверки рендера годится `/`.

## Architecture

### Solution Structure

Solution file is `CampaignManager.slnx` (new XML format).

### Feature-Based Vertical Slicing

Each domain lives in `CampaignManager.Web/Components/Features/{FeatureName}/` with subdirectories:
- `Components/` — Feature-specific UI components
- `Model/` (or `Models/`) — Domain models and DTOs
- `Pages/` — Full Razor page views
- `Services/` — Business logic and data access

Each feature folder has its own `CLAUDE.md` with that feature's services, models, and gotchas (entity relationships, cross-feature dependencies, deviations from the patterns below) — see "Documentation Conventions" below.

### Data Architecture

- **Two DbContexts**: `AppDbContext` (schema: "games") for app data, `AppIdentityDbContext` (schema: "identity") for auth
- **Base Entity**: All entities inherit `BaseDataBaseEntity` with `Id` (Guid v7), `CreatedAt`, `LastUpdated` — call `.Init()` on creation
- **JSONB heavily used**: Character stats, creature characteristics, scenario data stored as JSONB columns. Be careful when changing model shapes — JSONB serialization is sensitive to schema changes.
  Правка коллекции **на месте** (`entity.SomeDictionary[key] = value`) трекеру изменений не видна:
  ссылка не поменялась, а компаратора у jsonb-колонки нет. `SaveChanges` тогда отправляет UPDATE
  только по остальным полям, и запись молча теряется — первая (INSERT новой строки) проходит, вторая
  нет. Либо присваивать новый экземпляр, либо помечать свойство вручную:
  `db.Entry(e).Property(x => x.SomeDictionary).IsModified = true` (пример — `UserPreferencesService`).
- **Factory pattern**: Always use `IDbContextFactory<AppDbContext>` with `await using var dbContext = await dbContextFactory.CreateDbContextAsync()` — DbContext is NOT thread-safe

### API Endpoints

Uses minimal APIs (not controllers), mapped in `Utilities/Api/`:
- `AccountEndpoints.cs` — `/api/account/login`, `/api/account/logout`
- `MinioApi.cs` — File storage

Swagger available at `/swagger`.

## Code Patterns

### Service Pattern (Required)

All services must be `sealed`, use primary constructors, and follow this template:

```csharp
public sealed class FeatureService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IdentityService identityService,
    ILogger<FeatureService> logger)
{
    public async Task<List<Entity>> GetAllAsync()
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var userId = identityService.GetCurrentUserId();
        return await dbContext.Entities
            .Where(e => e.UserId == userId)
            .ToListAsync();
    }
}
```

Register as scoped in `Program.cs`: `builder.Services.AddScoped<FeatureService>();`

Reference-data services (catalog features like Items, Skills, Spells, Weapons, Books, Bestiary) additionally inject `IMemoryCache cache` for read caching — follow that convention for new catalog-style services. Runtime resolution engines (`Combat/Services/CombatService`, `Chase/Services/ChaseService`) deliberately break this pattern — they're stateful, non-DI session services, not persistence CRUD; see their feature `CLAUDE.md` files before copying their shape elsewhere.

### C# Conventions

- **Primary constructors** for dependency injection (no traditional constructor + field pattern)
- **Collection expressions**: `return [];` instead of `new List<T>()`
- **Pattern matching**: `if (x is null)`, `if (x is not null)`
- **Nullable reference types** enabled throughout
- **Structured logging**: `logger.LogError(ex, "Error loading {SkillId}", id)` — never string interpolation
- **`TreatWarningsAsErrors`** is enabled (except CS1591 for missing XML docs)

### UI Patterns

- All interactive components use `@rendermode InteractiveServer`
- **CSS isolation**: Always use `*.razor.css` files for component-scoped styles, never inline `<style>` blocks
- Tailwind CSS with custom design system in `wwwroot/css/design-system.css`
- Design system guide (in Russian) at `wwwroot/design-system-guide.md`
- Shared components in `Components/Shared/`: Badge, Button, Modal, ConfirmationModal, NotificationAlert, Pagination, FilterPanel, LoadingIndicator, EmptyState, Tabs, etc.
  Страница-список собирается из них в одном порядке (FilterPanel → LoadingIndicator/EmptyState →
  список → Pagination), вкладки — только `<Tabs>`, диалог — только `<Modal>`, свои спиннеры,
  пустые состояния и `fixed inset-0`-оверлеи не заводить. Подробности — в `design-system-guide.md`.

#### Уведомления — только `<Alert>`

Любое сообщение пользователю (ошибка сохранения, предупреждение по правилам, успех
операции) — это `<Alert Type="error|warning|success|info">`, а не свой `<div>` с
`bg-red-50`. Свои блоки расползлись по семи тонам: где-то `red-*` вместо `error-*`,
где-то `rounded-xl border-2`, где-то вообще бутстраповский `alert alert-danger`,
которого в сборке нет. Параметры: `ShowIcon`, `Icon="fa-skull"`, `Small` (для
плотных панелей листа сыщика), `Actions`, `OnClose`.

- Алерт несёт **собственный** нижний отступ (`mb-3`, `mb-2` у `Small`). Если он не
  нужен — `class="!mb-0"`; просто `mb-0` проиграет, у обоих классов одинаковая
  специфичность. Свой `class` подмешивается к классам компонента, не затирает их.
- **`<ValidationSummary>` классы игнорирует** — Blazor дописывает свой
  `class="validation-errors"` после splat'а переданных атрибутов. Оформление живёт
  в `.validation-errors` в `design-system.css` и повторяет `<Alert Type="error">`.
  Передавать туда утилиты Tailwind бесполезно, это уже проверено пятью формами.
- Панели правил в `Combat/` и `Chase/` (`AttackResultDisplay`, `ChaseActionPanel`)
  тонированные блоки рисуют сами — это содержимое экрана с контролами внутри,
  а не уведомления; на `<Alert>` их не переводили.

#### Page shell — the same on every page

Every routable page is `<PageHeader Title="…">` (with page-level actions in its `Actions` slot)
followed by one `<div class="cm-page">`. `Components/Pages/Home.razor` is the reference; the info
pages, the character sheet and the scenario detail page all use it too. Inside, group with
`cm-section` + `cm-section-title` and `cm-card` + `cm-card-header`/`-body`/`-footer`.

- **One card level, never two.** A `cm-card` inside a `cm-card`, or a grey inset around a table
  that already sits in a card, reads as clutter rather than structure. Separate blocks in one card
  with a rule (`cm-stack`), and render list items as rows
  (`border-t border-t-gray-200 first:border-t-0`), not as mini-cards. A component that always
  renders inside a card must not draw its own — say so in a comment at the top of the file.
- **Never** wrap page content in `max-w-*` + `mx-auto`. `page-with-sidebar` is a column flex
  container, so `mx-auto` on a flex item disables stretch and collapses the page to its content
  width — that is why sparse pages used to render as a narrow centred column.
- Buttons carry meaning by colour: `primary` = the one main action, `secondary` = neutral,
  `outline-primary`/`outline-error` = row edit/delete, `error` = destructive confirmation in a
  dialog, `success` = approving someone's request. `cm-btn-info` is not for buttons.
- Header actions are always `cm-btn-sm` — the topbar is 56px tall.
- `PageHeader` itself appends the Keeper's «Ширма» button (hidden from players) — one more reason
  never to hand-roll a `page-topbar`: such a header would lose the button.
  See `Features/KeeperScreen/CLAUDE.md`.
- Status colours (`--color-success-*`, `--color-warning-*`, `--color-error-*`) are defined in
  **both** `tailwind.config.js` and `:root` in `design-system.css`; keep them in sync, otherwise
  `cm-btn-error` and `<Button Variant="error">` render different reds.
- Full details and the class inventory: `wwwroot/design-system-guide.md`.

### Target device: iPad Pro M2

**The primary device for this app is an iPad Pro M2 — it's what the Keeper actually runs at the table.**
Any UI change must be checked at that viewport in the browser preview before it's called done, not only
at desktop width:

- **Landscape 1366×1024** — the main orientation at the table. Check this one first.
- **Portrait 1024×1366** — Tailwind's `lg:` breakpoint is exactly 1024px, so a three-column
  `lg:col-span-*` grid switches to its widest layout right at portrait width and columns get very tight.
  Verify nothing clips, and that no control ends up narrower than a comfortable tap target.

Practical rules that follow from it:
- Tap targets: buttons and checkboxes need real padding — `cm-btn-sm` is the floor, not `text-xs` bare links.
- Never rely on `title=` tooltips to carry information: there's no hover on a touch screen.
- Wide content (tracks, tables, timelines) scrolls inside its own `overflow-x-auto` container so the page
  body never scrolls sideways.
- Prefer `flex-wrap` on button rows — an unwrapped row of six actions overflows in portrait.

`mcp__Claude_Browser__resize_window` with `{width: 1366, height: 1024}` (and then `1024×1366`) is how to
check this; reset with `preset: "desktop"` when done.

## Circuit State Persistence

Blazor Server keeps page state in a server-side circuit, so a dropped connection, a backgrounded
iPad tab, or a deployment would otherwise wipe whatever the Keeper had on screen. The app opts into
the .NET 10 circuit persistence stack:

- `[PersistentState]` on a **public** property is what gets saved and restored — **и больше ничего**.
  Возобновление не «оживляет» страницу: она собирается заново, `OnInitializedAsync` отрабатывает
  снова, все приватные поля возвращаются к значениям по умолчанию. Любое состояние, которое обязано
  пережить паузу, либо помечено этим атрибутом, либо лежит в адресе (см. ниже) — третьего нет.
  A service opts in by being registered with
  `RegisterPersistentService<T>(RenderMode.InteractiveServer)` in `Program.cs`
  (`CombatService` and `ChaseService` today); компонент — просто публичным свойством с атрибутом
  (`CharacterPage.PersistedDraft`). The getter runs when the circuit is paused, the setter
  when it resumes — expose **one snapshot property** per service rather than marking every field.
- The persisted value must be JSON-serializable: plain POCOs, no cycles, no lazy EF navigations.
- Retention is configured on `CircuitOptions` (`PersistedCircuitInMemoryMaxRetained`,
  `PersistedCircuitInMemoryRetentionPeriod`). That state lives in the server's memory and does **not**
  survive a process restart.
- Surviving a restart relies on pausing circuits *before* shutdown: `ActiveCircuitTracker` asks every
  connected tab to call `Blazor.pauseCircuit()`, which moves the state into the browser. Data
  Protection keys are stored in PostgreSQL, so the new instance can unprotect what the browser sends
  back. .NET 11 replaces this with `Circuit.RequestCircuitPauseAsync`.
- **Подписка на остановку висит на `ApplicationStopping` и оформляется лениво, при первом
  подключившемся circuit — не трогай ни то, ни другое.** Из `IHostedService.StopAsync` просить
  вкладки о паузе поздно: SignalR закрывает все соединения своим обработчиком `ApplicationStopping`
  (`HttpConnectionManager.CloseAllConnections`), который отрабатывает раньше любого `StopAsync`, и
  трекер к тому моменту пуст. Обработчики `CancellationToken` идут в обратном порядке регистрации,
  поэтому наш обязан быть зарегистрирован позже сигналровского — отсюда лень: на момент первого
  `OnConnectionUpAsync` `HttpConnectionManager` уже подписан. Механизм ровно по этой причине
  простоял мёртвым: в логе не появлялось ни строчки, а после деплоя терялось всё.
  Подробности — в комментарии к `ActiveCircuitTracker.EnsureShutdownHook`.
- Client side: `wwwroot/js/circuit-persistence.js` pauses the circuit when the tab has been hidden for
  `PAUSE_AFTER_HIDDEN_MS` (30 с) — или сразу, по `freeze`/`pagehide`, если браузер вот-вот остановит
  на странице JS, — и возобновляет с нарастающей паузой. Порог не опускать обратно к секундам: пауза
  рвёт соединение и поднимает диалог, а заглянуть в соседнюю вкладку и вернуться — обычное дело.
  The (Russian) reconnect dialog is `#components-reconnect-modal` in `App.razor` plus
  `wwwroot/css/reconnect.css` — the `components-reconnect-*` class names come from the framework,
  don't rename them.
- Adding a field to a persisted service's state? Add it to that service's snapshot type as well,
  otherwise it silently disappears on resume.
- **Лог.** Жизнь circuit'ов пишет `ActiveCircuitTracker` (Information, строка на событие): открыт,
  потерял соединение, снова на связи, пауза, закрыт — со временем жизни. Своего события «пауза»
  у `CircuitHandler` нет: её ловит колбэк `RegisterOnPersisting`, который
  `ShutdownPauseCircuitHandler` вешает в области circuit (пререндер живёт в области HTTP-запроса
  и туда не попадает). Категория `Microsoft.AspNetCore.Components.Server.Circuits` остаётся
  заглушённой до Critical. Путь, статус и время каждого запроса — `Microsoft.AspNetCore.Hosting.Diagnostics`
  на Information (по две строки на запрос: начало и конец); по ним видно, какая страница медленная.
- **Приватные поля компонента паузу не переживают** — восстанавливается только `[PersistentState]`,
  а страница собирается заново. Поэтому «что сейчас открыто» (режим просмотра, выбранный элемент,
  активная вкладка) держим в query-строке через `[SupplyParameterFromQuery]`, а не в поле: адрес
  переживает и паузу, и F5, и на него можно дать ссылку. Пример — `ScenarioDetailPage` (`?mode=play`).

### Данные пререндера

Интерактивная страница рендерится дважды: статически (пререндер) и заново в circuit, и
`OnInitializedAsync` без мер читает базу оба раза. Тяжёлые страницы (главная — `HomeCampaignsPanel`,
`ScenarioDetailPage`, `CharacterPage` и её `FellowInvestigatorsPanel`) передают прочитанное в
пререндере тем же `[PersistentState]`, но **с `RestoreBehavior = RestoreBehavior.SkipLastSnapshot`**:
снимок поднимается при старте circuit и **не** поднимается при возобновлении после паузы — за паузу
данные могли устареть, там страница честно перечитывает базу.

- Геттер отдаёт снимок только из статического рендера (`RendererInfo.IsInteractive ? null : …`):
  иначе на каждой паузе в браузер уезжала бы копия, которую всё равно никто не поднимет.
- Снимок — отдельное свойство, не то, что держит правки на паузу: у `CharacterPage` черновик
  (`PersistedDraft`, `SkipInitialValue`) и лист из пререндера (`PrerenderedSheet`) разведены.
- Граф — без циклов EF: плоский DTO (`HomeCampaigns`) или сущности без обратных навигаций
  (`ScenarioPagePrerender`, см. `Scenarios/CLAUDE.md`). Цикл роняет пререндер исключением JSON.
- Снимок едет в HTML страницы (зашифрованным) и обратно по SignalR при старте circuit — в пределах
  `MaximumReceiveMessageSize` (2 МБ). Кандидат в снимок — страница с десятком запросов, а не любая.
- Берётся один раз и только если он от той же сущности (`Id` в снимке сверяется с параметром).

### Design System Colors

- **Primary**: Slate/graphite gray (#64748B) — headings, nav, buttons
- **Secondary**: Warm stone brown (#78716C) — backgrounds, accents
- **Accent**: Muted steel blue (#4B7FAF) — highlights, badges, info
- **Status**: Success (#2C9D49), Warning (#D97706), Error (#C71D20)
- **Fonts**: Inter (primary), Bookman Old Style (serif headings), JetBrains Mono (code)

### Naming Conventions

- Pages: `{Entity}Page.razor`, `{Entity}EditPage.razor`
- Components: `{Entity}Card.razor`, `{Entity}Form.razor`, `{Entity}List.razor`
- Modals: `Add{Entity}Modal.razor`, `Edit{Entity}Modal.razor`

## Authentication (Auth0)

Вход — OIDC (code flow) через Auth0, сессия — своя кука `.CampaignManager.Auth`; к Auth0 приложение
ходит только при входе и выходе. Способы входа — коннекшены тенанта: `google-oauth2` (со своими
ключами Google, не dev-ключами Auth0) и `Username-Password-Authentication` с **выключенной**
регистрацией — такие учётки заводит администратор через `auth0` CLI. Тенант один на dev и прод
(база у них общая, пользователи тоже), приложений в нём два: dev (`https://localhost:8080`) и прод
(`https://cthulhu.dmnet.dev`). У каждого в Allowed Callback URLs — `/signin-oidc`, в Allowed Logout
URLs — `/signout-callback-oidc`.

Домен входа — кастомный `auth.cthulhu.dmnet.dev` (CNAME в DNS dmnet.dev на Porkbun, сертификат
выпускает Auth0, в тенанте он домен по умолчанию); его и пишем в `Authentication:Auth0:Domain`.
Каноничный `cthulhu-dmnet.eu.auth0.com` остаётся за CLI. Google-клиент «Campaign manager» живёт в
проекте `dnd-project-371311`: в его redirect URIs — `/login/callback` обоих доменов Auth0, а само
приложение Google обязано быть **In production** — в Testing через Google входят только test users.
Redirect URI и публикацию Google меняют только в консоли: у `gcloud` для обычных OAuth-клиентов
команд нет (`gcloud iam oauth-clients` — это Workforce Identity, другое).

- **Все права держатся на почте** (белый список, `AdminEmails`, `KeeperEmail`, `PlayerEmail`…),
  поэтому проверку `email_verified` в `OnTokenValidated` не убирать никогда: учётка с паролем на
  чужой адрес иначе унаследует чужие кампании. Тестовым пользователям `email_verified: true`
  ставится при создании.
- `OnTokenValidated` пересобирает принципал из `ClaimTypes.Email`/`Name`/`NameIdentifier`
  (`MapInboundClaims = false`): так же были устроены куки прямого входа через Google, и они
  продолжают работать без перелогина. Новый claim из ID token в куку сам не попадёт — его надо
  добавить там же.
- `ResponseMode = Query`, а не `form_post` по умолчанию: куки корреляции и nonce — `Lax`, а
  `form_post` — кросс-сайтовый POST с домена Auth0, на который браузер их не отправит.
- **Автовход идёт через Google, а не через `prompt=none`.** Тихий вход Auth0 проверяет только
  сессию самого Auth0 (три дня без активности) и к Google за ней не ходит — после переезда он почти
  всегда кончался кнопкой «Войти». Браузер помнит способ прошлого входа (кука
  `.CampaignManager.LastLogin` на год: способ и почта, не сессия), и загрузку страницы без сессии
  middleware `UseAutoLogin` уводит в Auth0 с `connection=google-oauth2` и `login_hint` — Google
  возвращает обратно без единого клика. Попытка одна на сессию браузера (`.CampaignManager.AutoLogin`),
  выход куку забывает. Подробности — `Utilities/Authorization/AutoLogin.cs`.
- `prompt` не шлём вовсе: сменить учётку позволяет выход, он гасит и сессию Auth0.
  `/api/account/login?method=google|email` ведёт мимо страницы Auth0 прямо к Google или к форме
  пароля; `connection` и `login_hint` в запрос к Auth0 ставит `OnRedirectToIdentityProvider`.
- На `localhost` Auth0 всегда спрашивает согласие («Authorize App → Accept») — это его правило для
  локальных адресов, на проде экрана нет.
- Выход гасит и сессию Auth0 (`/oidc/logout` с `client_id`), иначе следующий вход молча пускает
  под прежней учёткой и переключиться между тестовыми пользователями нельзя.
- Страница входа Auth0 — внешний сайт: агент в браузере пароли туда не вводит. Проверки под
  разными пользователями делает человек или уже залогиненная вкладка.

## Configuration

- `ConnectionStrings:DefaultConnection` — PostgreSQL
- `Authentication:Auth0:Domain` / `ClientId` / `ClientSecret` — Auth0 (see "Authentication" above)
- Npgsql configured with dynamic JSON support and legacy timestamp behavior
- SignalR: 2MB message size limit, 15-buffer capacity, 30s handshake timeout
- Blazor Server: 20 max buffered render batches

## External Services

- **Russian localization**: `EnumExtensions.ToRussianString()` for weapon types, creature types, skill categories

## Key Files

- `CampaignManager.Web/Program.cs` — DI, auth, middleware, SignalR config
- `CampaignManager.Web/Utilities/DataBase/AppDbContext.cs` — Entity configuration, JSONB mappings
- `CampaignManager.Web/Utilities/DataBase/AppIdentityDbContext.cs` — Identity schema
- `CampaignManager.Web/Model/BaseDataBaseEntity.cs` — Base entity with Guid v7
- `CampaignManager.Web/Components/_Imports.razor` — Global using directives
- `CampaignManager.Web/Components/Features/` — All feature vertical slices

## Documentation Conventions

This file is for conventions and patterns that apply across the whole app. **Feature-specific knowledge (a feature's services, models, entity relationships, cross-feature dependencies, or deviations from the patterns above) belongs in that feature's own `Components/Features/{FeatureName}/CLAUDE.md`, not here.**

- Every feature under `Components/Features/` has a `CLAUDE.md` — it loads automatically only when you're working with files under that feature's directory.
- Adding a feature-specific gotcha, model, or service to this root file instead of the feature's own file is the failure mode this rule exists to prevent — it bloats every session's context regardless of which feature is being touched.
- When a feature gains a new service, model, or non-obvious relationship, update that feature's `CLAUDE.md`, not this one. Create the feature's `CLAUDE.md` if it doesn't exist yet.
- When adding a brand-new feature folder, give it a `CLAUDE.md` from the start.
