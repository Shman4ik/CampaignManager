# Characters (Server) — лист сыщика

Модуль T2.3: `CharactersModule` (`AddCharactersModule`/`MapCharactersApi`), `CharacterService`. Маршруты, DTO и
`ICharactersApi` — `Contracts/Characters`, клиент — `ApiClient/Characters`, страница —
[UI/Characters/CLAUDE.md](../../CampaignManager.UI/Characters/CLAUDE.md). Таблица — `characters` из T1.2; миграций
модуль не добавил. Создание листов, помощник, библиотека НПС, бронь прегенов — T2.4/T2.5.

## API

| Метод и адрес | Что | Права (`AccessPolicy`) |
|---|---|---|
| `GET /api/v1/characters/{id}` | лист: документ, вид, статус, версия, портрет, игрок, кампания и её эпоха, флаги; `ETag` = версия, `no-store` | `ForCharacterAsync` Read |
| `PUT /api/v1/characters/{id}/sheet` | документ целиком, `If-Match` | `ForCharacterAsync` Edit |
| `PUT /api/v1/characters/{id}/portrait` | `{ fileId }` (строка `files`; null — убрать), `If-Match` | `ForCharacterAsync` Edit |
| `PUT /api/v1/characters/{id}/status` | `{ status }`, `If-Match` | `ForCharacterAsync` Edit |
| `GET /api/v1/characters/{id}/party` | соседи по столу для «Знакомых сыщиков» | `ForCharacterAsync` Read |
| `GET /api/v1/campaigns/{id}/investigators` | активные сыщики кампании с листами — групповая проверка ширмы | `ForCampaignAsync` Edit (Хранитель кампании) |

Записи отвечают `{ version, updatedAt }` — новая версия для следующего `If-Match`.

## Правила

- **«Нет листа» и «нет доступа» — один 404** (`AccessPolicy`, знание v1): перебирать чужие листы нельзя. Сосед по
  столу чужой лист тоже не читает — только имя, профессию и игрока через `party`.
- **Каждая запись — с версией** (`HttpIfMatch.Version`, общий с справочниками): без заголовка — 428, чужая —
  409 `stale` до записи, гонка — `xmin` в самой записи (`OriginalValue` = присланная версия, тоже 409). В v1
  автосохранение одной вкладки молча затирало другую (AUDIT, «Ошибки», 8). Портрет и статус — тоже с версией:
  запись колонки сдвигает `xmin`, и без проверки устаревший документ с другого устройства прошёл бы следом.
- **Документ проверяется**: длина имени и граф, размер списков, **ссылки на навыки** (`skillId`, `parentSkillId`,
  навык оружия) — только существующие в `cm.skills` (лист без справочника не читается). Пишется
  `CmJson.Write` + `sheet_version = CurrentVersion`; незнакомые поля старого/нового клиента переживают запись
  (`[JsonExtensionData]`, тест `Unknown_fields_of_the_document_survive_save`).
- **Игрок, портрет, владелец, кампания — колонки, не документ**: запись листа их не трогает. Имя игрока —
  `PublicNames.Of(псевдоним в кампании, имя профиля)`, почты нет нигде (в v1 «Сохранить» затирало его).
- **Второй активный лист игрока в кампании** — 409 `conflict` с текстом (частичный уникальный индекс
  `characters_one_active_sheet`), а не 500.
- Эпоха в DTO — эпоха кампании (фаза развития пересчитывает деньги по столбцу таблицы II); без кампании — классика.

## Тесты

`Server.Tests/Characters/CharactersApiTests` на `CampaignsApp` (своя база со схемой `cm`): чтение владельцем и
Хранителем, 404 соседу и постороннему, ETag, запись с версией и неизменный владелец и игрок, конфликт двух
устройств, 428 без версии, 400 на неизвестный навык, незнакомые поля, преген только для чтения (403), соседи по
столу, сыщики кампании только Хранителю, второй активный лист, портрет.
