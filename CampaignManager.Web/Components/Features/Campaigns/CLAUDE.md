# Campaigns Feature

Top-level container a Keeper creates to run a game: players, era, status.

## Key Services
- `CampaignService(dbContextFactory, identityService, httpContextAccessor, logger)`.

## Key Models
- `Campaign : BaseDataBaseEntity` — `Name`, `Status` (`CampaignStatus`, default `Planning`), `KeeperEmail`, `Era` (`Eras`, default `Classic`), `Players` (`List<CampaignPlayer>`).
- `CampaignPlayer : BaseDataBaseEntity` — `CampaignId`, `Characters` (`ICollection<CharacterStorageDto>`) — a player's characters *within this campaign*.
- `CampaignCreateModel` — form/DTO shape for campaign creation.

## Владение персонажами
`CharacterStorageDto` (JSONB-обёртка листа) лежит в общем `CampaignManager.Web/Model/`, а кто им
владеет, описывает ровно один ключ:
- `CampaignPlayerId` — лист игрока в этой кампании (`Kind = PlayerCharacter`, либо забронированный преген);
- `CampaignId` — НПС кампании (`Kind = Npc`); `null` — НПС из общей библиотеки, доступный везде;
- `ScenarioId` — преген, созданный для сценария (см. `Scenarios/CLAUDE.md`).

НПС кампании **не занимает слот игрока**: в `UserCampaignsComponent` их отдаёт
`CharacterService.GetNpcsAsync(campaignId)`, а не `Players.SelectMany(p => p.Characters)`.
Удаление кампании не удаляет её НПС — FK стоит `SetNull`, и лист возвращается в библиотеку.
