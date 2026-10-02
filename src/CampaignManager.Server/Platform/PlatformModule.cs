using CampaignManager.Contracts;
using CampaignManager.Contracts.Platform;
using CampaignManager.Data;
using CampaignManager.Server.Components;
using Microsoft.AspNetCore.HttpOverrides;

namespace CampaignManager.Server.Platform;

/// <summary>
/// Платформа: база, JSON, ошибки, health, OpenAPI и хост WebAssembly. Вместо AppHost и
/// ServiceDefaults v1 — <c>/health</c> здесь маппится всегда, а не только в Development.
/// </summary>
public static class PlatformModule
{
    public static WebApplicationBuilder AddPlatformModule(this WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString(CmDatabase.ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Нет строки подключения ConnectionStrings:{CmDatabase.ConnectionStringName}.");

        var services = builder.Services;
        services.AddCmData(connectionString);

        // DTO сериализуются сгенерированным контекстом — тем же, что у клиентов.
        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, ContractsJsonContext.Default));
        services.AddProblemDetails();
        services.AddHealthChecks();
        services.AddOpenApi();

        services.AddRazorComponents()
            .AddInteractiveWebAssemblyComponents();

        services.AddScoped<PingService>();
        return builder;
    }

    public static WebApplication UsePlatform(this WebApplication app)
    {
        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
        });

        // Страницы отвечают страницей ошибки и «не найдено», API — ProblemDetails: HTML в ответ
        // клиенту API никому не нужен. Обработчики страниц — в основном конвейере: повторный проход
        // по /Error и /not-found заново ищет эндпоинт, а из ветки UseWhen он этого не делает и
        // отдаёт пустой 404. Обработчики API стоят внутри: их ответ уже с телом, и внешние его не трогают.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
        }

        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        app.UseWhen(IsApiRequest, api =>
        {
            api.UseExceptionHandler();
            api.UseStatusCodePages();
        });

        if (app.Environment.IsDevelopment())
        {
            app.UseWebAssemblyDebugging();
        }
        else
        {
            app.UseHsts();
        }

        // Antiforgery ставит IdentityModule.UseIdentity: ему место после авторизации.
        app.UseHttpsRedirection();
        return app;
    }

    public static IEndpointRouteBuilder MapPlatformApi(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health");
        app.MapOpenApi();

        app.MapGet(PlatformRoutes.Ping, (PingService ping, CancellationToken cancellationToken) =>
                ping.PingAsync(cancellationToken))
            .WithName("Ping")
            .WithTags("Platform");

        return app;
    }

    /// <summary>Приложение WebAssembly: оболочка документа и страницы из UI.</summary>
    public static WebApplication MapClientApp(this WebApplication app)
    {
        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(typeof(UI.Routes).Assembly);
        return app;
    }

    private static bool IsApiRequest(HttpContext context) =>
        context.Request.Path.StartsWithSegments(ApiRoutes.Prefix);
}
