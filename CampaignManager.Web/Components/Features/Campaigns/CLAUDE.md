# Campaigns Feature

Top-level container a Keeper creates to run a game: players, era, status.

## Key Services
- `CampaignService(dbContextFactory, identityService, httpContextAccessor, logger)`.

## Key Models
- `Campaign : BaseDataBaseEntity` — `Name`, `Status` (`CampaignStatus`, default `Planning`), `KeeperEmail`, `Era` (`Eras`, default `Classic`), `Players` (`List<CampaignPlayer>`).
- `CampaignPlayer : BaseDataBaseEntity` — `CampaignId`, `Characters` (`ICollection<CharacterStorageDto>`) — a player's characters *within this campaign*.
- `CampaignCreateModel` — form/DTO shape for campaign creation.

## Notes
- `CharacterStorageDto` (the JSONB wrapper for a `Character`) lives in the shared `CampaignManager.Web/Model/` folder, not under this feature — it's referenced from both `CampaignPlayer.Characters` here and `Scenario.Npcs` (see `Scenarios/CLAUDE.md`).
