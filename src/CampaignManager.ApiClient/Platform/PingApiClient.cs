using System.Net.Http.Json;
using CampaignManager.Contracts;
using CampaignManager.Contracts.Platform;

namespace CampaignManager.ApiClient.Platform;

public sealed class PingApiClient(HttpClient http) : IPingApi
{
    public async Task<PingResponse> PingAsync(CancellationToken cancellationToken = default) =>
        await http.GetFromJsonAsync(PlatformRoutes.Ping, ContractsJsonContext.Default.PingResponse, cancellationToken)
        ?? throw new InvalidOperationException($"Пустой ответ от {PlatformRoutes.Ping}.");
}
