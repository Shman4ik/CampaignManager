using CampaignManager.ServiceDefaults;
using CampaignManager.Web.Components;
using CampaignManager.Web.Components.Features.Bestiary.Services;
using CampaignManager.Web.Components.Features.Campaigns.Services;
using CampaignManager.Web.Components.Features.Characters.Services;
using CampaignManager.Web.Components.Features.Items.Services;
using CampaignManager.Web.Components.Features.Scenarios.Services;
using CampaignManager.Web.Components.Features.Skills.Services;
using CampaignManager.Web.Components.Features.Spells.Services;
using CampaignManager.Web.Components.Features.Weapons.Services;
using CampaignManager.Web.Components.Features.Combat.Services;
using CampaignManager.Web.Utilities.Api;
using CampaignManager.Web.Utilities.DataBase;
using CampaignManager.Web.Utilities.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
// Add service defaults & Aspire components.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure SignalR with increased message size limits
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 2 * 1024 * 1024; // 2MB instead of default 32KB
    options.StreamBufferCapacity = 15; // Increase buffer capacity
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.HandshakeTimeout = TimeSpan.FromSeconds(30);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
});

// Configure Blazor Server with optimized settings
builder.Services.AddServerSideBlazor(options =>
{
    options.DetailedErrors = builder.Environment.IsDevelopment();
    options.DisconnectedCircuitMaxRetained = 100;
    options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(3);
    options.JSInteropDefaultCallTimeout = TimeSpan.FromMinutes(2);
    options.MaxBufferedUnacknowledgedRenderBatches = 20; // Increase buffer for large updates
});

// Use data source mapping instead of global type mapper (fixing obsolete warning)
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
var dataSourceBuilder = new NpgsqlDataSourceBuilder(builder.Configuration.GetConnectionString("DefaultConnection"));
dataSourceBuilder.EnableDynamicJson();
var dataSource = dataSourceBuilder.Build();

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseNpgsql(dataSource));

builder.Services.AddDbContextFactory<AppIdentityDbContext>(options =>
    options.UseNpgsql(dataSource));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
    .AddCookie(options =>
    {
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.None;
        options.Cookie.IsEssential = true; // Mark as essential for GDPR compliance
        options.Cookie.Name = ".CampaignManager.Auth"; // Explicit cookie name
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        options.LoginPath = "/"; // Redirect to home if not authenticated
        options.AccessDeniedPath = "/"; // Redirect to home on access denied
    })
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? string.Empty;
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? string.Empty;
        options.ClaimActions.MapJsonKey("urn:google:profile", "link");
        options.ClaimActions.MapJsonKey("urn:google:image", "picture");
        options.SaveTokens = true;
        options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
        options.CorrelationCookie.SameSite = SameSiteMode.None;
        options.CorrelationCookie.IsEssential = true;
        options.CorrelationCookie.Name = ".CampaignManager.Correlation";

        // Add OAuth scopes
        options.Scope.Add("profile");
        options.Scope.Add("email");

        // Handle authentication failures gracefully
        options.Events.OnRemoteFailure = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogWarning("Google OAuth authentication failed: {Error} - {ErrorDescription}",
                context.Failure?.Message,
                context.Request.Query["error_description"]);

            // Check if this is a silent auth attempt
            var properties = context.Properties;
            var isSilent = properties?.Items.TryGetValue(AccountEndpoints.AuthModeItemKey, out var mode) == true
                           && mode == AccountEndpoints.SilentModeValue;

            if (isSilent)
            {
                // For silent failures, redirect back with error status
                var returnUrl = properties?.Items.TryGetValue(AccountEndpoints.FailureRedirectItemKey, out var url) == true
                    ? url
                    : "/";
                context.Response.Redirect($"{returnUrl}?authStatus=silentFailed");
                context.HandleResponse();
            }
            else
            {
                // For interactive failures, show error page or redirect
                context.Response.Redirect("/?authStatus=failed");
                context.HandleResponse();
            }

            return Task.CompletedTask;
        };

        // Optional: Log successful ticket creation
        options.Events.OnCreatingTicket = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Successfully created authentication ticket for user: {Email}",
                context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value);
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("RequireAdministratorRole", policy => policy.RequireRole("Administrator"));
builder.Services.AddCascadingAuthenticationState();

// Add OpenAPI services with comprehensive documentation (.NET 10 - OpenAPI 3.1)
builder.Services.AddEndpointsApiExplorer();

// Configure OpenAPI 3.1 with enhanced features
builder.Services.AddOpenApi("v1", options =>
{
    options.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_1;

    // Add document transformer for metadata using inline types
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        // OpenAPI document info is automatically populated from assembly attributes
        // Additional customization can be done here if needed
        if (document.Info != null)
        {
            document.Info.Description = "API for managing tabletop RPG campaigns, characters, scenarios, and game assets";
        }

        return Task.CompletedTask;
    });
});
builder.Services.AddOutputCache();

// Register services
builder.Services.AddScoped<CharacterService>();
builder.Services.AddScoped<CharacterGenerationService>();
builder.Services.AddScoped<CampaignService>();
builder.Services.AddScoped<IdentityService>();
builder.Services.AddScoped<WeaponService>();
builder.Services.AddScoped<SpellService>();
builder.Services.AddScoped<MarkdownService>();
builder.Services.AddScoped<PdfExportService>();

// Register scenario management services
builder.Services.AddScoped<ScenarioService>();
builder.Services.AddScoped<CreatureService>();
builder.Services.AddScoped<ItemService>();

//Register skills service
builder.Services.AddScoped<SkillService>();
builder.Services.AddScoped<CombatService>();

// Register Minio service
builder.Services.AddScoped<MinioService>();

builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();

builder.Services.AddScoped<DbInitializer>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto });

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseStaticFiles();
app.UseAntiforgery();

app.UseOutputCache();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.MapAccountEndpoints();
app.MapMinioEndpoints();
app.MapCharacterMigrationEndpoints();

// Enable middleware to serve generated OpenAPI specification in both JSON and YAML formats
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "v1");
    });
}

app.Run();