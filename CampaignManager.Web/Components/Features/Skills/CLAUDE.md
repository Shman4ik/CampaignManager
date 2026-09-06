# Skills Feature

Master skill catalog (independent entity — the definitions, not a character's rolled values).

## Key Services
- `SkillService(dbContextFactory, IMemoryCache, logger)` — CRUD + cached lookups.

## Key Models
- `SkillModel : BaseDataBaseEntity, INamedEntity`.

## Notes
- Do not confuse `Skills/Model/SkillModel.cs` (this catalog) with `Characters/Model/Skill.cs`, `SkillsModel`, `SkillGroup` (a specific character's skill values, embedded in that character's JSONB) — see `Characters/CLAUDE.md`.
- `CharacterGenerationService` (in the Characters feature) depends directly on `SkillService` to roll starting skill points.
