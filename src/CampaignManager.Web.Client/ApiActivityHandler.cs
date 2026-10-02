using CampaignManager.UI.Platform;

namespace CampaignManager.Web.Client;

/// <summary>
/// Каждый запрос <c>ApiClient</c> проходит здесь и сообщает <see cref="ApiActivity"/>: запись в пути,
/// сервер ответил, сервер недоступен. Любой код ответа значит «связь есть» — 409 или 500 страница
/// показывает сама; «нет связи» — только когда ответа не было вовсе.
/// </summary>
internal sealed class ApiActivityHandler(ApiActivity activity) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var isWrite = request.Method != HttpMethod.Get && request.Method != HttpMethod.Head && request.Method != HttpMethod.Options;
        using var write = isWrite ? activity.BeginWrite() : null;
        try
        {
            var response = await base.SendAsync(request, cancellationToken);
            activity.ReportReachable(saved: isWrite && response.IsSuccessStatusCode);
            return response;
        }
        catch (HttpRequestException)
        {
            activity.ReportUnreachable();
            throw;
        }
    }
}
