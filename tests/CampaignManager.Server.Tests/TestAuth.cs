using System.Security.Claims;
using System.Text.Encodings.Web;
using CampaignManager.Server.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace CampaignManager.Server.Tests;

/// <summary>
/// Сессия в тестах — заголовками вместо куки или JWT: <see cref="UserIdHeader"/> — как кука 2.0,
/// <see cref="SubjectHeader"/> + <see cref="EmailHeader"/> — как токен приложения или кука v1,
/// <see cref="MachineHeader"/> (+ <see cref="ScopeHeader"/>, почта — от чьего имени) — как токен агента (M2M).
/// Без заголовков запрос идёт настоящей схемой (кука), так что ответы анониму — настоящие.
/// Метаданные Auth0 заданы руками: вход и выход строят адрес Auth0, не ходя в сеть.
/// </summary>
public static class TestAuth
{
    public const string Scheme = "Test";
    public const string UserIdHeader = "X-Test-UserId";
    public const string SubjectHeader = "X-Test-Sub";
    public const string EmailHeader = "X-Test-Email";
    public const string MachineHeader = "X-Test-Machine";
    public const string ScopeHeader = "X-Test-Scope";

    public static void Add(IServiceCollection services)
    {
        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, Handler>(Scheme, _ => { });
        services.PostConfigure<PolicySchemeOptions>(IdentityModule.Scheme, options =>
        {
            var real = options.ForwardDefaultSelector;
            options.ForwardDefaultSelector = context =>
                context.Request.Headers.ContainsKey(UserIdHeader) || context.Request.Headers.ContainsKey(SubjectHeader)
                || context.Request.Headers.ContainsKey(MachineHeader)
                    ? Scheme
                    : real?.Invoke(context);
        });
        services.PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
        {
            options.Configuration = new OpenIdConnectConfiguration
            {
                Issuer = $"https://{CmApp.Auth0Domain}/",
                AuthorizationEndpoint = $"https://{CmApp.Auth0Domain}/authorize",
                TokenEndpoint = $"https://{CmApp.Auth0Domain}/oauth/token",
            };
            // Свой PostConfigure обработчика уже завёл менеджер, который ходит за discovery в сеть.
            options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(options.Configuration);
        });
    }

    public static HttpClient As(this HttpClient client, Guid userId)
    {
        client.DefaultRequestHeaders.Add(UserIdHeader, userId.ToString());
        return client;
    }

    public static HttpClient AsToken(this HttpClient client, string subject, string email)
    {
        client.DefaultRequestHeaders.Add(SubjectHeader, subject);
        client.DefaultRequestHeaders.Add(EmailHeader, email);
        return client;
    }

    /// <summary>Токен агента <paramref name="clientId"/> от имени <paramref name="actAs"/> со scope'ами через пробел.</summary>
    public static HttpClient AsMachine(this HttpClient client, string clientId, string actAs, string scopes)
    {
        client.DefaultRequestHeaders.Add(MachineHeader, clientId);
        client.DefaultRequestHeaders.Add(EmailHeader, actAs);
        client.DefaultRequestHeaders.Add(ScopeHeader, scopes);
        return client;
    }

    private sealed class Handler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Headers[MachineHeader].ToString() is { Length: > 0 } clientId)
            {
                // Тот же разбор, что у настоящего токена client credentials; допущен ровно этот клиент от имени почты из заголовка.
                var token = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim("gty", MachineAccess.GrantType),
                    new Claim("azp", clientId),
                    new Claim("sub", $"{clientId}@clients"),
                    new Claim("scope", Request.Headers[ScopeHeader].ToString()),
                ]));
                var (machine, failure) = MachineAccess.CreatePrincipal(token,
                    [new MachineClient { ClientId = clientId, ActAs = Request.Headers[EmailHeader].ToString() }], TestAuth.Scheme);
                return Task.FromResult(machine is null
                    ? AuthenticateResult.Fail(failure!)
                    : AuthenticateResult.Success(new AuthenticationTicket(machine, TestAuth.Scheme)));
            }

            List<Claim> claims = [];
            if (Request.Headers[UserIdHeader].ToString() is { Length: > 0 } userId)
            {
                claims.Add(new Claim(CmClaims.UserId, userId));
            }

            if (Request.Headers[SubjectHeader].ToString() is { Length: > 0 } subject)
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, subject));
            }

            if (Request.Headers[EmailHeader].ToString() is { Length: > 0 } email)
            {
                claims.Add(new Claim(ClaimTypes.Email, email));
            }

            if (claims.Count == 0)
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, TestAuth.Scheme, ClaimTypes.Name, ClaimTypes.Role));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, TestAuth.Scheme)));
        }
    }
}
