# Characters Feature

Player character sheets for Call of Cthulhu 7e, persisted as JSONB via `CharacterStorageDto` (see `CampaignManager.Web/Model/CharacterStorageDto.cs`).

## Key Services
- `CharacterService(dbContextFactory, identityService, logger, IMemoryCache)` — CRUD.
- `CharacterGenerationService(SkillService skillService)` — stateless random-generation logic; no `DbContextFactory` (it doesn't persist anything itself).
- `OccupationService(dbContextFactory, IMemoryCache, logger)` — occupation catalog (skill point formulas, tags).
- `LlmCharacterValidationService(llmClientFactory, IOptions<LlmValidationOptions>, dbContextFactory, identityService, ...)` — uses an LLM to validate/sanity-check generated or edited characters against CoC 7e rules.

## Key Models
- `Character` — composed of `PersonalInfo`, `Characteristics` (STR/DEX/CON/etc with `.Regular`/`.Half`/`.Fifth`), `DerivedAttributes` (HP/MP/Sanity/Luck as `AttributeWithMaxValue`), `Skills` (`SkillsModel` → `SkillGroup[]` → `Skill[]`, each with `.Regular`/`.Half`/`.Fifth`), `State` (`CharacterState` — IsUnconscious, HasSeriousInjury, IsDying, etc.), `Weapons` (`List<Weapon>`), `Spells` (`List<Spell>`), plus `BiographyInfo`, `Equipment`/`EquipmentItem`, `Finances`, `InsanityCondition`.
- `Occupation : BaseDataBaseEntity, INamedEntity`.
- `CharacterGenerationLog` / `GenerationLogEntry` — audit trail of random-generation rolls.
- `LlmKnowledgeEntry : BaseDataBaseEntity` — knowledge base entries fed to the LLM validation service.

## Notes
- `Characters/Model/Skill.cs` (a character's own skill *value*) is a different type from `Skills/Model/SkillModel.cs` (the master skill catalog) — don't confuse the two when searching for "Skill".
- Same pattern for `Weapon`/`Spell`: the character holds `List<Weapon>`/`List<Spell>` referencing the catalog types defined in the `Weapons`/`Spells` features.
