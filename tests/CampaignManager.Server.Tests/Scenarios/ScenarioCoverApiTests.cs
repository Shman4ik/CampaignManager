using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using CampaignManager.ApiClient.Scenarios;
using CampaignManager.Contracts;
using CampaignManager.Contracts.Files;
using CampaignManager.Contracts.Identity;
using CampaignManager.Contracts.Platform;
using CampaignManager.Contracts.Scenarios;
using CampaignManager.Core.Identity;
using CampaignManager.Data.Files;
using CampaignManager.Data.Identity;
using CampaignManager.Server.Tests.Campaigns;
using CampaignManager.Server.Tests.Files;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CampaignManager.Server.Tests.Scenarios;

/// <summary>
/// Обложка сценария — своя картинка, а не первая локация: поставить, сменить, убрать; видна в библиотеке и на рабочем месте;
/// права — Edit сценария; токен агента со scope сценариев ставит её; файл сценария переносит её, как иллюстрации локаций.
/// </summary>
public sealed class ScenarioCoverApiTests(CampaignsApp app) : IClassFixture<CampaignsApp>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private ScenariosApiClient Api(User user) => new(app.Http(user));

    private static async Task Fails(HttpStatusCode status, Func<Task> call)
    {
        var error = await Assert.ThrowsAsync<ApiException>(call);
        Assert.Equal(status, error.StatusCode);
    }

    private async Task<Guid[]> FilesAsync(int count)
    {
        await using var db = app.Database.CreateContext();
        var files = Enumerable.Range(0, count).Select(_ => new StoredFile { ExternalUrl = $"https://example.test/{Guid.NewGuid():N}.webp" }).ToArray();
        db.Files.AddRange(files);
        await db.SaveChangesAsync(Cancellation);
        return [.. files.Select(f => f.Id)];
    }

    private async Task<(User Keeper, ScenarioDto Scenario)> ScenarioAsync(string name = "Вечер поэзии")
    {
        var keeper = await app.AddUserAsync(UserRole.Keeper, "Хранитель");
        return (keeper, await Api(keeper).CreateAsync(new ScenarioInput(name), Cancellation));
    }

    [Fact]
    public async Task Cover_is_set_changed_and_removed_and_shows_in_library_and_workspace()
    {
        TestDatabase.SkipIfMissing();
        var (keeper, scenario) = await ScenarioAsync();
        var api = Api(keeper);
        var f = await FilesAsync(2);
        Assert.Null(scenario.CoverFileId);
        Assert.Null(scenario.CoverUrl);

        // Поставить: ответ — новая версия корня (обложка — колонка той же строки), старый If-Match шапки устарел.
        var saved = await api.SetCoverAsync(scenario.Id, f[0], Cancellation);
        Assert.NotEqual(scenario.Version, saved.Version);
        var read = await api.GetAsync(scenario.Id, Cancellation);
        Assert.Equal(f[0], read.CoverFileId);
        Assert.Equal(FilesRoutes.Content(f[0]), read.CoverUrl);
        Assert.Equal(saved.Version, read.Version);
        var row = (await api.ListAsync(Cancellation)).Items.Single(s => s.Id == scenario.Id);
        Assert.Equal(f[0], row.CoverFileId);
        Assert.Equal(FilesRoutes.Content(f[0]), row.CoverUrl);
        var stale = await Assert.ThrowsAsync<ApiException>(() =>
            api.UpdateAsync(scenario.Id, new ScenarioInput("Вечер поэзии"), scenario.Version, Cancellation));
        Assert.Equal(ApiProblemCodes.Stale, stale.Code);

        // Тот же файл ещё раз — без записи (версия прежняя); сменить — другой файл.
        Assert.Equal(saved.Version, (await api.SetCoverAsync(scenario.Id, f[0], Cancellation)).Version);
        await api.SetCoverAsync(scenario.Id, f[1], Cancellation);
        Assert.Equal(f[1], (await api.GetAsync(scenario.Id, Cancellation)).CoverFileId);

        // Живая обложка — не сирота: поиск сирот файлов видит новую ссылку сам (FileReferences по модели).
        Assert.False(await IsOrphanAsync(f[1]));
        Assert.True(await IsOrphanAsync(f[0]));

        // Убрать: обложки нет, файл остаётся в files; повтор ничего не меняет.
        await api.RemoveCoverAsync(scenario.Id, Cancellation);
        await api.RemoveCoverAsync(scenario.Id, Cancellation);
        var removed = await api.GetAsync(scenario.Id, Cancellation);
        Assert.Null(removed.CoverFileId);
        Assert.Null(removed.CoverUrl);
        Assert.Null((await api.ListAsync(Cancellation)).Items.Single(s => s.Id == scenario.Id).CoverUrl);
        await using var db = app.Database.CreateContext();
        Assert.Equal(2, await db.Files.CountAsync(x => f.Contains(x.Id), Cancellation));
    }

    [Fact]
    public async Task Unknown_file_hidden_or_missing_scenario_are_refused_and_other_keeper_may_edit()
    {
        TestDatabase.SkipIfMissing();
        var (keeper, scenario) = await ScenarioAsync();
        var file = (await FilesAsync(1))[0];

        // Файла нет в files — 400.
        await Fails(HttpStatusCode.BadRequest, () => Api(keeper).SetCoverAsync(scenario.Id, Guid.NewGuid(), Cancellation));

        // Игроку сценарий не виден — 404 на оба метода; несуществующий сценарий — 404.
        var player = await app.AddUserAsync();
        await Fails(HttpStatusCode.NotFound, () => Api(player).SetCoverAsync(scenario.Id, file, Cancellation));
        await Fails(HttpStatusCode.NotFound, () => Api(player).RemoveCoverAsync(scenario.Id, Cancellation));
        await Fails(HttpStatusCode.NotFound, () => Api(keeper).SetCoverAsync(Guid.NewGuid(), file, Cancellation));
        await Fails(HttpStatusCode.NotFound, () => Api(keeper).RemoveCoverAsync(Guid.NewGuid(), Cancellation));
        Assert.Null((await Api(keeper).GetAsync(scenario.Id, Cancellation)).CoverFileId);

        // Чужой Хранитель правит (общая библиотека), как и остальные части.
        var otherKeeper = await app.AddUserAsync(UserRole.Keeper);
        await Api(otherKeeper).SetCoverAsync(scenario.Id, file, Cancellation);
        Assert.Equal(file, (await Api(keeper).GetAsync(scenario.Id, Cancellation)).CoverFileId);

        // Без входа — 401.
        using var anonymous = await app.CreateClient().PutAsJsonAsync(ScenariosRoutes.Cover(scenario.Id), new ScenarioCoverInput(file),
            ContractsJsonContext.Default.ScenarioCoverInput, Cancellation);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        // Удаление сценария обложку не держит: файл остаётся, строка сценария уходит.
        await Api(keeper).DeleteAsync(scenario.Id, Cancellation);
        await using var db = app.Database.CreateContext();
        Assert.True(await db.Files.AnyAsync(x => x.Id == file, Cancellation));
    }

    // Скрипт оркестратора: токен агента со scenarios:write ставит обложку тем же способом, что иллюстрации локаций; без scope — 403.
    [Fact]
    public async Task Agent_with_scenarios_scope_sets_and_removes_the_cover()
    {
        TestDatabase.SkipIfMissing();
        var (keeper, scenario) = await ScenarioAsync();
        var file = (await FilesAsync(1))[0];
        HttpClient Agent(string scopes) => app.CreateClient().AsMachine("agents", keeper.Email, scopes);

        using var set = await Agent(MachineScopes.Scenarios).PutAsJsonAsync(ScenariosRoutes.Cover(scenario.Id), new ScenarioCoverInput(file),
            ContractsJsonContext.Default.ScenarioCoverInput, Cancellation);
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        Assert.Equal(file, (await Api(keeper).GetAsync(scenario.Id, Cancellation)).CoverFileId);

        using var onlyFiles = await Agent(MachineScopes.Files).PutAsJsonAsync(ScenariosRoutes.Cover(scenario.Id), new ScenarioCoverInput(file),
            ContractsJsonContext.Default.ScenarioCoverInput, Cancellation);
        Assert.Equal(HttpStatusCode.Forbidden, onlyFiles.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Agent(MachineScopes.CatalogImages).DeleteAsync(ScenariosRoutes.Cover(scenario.Id), Cancellation)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await Agent(MachineScopes.Scenarios).DeleteAsync(ScenariosRoutes.Cover(scenario.Id), Cancellation)).StatusCode);
        Assert.Null((await Api(keeper).GetAsync(scenario.Id, Cancellation)).CoverFileId);
    }

    // Файл сценария переносит обложку как иллюстрации локаций: есть в базе — ставится, нет — пропуск с предупреждением.
    [Fact]
    public async Task Export_carries_the_cover_and_import_sets_known_and_warns_about_foreign()
    {
        TestDatabase.SkipIfMissing();
        var (keeper, scenario) = await ScenarioAsync("Обложка для обмена");
        var file = (await FilesAsync(1))[0];
        await Api(keeper).SetCoverAsync(scenario.Id, file, Cancellation);

        var exchange = new ScenarioExchangeApiClient(app.Http(keeper));
        var exported = await exchange.ExportAsync(scenario.Id, Cancellation);
        Assert.Equal(FilesRoutes.Content(file), JsonNode.Parse(exported)!["coverUrl"]!.GetValue<string>());

        // Копия: та же обложка, экспорт копии совпадает с первым.
        var copy = await exchange.ImportAsync(new MemoryStream(Encoding.UTF8.GetBytes(exported)), dryRun: false, "Копия с обложкой", Cancellation);
        Assert.True(copy.Imported);
        Assert.DoesNotContain(copy.Warnings, w => w.Contains("Обложк", StringComparison.Ordinal));
        Assert.Equal(file, (await Api(keeper).GetAsync(copy.ScenarioId!.Value, Cancellation)).CoverFileId);
        Assert.Equal(exported.Replace("Обложка для обмена", "Копия с обложкой", StringComparison.Ordinal),
            await exchange.ExportAsync(copy.ScenarioId.Value, Cancellation));

        // Обложка другой базы — сценарий заводится без неё, отчёт предупреждает (и в пробном прогоне).
        var foreign = $$"""{ "name": "Чужая обложка", "coverUrl": "{{FilesRoutes.Content(Guid.NewGuid())}}" }""";
        var dry = await exchange.ImportAsync(new MemoryStream(Encoding.UTF8.GetBytes(foreign)), dryRun: true, null, Cancellation);
        Assert.Contains(dry.Warnings, w => w.Contains("Обложки сценария нет в этой базе", StringComparison.Ordinal));
        var report = await exchange.ImportAsync(new MemoryStream(Encoding.UTF8.GetBytes(foreign)), dryRun: false, null, Cancellation);
        Assert.True(report.Imported);
        Assert.Contains(report.Warnings, w => w.Contains("Обложки сценария нет в этой базе", StringComparison.Ordinal));
        Assert.Null((await Api(keeper).GetAsync(report.ScenarioId!.Value, Cancellation)).CoverFileId);

        // Замена берёт обложку из файла, как шапку: файл без обложки снимает её; пробный прогон ничего не трогает.
        var plain = """{ "name": "Обложка для обмена" }""";
        await exchange.ReplaceAsync(scenario.Id, new MemoryStream(Encoding.UTF8.GetBytes(plain)), dryRun: true, null, Cancellation);
        Assert.Equal(file, (await Api(keeper).GetAsync(scenario.Id, Cancellation)).CoverFileId);
        Assert.True((await exchange.ReplaceAsync(scenario.Id, new MemoryStream(Encoding.UTF8.GetBytes(plain)), dryRun: false, null, Cancellation)).Imported);
        Assert.Null((await Api(keeper).GetAsync(scenario.Id, Cancellation)).CoverFileId);
        Assert.True((await exchange.ReplaceAsync(scenario.Id, new MemoryStream(Encoding.UTF8.GetBytes(exported)), dryRun: false, null, Cancellation)).Imported);
        Assert.Equal(file, (await Api(keeper).GetAsync(scenario.Id, Cancellation)).CoverFileId);
    }

    private async Task<bool> IsOrphanAsync(Guid fileId)
    {
        await using var db = app.Database.CreateContext();
        var sql = "select f.* from cm.files f where f.id = {0} and " + FileReferences.NotReferencedSql(db.Model, "f");
        return await db.Files.FromSqlRaw(sql, fileId).AnyAsync(Cancellation);
    }
}
