using System.Text.Json;
using CampaignManager.Contracts.Profile;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Identity;
using CampaignManager.Data;
using CampaignManager.Data.Identity;
using CampaignManager.Server.Access;
using CampaignManager.Server.Platform;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CampaignManager.Server.Profile;

/// <summary>
/// Личный кабинет: имя, заявка на Хранителя, настройки. Только свои данные — пользователь берётся из
/// <see cref="CurrentUser"/>, чужой id сервис не принимает (Access, строка «Профиль»). Знание модуля —
/// <c>Profile/CLAUDE.md</c>.
/// </summary>
public sealed class ProfileService(
    CmDbContext dbContext,
    CurrentUser currentUser,
    TimeProvider timeProvider,
    ILogger<ProfileService> logger)
{
    public async Task<ProfileDto> GetAsync(CancellationToken cancellationToken)
    {
        var me = await RequireUserAsync(cancellationToken);

        var user = await dbContext.Users.Where(u => u.Id == me.Id)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.DisplayName,
                u.Role,
                CampaignCount = dbContext.CampaignMembers.Count(m => m.UserId == u.Id),
                AliasCount = dbContext.CampaignMembers.Count(m => m.UserId == u.Id && m.DisplayName != null),
                // Сыщик в кабинете — лист игрока; НПС и прегены владельца не имеют.
                CharacterCount = dbContext.Characters.Count(c =>
                    c.OwnerId == u.Id && c.Kind == CharacterKind.Player && c.Status != CharacterStatus.Archived),
            })
            .SingleAsync(cancellationToken);

        var application = await dbContext.KeeperApplications
            .Where(a => a.UserId == me.Id)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new MyKeeperApplicationDto(a.Id, a.Status, a.Message, a.CreatedAt, a.ReviewedAt, a.ReviewComment))
            .FirstOrDefaultAsync(cancellationToken);

        var canApply = user.Role is UserRole.Player && application?.Status is not KeeperApplicationStatus.Pending;
        return new ProfileDto(user.Id, user.Email, user.DisplayName, user.Role, user.CampaignCount, user.CharacterCount,
            user.AliasCount, application, canApply);
    }

    /// <summary>
    /// Новое отображаемое имя. <see cref="UpdateDisplayNameRequest.ReplaceAliases"/> сбрасывает псевдонимы
    /// во всех моих кампаниях той же транзакцией — там я снова под именем профиля. В v1 имя в кампании было
    /// копией, снятой при вступлении, и замена переписывала копии; в 2.0 пустой псевдоним и есть «имя профиля».
    /// </summary>
    public async Task<ProfileDto> UpdateDisplayNameAsync(UpdateDisplayNameRequest request, CancellationToken cancellationToken)
    {
        var me = await RequireUserAsync(cancellationToken);
        var name = ValidateName(request.DisplayName);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var user = await dbContext.Users.SingleAsync(u => u.Id == me.Id, cancellationToken);
        user.DisplayName = name;
        await dbContext.SaveChangesAsync(cancellationToken);

        var aliases = 0;
        if (request.ReplaceAliases)
        {
            aliases = await dbContext.CampaignMembers
                .Where(m => m.UserId == me.Id && m.DisplayName != null)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.DisplayName, (string?)null), cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Пользователь {UserId} сменил имя (псевдонимов сброшено: {Aliases})", me.Id, aliases);
        return await GetAsync(cancellationToken);
    }

    /// <summary>
    /// Заявка на роль Хранителя — любой вошедший игрок. Одна на рассмотрении: второй раз — 409 (и уникальный
    /// частичный индекс в базе на случай двух вкладок). Хранителю и администратору подавать незачем — 409.
    /// </summary>
    public async Task<ProfileDto> SubmitKeeperApplicationAsync(SubmitKeeperApplicationRequest request, CancellationToken cancellationToken)
    {
        var me = await RequireUserAsync(cancellationToken);
        if (me.IsKeeper)
        {
            throw ApiProblemException.Conflict("Вы уже Хранитель — заявка не нужна.");
        }

        var message = request.Message?.Trim() ?? "";
        if (message.Length > ProfileLimits.ApplicationMessageLength)
        {
            throw ApiProblemException.Invalid($"Текст заявки — не длиннее {ProfileLimits.ApplicationMessageLength} символов.");
        }

        if (await dbContext.KeeperApplications.AnyAsync(a => a.UserId == me.Id && a.Status == KeeperApplicationStatus.Pending,
                cancellationToken))
        {
            throw PendingConflict();
        }

        dbContext.KeeperApplications.Add(new KeeperApplication
        {
            UserId = me.Id,
            Message = message,
            Status = KeeperApplicationStatus.Pending,
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw PendingConflict();
        }

        logger.LogInformation("Пользователь {UserId} подал заявку на роль Хранителя", me.Id);
        return await GetAsync(cancellationToken);

        static ApiProblemException PendingConflict() =>
            ApiProblemException.Conflict("Заявка уже отправлена и ждёт рассмотрения.");
    }

    public async Task<PreferencesDto> GetPreferencesAsync(CancellationToken cancellationToken)
    {
        var me = await RequireUserAsync(cancellationToken);
        var rows = await dbContext.UserPreferences.Where(p => p.UserId == me.Id).ToListAsync(cancellationToken);
        return new PreferencesDto(rows.ToDictionary(p => p.Key, p => p.Value.RootElement.Clone(), StringComparer.Ordinal));
    }

    /// <summary>
    /// Одна настройка — одна строка: запись с другого устройства затрагивает только свой ключ (в v1 весь
    /// словарь писался целиком, и два устройства затирали друг друга).
    /// </summary>
    public async Task SetPreferenceAsync(string key, JsonElement value, CancellationToken cancellationToken)
    {
        var me = await RequireUserAsync(cancellationToken);
        ValidateKey(key);
        var json = value.GetRawText();
        if (json.Length > ProfileLimits.PreferenceValueLength)
        {
            throw ApiProblemException.Invalid($"Значение настройки — не больше {ProfileLimits.PreferenceValueLength} символов JSON.");
        }

        // Upsert одним запросом: две вкладки, впервые пишущие один ключ, не столкнутся на первичном ключе.
        var now = timeProvider.GetUtcNow();
        await dbContext.Database.ExecuteSqlAsync(
            $"""
             insert into cm.user_preferences (user_id, key, value, updated_at)
             values ({me.Id}, {key}, {json}::jsonb, {now})
             on conflict (user_id, key) do update set value = excluded.value, updated_at = excluded.updated_at
             """,
            cancellationToken);
    }

    public async Task RemovePreferenceAsync(string key, CancellationToken cancellationToken)
    {
        var me = await RequireUserAsync(cancellationToken);
        ValidateKey(key);
        await dbContext.UserPreferences.Where(p => p.UserId == me.Id && p.Key == key).ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>
    /// Имя: не пустое, не длиннее <see cref="ProfileLimits.DisplayNameLength"/> и без «@» — имя видят другие, а имя,
    /// похожее на почту, сервер другим не показывает вовсе (<c>PublicNames</c>): почту не показываем никому.
    /// </summary>
    private static string ValidateName(string? displayName)
    {
        var name = displayName?.Trim() ?? "";
        if (name.Length == 0)
        {
            throw ApiProblemException.Invalid("Имя не может быть пустым.");
        }

        if (name.Length > ProfileLimits.DisplayNameLength)
        {
            throw ApiProblemException.Invalid($"Имя — не длиннее {ProfileLimits.DisplayNameLength} символов.");
        }

        if (name.Contains('@', StringComparison.Ordinal))
        {
            throw ApiProblemException.Invalid("Имя без «@»: его видят другие игроки, а почту мы никому не показываем.");
        }

        return name;
    }

    private static void ValidateKey(string key)
    {
        if (!PreferenceKeys.IsValid(key))
        {
            throw ApiProblemException.Invalid("Неверный ключ настройки.");
        }
    }

    private async Task<SignedInUser> RequireUserAsync(CancellationToken cancellationToken) =>
        await currentUser.GetAsync(cancellationToken) ?? throw AccessDeniedException.Forbidden();
}
