using System.Net.Http.Headers;
using CampaignManager.Contracts;
using CampaignManager.Contracts.Scenarios;

namespace CampaignManager.ApiClient.Scenarios;

/// <summary>Импорт и экспорт сценария файлом (T2.5d). Файл уходит на сервер как есть — клиент его не разбирает.</summary>
public sealed class ScenarioExchangeApiClient(HttpClient http) : IScenarioExchangeApi
{
    public async Task<ScenarioImportReport> ImportAsync(Stream file, bool dryRun, string? name = null, CancellationToken cancellationToken = default)
    {
        using var content = FileContent(file);
        using var response = await http.PostAsync(WithQuery(ScenarioExchangeRoutes.Import, dryRun, name), content, cancellationToken);
        return await ApiResponses.ReadAsync(response, ContractsJsonContext.Default.ScenarioImportReport, cancellationToken, withCode: true);
    }

    public async Task<ScenarioImportReport> ReplaceAsync(Guid scenarioId, Stream file, bool dryRun, string? name = null,
        CancellationToken cancellationToken = default)
    {
        using var content = FileContent(file);
        using var response = await http.PutAsync(WithQuery(ScenarioExchangeRoutes.Replace(scenarioId), dryRun, name), content, cancellationToken);
        return await ApiResponses.ReadAsync(response, ContractsJsonContext.Default.ScenarioImportReport, cancellationToken, withCode: true);
    }

    public async Task<string> ExportAsync(Guid scenarioId, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(ScenarioExchangeRoutes.Export(scenarioId), cancellationToken);
        await ApiResponses.EnsureSuccessAsync(response, cancellationToken, withCode: true);
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static StreamContent FileContent(Stream file)
    {
        var content = new StreamContent(file);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return content;
    }

    private static string WithQuery(string route, bool dryRun, string? name)
    {
        var url = $"{route}?{ScenarioExchangeRoutes.DryRunQuery}={(dryRun ? "true" : "false")}";
        if (!string.IsNullOrWhiteSpace(name))
        {
            url += $"&{ScenarioExchangeRoutes.NameQuery}={Uri.EscapeDataString(name.Trim())}";
        }

        return url;
    }
}
