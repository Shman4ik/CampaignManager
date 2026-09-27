# Admin Feature

Keeper-application review and site administration.

## Key Services
- `AdminService(dbContextFactory, identityDbContextFactory, identityService, userClaimsCache, logger)` — the only feature service that opens **both** `AppDbContext` and `AppIdentityDbContext` in the same class (see root `CLAUDE.md` "Data Architecture" for why these are separate contexts/schemas).

## Key Models
- `KeeperApplication : BaseDataBaseEntity` — a user's request to be granted Keeper privileges.

## Pages
- Роль везде по-русски — `PlayerRole.ToRussianString()` (`Extensions/EnumExtensions`): и в бейдже,
  и в `<select>` смены роли. `value` у `<option>` остаётся именем члена перечисления — по нему
  `ChangeRoleAsync` разбирает выбор.
- Бейджам — только варианты `Badge` (`warning`/`success`/`error`/`secondary`/`accent`…), не имена
  цветов: «yellow»/«red» он не знает, и раньше все статусы и роли рисовались одним primary.
  Цвета ролей те же, что в личном кабинете.

## Authorization
- Every administrator-only method on `AdminService` calls `EnsureAdministratorAsync` first and throws
  `UnauthorizedAccessException` when the caller is not an administrator. The `[Authorize(Roles = "Administrator")]`
  attribute on the admin pages is a UI convenience, not the security boundary — keep the service-side check when
  adding new methods. `GetPendingApplicationsCountAsync` degrades to `0` instead of throwing, because it feeds a
  badge rendered in the shared layout.
- `SetUserRoleAsync` (и одобрение заявки через него) сбрасывает `UserClaimsCache` для этой почты:
  роль в claims берётся из кэша на 5 минут (см. `Utilities/Authorization/UserClaimsCache`), и без
  сброса новая роль доехала бы до пользователя только по истечении срока.
- `SubmitApplicationAsync` is intentionally open to any signed-in user — that is how a player asks to become a Keeper.
  Вызывает её личный кабинет (`Features/Profile`), он же показывает статус последней заявки; карточки
  на главной больше нет.
