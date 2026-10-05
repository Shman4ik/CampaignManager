using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using CampaignManager.Contracts;
using CampaignManager.Contracts.Campaigns;
using CampaignManager.Contracts.Files;
using CampaignManager.Contracts.Identity;
using CampaignManager.Contracts.Scenarios;
using CampaignManager.Core.Identity;
using CampaignManager.Server.Identity;
using CampaignManager.Server.Tests.Campaigns;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CampaignManager.Server.Tests.Identity;

/// <summary>Разбор токена client credentials: допущенное приложение — почта его человека и scope'ы, чужое — отказ.</summary>
public sealed class MachinePrincipalTests
{
    private static ClaimsPrincipal Token(string clientId, string scope) => new(new ClaimsIdentity(
    [
        new Claim("gty", MachineAccess.GrantType),
        new Claim("azp", clientId),
        new Claim("sub", $"{clientId}@clients"),
        new Claim("scope", scope),
    ]));

    [Fact]
    public void Allowed_client_acts_as_its_user_with_token_scopes()
    {
        MachineClient[] clients = [new() { ClientId = "agents", ActAs = " owner@example.test " }];

        var (principal, failure) = MachineAccess.CreatePrincipal(Token("agents", "scenarios:write  files:write"), clients, "Bearer");

        Assert.Null(failure);
        Assert.NotNull(principal);
        Assert.True(MachineAccess.IsMachine(principal));
        Assert.Equal("owner@example.test", principal.FindFirstValue(ClaimTypes.Email));
        Assert.Equal("agents@clients", principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.True(MachineAccess.HasScope(principal, MachineScopes.Scenarios));
        Assert.True(MachineAccess.HasScope(principal, MachineScopes.Files));
        Assert.Null(principal.FindFirstValue(CmClaims.UserId));
    }

    [Theory]
    [InlineData("stranger")]
    [InlineData("")]
    public void Client_not_in_config_is_rejected(string clientId)
    {
        MachineClient[] clients = [new() { ClientId = "agents", ActAs = "owner@example.test" }, new() { ClientId = "", ActAs = "x@example.test" }];

        var (principal, failure) = MachineAccess.CreatePrincipal(Token(clientId, MachineScopes.Scenarios), clients, "Bearer");

        Assert.Null(principal);
        Assert.Contains("MachineClients", failure);
    }

    [Fact]
    public void Only_client_credentials_tokens_are_machine_tokens()
    {
        Assert.True(MachineAccess.IsMachineToken(Token("agents", "")));
        Assert.False(MachineAccess.IsMachineToken(new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "google-oauth2|1")]))));
    }
}

/// <summary>
/// Токен агента через весь конвейер: работает от имени человека из конфигурации, пользователей не заводит и не привязывает,
/// ходит только на адреса, открытые его scope'ам (запрет по умолчанию).
/// </summary>
public sealed class MachineAccessApiTests(CampaignsApp app) : IClassFixture<CampaignsApp>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private HttpClient Agent(string actAs, string scopes) => app.CreateClient().AsMachine("agents", actAs, scopes);

    [Fact]
    public async Task Agent_works_on_scenarios_as_its_user()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper, "Хранитель");
        var agent = Agent(keeper.Email, $"{MachineScopes.Scenarios} {MachineScopes.Files}");

        var me = await agent.GetFromJsonAsync(IdentityRoutes.Me, ContractsJsonContext.Default.MeResponse, Cancellation);
        Assert.Equal(keeper.Id, me!.Id);

        var body = new StringContent("""{ "name": "Сценарий агента", "locations": [ { "name": "Дом" } ] }""", Encoding.UTF8, "application/json");
        using var import = await agent.PostAsync($"{ScenarioExchangeRoutes.Import}?dryRun=false", body, Cancellation);
        Assert.Equal(HttpStatusCode.OK, import.StatusCode);
        var report = await import.Content.ReadFromJsonAsync(ContractsJsonContext.Default.ScenarioImportReport, Cancellation);

        await using var db = app.Database.CreateContext();
        var scenario = await db.Scenarios.SingleAsync(s => s.Id == report!.ScenarioId, Cancellation);
        Assert.Equal(keeper.Id, scenario.AuthorId);

        using var list = await agent.GetAsync(ScenariosRoutes.Scenarios, Cancellation);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
    }

    [Fact]
    public async Task Agent_is_denied_everything_outside_its_scopes()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Admin, "Админ");
        var scenarios = Agent(keeper.Email, MachineScopes.Scenarios);
        var files = Agent(keeper.Email, MachineScopes.Files);

        // Не помеченные адреса закрыты при любом scope: кампании, прохождения сценария, админка файлов.
        Assert.Equal(HttpStatusCode.Forbidden, (await scenarios.GetAsync(CampaignsRoutes.Campaigns, Cancellation)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await scenarios.GetAsync($"{ScenariosRoutes.Scenarios}/{Guid.NewGuid()}/runs", Cancellation)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await files.GetAsync(FilesRoutes.Orphans, Cancellation)).StatusCode);

        // Помеченные — только со своим scope.
        Assert.Equal(HttpStatusCode.Forbidden, (await files.GetAsync(ScenariosRoutes.Scenarios, Cancellation)).StatusCode);
        using var upload = new MultipartFormDataContent { { new ByteArrayContent([1, 2, 3]), FilesRoutes.UploadField, "x.png" } };
        Assert.Equal(HttpStatusCode.Forbidden, (await scenarios.PostAsync(FilesRoutes.Upload, upload, Cancellation)).StatusCode);

        // Страницы приложения агенту тоже закрыты.
        Assert.Equal(HttpStatusCode.Forbidden, (await scenarios.GetAsync("/scenarios", Cancellation)).StatusCode);
    }

    [Fact]
    public async Task Agent_neither_links_nor_creates_users()
    {
        TestDatabase.SkipIfMissing();
        var migrated = await app.AddUserAsync(UserRole.Keeper); // перенесённый из v1: sub пуст

        Assert.Equal(HttpStatusCode.OK, (await Agent(migrated.Email, "").GetAsync(IdentityRoutes.Me, Cancellation)).StatusCode);

        var stranger = $"{Guid.NewGuid():N}@example.test";
        Assert.NotEqual(HttpStatusCode.OK, (await Agent(stranger, "").GetAsync(IdentityRoutes.Me, Cancellation)).StatusCode);

        await using var db = app.Database.CreateContext();
        Assert.Null(await db.Users.Where(u => u.Id == migrated.Id).Select(u => u.Auth0Sub).SingleAsync(Cancellation));
        Assert.False(await db.Users.AnyAsync(u => u.Email == stranger, Cancellation));
    }
}
