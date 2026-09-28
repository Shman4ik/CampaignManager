# Campaigns Feature

Top-level container a Keeper creates to run a game: players, era, status.

## Key Services
- `CampaignService(dbContextFactory, identityService, httpContextAccessor, logger)`.
  `GetKeeperCampaignsAsync` берёт email через `GetCurrentUserEmailAsync`: его зовёт групповая
  проверка в ширме Хранителя, которая рендерится уже в живом circuit, без пререндера, а
  синхронный `GetCurrentUserEmail` читает `HttpContext` и там может вернуть `null`.
  `GetAllCampaignsAsync` (им пользуется `Combat/Components/CampaignSelector`) пока синхронный.
- Сыщики кампании для проверок и боя — `GetCampaignWithCharactersAsync` (игроки со всеми листами):
  так их берут `Combat/Components/ParticipantPicker` и `Checks/Model/CheckInvestigator.FromCampaign`.

## Key Models
- `Campaign : BaseDataBaseEntity` — `Name`, `Status` (`CampaignStatus`, default `Planning`), `KeeperEmail`, `Era` (`Eras`, default `Classic`), `Players` (`List<CampaignPlayer>`).
- `CampaignPlayer : BaseDataBaseEntity` — `CampaignId`, `Characters` (`ICollection<CharacterStorageDto>`) — a player's characters *within this campaign*.
  `PlayerName` — копия имени, снятая при вступлении, и у одного игрока она в разных кампаниях
  разная намеренно. Выровнять её по имени из личного кабинета можно только вручную, галочкой
  на `/profile` (см. `Features/Profile/CLAUDE.md`).
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
