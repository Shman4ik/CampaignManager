using CampaignManager.Contracts.Admin;

namespace CampaignManager.UI.Admin;

/// <summary>
/// Счётчик у пункта меню «Заявки» — заявок на рассмотрении. Наполняет оболочка (<c>MainLayout</c>) при входе
/// администратора и страница заявок после одобрения или отказа. В v1 то же делал
/// <c>NavigationBadgeService</c>, один на circuit.
/// </summary>
public sealed class AdminBadges
{
    public IReadOnlyDictionary<string, int> Counts { get; private set; } = new Dictionary<string, int>();

    public event Action? Changed;

    /// <summary>Перечитать сводку. Ошибка не мешает меню — счётчик просто остаётся прежним.</summary>
    public async Task RefreshAsync(IAdminApi api, CancellationToken cancellationToken = default)
    {
        try
        {
            var summary = await api.GetSummaryAsync(cancellationToken);
            Counts = new Dictionary<string, int> { [AdminPages.Applications] = summary.PendingApplications };
            Changed?.Invoke();
        }
        catch (HttpRequestException)
        {
        }
    }
}

/// <summary>Адреса страниц админки относительно корня — так их понимают <c>NavLink</c> и <c>NavMenu</c>.</summary>
public static class AdminPages
{
    public const string Users = "admin/users";
    public const string Applications = "admin/applications";
    public const string Files = "admin/files";
}
