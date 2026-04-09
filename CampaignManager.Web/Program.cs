using CampaignManager.Web.Components;
using CampaignManager.Web.Components.Features.Admin.Services;
using CampaignManager.Web.Components.Features.Bestiary.Services;
using CampaignManager.Web.Components.Features.Campaigns.Services;
using CampaignManager.Web.Components.Features.Characters.Services;
using CampaignManager.Web.Components.Features.Items.Services;
using CampaignManager.Web.Components.Features.Scenarios.Services;
using CampaignManager.Web.Components.Features.Skills.Services;
using CampaignManager.Web.Components.Features.Spells.Services;
using CampaignManager.Web.Components.Features.Weapons.Services;
using CampaignManager.Web.Components.Features.Chase.Services;
using CampaignManager.Web.Components.Features.Combat.Services;
using CampaignManager.Web.Components.Features.Wiki.Services;
using CampaignManager.Web.Utilities.Api;
using CampaignManager.Web.Utilities.Authorization;
using CampaignManager.Web.Utilities.DataBase;
using CampaignManager.Web.Utilities.DataBase.Interceptors;
using Microsoft.AspNetCore.DataProtection;
using CampaignManager.Web.Utilities.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Threading.RateLimiting;

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

builder.Services.AddSingleton<WikiAuditInterceptor>();

builder.Services.AddDbContextFactory<AppDbContext>((sp, options) =>
    options.UseNpgsql(dataSource)
        .AddInterceptors(sp.GetRequiredService<WikiAuditInterceptor>()));

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

            var isAccessDenied = context.Failure?.Message.Contains("not authorized", StringComparison.OrdinalIgnoreCase) == true;

            if (isSilent)
            {
                // For silent failures, redirect back with error status
                var returnUrl = properties?.Items.TryGetValue(AccountEndpoints.FailureRedirectItemKey, out var url) == true
                    ? url
                    : "/";
                context.Response.Redirect($"{returnUrl}?authStatus=silentFailed");
                context.HandleResponse();
            }
            else if (isAccessDenied)
            {
                context.Response.Redirect("/?authStatus=accessDenied");
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

        // Validate user against allowed emails/domains whitelist and bootstrap admin
        options.Events.OnCreatingTicket = async context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            var email = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;

            var config = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var allowedEmails = config.GetSection("Authorization:AllowedEmails").Get<string[]>() ?? [];
            var allowedDomains = config.GetSection("Authorization:AllowedDomains").Get<string[]>() ?? [];

            if ((allowedEmails.Length > 0 || allowedDomains.Length > 0) && email is not null)
            {
                var emailDomain = email.Split('@').LastOrDefault() ?? string.Empty;
                var isAllowed = allowedEmails.Contains(email, StringComparer.OrdinalIgnoreCase)
                    || allowedDomains.Contains(emailDomain, StringComparer.OrdinalIgnoreCase);

                if (!isAllowed)
                {
                    logger.LogWarning("Access denied for user {Email}: not in allowed list.", email);
                    context.Fail("Access denied: your account is not authorized to access this application.");
                    return;
                }
            }

            // Bootstrap admin from config
            if (email is not null)
            {
                var adminEmails = config.GetSection("Authorization:AdminEmails").Get<string[]>() ?? [];
                if (adminEmails.Contains(email, StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        var identityFactory = context.HttpContext.RequestServices.GetRequiredService<IDbContextFactory<AppIdentityDbContext>>();
                        await using var identityDb = await identityFactory.CreateDbContextAsync();
                        var user = await identityDb.Users.SingleOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email.ToLower());
                        if (user is not null && user.Role != CampaignManager.Web.Components.Features.Characters.Model.PlayerRole.Administrator)
                        {
                            user.Role = CampaignManager.Web.Components.Features.Characters.Model.PlayerRole.Administrator;
                            await identityDb.SaveChangesAsync();
                            logger.LogInformation("Bootstrapped admin role for {Email}", email);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to bootstrap admin for {Email}", email);
                    }
                }
            }

            logger.LogInformation("Successfully created authentication ticket for user: {Email}", email);
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("RequireAdministratorRole", policy => policy.RequireRole("Administrator"))
    .AddPolicy("RequireKeeper", policy => policy.RequireRole("GameMaster", "Administrator"));
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddTransient<IClaimsTransformation, RoleClaimsTransformation>();

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
builder.Services.AddScoped<OccupationService>();
builder.Services.AddScoped<CampaignService>();
builder.Services.AddScoped<IdentityService>();
builder.Services.AddScoped<WeaponService>();
builder.Services.AddHostedService<WeaponDataMigrationService>();
builder.Services.AddScoped<SpellService>();
builder.Services.AddScoped<MarkdownService>();

// Register scenario management services
builder.Services.AddScoped<ScenarioService>();
builder.Services.AddScoped<CreatureService>();
builder.Services.AddScoped<CreatureDataMigrationService>();
builder.Services.AddScoped<ItemService>();

//Register skills service
builder.Services.AddScoped<SkillService>();
builder.Services.AddScoped<CombatService>();
builder.Services.AddScoped<ChaseService>();

// Register Admin and Wiki services
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<WikiHistoryService>();
builder.Services.AddScoped<UserPreferencesService>();

// Register Minio service
builder.Services.AddScoped<MinioService>();

builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();

builder.Services.AddScoped<DbInitializer>();

// Persist Data Protection keys to PostgreSQL so they survive container restarts and work across replicas
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<AppDbContext>()
    .SetApplicationName("CampaignManager");

// Rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("api", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

var app = builder.Build();

// Seed database on startup
using (var scope = app.Services.CreateScope())
{
    var dbInitializer = scope.ServiceProvider.GetRequiredService<DbInitializer>();
    await dbInitializer.InitializeDatabaseAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto });

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.UseStaticFiles();
app.UseAntiforgery();

app.UseOutputCache();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.MapAccountEndpoints();
app.MapMinioEndpoints();
app.MapCharacterMigrationEndpoints();
app.MapCreatureMigrationEndpoints();

// Enable middleware to serve generated OpenAPI specification in both JSON and YAML formats
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "v1");
    });
}

await app.RunAsync();