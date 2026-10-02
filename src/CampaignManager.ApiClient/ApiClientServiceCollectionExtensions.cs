using CampaignManager.ApiClient.Admin;
using CampaignManager.ApiClient.Campaigns;
using CampaignManager.ApiClient.Catalogs;
using CampaignManager.ApiClient.Characters;
using CampaignManager.ApiClient.Files;
using CampaignManager.ApiClient.Identity;
using CampaignManager.ApiClient.Music;
using CampaignManager.ApiClient.Platform;
using CampaignManager.ApiClient.Profile;
using CampaignManager.ApiClient.Scenarios;
using CampaignManager.Contracts.Admin;
using CampaignManager.Contracts.Campaigns;
using CampaignManager.Contracts.Catalogs;
using CampaignManager.Contracts.Characters;
using CampaignManager.Contracts.Files;
using CampaignManager.Contracts.Identity;
using CampaignManager.Contracts.Music;
using CampaignManager.Contracts.Platform;
using CampaignManager.Contracts.Profile;
using CampaignManager.Contracts.Scenarios;
using Microsoft.Extensions.DependencyInjection;

namespace CampaignManager.ApiClient;

public static class ApiClientServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует клиенты всех модулей API к серверу по адресу <paramref name="baseAddress"/>.
    /// Клиент нового модуля добавляется сюда одной строкой.
    /// </summary>
    public static IServiceCollection AddCampaignManagerApi(this IServiceCollection services, Uri baseAddress)
    {
        services.AddHttpClient<IPingApi, PingApiClient>(http => http.BaseAddress = baseAddress);
        services.AddHttpClient<IIdentityApi, IdentityApiClient>(http => http.BaseAddress = baseAddress);
        // Загрузка трека в 50 МБ по медленной сети идёт дольше стандартных 100 секунд.
        services.AddHttpClient<IFilesApi, FilesApiClient>(http =>
        {
            http.BaseAddress = baseAddress;
            http.Timeout = TimeSpan.FromMinutes(10);
        });
        services.AddHttpClient<ICampaignsApi, CampaignsApiClient>(http => http.BaseAddress = baseAddress);
        services.AddHttpClient<ICharactersApi, CharactersApiClient>(http => http.BaseAddress = baseAddress);

        // Справочники — один клиент на справочник, общий код в CatalogApiClient.
        services.AddHttpClient<ICatalogApi<SkillDto>, SkillsApiClient>(http => http.BaseAddress = baseAddress);
        services.AddHttpClient<ICatalogApi<OccupationDto>, OccupationsApiClient>(http => http.BaseAddress = baseAddress);
        services.AddHttpClient<ICatalogApi<WeaponDto>, WeaponsApiClient>(http => http.BaseAddress = baseAddress);
        services.AddHttpClient<ICatalogApi<SpellDto>, SpellsApiClient>(http => http.BaseAddress = baseAddress);
        services.AddHttpClient<ICatalogApi<BookDto>, BooksApiClient>(http => http.BaseAddress = baseAddress);
        services.AddHttpClient<ICatalogApi<ItemDto>, ItemsApiClient>(http => http.BaseAddress = baseAddress);
        services.AddHttpClient<ICatalogApi<CreatureDto>, CreaturesApiClient>(http => http.BaseAddress = baseAddress);
        services.AddHttpClient<IProfileApi, ProfileApiClient>(http => http.BaseAddress = baseAddress);
        services.AddHttpClient<IAdminApi, AdminApiClient>(http => http.BaseAddress = baseAddress);

        // Фонотека: треки — тот же справочник, сверх него — настроения и пул.
        services.AddHttpClient<ICatalogApi<MusicTrackDto>, MusicTracksApiClient>(http => http.BaseAddress = baseAddress);
        services.AddHttpClient<IMusicApi, MusicApiClient>(http => http.BaseAddress = baseAddress);
        services.AddHttpClient<IScenariosApi, ScenariosApiClient>(http => http.BaseAddress = baseAddress);
        return services;
    }
}
