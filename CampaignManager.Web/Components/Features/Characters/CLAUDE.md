# Characters Feature

Player character sheets for Call of Cthulhu 7e, persisted as JSONB via `CharacterStorageDto` (see `CampaignManager.Web/Model/CharacterStorageDto.cs`).

## Key Services
- `CharacterService(dbContextFactory, identityService, logger, IMemoryCache)` — CRUD.
  `CreateCharacterAsync` требует явный `CharacterKind` и ровно одного владельца
  (`campaignPlayerId` / `campaignId` / `scenarioId` — или ничего, тогда НПС попадает в общую
  библиотеку). Списки: `GetNpcsAsync`, `GetPregenTemplatesAsync`, `GetScenarioPregensAsync`.
- `CharacterGenerationService(SkillService skillService)` — stateless random-generation logic; no `DbContextFactory` (it doesn't persist anything itself).
- `OccupationService(dbContextFactory, IMemoryCache, logger)` — occupation catalog (skill point formulas, tags).
- `LlmCharacterValidationService(llmClientFactory, IOptions<LlmValidationOptions>, dbContextFactory, identityService, ...)` — uses an LLM to validate/sanity-check generated or edited characters against CoC 7e rules.

## Key Models
- `CharacterStorageDto.Kind` (`CharacterKind`: `PlayerCharacter` / `Pregen` / `Npc`) — **единственный**
  признак вида персонажа, обычная колонка. Не выводить вид из статуса, из набора внешних ключей или
  из полей внутри JSONB: ровно так эта модель и запуталась до упрощения.
- `Character` — composed of `PersonalInfo`, `Characteristics` (STR/DEX/CON/etc with `.Regular`/`.Half`/`.Fifth`), `DerivedAttributes` (HP/MP/Sanity/Luck as `AttributeWithMaxValue`), `Skills` (`SkillsModel` → `SkillGroup[]` → `Skill[]`, each with `.Regular`/`.Half`/`.Fifth`), `State` (`CharacterState` — IsUnconscious, HasSeriousInjury, IsDying, etc.), `Weapons` (`List<Weapon>`), `Spells` (`List<Spell>`), plus `BiographyInfo`, `Equipment`/`EquipmentItem`, `Finances`, `InsanityCondition`.
- `Occupation : BaseDataBaseEntity, INamedEntity`.
- `CharacterGenerationLog` / `GenerationLogEntry` — audit trail of random-generation rolls.
- `LlmKnowledgeEntry : BaseDataBaseEntity` — knowledge base entries fed to the LLM validation service.

## Notes
- Идентификатор строки и `Character.Id` внутри JSONB всегда равны. Их расхождение раньше приводило
  к тому, что сохранение листа создавало новую строку вместо обновления, поэтому и
  `CreateCharacterAsync`, и `CopyPregenToScenarioAsync` выставляют оба.
- Копия листа делается ровно в одном месте — `CopyPregenToScenarioAsync` (преген расходуется
  бронью). НПС в сценарий не копируется: там связь `ScenarioNpc`, см. `Scenarios/CLAUDE.md`.
- `Characters/Model/Skill.cs` (a character's own skill *value*) is a different type from `Skills/Model/SkillModel.cs` (the master skill catalog) — don't confuse the two when searching for "Skill".
- Same pattern for `Weapon`/`Spell`: the character holds `List<Weapon>`/`List<Spell>` referencing the catalog types defined in the `Weapons`/`Spells` features.

## Authorization
- `CharacterService.CanAccessCharacterAsync(dbContext, campaignPlayerId, access)` is the single access rule for
  stored characters; every read and write path goes through it. A character bound to a `CampaignPlayer` belongs to
  that player and to the keeper of their campaign. An unbound row (`CampaignPlayerId is null` — NPC and pregen
  templates) is shared library content: anyone signed in may read it, only keepers may change it. Administrators
  bypass both rules.
- `GetCharacterByIdAsync` returns `null` both for "does not exist" and for "no access" on purpose — the caller must
  not be able to probe which characters exist.
- `ReservePregenAsync` deliberately does **not** use that helper: reserving a pregen is a player writing to an
  unbound row, and it is constrained by its own rules (`Kind = Pregen`, ещё не забронирован, привязан к сценарию).
- Use `identityService.GetCurrentUserEmailAsync()` in authorization paths, never the synchronous
  `GetCurrentUserEmail()` — the latter reads `HttpContext` and returns `null` during interactive rendering.
