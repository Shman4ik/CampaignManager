# Bestiary Feature

Creature/monster catalog for Call of Cthulhu 7e (independent entity — not owned by a Campaign or Scenario).

## Key Services
- `CreatureService(dbContextFactory, IMemoryCache, logger)` — CRUD + cached lookups.

## Key Models
- `Creature : BaseDataBaseEntity, INamedEntity` — `CreatureCharacteristics` (STR/DEX/CON/etc as `.Value`, Initiative, HealPoint, ManaPoint, AverageBonusToHit, AverageComplexity), `CombatDescriptions` (`Dictionary<string,string>`, attack name → description/damage), `SpecialAbilities` (`Dictionary<string,string>`).
- `CreatureAttack`, `CreatureCharacteristicModel` — supporting types for attack definitions and characteristic display.

## Notes
- Scenarios embed creatures via `ScenarioCreature : Creature` (see `Scenarios/CLAUDE.md`) rather than referencing the Bestiary catalog directly — a scenario's creature instance is its own row, not a foreign key.
