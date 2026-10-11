using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using CampaignManager.ApiClient.Files;
using CampaignManager.ApiClient.Scenarios;
using CampaignManager.Contracts;
using CampaignManager.Contracts.Catalogs;
using CampaignManager.Contracts.Files;
using CampaignManager.Contracts.Identity;
using CampaignManager.Contracts.Platform;
using CampaignManager.Contracts.Scenarios;
using CampaignManager.Core.Identity;
using CampaignManager.Data.Files;
using CampaignManager.Data.Identity;
using CampaignManager.Server.Files.Storage;
using CampaignManager.Server.Tests.Campaigns;
using CampaignManager.Server.Tests.Files;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace CampaignManager.Server.Tests.Scenarios;

/// <summary>
/// Иллюстрации локации (галерея, первая — обложка): список целиком, добавить одну (обложкой), убрать; права — Edit сценария;
/// токен агента со scope сценариев и файлов грузит картинку и привязывает её к локации; картинки переезжают файлом сценария.
/// </summary>
public sealed class ScenarioLocationImagesApiTests(CampaignsApp app) : IClassFixture<CampaignsApp>
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

    private async Task<(User Keeper, ScenarioDto Scenario, ScenarioLocationDto Location)> LocationAsync()
    {
        var keeper = await app.AddUserAsync(UserRole.Keeper, "Хранитель");
        var scenario = await Api(keeper).CreateAsync(new ScenarioInput("Вечер поэзии"), Cancellation);
        var location = await Api(keeper).AddLocationAsync(scenario.Id, new LocationInput("Кофейня у озера"), Cancellation);
        return (keeper, scenario, location);
    }

    [Fact]
    public async Task Gallery_is_set_whole_added_one_by_one_and_removed()
    {
        TestDatabase.SkipIfMissing();
        var (keeper, scenario, location) = await LocationAsync();
        var api = Api(keeper);
        var f = await FilesAsync(4);

        // Список целиком: порядок — порядок списка, повтор файла не попадёт, пустая подпись — null.
        var saved = await api.SetLocationImagesAsync(scenario.Id, location.Id, new LocationImagesInput(
            [new(f[0], null, " Вид с пирса "), new(f[1], null, ""), new(f[0], null, "двойник")]), Cancellation);
        Assert.Equal([f[0], f[1]], saved.Images.Select(i => i.FileId));
        Assert.Equal(["Вид с пирса", null], saved.Images.Select(i => i.Caption));
        Assert.Equal(FilesRoutes.Content(f[0]), saved.Images[0].Url);

        // Рабочее место отдаёт их в локации; правка самой локации их не трогает.
        await api.UpdateLocationAsync(scenario.Id, location.Id, new LocationInput("Кофейня у озера", "Берег"), Cancellation);
        var read = (await api.GetAsync(scenario.Id, Cancellation)).Locations.Single(l => l.Id == location.Id);
        Assert.Equal([f[0], f[1]], read.Images.Select(i => i.FileId));

        // Одна картинка — в конец; обложкой — первой, прежняя обложка сдвигается, а не уходит; повтор не размножается.
        Assert.Equal([f[0], f[1], f[2]],
            (await api.AddLocationImageAsync(scenario.Id, location.Id, new LocationImageInput(f[2]), Cancellation)).Images.Select(i => i.FileId));
        var covered = await api.AddLocationImageAsync(scenario.Id, location.Id, new LocationImageInput(f[3], "Ритуал", Cover: true), Cancellation);
        Assert.Equal([f[3], f[0], f[1], f[2]], covered.Images.Select(i => i.FileId));
        Assert.Equal("Ритуал", covered.Images[0].Caption);
        var again = await api.AddLocationImageAsync(scenario.Id, location.Id, new LocationImageInput(f[2], Cover: true), Cancellation);
        Assert.Equal([f[2], f[3], f[0], f[1]], again.Images.Select(i => i.FileId));
        Assert.Equal([f[2], f[3], f[0], f[1]],
            (await api.AddLocationImageAsync(scenario.Id, location.Id, new LocationImageInput(f[0]), Cancellation)).Images.Select(i => i.FileId));
        Assert.Equal("Вид с пирса", again.Images[2].Caption); // подпись не стёрта повтором без подписи

        await api.RemoveLocationImageAsync(scenario.Id, location.Id, f[3], Cancellation);
        Assert.Equal([f[2], f[0], f[1]],
            (await api.GetAsync(scenario.Id, Cancellation)).Locations.Single(l => l.Id == location.Id).Images.Select(i => i.FileId));
        await Fails(HttpStatusCode.NotFound, () => api.RemoveLocationImageAsync(scenario.Id, location.Id, f[3], Cancellation));

        // Пустой список убирает всё; файлы остаются в files (сироты — админке).
        Assert.Empty((await api.SetLocationImagesAsync(scenario.Id, location.Id, new LocationImagesInput([]), Cancellation)).Images);
        await using var db = app.Database.CreateContext();
        Assert.Equal(4, await db.Files.CountAsync(x => f.Contains(x.Id), Cancellation));
    }

    [Fact]
    public async Task Unknown_file_too_many_images_and_foreign_or_hidden_scenario_are_refused()
    {
        TestDatabase.SkipIfMissing();
        var (keeper, scenario, location) = await LocationAsync();
        var api = Api(keeper);
        var file = (await FilesAsync(1))[0];

        await Fails(HttpStatusCode.BadRequest, () =>
            api.AddLocationImageAsync(scenario.Id, location.Id, new LocationImageInput(Guid.NewGuid()), Cancellation));
        await Fails(HttpStatusCode.BadRequest, () =>
            api.SetLocationImagesAsync(scenario.Id, location.Id, new LocationImagesInput([new(Guid.NewGuid(), null, null)]), Cancellation));
        var many = await FilesAsync(ScenarioLimits.MaxLocationImages + 1);
        await Fails(HttpStatusCode.BadRequest, () =>
            api.SetLocationImagesAsync(scenario.Id, location.Id, new LocationImagesInput([.. many.Select(id => new CatalogImageDto(id, null, null))]),
                Cancellation));

        // Игроку сценарий не виден — 404; чужой Хранитель правит (общая библиотека); локация чужого сценария по адресу этого — 404.
        var player = await app.AddUserAsync();
        await Fails(HttpStatusCode.NotFound, () =>
            Api(player).AddLocationImageAsync(scenario.Id, location.Id, new LocationImageInput(file), Cancellation));
        await Fails(HttpStatusCode.NotFound, () => Api(player).RemoveLocationImageAsync(scenario.Id, location.Id, file, Cancellation));
        var otherKeeper = await app.AddUserAsync(UserRole.Keeper);
        Assert.Single((await Api(otherKeeper).AddLocationImageAsync(scenario.Id, location.Id, new LocationImageInput(file), Cancellation)).Images);
        var other = await api.CreateAsync(new ScenarioInput("Другой сценарий"), Cancellation);
        await Fails(HttpStatusCode.NotFound, () => api.AddLocationImageAsync(other.Id, location.Id, new LocationImageInput(file), Cancellation));
        await Fails(HttpStatusCode.NotFound, () =>
            api.SetLocationImagesAsync(other.Id, location.Id, new LocationImagesInput([]), Cancellation));

        // Удаление локации уносит её картинки каскадом.
        await api.DeleteLocationAsync(scenario.Id, location.Id, Cancellation);
        await using var db = app.Database.CreateContext();
        Assert.False(await db.ScenarioLocationImages.AnyAsync(i => i.LocationId == location.Id, Cancellation));
    }

    // Скрипт оркестратора: токен агента со scenarios:write и files:write грузит PNG (сервер переводит в WebP) и привязывает его к
    // локации; с одним из двух scope'ов — 403 на другой шаг.
    [Fact]
    public async Task Agent_with_scenarios_and_files_scopes_uploads_and_binds_a_location_image()
    {
        TestDatabase.SkipIfMissing();
        var (keeper, scenario, location) = await LocationAsync();
        var storage = new InMemoryObjectStorage();
        await using var server = app.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IObjectStorage>();
            services.AddSingleton<IObjectStorage>(storage);
        }));
        HttpClient Agent(string scopes) => server.CreateClient().AsMachine("agents", keeper.Email, scopes);

        var agent = Agent($"{MachineScopes.Scenarios} {MachineScopes.Files}");
        await using var png = new MemoryStream(Png());
        var file = await new FilesApiClient(agent).UploadWebpAsync(png, "lakeside-cafe.png", Cancellation);
        Assert.EndsWith(".webp", Assert.Single(storage.Keys), StringComparison.Ordinal);

        using var bind = await agent.PostAsJsonAsync(ScenariosRoutes.LocationImages(scenario.Id, location.Id),
            new LocationImageInput(file.Id, "Кофейня у озера", Cover: true), ContractsJsonContext.Default.LocationImageInput, Cancellation);
        Assert.Equal(HttpStatusCode.OK, bind.StatusCode);
        var bound = await bind.Content.ReadFromJsonAsync(ContractsJsonContext.Default.ScenarioLocationDto, Cancellation);
        Assert.Equal(file.Id, bound!.Images.Single().FileId);

        // Найти локацию по названию скрипту — рабочее место тем же токеном.
        Assert.Equal(HttpStatusCode.OK, (await agent.GetAsync(ScenariosRoutes.Scenario(scenario.Id), Cancellation)).StatusCode);

        // Один scope — только свой шаг.
        using var upload = new MultipartFormDataContent { { new ByteArrayContent(Png()), FilesRoutes.UploadField, "x.png" } };
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Agent(MachineScopes.Scenarios).PostAsync($"{FilesRoutes.Upload}?{FilesRoutes.FormatQuery}={FilesRoutes.WebpFormat}", upload, Cancellation))
            .StatusCode);
        using var bindOnlyFiles = await Agent(MachineScopes.Files).PostAsJsonAsync(ScenariosRoutes.LocationImages(scenario.Id, location.Id),
            new LocationImageInput(file.Id), ContractsJsonContext.Default.LocationImageInput, Cancellation);
        Assert.Equal(HttpStatusCode.Forbidden, bindOnlyFiles.StatusCode);
        using var images = await Agent(MachineScopes.CatalogImages).PutAsJsonAsync(ScenariosRoutes.LocationImages(scenario.Id, location.Id),
            new LocationImagesInput([]), ContractsJsonContext.Default.LocationImagesInput, Cancellation);
        Assert.Equal(HttpStatusCode.Forbidden, images.StatusCode);
    }

    // Файл сценария переносит картинки локации как файл раздатки: есть в базе — ставится, нет — предупреждение строки.
    [Fact]
    public async Task Export_carries_location_images_and_import_sets_known_and_warns_about_foreign()
    {
        TestDatabase.SkipIfMissing();
        var (keeper, scenario, location) = await LocationAsync();
        var f = await FilesAsync(2);
        await Api(keeper).SetLocationImagesAsync(scenario.Id, location.Id,
            new LocationImagesInput([new(f[1], null, "Пирс"), new(f[0], null, null)]), Cancellation);

        var exchange = new ScenarioExchangeApiClient(app.Http(keeper));
        var exported = await exchange.ExportAsync(scenario.Id, Cancellation);
        var images = JsonNode.Parse(exported)!["locations"]![0]!["images"]!.AsArray();
        Assert.Equal([FilesRoutes.Content(f[1]), FilesRoutes.Content(f[0])], images.Select(i => i!["url"]!.GetValue<string>()));
        Assert.Equal("Пирс", images[0]!["caption"]!.GetValue<string>());

        // Копия: картинки той же базы — на месте и в том же порядке; экспорт копии совпадает.
        var copy = await exchange.ImportAsync(new MemoryStream(Encoding.UTF8.GetBytes(exported)), dryRun: false, "Копия с картинками", Cancellation);
        Assert.True(copy.Imported);
        var copied = (await Api(keeper).GetAsync(copy.ScenarioId!.Value, Cancellation)).Locations.Single();
        Assert.Equal([f[1], f[0]], copied.Images.Select(i => i.FileId));
        Assert.Equal(exported.Replace("Вечер поэзии", "Копия с картинками", StringComparison.Ordinal),
            await exchange.ExportAsync(copy.ScenarioId.Value, Cancellation));

        // Картинка другой базы — локация заводится без неё, строка отчёта предупреждает; битый адрес — так же.
        var foreign = $$"""
            { "name": "Чужие картинки", "locations": [ { "name": "Пирс", "images": [
                { "url": "{{FilesRoutes.Content(Guid.NewGuid())}}" }, { "url": "{{FilesRoutes.Content(f[0])}}", "caption": "своя" }, { "url": "https://example.test/x.png" } ] } ] }
            """;
        var report = await exchange.ImportAsync(new MemoryStream(Encoding.UTF8.GetBytes(foreign)), dryRun: false, null, Cancellation);
        Assert.True(report.Imported);
        var line = Assert.Single(report.Lines, l => l.Part == ScenarioImportPart.Location);
        Assert.Equal(ScenarioImportOutcome.Created, line.Outcome);
        Assert.Contains("картинок нет в этой базе (2)", line.Message, StringComparison.Ordinal);
        var imported = (await Api(keeper).GetAsync(report.ScenarioId!.Value, Cancellation)).Locations.Single();
        Assert.Equal([(f[0], "своя")], imported.Images.Select(i => (i.FileId, i.Caption)));

        // Замена своего сценария: картинки — из файла, старые строки уходят с локациями.
        var replaced = await exchange.ReplaceAsync(scenario.Id, new MemoryStream(Encoding.UTF8.GetBytes(foreign)), dryRun: false, null, Cancellation);
        Assert.True(replaced.Imported);
        Assert.Equal([f[0]], (await Api(keeper).GetAsync(scenario.Id, Cancellation)).Locations.Single().Images.Select(i => i.FileId));
    }

    private static byte[] Png()
    {
        using var image = new SkiaSharp.SKBitmap(48, 32);
        for (var y = 0; y < 32; y++)
        {
            for (var x = 0; x < 48; x++)
            {
                image.SetPixel(x, y, new SkiaSharp.SKColor((byte)Random.Shared.Next(256), (byte)(x * 5), (byte)(y * 7)));
            }
        }

        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
