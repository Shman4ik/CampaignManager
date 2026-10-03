using System.Net.Http.Json;
using System.Text.Json;
using CampaignManager.Contracts;
using CampaignManager.Contracts.Profile;

namespace CampaignManager.ApiClient.Profile;

public sealed class ProfileApiClient(HttpClient http) : IProfileApi
{
    private static ContractsJsonContext Json => ContractsJsonContext.Default;

    public async Task<ProfileDto> GetProfileAsync(CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(ProfileRoutes.Profile, cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.ProfileDto, cancellationToken);
    }

    public async Task<ProfileDto> UpdateDisplayNameAsync(UpdateDisplayNameRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await http.PutAsJsonAsync(ProfileRoutes.DisplayName, request, Json.UpdateDisplayNameRequest, cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.ProfileDto, cancellationToken);
    }

    public async Task<ProfileDto> SubmitKeeperApplicationAsync(SubmitKeeperApplicationRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync(ProfileRoutes.KeeperApplication, request, Json.SubmitKeeperApplicationRequest,
            cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.ProfileDto, cancellationToken);
    }

    public async Task<ProfileDto> UpdateKeeperApplicationAsync(SubmitKeeperApplicationRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await http.PutAsJsonAsync(ProfileRoutes.KeeperApplication, request, Json.SubmitKeeperApplicationRequest,
            cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.ProfileDto, cancellationToken);
    }

    public async Task<ProfileDto> WithdrawKeeperApplicationAsync(CancellationToken cancellationToken = default)
    {
        using var response = await http.DeleteAsync(ProfileRoutes.KeeperApplication, cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.ProfileDto, cancellationToken);
    }

    public async Task<PreferencesDto> GetPreferencesAsync(CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(ProfileRoutes.Preferences, cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.PreferencesDto, cancellationToken);
    }

    public async Task SetPreferenceAsync(string key, JsonElement value, CancellationToken cancellationToken = default)
    {
        using var response = await http.PutAsJsonAsync(ProfileRoutes.Preference(key), value, Json.JsonElement, cancellationToken);
        await ApiResponses.EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task RemovePreferenceAsync(string key, CancellationToken cancellationToken = default)
    {
        using var response = await http.DeleteAsync(ProfileRoutes.Preference(key), cancellationToken);
        await ApiResponses.EnsureSuccessAsync(response, cancellationToken);
    }
}
