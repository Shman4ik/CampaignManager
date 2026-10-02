using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using CampaignManager.Contracts.Platform;

namespace CampaignManager.ApiClient;

/// <summary>
/// Ответы API для клиентов модулей: успех — тело по сгенерированному контексту, отказ —
/// <see cref="HttpRequestException"/> с кодом статуса и текстом ProblemDetails (его и показывает UI).
/// С <c>withCode</c> — <see cref="ApiException"/> (наследник), ещё и с машинным кодом отказа
/// (<see cref="ApiProblemCodes"/>: устаревшая версия, дубль, занято): по нему справочники предлагают
/// «перечитать». Тип исключения у остальных модулей не меняется — их тесты ждут ровно HttpRequestException.
/// </summary>
internal static class ApiResponses
{
    public static async Task<T> ReadAsync<T>(HttpResponseMessage response, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken, bool withCode = false)
    {
        await EnsureSuccessAsync(response, cancellationToken, withCode);
        return await response.Content.ReadFromJsonAsync(typeInfo, cancellationToken)
            ?? throw new InvalidOperationException($"Пустой ответ от {response.RequestMessage?.RequestUri}.");
    }

    public static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken, bool withCode = false)
    {
        if (!response.IsSuccessStatusCode)
        {
            var (message, code) = await ReadProblemAsync(response, cancellationToken);
            throw withCode
                ? new ApiException(message, response.StatusCode, code)
                : new HttpRequestException(message, null, response.StatusCode);
        }
    }

    // ProblemDetails читается без типа: нужны только текст для пользователя и код.
    private static async Task<(string Message, string? Code)> ReadProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var message = $"{(int)response.StatusCode} {response.ReasonPhrase}";
        string? code = null;
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            using var problem = JsonDocument.Parse(body);
            var root = problem.RootElement;
            if (root.TryGetProperty(ApiProblemCodes.Extension, out var codeValue) && codeValue.ValueKind == JsonValueKind.String)
            {
                code = codeValue.GetString();
            }

            foreach (var name in (ReadOnlySpan<string>)["detail", "title"])
            {
                if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                    && value.GetString() is { Length: > 0 } text)
                {
                    message = text;
                    break;
                }
            }
        }
        catch (JsonException)
        {
        }

        return (message, code);
    }
}
