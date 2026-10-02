using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace CampaignManager.ApiClient;

/// <summary>
/// Ответы API для клиентов модулей: успех — тело по сгенерированному контексту, отказ —
/// <see cref="HttpRequestException"/> с кодом и текстом ProblemDetails (его и показывает UI).
/// </summary>
internal static class ApiResponses
{
    public static async Task<T> ReadAsync<T>(HttpResponseMessage response, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync(typeInfo, cancellationToken)
            ?? throw new InvalidOperationException($"Пустой ответ от {response.RequestMessage?.RequestUri}.");
    }

    public static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(await ReadProblemAsync(response, cancellationToken), null, response.StatusCode);
        }
    }

    // ProblemDetails читается без типа: нужен только текст для пользователя.
    private static async Task<string> ReadProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var fallback = $"{(int)response.StatusCode} {response.ReasonPhrase}";
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            using var problem = JsonDocument.Parse(body);
            foreach (var name in (ReadOnlySpan<string>)["detail", "title"])
            {
                if (problem.RootElement.TryGetProperty(name, out var value) && value.GetString() is { Length: > 0 } text)
                {
                    return text;
                }
            }
        }
        catch (JsonException)
        {
        }

        return fallback;
    }
}
