# CampaignManager

Веб-приложение для управления кампаниями настольной ролевой игры **Call of Cthulhu 7e**. Позволяет вести кампании, создавать и редактировать персонажей, сценарии, существ, предметы, оружие, заклинания и навыки.

## Технологии

- **.NET 10** — Blazor Server с InteractiveServer render mode
- **PostgreSQL** — база данных, EF Core с JSONB для хранения сложных структур
- **Tailwind CSS 3** — стилизация с кастомной дизайн-системой
- **Google OAuth** — аутентификация
- **.NET Aspire** — оркестрация сервисов
- **Minio** — S3-совместимое хранилище файлов
- **QuestPDF** — генерация PDF-листов персонажей
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

## Структура проекта

| Проект | Описание |
|---|---|
| **CampaignManager.Web** | Основное Blazor Server приложение |
| **CampaignManager.AppHost** | .NET Aspire оркестрация |
| **CampaignManager.ServiceDefaults** | Общая конфигурация (OpenTelemetry, health checks, resilience) |

Архитектура — feature-based vertical slicing: `Components/Features/{Feature}/{Components,Models,Pages,Services}/`.

## Запуск

### Требования

- .NET 10 SDK
- Node.js (для Tailwind CSS)
- PostgreSQL

### Конфигурация

Задайте переменные окружения или используйте `appsettings.json`:

- `ConnectionStrings:DefaultConnection` — строка подключения к PostgreSQL
- `Authentication:Google:ClientId` / `ClientSecret` — Google OAuth

### Команды

```bash
# Запуск приложения
dotnet run --project CampaignManager.Web

# Запуск через .NET Aspire
dotnet run --project CampaignManager.AppHost

# Сборка
dotnet build

# Миграции базы данных
dotnet ef migrations add <Name> --project CampaignManager.Web --context AppDbContext
dotnet ef database update --project CampaignManager.Web --context AppDbContext

dotnet ef migrations add <Name> --project CampaignManager.Web --context AppIdentityDbContext
dotnet ef database update --project CampaignManager.Web --context AppIdentityDbContext
```

Tailwind CSS компилируется автоматически при сборке.

## Деплой

Docker-образ собирается и публикуется в GitHub Container Registry через CI/CD pipeline (`.github/workflows/docker-build-deploy.yml`). Автоматический деплой на сервер при push в `master`.

```bash
docker pull ghcr.io/shman4ik/campaign-manager:latest
docker run -p 8080:8080 ghcr.io/shman4ik/campaign-manager:latest
```

## API

Swagger UI доступен по адресу `/swagger`.

## Правовая информация

**Call of Cthulhu** является торговой маркой Chaosium Inc. Данный проект — неофициальный фанатский инструмент, не связанный с Chaosium Inc. и не одобренный ими. Подробнее см. [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

## Лицензия

[GNU Affero General Public License v3](LICENSE)
