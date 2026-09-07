# Scenarios Feature

A prepared adventure/one-shot: optionally linked to a `Campaign` (`CampaignId` is nullable — scenarios can exist standalone).

## Key Services
- `ScenarioService(dbContextFactory, IMemoryCache, CampaignService campaignService, logger)` — the only feature service with a direct dependency on another feature's service (`CampaignService`).

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
