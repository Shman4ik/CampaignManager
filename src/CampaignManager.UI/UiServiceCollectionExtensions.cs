using CampaignManager.Core.Dice;
using CampaignManager.UI.Admin;
using CampaignManager.UI.KeeperScreen;
using CampaignManager.UI.Music;
using CampaignManager.UI.Platform;
using CampaignManager.UI.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CampaignManager.UI;

public static class UiServiceCollectionExtensions
{
    /// <summary>
    /// Службы UI-кита: диалог подтверждения, уведомления, состояние связи, кости. В WebAssembly
    /// синглтон — это одна вкладка браузера.
    /// </summary>
    /// <param name="isDevelopment">Окружение хоста: страницы /dev/* видны только в Development.</param>
    public static IServiceCollection AddCampaignManagerUi(this IServiceCollection services, bool isDevelopment)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(DiceRoller.Shared);
        services.AddSingleton(new UiEnvironment(isDevelopment));
        services.AddSingleton<ApiActivity>();
        services.AddSingleton<DialogService>();
        services.AddSingleton<ToastService>();
        services.AddSingleton<KeeperScreenState>();
        // Плеер — на вкладку: переходы между страницами музыку не обрывают.
        services.AddSingleton(_ => new MusicPlayer());
        services.AddSingleton<BrowserStorage>();
        services.AddSingleton<AdminBadges>();
        return services;
    }
}

/// <summary>Что UI знает об окружении хоста.</summary>
public sealed record UiEnvironment(bool IsDevelopment);
