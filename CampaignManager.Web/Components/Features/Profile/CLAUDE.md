# Profile Feature

Личный кабинет игрока: `/profile` — отображаемое имя, роль и заявка на Хранителя, сводка по
кампаниям и сыщикам, настройки интерфейса.

## Key Services
- `ProfileService(identityDbContextFactory, dbContextFactory, identityService, logger)` — вместе с
  `AdminService` это второй сервис, открывающий **оба** контекста: пользователь лежит в схеме
  `identity`, а его кампании и листы — в `games`.

## Key Models
- `UserProfile` — снимок данных страницы, собирается одним `GetProfileAsync()`. Не сущность БД.

## Имя пользователя живёт в трёх местах — и это осознанно
1. `ApplicationUser.UserName` (схема `identity`) — **источник истины**, его и меняет кабинет.
2. Claim `ClaimTypes.Name` в куке — то, что отдал Google при входе. Именно его читает
   `context.User.Identity?.Name` в сайдбаре, на главной и в `JoinCampaignComponent`.
   Поэтому `RoleClaimsTransformation` **заменяет** этот claim значением из базы (не добавляет
   вторым: `ClaimsIdentity.Name` читает первый claim своего типа). Без этого смена имени в
   кабинете не меняла бы ничего из видимого.
   Отсюда же `Nav.NavigateTo(..., forceLoad: true)` после сохранения: claim собирается на
   HTTP-запросе, и без полной перезагрузки страница показывала бы новое имя, а сайдбар — старое.
3. `CampaignPlayer.PlayerName` — копия, снятая при вступлении в кампанию. В разных кампаниях у
   одного человека имена **разные** и часто намеренно («Дима» в одной, полное имя в другой),
   поэтому галочка «заменить имя и в кампаниях» по умолчанию **выключена**, а сама замена —
   отдельный `ExecuteUpdateAsync` по всем `CampaignPlayer` пользователя.

`NormalizedUserName` не трогаем: на нём в `AspNetUsers` висит уникальный индекс, а приложение
ищет пользователей только по `Email`. Два игрока с одинаковым отображаемым именем — норма.

## Настройки интерфейса
Ключи лежат в `Utilities/Services/UserPreferenceKeys.cs`, значения — в `UserPreferences`
(JSONB, схема `games`):
- `ui.sidebarExpanded` — читает `Layout/Sidebar.razor`. localStorage остаётся быстрым кэшем
  для мгновенной отрисовки, но настройка пользователя перебивает его: на новом устройстве
  localStorage пуст, а привычное меню должно приехать вместе с аккаунтом.
- `ui.syncLastCharacter` + `ui.lastCharacterId`/`Name` — пишет `CharacterPage`, читает
  `Layout/MobileBottomNav`. По умолчанию включено.

## Cross-feature dependencies
- `AdminService.SubmitApplicationAsync` — подача заявки на Хранителя. Карточка `BecomeKeeperCard`
  с главной удалена: статус заявки и кнопку теперь показывает кабинет, где их нельзя «скрыть
  навсегда» и потом не найти.
- Читает `KeeperApplications` (модель фичи Admin) напрямую — только чтение последней заявки
  текущего пользователя.
