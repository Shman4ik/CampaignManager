using System.Net;
using System.Net.Http.Json;
using CampaignManager.Contracts;
using CampaignManager.Contracts.Identity;

namespace CampaignManager.ApiClient.Identity;

public sealed class IdentityApiClient(HttpClient http) : IIdentityApi
{
    public async Task<MeResponse?> GetMeAsync(CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(IdentityRoutes.Me, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(ContractsJsonContext.Default.MeResponse, cancellationToken);
    }
}
