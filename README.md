# CampaignManager

Веб-приложение для ведения игр **Call of Cthulhu 7e**: кампании и журнал встреч, лист сыщика с проверками по правилам
книги, помощник создания сыщика, сценарии с режимом игры, бой и погоня, ширма Хранителя, фонотека и справочники
(бестиарий, оружие, предметы, заклинания, книги, навыки, профессии). Работает на https://cthulhu.dmnet.dev и
рассчитан на iPad за игровым столом и телефон игрока.

## Технологии

- **.NET 10** — Blazor WebAssembly поверх HTTP API (`/api/v1`), ASP.NET Core minimal APIs
- **PostgreSQL** (Neon) — EF Core, схема `cm`; документы листа, статблоков и сцен — JSONB с версией
- **Tailwind CSS 4** — дизайн-система [docs/design-system.md](docs/design-system.md)
- **Auth0** (OpenID Connect) — вход через Google, passkey или почту с паролем
- **MinIO** — S3-совместимое хранилище картинок и музыки

## Структура репозитория

| Папка | Что там |
|---|---|
| `src/` | приложение: `Core` (правила книги, документы), `Contracts`, `ApiClient`, `Data` (EF, схема `cm`), `Server` (API и хост), `UI` (Razor-компоненты), `Web.Client` (хост WebAssembly) |
| `tests/` | `Core.Tests` (правила книги), `Server.Tests` (API на Postgres), `UI.Tests` (bUnit), `Migrate.Tests` |
| `tools/` | `CampaignManager.Migrate` — перенос данных первой версии в схему `cm`; `auth0/` — страница входа Auth0 |
| `docs/` | дизайн-система (`design-system.md`), план и история 2.0 (`v2/`), исходники логотипа (`assets/`) |

Правила работы с кодом — [src/CLAUDE.md](src/CLAUDE.md) и `CLAUDE.md` модулей.

## Запуск

Нужны .NET 10 SDK, Node.js (Tailwind собирается при сборке) и PostgreSQL 15+.

Настройки — `src/CampaignManager.Server/appsettings.Development.json` (не в git) или переменные окружения:
`ConnectionStrings:DefaultConnection`, `Authentication:Auth0:{Domain,ClientId,ClientSecret}`,
`Minio:{Endpoint,AccessKey,SecretKey,BucketName}`.

```bash
dotnet run --project src/CampaignManager.Server                 # https://localhost:8080; /dev/login?as=keeper — вход без Auth0 в Development
dotnet build CampaignManager.slnx -c Release -warnaserror
dotnet test                                                     # тесты с базой — при заданной CM_TEST_DB
dotnet ef migrations add <Name> --project src/CampaignManager.Data
dotnet ef database update --project src/CampaignManager.Data   # строка — из CM_DB
```

## Деплой

На каждый PR CI собирает решение и гоняет тесты (`.github/workflows/ci.yml`). Push в `master`, задевший `src/**`,
собирает образ `ghcr.io/shman4ik/campaign-manager-v2` и коммитит тег в GitOps-репозиторий — на прод
(https://cthulhu.dmnet.dev): `.github/workflows/v2-deploy.yml`. Миграции
применяются вручную до слияния — см. [src/CLAUDE.md](src/CLAUDE.md), «Деплой».

## Правовая информация

**Call of Cthulhu** является торговой маркой Chaosium Inc. Данный проект — неофициальный фанатский инструмент, не связанный с Chaosium Inc. и не одобренный ими. Подробнее см. [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

## Лицензия

[GNU Affero General Public License v3](LICENSE)
