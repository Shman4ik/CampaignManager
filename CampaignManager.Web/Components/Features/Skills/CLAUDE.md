# Skills Feature

Master skill catalog (independent entity — the definitions, not a character's rolled values).

## Key Services
- `SkillService(dbContextFactory, IMemoryCache, identityService, logger)` — CRUD + cached lookups.
  Правят только Хранитель и администратор — как у оружия (`Weapons/CLAUDE.md`, «Права»):
  `Create/Update/DeleteSkillAsync` зовут `EnsureKeeperAsync`, `SkillCard` получает `CanEdit`.
  `GetSkillByIdAsync` берёт навык из кэша справочника (`GetAllSkillsUnpagedAsync`), а не из базы,
  и отдаёт **копию** (JSON-круг): форма правки меняет объект на месте, а экземпляр в кэше общий на
  всех. Отдать кэшированный объект напрямую — значит пустить отменённую правку в справочник.

## Key Models
- `SkillModel : BaseDataBaseEntity, INamedEntity`.

## Notes
- Do not confuse `Skills/Model/SkillModel.cs` (this catalog) with `Characters/Model/Skill.cs`, `SkillsModel`, `SkillGroup` (a specific character's skill values, embedded in that character's JSONB) — see `Characters/CLAUDE.md`.
- `CharacterGenerationService` (in the Characters feature) depends directly on `SkillService` to roll starting skill points.
