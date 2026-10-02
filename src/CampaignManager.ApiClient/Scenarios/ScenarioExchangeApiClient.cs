using System.Net.Http.Headers;
using CampaignManager.Contracts;
using CampaignManager.Contracts.Scenarios;

namespace CampaignManager.ApiClient.Scenarios;

/// <summary>Импорт и экспорт сценария файлом (T2.5d). Файл уходит на сервер как есть — клиент его не разбирает.</summary>
public sealed class ScenarioExchangeApiClient(HttpClient http) : IScenarioExchangeApi
{
    public async Task<ScenarioImportReport> ImportAsync(Stream file, bool dryRun, string? name = null, CancellationToken cancellationToken = default)
    {
        using var content = new StreamContent(file);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        var url = $"{ScenarioExchangeRoutes.Import}?{ScenarioExchangeRoutes.DryRunQuery}={(dryRun ? "true" : "false")}";
        if (!string.IsNullOrWhiteSpace(name))
        {
            url += $"&{ScenarioExchangeRoutes.NameQuery}={Uri.EscapeDataString(name.Trim())}";
        }

        using var response = await http.PostAsync(url, content, cancellationToken);
        return await ApiResponses.ReadAsync(response, ContractsJsonContext.Default.ScenarioImportReport, cancellationToken, withCode: true);
    }

    public async Task<string> ExportAsync(Guid scenarioId, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(ScenarioExchangeRoutes.Export(scenarioId), cancellationToken);
        await ApiResponses.EnsureSuccessAsync(response, cancellationToken, withCode: true);
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}
