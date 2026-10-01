namespace CampaignManager.Contracts.Platform;

public interface IPingApi
{
    Task<PingResponse> PingAsync(CancellationToken cancellationToken = default);
}
