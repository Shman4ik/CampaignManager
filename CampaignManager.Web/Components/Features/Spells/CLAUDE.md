# Spells Feature

Master spell catalog (independent entity).

## Key Services
- `SpellService(dbContextFactory, IMemoryCache, logger)` — CRUD + cached lookups.

## Key Models
- `Spell : BaseDataBaseEntity, INamedEntity` (declared in `SpellModel.cs`).

## Notes
- A character's known spells are just `Character.Spells` (`List<Spell>`) — same catalog type, no separate character-specific spell model (unlike Skills/Weapons, which have character-specific wrapper types).
