# CampaignManager

Веб-приложение для управления кампаниями настольной ролевой игры **Call of Cthulhu 7e**. Позволяет вести кампании, создавать и редактировать персонажей, сценарии, существ, предметы, оружие, заклинания и навыки.

## Технологии

- **.NET 10** — Blazor Server с InteractiveServer render mode
- **PostgreSQL** — база данных, EF Core с JSONB для хранения сложных структур
- **Tailwind CSS 3** — стилизация с кастомной дизайн-системой
- **Auth0** (OpenID Connect) — аутентификация: Google-аккаунт или почта с паролем
- **Minio** — S3-совместимое хранилище файлов
- **OpenTelemetry** — observability

## Возможности

- **Кампании** — создание и управление игровыми кампаниями
- **Персонажи** — полное создание и редактирование по правилам CoC 7e
- **Бестиарий** — каталог существ с характеристиками и способностями
- **Сценарии** — дизайн и организация сценариев
- **Боевая система** — трекинг боевых столкновений
- **Погони** — механика сцен преследования
- **Предметы, Оружие, Заклинания, Навыки** — справочники игровых сущностей
- **NPC** — управление неигровыми персонажами

## CampaignManager 2.0

Приложение переписывается: новая схема базы, Blazor WebAssembly поверх HTTP API, позже — мобильное
приложение на Avalonia. Текущая версия (v1) заморожена до переключения. План, решения и задачи —
[docs/v2/README.md](docs/v2/README.md).

## Структура репозитория

| Папка | Что там |
|---|---|
| `CampaignManager.Web/` | v1 — Blazor Server приложение; фичи в `Components/Features/{Feature}/` |
| `CampaignManager.ServiceDefaults/` | OpenTelemetry и health checks для v1 (в 2.0 уходит) |
| `src/` | проекты 2.0 (появятся с задачи T1.1) |
| `tests/` | тесты: `CampaignManager.Rules.Tests` — правила книги на коде v1 |
| `docs/` | план 2.0 (`v2/`), гайд дизайн-системы (`design-system.md`), исходники логотипа (`assets/`) |
| `scripts/` | вспомогательные скрипты v1 |

## Запуск

### Требования

- .NET 10 SDK
- Node.js (для Tailwind CSS)
- PostgreSQL

### Конфигурация

Задайте переменные окружения или используйте `appsettings.json`:

- `ConnectionStrings:DefaultConnection` — строка подключения к PostgreSQL
- `Authentication:Auth0:Domain` / `ClientId` / `ClientSecret` — приложение Auth0 (Regular Web Application)

### Команды

```bash
# Запуск приложения
dotnet run --project CampaignManager.Web

# Сборка и тесты
dotnet build
dotnet test

# Миграции базы данных
dotnet ef migrations add <Name> --project CampaignManager.Web --context AppDbContext
dotnet ef database update --project CampaignManager.Web --context AppDbContext

dotnet ef migrations add <Name> --project CampaignManager.Web --context AppIdentityDbContext
dotnet ef database update --project CampaignManager.Web --context AppIdentityDbContext
```

Tailwind CSS компилируется автоматически при сборке.

## Деплой

На каждый PR CI собирает решение и гоняет тесты (`.github/workflows/ci.yml`). При push в `master` Docker-образ публикуется в GitHub Container Registry, а тег коммитится в GitOps-репозиторий (`.github/workflows/docker-build-deploy.yml`).

```bash
docker pull ghcr.io/shman4ik/campaign-manager:latest
docker run -p 8080:8080 ghcr.io/shman4ik/campaign-manager:latest
```

## Правовая информация

**Call of Cthulhu** является торговой маркой Chaosium Inc. Данный проект — неофициальный фанатский инструмент, не связанный с Chaosium Inc. и не одобренный ими. Подробнее см. [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

## Лицензия

[GNU Affero General Public License v3](LICENSE)
