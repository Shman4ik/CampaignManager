# Encounters (Server) — сцены: бой и погоня

Модуль T2.6a: `EncountersModule` (`AddEncountersModule`/`MapEncountersApi`), `EncounterService`. Маршруты, DTO и
`IEncountersApi` — `Contracts/Encounters`, клиент — `ApiClient/Encounters`, ядро — `Core/Encounters`
([Core/CLAUDE.md](../../CampaignManager.Core/CLAUDE.md), «Ядро сцены»), страницы — [UI/Encounters/CLAUDE.md](../../CampaignManager.UI/Encounters/CLAUDE.md).
Таблица — `encounters` из T1.2 (`state` jsonb + `state_version`, `xmin`, частичный уникальный индекс
`encounters_one_active_combat` — миграция `ChaseManyActive`, 2026-10-02; до неё `encounters_one_active` держал и погони).

## API

| Метод и адрес | Что | Права (`AccessPolicy`) |
|---|---|---|
| `GET /api/v1/encounters?kind=` | активные сцены вошедшего (раунд, участников, кампания) — «продолжить» | только свои; не Хранителю — пусто |
| `POST /api/v1/encounters` | `{ kind, campaignId? }` → 201 `EncounterDto`; второй активный **бой** в той же кампании — 409 `conflict`; погонь — сколько угодно | `CanStartEncounterAsync` (Хранитель; в кампании — её Хранитель) |
| `GET /api/v1/encounters/{id}` | документ, версия, кампания; `ETag`, `no-store` | `ForEncounterAsync` Read (ведущий, администратор) |
| `PUT /api/v1/encounters/{id}/state` | документ целиком, `If-Match` → `{ version, updatedAt }` | `ForEncounterAsync` Edit |
| `POST /api/v1/encounters/{id}/finish` | статус `Finished`, `If-Match` | `ForEncounterAsync` Edit |

## Правила

- **Сервер правил сцены не исполняет.** Их исполняет Core на клиенте (`EncounterEngine`, `EncounterQueue`), сервер
  хранит документ и проверяет пределы: ≤ `EncounterEngine.MaxParticipants` участников, ≤ `MaxLogEntries` записей журнала,
  разные id участников, **один лист — один участник**, документ до `EncounterLimits.MaxStateBytes`; у погони (T2.6c) —
  трасса до `ChaseRules.MaxLocations`, каждый бегущий — участник сцены и один раз. Чужой лист в документе
  ничего не даёт: запись итогов в лист идёт через API листа со своими правами.
- **Запись с версией** — тот же приём, что у листа: без `If-Match` — 428, чужая версия — 409 `stale` до записи, гонка —
  `xmin` в самой записи. Сцену ведут с двух устройств (iPad за столом и ноутбук) — молча не затираем.
- **Один активный бой на кампанию** (и один вне кампаний — `NULLS NOT DISTINCT`): индекс базы
  `encounters_one_active_combat` (`where status = 'Active' and kind = 'Combat'`), отказ — 409 с текстом, клиент открывает
  идущий. **Погонь — сколько угодно** (решение владельца 2026-10-02): разделившихся ведут отдельными погонями (стр. 142), в
  сервисе проверки нет — только индекс. Завершённую не пишут (409 `conflict`) и не возобновляют; место под новый бой освобождается.
- Строка «идут сейчас» несёт имена участников (`ParticipantNames`) — две погони одной кампании различают по тому, кто бежит.
- Список «идут сейчас» — только сцены вошедшего, администратор чужих там не видит (по `GET {id}` прочитать может).
- В v1 бой в базу не писался вовсе (жил в circuit), погоня писала снимок после каждого рендера вместе с полными листами
  и картинками тварей. Здесь документ — снимок чисел, без листов и статблоков.

## Тесты

`Server.Tests/Encounters/EncountersApiTests` на `CampaignsApp`: один активный бой на кампанию (и вне кампаний), две погони в
одной кампании со своими документами (`Two_chases_run_in_one_campaign_combat_stays_single`), права (игрок, чужой
Хранитель), конфликт двух вкладок (409 `stale`), 428 без версии, завершённую не пишут, перезагрузка посреди сцены
(участники, очередь, предложенный результат и неписанные в лист эффекты — из базы), запись итогов в лист через API листа,
**лист изменён на другом устройстве между чтением и записью** (409 → перечитать и применить поверх: оба изменения в
листе, а старая вкладка игрока получает свой 409), запись без прав — «заблокирована», невалидное состояние — 400 (в том
числе бегущий погони без участника), погоня переживает перезагрузку, урон помехи доходит до листа
(`Chase_survives_reload_and_hazard_damage_reaches_the_sheet`).
Правило — в `AccessPolicyTests` (`Encounter_is_its_keepers`, `Encounter_in_campaign_is_started_by_its_keeper`).
