using System.Text;
using System.Text.Json;
using CampaignManager.Contracts.Scenarios;
using CampaignManager.Server.Platform;

namespace CampaignManager.Server.Scenarios;

/// <summary>Импорт, замена и экспорт сценария файлом (T2.5d) — в группе модуля сценариев (вошедшим; права — в сервисе).</summary>
internal static class ScenarioExchangeEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        // Тело — файл как есть: клиенту не нужно его разбирать, а сервер читает его своим контекстом (комментарии, запятые).
        group.MapPost(ScenarioExchangeRoutes.Import, async Task<IResult> (HttpRequest request, ScenarioExchangeService exchange, CancellationToken ct) =>
            {
                var file = await ReadFileAsync(request, ct);
                return TypedResults.Ok(await exchange.ImportAsync(file, DryRun(request), request.Query[ScenarioExchangeRoutes.NameQuery], ct));
            })
            .WithName("ImportScenario")
            .Accepts<ScenarioFile>("application/json");

        group.MapPut(ScenarioExchangeRoutes.ReplacePattern, async Task<IResult> (Guid scenarioId, HttpRequest request, ScenarioExchangeService exchange,
                CancellationToken ct) =>
            {
                var file = await ReadFileAsync(request, ct);
                return TypedResults.Ok(await exchange.ReplaceAsync(scenarioId, file, DryRun(request), request.Query[ScenarioExchangeRoutes.NameQuery], ct));
            })
            .WithName("ReplaceScenario")
            .Accepts<ScenarioFile>("application/json");

        group.MapGet(ScenarioExchangeRoutes.ExportPattern, async Task<IResult> (Guid scenarioId, ScenarioExchangeService exchange, CancellationToken ct) =>
            {
                var file = await exchange.ExportAsync(scenarioId, ct);
                var json = JsonSerializer.Serialize(file, ScenarioFileJsonContext.Readable.ScenarioFile);
                return TypedResults.File(Encoding.UTF8.GetBytes(json), "application/json", $"{FileName(file.Name)}.json");
            })
            .WithName("ExportScenario");
    }

    private static async Task<ScenarioFile> ReadFileAsync(HttpRequest request, CancellationToken ct)
    {
        ScenarioFile? file;
        try
        {
            file = await JsonSerializer.DeserializeAsync(request.Body, ScenarioFileJsonContext.Default.ScenarioFile, ct);
        }
        catch (JsonException ex)
        {
            throw ApiProblemException.Invalid($"Не читается как файл сценария: {ex.Message}");
        }

        return file ?? throw ApiProblemException.Invalid("Файл пуст.");
    }

    private static bool DryRun(HttpRequest request) =>
        bool.TryParse(request.Query[ScenarioExchangeRoutes.DryRunQuery], out var flag) && flag;

    /// <summary>Имя файла из названия: без знаков, которых не любят файловые системы.</summary>
    private static string FileName(string name)
    {
        const string invalid = "\\/:*?\"<>|«»";
        var cleaned = new string([.. name.Select(c => char.IsControl(c) || invalid.Contains(c, StringComparison.Ordinal) ? ' ' : c)]).Trim();
        return cleaned.Length == 0 ? "scenario" : cleaned.Length > 80 ? cleaned[..80].Trim() : cleaned;
    }
}
