# Campaigns Feature

Top-level container a Keeper creates to run a game: players, era, status.

## Key Services
- `CampaignService(dbContextFactory, identityService, characterService, httpContextAccessor, userClaimsCache, logger)`.
  `GetKeeperCampaignsAsync` берёт email через `GetCurrentUserEmailAsync`: его зовёт групповая
  проверка в ширме Хранителя, которая рендерится уже в живом circuit, без пререндера, а
  синхронный `GetCurrentUserEmail` читает `HttpContext` и там может вернуть `null`.
  `GetAllCampaignsAsync` (им пользуется `Combat/Components/CampaignSelector`) пока синхронный.
- **Главная — `GetHomeCampaignsAsync`, один раз на загрузку.** Три блока главной
  (`UserCampaignsComponent`, `JoinCampaignComponent`, `KeeperCharactersComponent`) — не острова, а
  дети одного `HomeCampaignsPanel` (`@rendermode InteractiveServer`), который читает снимок
  `HomeCampaigns` (`Models/HomeCampaigns.cs`) и раздаёт его параметром. Раньше каждый блок был своим
  островом и грузил `Campaigns → Players → Characters` с полными листами сам (6 SQL на проход, плюс
  по запросу НПС на каждую кампанию Хранителя). Теперь три параллельных запроса: свои кампании (листы —
  только свой активный и все листы кампаний, которые ведёшь), доступные для вступления и НПС своих
  кампаний (`CharacterService.GetKeptCampaignNpcsAsync`, без JSONB). Почта и роль — из claims.
  Новый блок главной про кампании берёт данные из снимка, а не зовёт сервис
  сам; снимок плоский (без EF-навигаций), потому что переезжает из пререндера через `[PersistentState]`.
- **Главная открыта без входа, поэтому анониму снимок пуст** — ни одного запроса, ни одной кампании.
  **Почт Хранителей в снимке нет**: `HomeCampaign`/`HomeAvailableCampaign` несут `KeeperName` —
  `ApplicationUser.UserName` через `UserClaimsCache`, а если имя не задано или само почта (так его
  заводит вход без `name` у провайдера), `null`, и строка «Хранитель:» просто не рисуется.
- Сыщики кампании для проверок и боя — `GetCampaignWithCharactersAsync` (игроки со всеми листами):
  так их берут `Combat/Components/ParticipantPicker` и `Checks/Model/CheckInvestigator.FromCampaign`.
- `CampaignJournalService(dbContextFactory, identityService, logger)` — журнал встреч кампании
  (см. «Журнал встреч» ниже). Права проверяет сам, страница только показывает.

## Key Models
- `Campaign : BaseDataBaseEntity` — `Name`, `Status` (`CampaignStatus`, default `Planning`), `KeeperEmail`, `Era` (`Eras`, default `Classic`), `Players` (`List<CampaignPlayer>`).
- `CampaignPlayer : BaseDataBaseEntity` — `CampaignId`, `Characters` (`ICollection<CharacterStorageDto>`) — a player's characters *within this campaign*.
  `PlayerName` — копия имени, снятая при вступлении, и у одного игрока она в разных кампаниях
  разная намеренно. Выровнять её по имени из личного кабинета можно только вручную, галочкой
  на `/profile` (см. `Features/Profile/CLAUDE.md`).
- `CampaignCreateModel` — form/DTO shape for campaign creation.
- `CampaignSession : BaseDataBaseEntity` — одна встреча журнала, таблица `CampaignSessions`
  (миграция `AddCampaignSessions`). `CampaignId` — FK с каскадом (удалили кампанию — удалили
  хронику), `ScenarioId` — необязательный FK с `SetNull`. У `Campaign` навигации на встречи нет
  намеренно: журнал всегда читается отдельным запросом по кампании.
- `CampaignJournal` / `CampaignSessionView` / `CampaignSessionInput` — то, что сервис отдаёт
  странице, и форма модалки (`Models/CampaignJournal.cs`).

## Журнал встреч
`/campaigns/{id}/journal` (`CampaignJournalPage`), вход — кнопка «Журнал встреч» на карточке
кампании в `/campaigns` и на главной (`UserCampaignsComponent`, у всех участников).
- **Писать** может Хранитель кампании (`Campaign.KeeperEmail`) и администратор, **читать** — они же
  и игроки кампании (строка в `CampaignPlayers`). Остальным `GetJournalAsync` отдаёт `null` —
  «нет кампании» и «нет доступа» неразличимы нарочно.
- Текста два: `Summary` (хроника, видна всем) и `KeeperNotes` (только Хранителю). Заметки
  вырезает **сервис**, а не разметка: у игрока в `CampaignSessionView.KeeperNotes` всегда `null`.
  Новое секретное поле — туда же, в ветку `canEdit` сервиса.
- «Сценарий или глава завершены» (`ScenarioCompleted`) — только подсказка о фазе развития
  (стр. 92). Ссылки на листы сыщиков показываются у **последней** встречи: когда записана
  следующая, момент прошёл. Сама фаза — `DevelopmentPhaseModal` в листе, журнал ничего не считает.
  Хранитель видит все активные листы игроков кампании, игрок — только свой.
- К встрече привязываются сценарии самой кампании и все сценарии её Хранителя (`CreatorEmail`):
  ваншоты и шаблоны часто играют, не «добавляя в кампанию». Проверка повторена в
  `SaveSessionAsync`; уже привязанный сценарий при правке не перепроверяется.
- Номер встречи предлагается следующим (`NextNumber`), но правится руками. Сортировка — по дате
  встречи, потом по номеру, новые сверху.
- Открытая модалка с недописанной записью переживает паузу circuit через
  `CampaignJournalPage.PersistedEditor` (`[PersistentState]`). Новое поле формы — в
  `CampaignSessionInput`, иначе оно пропадёт при возобновлении.

## Владение персонажами
`CharacterStorageDto` (JSONB-обёртка листа) лежит в общем `CampaignManager.Web/Model/`, а кто им
владеет, описывает ключ, положенный его виду (единственное исключение — забронированный преген:
у него и `ScenarioId`, и `CampaignPlayerId`):
- `CampaignPlayerId` — лист игрока в этой кампании (`Kind = PlayerCharacter`, либо забронированный преген);
  у листа игрока он обязателен — без него лист читается как общая библиотека;
- `CampaignId` — НПС кампании (`Kind = Npc`); `null` — НПС из общей библиотеки, доступный везде;
- `ScenarioId` — преген, созданный для сценария (см. `Scenarios/CLAUDE.md`).

Сочетания держит CHECK `CK_Characters_Owner` — подробности в `Characters/CLAUDE.md`.

НПС кампании **не занимает слот игрока**: на главной их отдаёт
`CharacterService.GetKeptCampaignNpcsAsync` (все НПС кампаний Хранителя одним запросом), а не
`Players.SelectMany(p => p.Characters)`.
Удаление кампании не удаляет её НПС — FK стоит `SetNull`, и лист возвращается в библиотеку.
