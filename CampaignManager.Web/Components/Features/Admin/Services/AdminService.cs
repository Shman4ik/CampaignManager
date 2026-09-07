using CampaignManager.Web.Components.Features.Admin.Model;
using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Model;
using CampaignManager.Web.Utilities.DataBase;
using CampaignManager.Web.Utilities.Services;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Web.Components.Features.Admin.Services;

public sealed class AdminService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IDbContextFactory<AppIdentityDbContext> identityDbContextFactory,
    IdentityService identityService,
    ILogger<AdminService> logger)
{
    /// <summary>
    ///     Administrator-only operations must not rely on page-level [Authorize] alone: the service is the
    ///     last line of defence if a caller is ever added outside the admin pages.
    /// </summary>
    private async Task EnsureAdministratorAsync(string operation)
    {
        if (await identityService.IsAdministrator())
            return;

        var email = await identityService.GetCurrentUserEmailAsync();
        logger.LogWarning("Denied administrator operation {Operation} for {Email}", operation, email ?? "<anonymous>");
        throw new UnauthorizedAccessException($"Операция '{operation}' доступна только администратору");
    }

    public async Task<(List<ApplicationUser> Users, int TotalCount)> ListUsersAsync(string? search, int page, int pageSize = 20)
    {
        await EnsureAdministratorAsync("просмотр пользователей");

        try
        {
            await using var db = await identityDbContextFactory.CreateDbContextAsync();
            var query = db.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.ToLower();
                query = query.Where(u => (u.Email != null && u.Email.ToLower().Contains(term))
                                         || (u.UserName != null && u.UserName.ToLower().Contains(term)));
            }

            var total = await query.CountAsync();
            var users = await query
                .OrderBy(u => u.Email)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (users, total);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing users");
            return ([], 0);
        }
    }

    public async Task<bool> SetUserRoleAsync(string email, PlayerRole role)
    {
        await EnsureAdministratorAsync("изменение роли пользователя");

        try
        {
            await using var db = await identityDbContextFactory.CreateDbContextAsync();
            var user = await db.Users.SingleOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email.ToLower());
            if (user is null) return false;

            user.Role = role;
            await db.SaveChangesAsync();
            logger.LogInformation("Role for {Email} changed to {Role}", email, role);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting role for {Email}", email);
            return false;
        }
    }

    public async Task<List<KeeperApplication>> ListApplicationsAsync(KeeperApplicationStatus? status = null)
    {
        await EnsureAdministratorAsync("просмотр заявок");

        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var query = db.KeeperApplications.AsQueryable();

            if (status.HasValue)
                query = query.Where(a => a.Status == status.Value);

            return await query.OrderByDescending(a => a.CreatedAt).ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing keeper applications");
            return [];
        }
    }

    public async Task<KeeperApplication?> SubmitApplicationAsync(string message)
    {
        try
        {
            var email = await identityService.GetCurrentUserEmailAsync();
            if (email is null) return null;

            await using var db = await dbContextFactory.CreateDbContextAsync();

            // Check for existing pending application
            var hasPending = await db.KeeperApplications
                .AnyAsync(a => a.UserEmail.ToLower() == email.ToLower()
                               && a.Status == KeeperApplicationStatus.Pending);
            if (hasPending)
            {
                logger.LogWarning("User {Email} already has a pending application", email);
                return null;
            }

            var user = await identityService.GetUserAsync(email);

            var application = new KeeperApplication
            {
                UserEmail = email,
                UserName = user?.UserName ?? email,
                Message = message,
                Status = KeeperApplicationStatus.Pending
            };
            application.Init();

            db.KeeperApplications.Add(application);
            await db.SaveChangesAsync();

            logger.LogInformation("Keeper application submitted by {Email}", email);
            return application;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error submitting keeper application");
            return null;
        }
    }

    public async Task<bool> ApproveAsync(Guid id)
    {
        await EnsureAdministratorAsync("одобрение заявки");

        try
        {
            var reviewerEmail = await identityService.GetCurrentUserEmailAsync();
            await using var db = await dbContextFactory.CreateDbContextAsync();

            var application = await db.KeeperApplications.FindAsync(id);
            if (application is null || application.Status != KeeperApplicationStatus.Pending) return false;

            application.Status = KeeperApplicationStatus.Approved;
            application.ReviewedByEmail = reviewerEmail;
            application.ReviewedAt = DateTimeOffset.UtcNow;
            application.LastUpdated = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync();

            // Promote user to GameMaster
            await SetUserRoleAsync(application.UserEmail, PlayerRole.GameMaster);

            logger.LogInformation("Keeper application {Id} approved by {Reviewer}", id, reviewerEmail);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error approving keeper application {Id}", id);
            return false;
        }
    }

    public async Task<bool> RejectAsync(Guid id, string? comment = null)
    {
        await EnsureAdministratorAsync("отклонение заявки");

        try
        {
            var reviewerEmail = await identityService.GetCurrentUserEmailAsync();
            await using var db = await dbContextFactory.CreateDbContextAsync();

            var application = await db.KeeperApplications.FindAsync(id);
            if (application is null || application.Status != KeeperApplicationStatus.Pending) return false;

            application.Status = KeeperApplicationStatus.Rejected;
            application.ReviewedByEmail = reviewerEmail;
            application.ReviewedAt = DateTimeOffset.UtcNow;
            application.ReviewComment = comment;
            application.LastUpdated = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync();

            logger.LogInformation("Keeper application {Id} rejected by {Reviewer}", id, reviewerEmail);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error rejecting keeper application {Id}", id);
            return false;
        }
    }

    public async Task<int> GetPendingApplicationsCountAsync()
    {
        if (!await identityService.IsAdministrator())
            return 0;

        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            return await db.KeeperApplications.CountAsync(a => a.Status == KeeperApplicationStatus.Pending);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error counting pending applications");
            return 0;
        }
    }
}
