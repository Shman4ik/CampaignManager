using CampaignManager.ApiClient.Platform;
using CampaignManager.Contracts.Platform;
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
        return services;
    }
}
