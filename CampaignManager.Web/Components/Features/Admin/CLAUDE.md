# Admin Feature

Keeper-application review and site administration.

## Key Services
- `AdminService(dbContextFactory, identityDbContextFactory, identityService, logger)` — the only feature service that opens **both** `AppDbContext` and `AppIdentityDbContext` in the same class (see root `CLAUDE.md` "Data Architecture" for why these are separate contexts/schemas).

## Key Models
- `KeeperApplication : BaseDataBaseEntity` — a user's request to be granted Keeper privileges.
