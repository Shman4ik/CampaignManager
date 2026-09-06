# Items Feature

General equipment/item catalog (independent entity).

## Key Services
- `ItemService(dbContextFactory, IMemoryCache, logger)` — CRUD + cached lookups.

## Key Models
- `Item : BaseDataBaseEntity, INamedEntity`.

## Notes
- `Scenarios/Model/ScenarioItem : Item` subclasses this to attach scenario-specific item instances (see `Scenarios/CLAUDE.md`).
