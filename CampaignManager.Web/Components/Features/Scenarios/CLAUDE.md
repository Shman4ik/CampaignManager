# Scenarios Feature

A prepared adventure/one-shot: optionally linked to a `Campaign` (`CampaignId` is nullable — scenarios can exist standalone).

## Key Services
- `ScenarioService(dbContextFactory, IMemoryCache, CampaignService campaignService, logger)` — the only feature service with a direct dependency on another feature's service (`CampaignService`).
- `ScenarioImportService(ScenarioService, CharacterService, logger)` — перенос сценария целиком
  одним JSON-файлом. Ничего не пишет в базу сам: только через два сервиса выше, поэтому права,
  валидация и `Init()` отрабатывают как при ручном вводе.

## Импорт / экспорт JSON

Заполнять полтора десятка локаций и десяток листов НПС по одной форме — часы кликов, а текст
сценария всё равно готовится снаружи. Поэтому есть обмен целым файлом:

- **Импорт** — кнопка «Импорт JSON» на `/scenarios` (`ScenarioImportModal`). Всегда создаёт
  **новый** сценарий: повторный запуск того же файла даёт второй экземпляр, который видно в списке.
- **Экспорт** — кнопка «Экспорт JSON» на странице сценария (`ScenarioExportModal`). Отдаёт ровно
  тот формат, который принимает импорт, в camelCase — он же образец при переносе нового сценария.
- Формат описан в `Model/ScenarioImportDto.cs`. Он намеренно **не** повторяет `Scenario`:
  идентификаторов в нём нет, а родительская локация задаётся **по имени** (`parent`), потому что
  автор файла (человек или LLM, переносящая сценарий из книги правил) идентификаторов не знает.
  Несовпавшее имя родителя не роняет импорт — локация остаётся на верхнем уровне, а предупреждение
  показывается в отчёте.
- Локации/факты/раздатки — JSONB-колонки самой строки, поэтому импорт кладёт их **одной вставкой**
  через `CreateScenarioAsync`, а не N вызовами `AddLocationAsync`. Состав НПС — настоящая связь,
  так что он заводится после создания сценария, по одному вызову `AddNpcToScenarioAsync` на НПС.
- Лист НПС с таким же именем уже в библиотеке — импорт **занимает существующий**, а не создаёт
  двойника (НПС общий для всех сценариев), и пишет об этом в предупреждения: параметры из файла
  к чужому листу не применяются.
- Навыки НПС ложатся на `SkillsModel.DefaultSkillsModel()` по имени (без учёта регистра и «ё»);
  что не совпало — собирается в группу «Особые навыки».

## Key Models
- `Scenario : BaseDataBaseEntity, INamedEntity` owns, all as separate child collections:
  - `Cast` (`ICollection<ScenarioNpc>`) — занятые в сценарии НПС. Это **связь**, а не копии листов:
    `ScenarioNpc` хранит `CharacterId`, `Role` и `Count` (три одинаковых громилы — одна строка
    с `Count = 3`). Один НПС может быть занят в любом числе сценариев.
  - `Pregens` (`ICollection<CharacterStorageDto>`) — преген-персонажи, принадлежащие сценарию
    (`CharacterStorageDto.ScenarioId`). Здесь копия листа осмысленна: преген расходуется бронью.
  - `ScenarioCreatures` (`ICollection<ScenarioCreature>`, and `ScenarioCreature : Creature`) — monster/threat instances, copied from or shaped like the Bestiary's `Creature`, not a foreign key to it.
  - `ScenarioItems` (`ICollection<ScenarioItem>`, `ScenarioItem : Item`).
  - `Locations` (`ScenarioLocation`), `KeyFacts` (`ScenarioKeyFact`), `Handouts` (`ScenarioHandout`).
  - `ScenarioSkillCheck` — predefined skill checks tied to the scenario.

## Notes
- NPCs and creatures are modeled differently: NPCs are full `CharacterStorageDto` character sheets; creatures/monsters subclass `Creature`. Don't try to unify them.
- Состав НПС меняют только `AddNpcToScenarioAsync` / `UpdateScenarioNpcAsync` /
  `RemoveNpcFromScenarioAsync`. «Убрать НПС из сценария» удаляет связь, а не лист —
  персонаж остаётся в библиотеке (раньше отвязка делала строку невидимой навсегда).
- `ScenarioCreatures`, `ScenarioItems`, `Locations`, `KeyFacts`, `Handouts` — это JSONB-колонки
  самой таблицы `Scenarios`, а не отдельные таблицы; `Cast` и `Pregens` — настоящие связи.
  Поэтому `ScenarioLocation.NpcIds` ссылается на идентификаторы листов, и копирование сценария
  из шаблона переносит состав связями, сохраняя эти ссылки валидными.
- `Show()`/`Hide()` у модалок сценария зовёт **родительская страница**, а не сам компонент.
  Параметры при этом не меняются, поэтому Blazor дочерний компонент не перерисовывает — каждая
  такая модалка обязана звать `StateHasChanged()` сама. `AddItemModal` этого не делала, и кнопка
  «Добавить» в разделе «Предметы» просто ничего не открывала.
- `AddCreatureModal` и `AddItemModal` используют одни и те же DOM-идентификаторы `quantity`,
  `location`, `notes`. Обе модалки всегда есть в разметке страницы сценария, поэтому
  `getElementById` попадает в первую (существа) — учитывайте это в автотестах и при отладке.
- Режим игры на `ScenarioDetailPage` — это `?mode=play&location=<guid>` в адресе, а не поля
  компонента. Circuit восстанавливает только `[PersistentState]`, поэтому раньше пауза вкладки
  (см. `js/circuit-persistence.js`) выбрасывала Хранителя из трёхпанельного режима в обзор посреди
  игры. Новое состояние режима игры добавлять туда же, в query, а не в приватное поле; менять его
  только через `GoTo(mode, locationId)` — она ходит с `replace: true`, чтобы переходы по локациям
  не забивали историю.
