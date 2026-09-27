using CampaignManager.Web.Components.Features.Admin.Services;

namespace CampaignManager.Web.Components.Layout.Services;

/// <summary>
///     Счётчик заявок на роль Хранителя для бейджа «Заявки» в навигации. Бейдж показывают и
///     боковое меню, и нижняя навигация — оба компонента всегда в разметке (какой из них виден,
///     решает CSS по ширине окна), и раньше каждый считал заявки в базе сам. Сервис scoped, то
///     есть один на circuit: оба получают одну и ту же задачу, а через
///     <see cref="Lifetime" /> счётчик перечитывается, чтобы новая заявка всё-таки доехала.
/// </summary>
public sealed class NavigationBadgeService(AdminService adminService)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);

    private Task<int>? _pending;
    private DateTimeOffset _loadedAt;

    public Task<int> GetPendingApplicationsCountAsync()
    {
        if (_pending is null || _pending.IsFaulted || DateTimeOffset.UtcNow - _loadedAt > Lifetime)
        {
            _loadedAt = DateTimeOffset.UtcNow;
            _pending = adminService.GetPendingApplicationsCountAsync();
        }

        return _pending;
    }
}
