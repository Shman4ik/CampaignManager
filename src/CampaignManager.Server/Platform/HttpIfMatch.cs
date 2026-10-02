using System.Globalization;
using Microsoft.Net.Http.Headers;

namespace CampaignManager.Server.Platform;

/// <summary>Версия строки из <c>If-Match</c> — одна для справочников и листа.</summary>
public static class HttpIfMatch
{
    /// <summary><c>If-Match: "123"</c> → 123; нет заголовка или не версия — null (сервис ответит 428).</summary>
    public static uint? Version(HttpRequest request) =>
        EntityTagHeaderValue.TryParseList(request.Headers.IfMatch, out var tags) && tags.Count == 1
        && uint.TryParse(tags[0].Tag.AsSpan().Trim('"'), NumberStyles.None, CultureInfo.InvariantCulture, out var version)
            ? version
            : null;
}
