# Weapons Feature

Master weapon catalog (independent entity).

## Key Services
- `WeaponService(dbContextFactory, IMemoryCache, logger)` — CRUD + cached lookups.

## Key Models
- `Weapon : BaseDataBaseEntity, INamedEntity`.
- `WeaponDamageInfo`, `RangeDamageEntry` — structured damage/range data (used for PDF character-sheet rendering via QuestPDF and in-app display).

## Notes
- A character's carried weapons are just `Character.Weapons` (`List<Weapon>`) — same catalog type, no separate character-specific wrapper.
