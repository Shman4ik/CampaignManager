using System.Security.Claims;
using CampaignManager.Contracts;
using CampaignManager.Contracts.Identity;
using CampaignManager.Data;
using CampaignManager.Data.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace CampaignManager.Server.Identity;

/// <summary>
/// Вход через Auth0 и пользователи. Две схемы под одной политикой:
/// <list type="bullet">
/// <item>веб — OIDC (code flow) и своя кука <c>.CampaignManager.Auth</c>, к Auth0 ходим только при входе и выходе;</item>
/// <item>мобильное приложение — JWT Bearer (Auth0, PKCE на стороне приложения).</item>
/// </list>
/// Схема выбирается по заголовку <c>Authorization: Bearer</c>, политики принимают обе.
/// Знание v1 — <c>Server/Identity/CLAUDE.md</c>.
/// </summary>
public static class IdentityModule
{
    /// <summary>Схема по умолчанию: пересылает в JWT при заголовке Bearer, иначе в куку.</summary>
    public const string Scheme = "CampaignManager";

    /// <summary>Пространство имён custom claims в access token (Auth0 Action, см. CLAUDE.md модуля).</summary>
    public const string ClaimsNamespace = "https://cthulhu.dmnet.dev/";

    public static WebApplicationBuilder AddIdentityModule(this WebApplicationBuilder builder)
    {
        var configuration = builder.Configuration;
        var services = builder.Services;

        var auth0Domain = configuration["Authentication:Auth0:Domain"];
        var auth0ClientId = configuration["Authentication:Auth0:ClientId"];
        var auth0 = IsAuth0Configured(configuration);
        if (!auth0 && !builder.Environment.IsDevelopment())
        {
            // В v1 пустая настройка давала 500 на каждый запрос (ArgumentException из middleware
            // аутентификации); пусть лучше сервер не стартует и скажет почему. Исключение —
            // Development: там без Auth0 работает тестовый вход (DevLogin).
            throw new InvalidOperationException(
                "Нет настроек Auth0: Authentication:Auth0:Domain и Authentication:Auth0:ClientId.");
        }

        services.Configure<AccessListOptions>(configuration.GetSection(AccessListOptions.Section));
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<UserDirectory>();

        // Ключи в cm.data_protection_keys (T1.3 копирует их из v1), имя приложения — как в v1:
        // куки входа переживают и рестарт, и переключение на 2.0.
        services.AddDataProtection()
            .PersistKeysToDbContext<CmDbContext>()
            .SetApplicationName("CampaignManager");

        var authentication = services.AddAuthentication(options =>
            {
                options.DefaultScheme = Scheme;
                options.DefaultChallengeScheme = Scheme;
                options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddPolicyScheme(Scheme, "Кука или JWT", options =>
                options.ForwardDefaultSelector = context =>
                    auth0 && context.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                        ? JwtBearerDefaults.AuthenticationScheme
                        : CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options => ConfigureCookie(options));

        // Без Auth0 (только Development) схем OIDC и JWT нет вовсе: вход — только тестовый.
        if (auth0)
        {
            authentication
                .AddOpenIdConnect(options => ConfigureOpenIdConnect(options, auth0Domain!, auth0ClientId!,
                    configuration["Authentication:Auth0:ClientSecret"]))
                .AddJwtBearer(options => ConfigureJwtBearer(options, auth0Domain!, configuration["Authentication:Auth0:Audience"]));
        }

        // Статическая /Error рендерится через Routes с AuthorizeRouteView.
        services.AddCascadingAuthenticationState();
        return builder;
    }

    /// <summary>Аутентификация, автовход, авторизация и antiforgery — именно в этом порядке.</summary>
    public static WebApplication UseIdentity(this WebApplication app)
    {
        app.UseAuthentication();
        if (IsAuth0Configured(app.Configuration))
        {
            // Между ними: страница под [Authorize] без сессии сначала пробует автовход через Google.
            app.UseAutoLogin();
        }
        else
        {
            app.Logger.LogWarning("Auth0 не настроен: вход только тестовый, {DevLogin}?as=player|keeper|admin", IdentityRoutes.DevLogin);
        }

        app.UseAuthorization();
        // Antiforgery — после авторизации: так требует ASP.NET Core.
        app.UseAntiforgery();
        return app;
    }

    private static void ConfigureCookie(CookieAuthenticationOptions options)
    {
        options.Cookie.Name = ".CampaignManager.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        // Lax хватает редиректа Auth0 (GET, см. ResponseMode) и сохраняет защиту браузера от CSRF:
        // с None кука уезжала бы с каждым кросс-сайтовым запросом.
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.IsEssential = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        options.LoginPath = IdentityRoutes.LoginPage;
        options.ReturnUrlParameter = IdentityRoutes.ReturnUrlParameter;

        // API отвечает кодом, а не редиректом на страницу входа: клиенту WebAssembly и приложению
        // нужен 401, а не HTML. Нет прав — всегда 403: страница сама покажет «нет доступа»
        // (сервер отдаёт оболочку через /not-found, клиентский роутер рисует NotAuthorized).
        options.Events.OnRedirectToLogin = context =>
        {
            if (IsApiRequest(context.Request))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            }
            else
            {
                context.Response.Redirect(context.RedirectUri);
            }

            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    }

    private static void ConfigureOpenIdConnect(OpenIdConnectOptions options, string domain, string clientId, string? clientSecret)
    {
        options.Authority = $"https://{domain}/";
        options.ClientId = clientId;
        options.ClientSecret = clientSecret;
        options.ResponseType = OpenIdConnectResponseType.Code;
        // Код возвращается GET-редиректом, а не form_post (умолчание обработчика): form_post — кросс-сайтовый
        // POST со страницы Auth0, и Lax-куки корреляции и nonce браузер на него не отправит.
        options.ResponseMode = OpenIdConnectResponseMode.Query;
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        // Claims остаются под именами ID token ("email", "sub"), куку из них собирает OnTokenValidated.
        // Всё нужное есть в ID token, userinfo не запрашиваем.
        options.MapInboundClaims = false;
        options.GetClaimsFromUserInfoEndpoint = false;
        // id_token уходит в Auth0 как id_token_hint при выходе.
        options.SaveTokens = true;
        options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
        options.CorrelationCookie.SameSite = SameSiteMode.Lax;
        options.CorrelationCookie.IsEssential = true;
        options.CorrelationCookie.Name = ".CampaignManager.Correlation";
        options.NonceCookie.SecurePolicy = CookieSecurePolicy.Always;
        options.NonceCookie.SameSite = SameSiteMode.Lax;
        options.NonceCookie.IsEssential = true;
        options.NonceCookie.Name = ".CampaignManager.Nonce.";

        // connection и login_hint обработчик сам не передаёт — их кладёт AccountEndpoints.CreateChallenge.
        options.Events.OnRedirectToIdentityProvider = context =>
        {
            var connection = context.Properties.GetParameter<string>(AccountEndpoints.ConnectionParameter);
            if (!string.IsNullOrWhiteSpace(connection))
            {
                context.ProtocolMessage.SetParameter(AccountEndpoints.ConnectionParameter, connection);
            }

            var loginHint = context.Properties.GetParameter<string>(OpenIdConnectParameterNames.LoginHint);
            if (!string.IsNullOrWhiteSpace(loginHint))
            {
                context.ProtocolMessage.LoginHint = loginHint;
            }

            return Task.CompletedTask;
        };

        // Выход гасит и сессию Auth0, иначе следующий вход молча пустил бы под тем же пользователем.
        options.Events.OnRedirectToIdentityProviderForSignOut = context =>
        {
            // end_session_endpoint Auth0 публикует в discovery только с включённой настройкой тенанта
            // «RP-Initiated Logout End Session Endpoint Discovery», а сам эндпоинт есть всегда.
            if (string.IsNullOrEmpty(context.ProtocolMessage.IssuerAddress))
            {
                context.ProtocolMessage.IssuerAddress = $"https://{domain}/oidc/logout";
            }

            // С post_logout_redirect_uri Auth0 требует id_token_hint или client_id; у кук v1 id_token может не быть.
            context.ProtocolMessage.ClientId = clientId;
            return Task.CompletedTask;
        };

        options.Events.OnRemoteFailure = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<UserDirectory>>();
            logger.LogWarning("Вход через Auth0 не удался: {Error} — {ErrorDescription}",
                context.Failure?.Message, context.Request.Query["error_description"].ToString());

            // Повтора после неудачного автовхода не будет: его держит метка попытки (AutoLogin).
            var denied = context.Failure is AccessDeniedLoginException || context.Failure?.InnerException is AccessDeniedLoginException;
            context.Response.Redirect($"{IdentityRoutes.LoginPage}?authStatus={(denied ? "accessDenied" : "failed")}");
            context.HandleResponse();
            return Task.CompletedTask;
        };

        options.Events.OnTokenValidated = async context =>
        {
            var token = context.Principal;
            var email = token?.FindFirstValue("email");
            var subject = token?.FindFirstValue("sub");

            // Права и связь с перенесёнными данными держатся на почте: неподтверждённый адрес никогда
            // не принимаем — учётка с паролем на чужой адрес унаследовала бы чужие кампании.
            if (email is null || subject is null || !IsTrue(token?.FindFirstValue("email_verified")))
            {
                context.Fail(new AccessDeniedLoginException($"Почта {email ?? "<нет>"} не подтверждена в Auth0."));
                return;
            }

            var directory = context.HttpContext.RequestServices.GetRequiredService<UserDirectory>();
            var user = await directory.SignInAsync(new ExternalLogin(subject, email, token?.FindFirstValue("name")),
                context.HttpContext.RequestAborted);
            if (user is null)
            {
                context.Fail(new AccessDeniedLoginException($"Адреса {email} нет в белом списке."));
                return;
            }

            context.Principal = CreateSessionPrincipal(user, subject, context.Scheme.Name);
        };
    }

    /// <summary>
    /// Access token приложения. Почты в нём по умолчанию нет — её и <c>email_verified</c> кладёт Action
    /// тенанта под <see cref="ClaimsNamespace"/>. Без них токен годится только тому, кто уже входил через
    /// веб (поиск по sub). Без <c>Audience</c> ни один токен не пройдёт проверку — так и задумано.
    /// </summary>
    private static void ConfigureJwtBearer(JwtBearerOptions options, string domain, string? audience)
    {
        options.Authority = $"https://{domain}/";
        options.Audience = audience;
        options.MapInboundClaims = false;
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var token = context.Principal!;
                var identity = new ClaimsIdentity(context.Scheme.Name, ClaimTypes.Name, ClaimTypes.Role);
                if (token.FindFirstValue("sub") is { } subject)
                {
                    identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, subject));
                }

                if (token.FindFirstValue(ClaimsNamespace + "email") is { } email
                    && IsTrue(token.FindFirstValue(ClaimsNamespace + "email_verified")))
                {
                    var directory = context.HttpContext.RequestServices.GetRequiredService<UserDirectory>();
                    if (!directory.IsAllowed(email))
                    {
                        context.Fail("Адреса нет в белом списке.");
                        return Task.CompletedTask;
                    }

                    identity.AddClaim(new Claim(ClaimTypes.Email, email));
                }

                if (token.FindFirstValue(ClaimsNamespace + "name") is { } name)
                {
                    identity.AddClaim(new Claim(ClaimTypes.Name, name));
                }

                context.Principal = new ClaimsPrincipal(identity);
                return Task.CompletedTask;
            },
        };
    }

    /// <summary>Настроен ли вход через Auth0. Вне Development без него сервер не стартует.</summary>
    public static bool IsAuth0Configured(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration["Authentication:Auth0:Domain"])
        && !string.IsNullOrWhiteSpace(configuration["Authentication:Auth0:ClientId"]);

    /// <summary>
    /// Claims куки входа — одни у входа через Auth0 и тестового (<see cref="DevLogin"/>). Роль в куку не
    /// кладём: её каждый запрос читает CurrentUser из cm.users. Email/Name/NameIdentifier — те же claims,
    /// что у кук v1, поэтому и они продолжают работать.
    /// </summary>
    internal static ClaimsPrincipal CreateSessionPrincipal(User user, string subject, string authenticationType,
        params IEnumerable<Claim> extraClaims) =>
        new(new ClaimsIdentity(
            [
                new Claim(CmClaims.UserId, user.Id.ToString()),
                new Claim(ClaimTypes.NameIdentifier, subject),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.DisplayName),
                .. extraClaims,
            ],
            authenticationType, ClaimTypes.Name, ClaimTypes.Role));

    // Без MapInboundClaims email_verified приходит строкой "true"/"false".
    private static bool IsTrue(string? value) => bool.TryParse(value, out var result) && result;

    private static bool IsApiRequest(HttpRequest request) => request.Path.StartsWithSegments(ApiRoutes.Prefix);

    /// <summary>Отказ во входе по нашим правилам (почта, белый список) — в отличие от сбоя Auth0.</summary>
    private sealed class AccessDeniedLoginException(string message) : Exception(message);
}
