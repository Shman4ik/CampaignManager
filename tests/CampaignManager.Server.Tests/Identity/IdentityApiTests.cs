using System.Net;
using CampaignManager.ApiClient.Identity;
using CampaignManager.Contracts;
using CampaignManager.Contracts.Identity;
using CampaignManager.Core.Identity;
using CampaignManager.Data.Identity;
using CampaignManager.Server.Access;
using CampaignManager.Server.Identity;
using CampaignManager.Server.Tests.Schema;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.Server.Tests.Identity;

/// <summary>Сервер со своей одноразовой базой со схемой <c>cm</c>.</summary>
public sealed class IdentityApp : CmApp, IAsyncLifetime
{
    public SchemaDatabase Database { get; } = new();

    protected override string ConnectionString =>
        TestDatabase.ConnectionString is null ? TestDatabase.Unreachable : Database.ConnectionString;

    ValueTask IAsyncLifetime.InitializeAsync() => Database.InitializeAsync();

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await Database.DisposeAsync();
    }

    public HttpClient Browser() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://localhost"),
    });

    public async Task<User> AddUserAsync(UserRole role, string? subject = null)
    {
        await using var db = Database.CreateContext();
        var user = new User { Email = $"{Guid.NewGuid():N}@example.test", DisplayName = "Сыщик", Role = role, Auth0Sub = subject };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }
}

public sealed class IdentityApiTests(IdentityApp app) : IClassFixture<IdentityApp>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    // Клиенту WebAssembly и приложению нужен 401, а не редирект на страницу входа.
    [Fact]
    public async Task Me_without_session_is_401_not_redirect()
    {
        var response = await app.Browser().GetAsync(IdentityRoutes.Me, Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(await new IdentityApiClient(app.Browser()).GetMeAsync(Cancellation));
    }

    // Роль — из cm.users на каждый запрос: в v1 новая роль доезжала через пять минут кэша.
    [Fact]
    public async Task Me_reads_role_from_database_on_every_request()
    {
        TestDatabase.SkipIfMissing();
        var user = await app.AddUserAsync(UserRole.Player);
        var api = new IdentityApiClient(app.Browser().As(user.Id));

        Assert.Equal(UserRole.Player, (await api.GetMeAsync(Cancellation))?.Role);

        await using (var db = app.Database.CreateContext())
        {
            await db.Users.Where(u => u.Id == user.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Role, UserRole.Keeper), Cancellation);
        }

        var me = await api.GetMeAsync(Cancellation);
        Assert.Equal(new MeResponse(user.Id, user.Email, user.DisplayName, UserRole.Keeper), me);
    }

    // Куки v1 и токены приложения несут sub и почту, а не id: пользователь находится по ним.
    [Fact]
    public async Task Session_without_user_id_is_resolved_by_sub()
    {
        TestDatabase.SkipIfMissing();
        var user = await app.AddUserAsync(UserRole.Keeper, subject: $"google-oauth2|{Guid.NewGuid():N}");

        var me = await new IdentityApiClient(app.Browser().AsToken(user.Auth0Sub!, "changed@example.test")).GetMeAsync(Cancellation);

        Assert.Equal(user.Id, me?.Id);
    }

    // Первый запрос приложения с токеном — это и есть вход: перенесённый пользователь связывается по почте.
    [Fact]
    public async Task First_token_request_links_migrated_user_by_email()
    {
        TestDatabase.SkipIfMissing();
        var migrated = await app.AddUserAsync(UserRole.Keeper);
        var subject = $"auth0|{Guid.NewGuid():N}";

        var me = await new IdentityApiClient(app.Browser().AsToken(subject, migrated.Email)).GetMeAsync(Cancellation);

        Assert.Equal((migrated.Id, UserRole.Keeper), (me?.Id, me?.Role));
        await using var db = app.Database.CreateContext();
        Assert.Equal(subject, await db.Users.Where(u => u.Id == migrated.Id).Select(u => u.Auth0Sub).SingleAsync(Cancellation));
    }

    [Fact]
    public async Task Session_of_deleted_user_is_not_accepted()
    {
        TestDatabase.SkipIfMissing();
        var response = await app.Browser().As(Guid.CreateVersion7()).GetAsync(IdentityRoutes.Me, Cancellation);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Каждый эндпоинт API закрыт авторизацией; анонимно — только то, что перечислено здесь.
    [Fact]
    public void Every_api_endpoint_requires_authorization()
    {
        string[] anonymous = [Contracts.Platform.PlatformRoutes.Ping];
        _ = app.Server;
        var endpoints = app.Services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(e => ("/" + e.RoutePattern.RawText?.TrimStart('/')).StartsWith(ApiRoutes.Prefix, StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(endpoints);
        Assert.All(endpoints, endpoint =>
        {
            var route = "/" + endpoint.RoutePattern.RawText!.TrimStart('/');
            var open = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null
                       || endpoint.Metadata.GetMetadata<IAuthorizeData>() is null;
            Assert.True(!open || anonymous.Contains(route), $"{route} доступен без входа");
        });
    }

    [Theory]
    [InlineData(404, "Не найдено.")]
    [InlineData(403, "Недостаточно прав.")]
    public async Task Access_denial_becomes_problem_details(int status, string title)
    {
        var context = new DefaultHttpContext { RequestServices = app.Services };
        context.Response.Body = new MemoryStream();
        var denied = status == 404 ? AccessDeniedException.NotFound() : AccessDeniedException.Forbidden();

        var handled = await new AccessExceptionHandler(app.Services.GetRequiredService<IProblemDetailsService>())
            .TryHandleAsync(context, denied, Cancellation);

        Assert.True(handled);
        Assert.Equal(status, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        Assert.Contains(title, await new StreamReader(context.Response.Body).ReadToEndAsync(Cancellation));
    }
}
