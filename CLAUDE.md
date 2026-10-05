# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

CampaignManager — менеджер игр Call of Cthulhu 7e: кампании и журнал, лист сыщика, помощник создания, сценарии с режимом
игры, бой и погоня, ширма, фонотека, справочники. **.NET 10, Blazor WebAssembly поверх HTTP API (`/api/v1`), PostgreSQL
(Neon) через EF Core, Auth0.** Прод — https://cthulhu.dmnet.dev (отдельного beta-стенда нет — погашен 2026-10-05).

Это «2.0»: первая версия (Blazor Server, `CampaignManager.Web`) работала до 2026-10-04 и удалена из репозитория в T3.3
(история — в git и `docs/v2/`). Схем v1 (`games`, `identity`) в нынешней базе нет: в
Neon-проект на PostgreSQL 17 (T3.4) переехала только `cm`.

Где что:
- **Правила кода** — [src/CLAUDE.md](src/CLAUDE.md) (проекты, база, тесты, вход под ролями, запуск, деплой, памятка
  исполнителя) и `CLAUDE.md` каждого модуля (`src/CampaignManager.{Server,UI}/<Модуль>/CLAUDE.md`, `Core`, `tools/…`).
- **Дизайн** — [docs/design-system.md](docs/design-system.md) (композиция, ревью по скриншоту, правила после аудита) и
  скилл `campaign-manager-design`; UI-кит и раскладки — [src/CampaignManager.UI/CLAUDE.md](src/CampaignManager.UI/CLAUDE.md).
- **План и история 2.0** — [docs/v2/README.md](docs/v2/README.md), [TASKS.md](docs/v2/TASKS.md),
  [ORCHESTRATOR.md](docs/v2/ORCHESTRATOR.md) (как вести карточки через исполнителей), [CUTOVER.md](docs/v2/CUTOVER.md).

## Development Commands

```bash
dotnet run --project src/CampaignManager.Server                # https://localhost:8080 (launch.json: "v2")
dotnet build CampaignManager.slnx -c Release -warnaserror        # как в CI
dotnet test                                                      # тесты с базой — при заданной CM_TEST_DB (src/CLAUDE.md, «Тесты»)
dotnet ef migrations add <Name> --project src/CampaignManager.Data
dotnet ef database update --project src/CampaignManager.Data     # строка — из CM_DB
```

Tailwind собирается MSBuild-таргетом `UI` (`npm ci` + `npx`), поэтому сборке нужен Node.js. Вход без Auth0 в
Development — `/dev/login?as=player|keeper|admin` (src/CLAUDE.md, «Проверка под ролями»).

## Данные и деплой

- **Neon:** проект `CampaignManager` (`jolly-hill-30043612`, PostgreSQL 17, aws-eu-central-1, с 2026-10-04); ветка
  `main` — прод, `dev` — локальная разработка (пароль роли на ветках разный). В прод не писать из разработки;
  проверять на своей копии `dev` (`pg_dump --schema=cm` в контейнер `wslc`). Старый проект `old-wood-199224` (PG 15) —
  только откат до 2026-10-18 (`docs/v2/TASKS.md`, T3.4).
- **Push в `master`, задевший `src/**`,** выкатывает **прод** (`.github/workflows/v2-deploy.yml` → тег в
  `Shman4ik/dmnet-gitops` → Argo CD). Миграции сервер не применяет: новую миграцию накатить **до слияния на `dev` и
  `main`**. Подробности и откат — src/CLAUDE.md, «Деплой». Бета-стенда нет: проверять до слияния — локально и на своей копии `dev`.
- Кластер k3s на VPS: `ssh vps` — через Windows OpenSSH (`C:\Windows\System32\OpenSSH\ssh.exe`), `KUBECONFIG=/etc/rancher/k3s/k3s.yaml`.
  Секреты — SealedSecret (`kubeseal --raw` на VPS), значения не печатать.

## Pull requests

Обычно задача — **один PR** от `master`, сразу готовый к ревью, не черновик (draft).

Для **очень крупной** работы, которую тяжело ревьюить одним куском, есть
[stacked pull requests](https://docs.github.com/en/pull-requests/how-tos/stacked-pull-requests)
GitHub: каждый слой — своя ветка и свой PR поверх слоя ниже. Управляет ими расширение `gh stack`
(уже стоит на машине владельца): `gh stack init --base master <ветки…>`,
`gh stack submit --auto` (без `--auto` откроется интерактивный редактор), `gh stack merge <PR>`.
Это возможность, а не правило: без явной нужды стек не заводить. `gh stack submit` создаёт PR
черновиками — после него `gh pr ready <номер>`.

- CI на PR — `.github/workflows/ci.yml`: на каждый PR, включая верхние слои стека (их база —
  ветка ниже, поэтому фильтра по базе у воркфлоу нет), гоняет
  `dotnet build CampaignManager.slnx -c Release -warnaserror` с включённым NuGet Audit и
  `dotnet test`. Новый тестовый проект достаточно добавить в `.slnx` — воркфлоу не трогать.
  В job поднят `postgres:17`, строка подключения — в `CM_TEST_DB`.
- CI проверяет только сборку и тесты: изменения UI перед PR проверяются в браузере на вьюпортах ниже, по чек-листу
  «Ревью по скриншоту» (`docs/design-system.md`), итог — в описании PR.
- Ветку, которая checked out в worktree, не сливать с `--delete-branch` из основного чекаута: `gh` пытается удалить
  этот worktree и сносит его файлы.

### Целевые устройства: iPad Pro 11" и iPhone (решение владельца 2026-10-03)

**Все страницы оптимизируются под устройства владельца** — их замерил он сам (whatismyviewport.com).

| Устройство | Экран, CSS px | Вьюпорт браузера (что проверять) | Колонка страницы с рельсом |
|---|---|---|---|
| **iPad Pro 11" (M2), ландшафт** — главный, за столом | 1194×834, DPR 2 | **1194×696** (Chrome: панели ~137px) | ~1064px |
| **iPad Pro 11", портрет** | 834×1194 | **834×1056** (Chrome; Telegram — 834×1094) | ~704px |
| **iPhone** (страницы игрока) | 393×852, DPR 3 | **393×651** (Safari с нижней панелью) | ~361px, рельса нет |

- Tailwind: портрет iPad — между `md` (768) и `lg` (1024), ландшафт — выше `lg`. Сетка `lg:*` в портрете **не**
  включается — портрету нужна своя раскладка (`md:`), а не «столбик как на телефоне».
- Раскладку внутри страницы лучше вести от ширины колонки (`@container`), а не окна: рельс и поля съедают её
  по-разному. Опорные ширины — последний столбец таблицы.
- Высота мала: в ландшафте под шапкой ~620px. Частые действия не должны уезжать за низ экрана.
- **Тап-цели — правило владельца 2026-10-03:** кнопка, поле, отметка — **от 32px**; строка плотного списка, которая
  целиком кнопка (навык в «Игре»), — **от 28px**.
- Never rely on `title=` tooltips to carry information: there's no hover on a touch screen.
- Wide content (tracks, tables, timelines) scrolls inside its own `overflow-x-auto` container so the page
  body never scrolls sideways.
- Prefer `flex-wrap` on button rows — an unwrapped row of six actions overflows in portrait.
- **Каждый список и выбор — целиком:** пролистать до последней записи и сверить число строк с источником (вопрос 13
  чек-листа «Ревью по скриншоту»).

Проверка — `mcp__Claude_Browser__resize_window` на `{width: 1194, height: 696}`, затем `834×1056` и (страницы игрока)
`393×651`; в эмуляции Chromium рисует полосу прокрутки (−15px ширины), на iPad её нет. В конце — `preset: "desktop"`.
При нескольких параллельных агентах встроенная панель часто не отдаёт скриншоты — тогда свой headless Chrome через
`puppeteer-core` (`executablePath` — установленный Chrome); сервер открывать по `127.0.0.1`, не `localhost`.

## Authentication (Auth0)

Вход — OIDC (code flow) через Auth0, сессия — своя кука `.CampaignManager.Auth`; к Auth0 приложение ходит только при
входе и выходе. Как это устроено в коде — [src/CampaignManager.Server/Identity/CLAUDE.md](src/CampaignManager.Server/Identity/CLAUDE.md).

- **Тенант один** на dev и прод, приложений в нём два: dev (`https://localhost:8080`) и прод
  (`https://cthulhu.dmnet.dev`). У каждого в Allowed Callback URLs — `/signin-oidc`, в Allowed Logout URLs —
  `/signout-callback-oidc`. Коннекшены: `google-oauth2` (со своими ключами Google) и `Username-Password-Authentication`
  с **выключенной** регистрацией — такие учётки заводит администратор через `auth0` CLI.
- **Домен входа** — кастомный `auth.cthulhu.dmnet.dev` (CNAME в DNS dmnet.dev на Porkbun, сертификат выпускает Auth0); его
  и пишем в `Authentication:Auth0:Domain`. Каноничный `cthulhu-dmnet.eu.auth0.com` остаётся за CLI.
- **Google-клиент** «Campaign manager» — проект `dnd-project-371311`: в redirect URIs — `/login/callback` обоих доменов
  Auth0; приложение Google обязано быть **In production** (в Testing входят только test users). Redirect URI и публикацию
  меняют только в консоли.
- **Все права держатся на почте** (белый список, `Authorization:AdminEmails`), поэтому проверку `email_verified` не
  убирать никогда: учётка с паролем на чужой адрес иначе унаследует чужие кампании.
- **Страница входа Auth0 — в репозитории, `tools/auth0/`**: шаблон, тема, тексты и `apply.sh`, который приводит тенант к
  этим файлам (правка в дашборде затрётся). Картинка и знак — `src/CampaignManager.Server/wwwroot/img/auth/`, страница
  Auth0 берёт их с домена прода. Тенант общий: `apply.sh` меняет страницу входа dev и прода разом.
- На `localhost` Auth0 всегда спрашивает согласие («Authorize App → Accept») — это его правило для локальных адресов.
- Страница входа Auth0 — внешний сайт: агент в браузере пароли туда не вводит. Проверки под ролями — `/dev/login`.
- **Агенты ходят в API токеном M2M** (client credentials): API тенанта `https://cthulhu.dmnet.dev/api`, приложение
  «CampaignManager agents», токен — с домена входа `auth.cthulhu.dmnet.dev` (иначе `iss` не совпадёт). Работает от имени
  почты из `Authorization:MachineClients`, только по scope'ам (`scenarios:write`, `files:write`), остальное — 403.
  Секрет — у владельца в `~/.config/campaign-manager/agent.env`. Подробно —
  [src/CampaignManager.Server/Identity/CLAUDE.md](src/CampaignManager.Server/Identity/CLAUDE.md), «Токены агентов».

## Работа в контейнере Claude Code on the web

В удалённом контейнере .NET SDK по умолчанию нет, а `dot.net` / `builds.dotnet.microsoft.com`
закрыты egress-политикой (`curl` получает 403 от прокси) — скрипт `dotnet-install.sh` там не качается.
Ставить надо из репозитория Ubuntu, где .NET 10 уже есть:

```bash
apt-get update                                  # без этого dotnet-sdk-10.0 не виден
DEBIAN_FRONTEND=noninteractive apt-get install -y dotnet-sdk-10.0
dotnet --version
```

`apt-get update` ругается на недоступные PPA (deadsnakes, ondrej) — это не мешает. `nuget.org` доступен, поэтому
`dotnet restore` и `dotnet tool install` работают; `npm` для Tailwind — тоже.

PostgreSQL 16 в контейнере уже установлен, только не запущен — для тестов с базой:

```bash
service postgresql start
su postgres -c "psql -c \"ALTER USER postgres PASSWORD 'postgres';\""
export CM_TEST_DB="Host=localhost;Port=5432;Database=cm_test;Username=postgres;Password=postgres"
dotnet test
```

Запуск сервера без Auth0: в Development он стартует и без его настроек (схем OIDC нет, вход — `/dev/login`):

```bash
ASPNETCORE_ENVIRONMENT=Development ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=cm;Username=postgres;Password=postgres" \
  dotnet run --project src/CampaignManager.Server --no-launch-profile --urls https://127.0.0.1:5199
```

## Documentation Conventions

Этот файл — для общего по всему репозиторию. **Знание модуля (его сервисы, модели, связи, отступления от общих правил)
живёт в `CLAUDE.md` этого модуля**, общие правила кода — в [src/CLAUDE.md](src/CLAUDE.md).

- У каждого модуля сервера и UI есть свой `CLAUDE.md` — он подгружается, только когда работа идёт в его папке.
- Новый модуль получает `CLAUDE.md` сразу; новое правило модуля пишется туда, а не сюда.
- Общие документы плана (`docs/v2/TASKS.md`, `README.md`) правит оркестратор после слияния (`docs/v2/ORCHESTRATOR.md`).
