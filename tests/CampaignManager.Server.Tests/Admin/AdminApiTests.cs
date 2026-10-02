using System.Net;
using CampaignManager.ApiClient.Admin;
using CampaignManager.ApiClient.Identity;
using CampaignManager.ApiClient.Profile;
using CampaignManager.Contracts.Profile;
using CampaignManager.Core.Identity;
using CampaignManager.Data.Identity;
using CampaignManager.Server.Tests.Campaigns;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CampaignManager.Server.Tests.Admin;

/// <summary>
/// Админка через API: роль меняет только администратор, одобрение заявки — статус и роль одной транзакцией,
/// последнего администратора не понизить.
/// </summary>
public sealed class AdminApiTests(CampaignsApp app) : IClassFixture<CampaignsApp>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private AdminApiClient Admin(User user) => new(app.Http(user));

    private ProfileApiClient Profile(User user) => new(app.Http(user));

    [Fact]
    public async Task Only_admin_changes_roles_and_the_change_is_seen_at_once()
    {
        TestDatabase.SkipIfMissing();
        var admin = await app.AddUserAsync(UserRole.Admin, "Админ");
        var keeper = await app.AddUserAsync(UserRole.Keeper, "Хранитель");
        var player = await app.AddUserAsync(UserRole.Player, "Игрок");

        await ApiAssert.FailsWith(HttpStatusCode.Forbidden, () => Admin(player).ChangeRoleAsync(player.Id, UserRole.Admin, Cancellation));
        await ApiAssert.FailsWith(HttpStatusCode.Forbidden, () => Admin(keeper).ChangeRoleAsync(player.Id, UserRole.Keeper, Cancellation));
        await ApiAssert.FailsWith(HttpStatusCode.Forbidden, () => Admin(keeper).GetUsersAsync(Cancellation));
        await ApiAssert.FailsWith(HttpStatusCode.Unauthorized,
            () => new AdminApiClient(app.CreateClient()).ChangeRoleAsync(player.Id, UserRole.Keeper, Cancellation));
        Assert.Equal(UserRole.Player, await RoleOf(player));

        var updated = await Admin(admin).ChangeRoleAsync(player.Id, UserRole.Keeper, Cancellation);

        Assert.Equal(UserRole.Keeper, updated.Role);
        Assert.False(updated.IsMe);
        // Роль — из базы на каждый запрос: без кэша claims v1 (5 минут) новая видна сразу.
        var me = await new IdentityApiClient(app.Http(player)).GetMeAsync(Cancellation);
        Assert.Equal(UserRole.Keeper, me!.Role);

        var users = await Admin(admin).GetUsersAsync(Cancellation);
        Assert.Contains(users, u => u.Id == admin.Id && u.IsMe);
        Assert.Contains(users, u => u.Id == player.Id && u.Role == UserRole.Keeper && u.Email == player.Email);
    }

    [Fact]
    public async Task Unknown_user_is_404()
    {
        TestDatabase.SkipIfMissing();
        var admin = await app.AddUserAsync(UserRole.Admin);

        await ApiAssert.FailsWith(HttpStatusCode.NotFound, () => Admin(admin).ChangeRoleAsync(Guid.NewGuid(), UserRole.Keeper, Cancellation));
    }

    [Fact]
    public async Task Last_admin_cannot_be_demoted()
    {
        TestDatabase.SkipIfMissing();
        var admin = await app.AddUserAsync(UserRole.Admin, "Единственный админ");
        await using (var db = app.Database.CreateContext())
        {
            await db.Users.Where(u => u.Role == UserRole.Admin && u.Id != admin.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Role, UserRole.Keeper), Cancellation);
        }

        var message = await ApiAssert.FailsWith(HttpStatusCode.Conflict,
            () => Admin(admin).ChangeRoleAsync(admin.Id, UserRole.Keeper, Cancellation));
        Assert.Contains("последний", message);
        Assert.Equal(UserRole.Admin, await RoleOf(admin));

        // Со вторым администратором — можно, в том числе себя.
        var second = await app.AddUserAsync(UserRole.Admin);
        var demoted = await Admin(admin).ChangeRoleAsync(admin.Id, UserRole.Keeper, Cancellation);
        Assert.Equal(UserRole.Keeper, demoted.Role);
        Assert.True(demoted.IsMe);
        await ApiAssert.FailsWith(HttpStatusCode.Forbidden, () => Admin(admin).GetUsersAsync(Cancellation));
        Assert.NotEmpty(await Admin(second).GetUsersAsync(Cancellation));
    }

    // Сценарий паритета 11: игрок подаёт заявку, администратор одобряет — игрок становится Хранителем.
    [Fact]
    public async Task Approval_sets_status_and_role_together()
    {
        TestDatabase.SkipIfMissing();
        var admin = await app.AddUserAsync(UserRole.Admin, "Админ");
        var player = await app.AddUserAsync(UserRole.Player, "Соискатель");
        await Profile(player).SubmitKeeperApplicationAsync(new SubmitKeeperApplicationRequest("Хочу вести"), Cancellation);
        var pending = Assert.Single(await Admin(admin).GetApplicationsAsync(KeeperApplicationStatus.Pending, Cancellation),
            a => a.UserId == player.Id);
        Assert.True((await Admin(admin).GetSummaryAsync(Cancellation)).PendingApplications >= 1);
        Assert.Contains(await Admin(admin).GetUsersAsync(Cancellation), u => u.Id == player.Id && u.HasPendingApplication);

        var approved = await Admin(admin).ApproveAsync(pending.Id, Cancellation);

        Assert.Equal(KeeperApplicationStatus.Approved, approved.Status);
        Assert.Equal(UserRole.Keeper, approved.UserRole);
        Assert.Equal("Админ", approved.ReviewerName);
        Assert.NotNull(approved.ReviewedAt);
        Assert.Equal(UserRole.Keeper, await RoleOf(player));

        var profile = await Profile(player).GetProfileAsync(Cancellation);
        Assert.Equal(UserRole.Keeper, profile.Role);
        Assert.Equal(KeeperApplicationStatus.Approved, profile.LatestApplication!.Status);
        Assert.False(profile.CanApply);

        // Второй раз не рассмотреть.
        await ApiAssert.FailsWith(HttpStatusCode.Conflict, () => Admin(admin).ApproveAsync(pending.Id, Cancellation));
        await ApiAssert.FailsWith(HttpStatusCode.NotFound, () => Admin(admin).ApproveAsync(Guid.NewGuid(), Cancellation));
    }

    // Одна транзакция: роль не записалась — и статус заявки остался прежним (в v1 — два контекста).
    [Fact]
    public async Task Failed_role_update_rolls_back_the_approval()
    {
        TestDatabase.SkipIfMissing();
        var admin = await app.AddUserAsync(UserRole.Admin);
        var player = await app.AddUserAsync(UserRole.Player, "Невезучий");
        await Profile(player).SubmitKeeperApplicationAsync(new SubmitKeeperApplicationRequest(null), Cancellation);
        var applicationId = (await Profile(player).GetProfileAsync(Cancellation)).LatestApplication!.Id;

        // Триггер роняет любую правку именно этого пользователя — как сбой между двумя записями.
        var trigger = $"test_fail_{player.Id:N}";
        var createTrigger = $"create trigger {trigger} before update on cm.users for each row when (new.id = '{player.Id}') " +
                            "execute function cm.test_fail_user_update();";
        var dropTrigger = $"drop trigger {trigger} on cm.users;";
        await using (var db = app.Database.CreateContext())
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                create or replace function cm.test_fail_user_update() returns trigger language plpgsql as
                $$ begin raise exception 'сбой записи роли'; end $$;
                """, Cancellation);
            await db.Database.ExecuteSqlRawAsync(createTrigger, Cancellation);
        }

        await ApiAssert.FailsWith(HttpStatusCode.InternalServerError, () => Admin(admin).ApproveAsync(applicationId, Cancellation));

        await using (var db = app.Database.CreateContext())
        {
            var application = await db.KeeperApplications.SingleAsync(a => a.Id == applicationId, Cancellation);
            Assert.Equal(KeeperApplicationStatus.Pending, application.Status);
            Assert.Null(application.ReviewedById);
            Assert.Null(application.ReviewedAt);
            await db.Database.ExecuteSqlRawAsync(dropTrigger, Cancellation);
        }

        Assert.Equal(UserRole.Player, await RoleOf(player));
        var approved = await Admin(admin).ApproveAsync(applicationId, Cancellation);
        Assert.Equal(KeeperApplicationStatus.Approved, approved.Status);
        Assert.Equal(UserRole.Keeper, await RoleOf(player));
    }

    // Роль при одобрении только повышается: администратор Хранителем не становится.
    [Fact]
    public async Task Approval_never_demotes_an_admin()
    {
        TestDatabase.SkipIfMissing();
        var admin = await app.AddUserAsync(UserRole.Admin);
        var otherAdmin = await app.AddUserAsync(UserRole.Admin);
        Guid applicationId;
        await using (var db = app.Database.CreateContext())
        {
            var application = new KeeperApplication { UserId = otherAdmin.Id, Message = "старая заявка" };
            db.KeeperApplications.Add(application);
            await db.SaveChangesAsync(Cancellation);
            applicationId = application.Id;
        }

        var approved = await Admin(admin).ApproveAsync(applicationId, Cancellation);

        Assert.Equal(KeeperApplicationStatus.Approved, approved.Status);
        Assert.Equal(UserRole.Admin, await RoleOf(otherAdmin));
    }

    [Fact]
    public async Task Rejection_keeps_role_shows_comment_and_allows_new_application()
    {
        TestDatabase.SkipIfMissing();
        var admin = await app.AddUserAsync(UserRole.Admin, "Админ");
        var player = await app.AddUserAsync(UserRole.Player, "Соискатель");
        var keeper = await app.AddUserAsync(UserRole.Keeper);
        await Profile(player).SubmitKeeperApplicationAsync(new SubmitKeeperApplicationRequest(null), Cancellation);
        var applicationId = (await Profile(player).GetProfileAsync(Cancellation)).LatestApplication!.Id;

        await ApiAssert.FailsWith(HttpStatusCode.Forbidden, () => Admin(keeper).RejectAsync(applicationId, "нет", Cancellation));
        await ApiAssert.FailsWith(HttpStatusCode.Forbidden, () => Admin(player).ApproveAsync(applicationId, Cancellation));

        var rejected = await Admin(admin).RejectAsync(applicationId, "  Сначала сыграйте пару ваншотов  ", Cancellation);

        Assert.Equal(KeeperApplicationStatus.Rejected, rejected.Status);
        Assert.Equal("Сначала сыграйте пару ваншотов", rejected.ReviewComment);
        Assert.Equal(UserRole.Player, await RoleOf(player));
        Assert.Contains(await Admin(admin).GetApplicationsAsync(KeeperApplicationStatus.Rejected, Cancellation), a => a.Id == applicationId);
        Assert.DoesNotContain(await Admin(admin).GetApplicationsAsync(KeeperApplicationStatus.Pending, Cancellation), a => a.Id == applicationId);
        await ApiAssert.FailsWith(HttpStatusCode.Conflict, () => Admin(admin).ApproveAsync(applicationId, Cancellation));

        var profile = await Profile(player).GetProfileAsync(Cancellation);
        Assert.Equal("Сначала сыграйте пару ваншотов", profile.LatestApplication!.ReviewComment);
        Assert.True(profile.CanApply);
        var again = await Profile(player).SubmitKeeperApplicationAsync(new SubmitKeeperApplicationRequest("Сыграл"), Cancellation);
        Assert.Equal(KeeperApplicationStatus.Pending, again.LatestApplication!.Status);
    }

    private async Task<UserRole> RoleOf(User user)
    {
        await using var db = app.Database.CreateContext();
        return await db.Users.Where(u => u.Id == user.Id).Select(u => u.Role).SingleAsync(Cancellation);
    }
}
