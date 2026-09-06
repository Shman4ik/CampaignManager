# Scenarios Feature

A prepared adventure/one-shot: optionally linked to a `Campaign` (`CampaignId` is nullable — scenarios can exist standalone).

## Key Services
- `ScenarioService(dbContextFactory, IMemoryCache, CampaignService campaignService, logger)` — the only feature service with a direct dependency on another feature's service (`CampaignService`).

## Key Models
- `Scenario : BaseDataBaseEntity, INamedEntity` owns, all as separate child collections:
  - `Npcs` (`ICollection<CharacterStorageDto>`) — full character sheets acting as NPCs.
  - `ScenarioCreatures` (`ICollection<ScenarioCreature>`, and `ScenarioCreature : Creature`) — monster/threat instances, copied from or shaped like the Bestiary's `Creature`, not a foreign key to it.
  - `ScenarioItems` (`ICollection<ScenarioItem>`, `ScenarioItem : Item`).
  - `Locations` (`ScenarioLocation`), `KeyFacts` (`ScenarioKeyFact`), `Handouts` (`ScenarioHandout`).
  - `ScenarioSkillCheck` — predefined skill checks tied to the scenario.

## Notes
- NPCs and creatures are modeled differently: NPCs are full `CharacterStorageDto` character sheets; creatures/monsters subclass `Creature`. Don't try to unify them.
