# Admin Feature

Keeper-application review and site administration.

## Key Services
- `AdminService(dbContextFactory, identityDbContextFactory, identityService, logger)` — the only feature service that opens **both** `AppDbContext` and `AppIdentityDbContext` in the same class (see root `CLAUDE.md` "Data Architecture" for why these are separate contexts/schemas).

## Key Models
- `KeeperApplication : BaseDataBaseEntity` — a user's request to be granted Keeper privileges.

## Authorization
- Every administrator-only method on `AdminService` calls `EnsureAdministratorAsync` first and throws
  `UnauthorizedAccessException` when the caller is not an administrator. The `[Authorize(Roles = "Administrator")]`
  attribute on the admin pages is a UI convenience, not the security boundary — keep the service-side check when
  adding new methods. `GetPendingApplicationsCountAsync` degrades to `0` instead of throwing, because it feeds a
  badge rendered in the shared layout.
- `SubmitApplicationAsync` is intentionally open to any signed-in user — that is how a player asks to become a Keeper.
