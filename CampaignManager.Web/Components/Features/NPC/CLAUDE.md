# NPC Feature

UI-only feature — there is no `NPC/Model` or `NPC/Services`. It presents and links NPCs, which are just regular `Character`/`CharacterStorageDto` records (see `Characters/CLAUDE.md`) attached to a `Scenario.Npcs` collection (see `Scenarios/CLAUDE.md`).

## Key Components
- `NpcListPage.razor` — listing/browsing page.
- `CharacterTemplateCard.razor`, `LinkToScenarioDialog.razor` — reused to create/attach an NPC character to a scenario.

## Notes
- When looking for "NPC data model," look in `Characters/Model/Character.cs` and `Scenarios/Model/Scenario.cs` (`Npcs` property), not here.
