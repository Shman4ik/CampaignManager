# Bestiary Feature

Creature/monster catalog for Call of Cthulhu 7e (independent entity — not owned by a Campaign or Scenario).

## Key Services
- `CreatureService(dbContextFactory, IMemoryCache, logger)` — CRUD + cached lookups.

## Key Models
- `Creature : BaseDataBaseEntity, INamedEntity` — `CreatureCharacteristics` (STR/DEX/CON/etc as `.Value`, Initiative, HealPoint, ManaPoint, AverageBonusToHit, AverageComplexity), `CombatDescriptions` (`Dictionary<string,string>`, attack name → description/damage), `SpecialAbilities` (`Dictionary<string,string>`).
- `CreatureAttack`, `CreatureCharacteristicModel` — supporting types for attack definitions and characteristic display.

## Notes
- `CreatureCharacteristics.SanityLoss` — потеря рассудка при встрече в формате «успех/провал»
  («0/1d6»). Лежит в JSONB, отдельной колонки нет. Из провальной части
  `Services/SanityLossFormula.MaxLoss` выводит предел привыкания к ужасному (стр. 167),
  которым пользуется лист персонажа (`Characters/Components/MythosHabituationPanel`).
  Пустая строка — предел придётся вписать Хранителю вручную, поэтому парсер возвращает 0,
  а не «угадывает».
- Scenarios embed creatures via `ScenarioCreature : Creature` (see `Scenarios/CLAUDE.md`) rather than referencing the Bestiary catalog directly — a scenario's creature instance is its own row, not a foreign key.
