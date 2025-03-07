using Blazorise;
using Blazorise.Icons.FontAwesome;
using Blazorise.Tailwind;
using CampaignManager.ServiceDefaults;
using CampaignManager.Web.Components;
using CampaignManager.Web.Model;
using CampaignManager.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);
// Add service defaults & Aspire components.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
.AddCookie()
.AddGoogle(options =>
{
    options.ClientId = builder.Configuration["Authentication:Google:ClientId"];
    options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
    options.ClaimActions.MapJsonKey("urn:google:profile", "link");
    options.ClaimActions.MapJsonKey("urn:google:image", "picture");
    options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
    options.CorrelationCookie.SameSite = SameSiteMode.None;
    options.Events = new OAuthEvents
    {
        OnRedirectToAuthorizationEndpoint = context =>
        {
            if (context.RedirectUri.StartsWith("http:"))
            {
                context.RedirectUri = context.RedirectUri.Replace("http:", "https:");
            }
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        },
        OnTicketReceived = async context =>
        {
            // Получаем email пользователя
            var email = context.Principal.FindFirstValue(ClaimTypes.Email);
            if (!string.IsNullOrEmpty(email))
            {
                // Создаем scope для доступа к сервисам
                using var scope = context.HttpContext.RequestServices.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

                try
                {
                    // Ищем пользователя в базе данных
                    var existingUser = await dbContext.ApplicationUsers.FirstOrDefaultAsync(u => u.Email == email);

                    // Если пользователя нет, регистрируем его
                    if (existingUser == null)
                    {
                        var newUser = new ApplicationUser
                        {
                            Email = email,
                            NormalizedEmail = email.ToUpper(),
                            UserName = context.Principal.FindFirstValue(ClaimTypes.Name) ?? email,
                            NormalizedUserName = (context.Principal.FindFirstValue(ClaimTypes.Name) ?? email).ToUpper(),
                            EmailConfirmed = true,
                            SecurityStamp = Guid.NewGuid().ToString(),
                            Role = PlayerRole.Player // По умолчанию обычный игрок
                        };

                        dbContext.ApplicationUsers.Add(newUser);
                        await dbContext.SaveChangesAsync();

                        logger.LogInformation($"Автоматически зарегистрирован новый пользователь с email: {email}");
                    }
                    else
                    {
                        logger.LogInformation($"Пользователь с email {email} уже существует в системе");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"Ошибка при автоматической регистрации пользователя с email: {email}");
                }
            }
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdministratorRole", policy => policy.RequireRole("Administrator"));
});
builder.Services.AddScoped<AuthenticationStateProvider, RevalidatingIdentityAuthenticationStateProvider<ApplicationUser>>();
// Add Swagger services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "CampaignManager API", Version = "v1" });
});
builder.Services.AddOutputCache();
builder.Services
    .AddBlazorise()
    .AddTailwindProviders()
    .AddFontAwesomeIcons();

// Register services
builder.Services.AddScoped<CharacterService>();
builder.Services.AddScoped<CharacterGenerationService>();
builder.Services.AddScoped<CampaignService>();
builder.Services.AddScoped<CampaignCharacterService>(); // Add new service
builder.Services.AddScoped<UserRegistrationService>(); // Add user registration service

builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseStaticFiles();
app.UseAntiforgery();

app.UseOutputCache();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();
// Enable middleware to serve generated Swagger as a JSON endpoint.
app.UseSwagger();

// Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.),
// specifying the Swagger JSON endpoint.
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "CampaignManager API v1");
});

app.MapGet("api/account/login", async (string? returnUrl, HttpContext httpContext) =>
{
    var properties = new AuthenticationProperties { RedirectUri = returnUrl ?? "/" };
    await httpContext.ChallengeAsync(GoogleDefaults.AuthenticationScheme, properties);
});
app.MapGet("api/account/logout", async (HttpContext httpContext) =>
{
    var properties = new AuthenticationProperties { RedirectUri = "/" };
    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme, properties);
    return Results.Ok();
});

app.MapPost("api/join-as-user", async (string userEmail, [FromServices] ApplicationDbContext dbContext) =>
{
    var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
    if (user == null)
    {
        user = new ApplicationUser { Email = userEmail, UserName = userEmail, Role = PlayerRole.Player };
        dbContext.Users.Add(user);
    }

    await dbContext.SaveChangesAsync();
    return Results.Ok($"User {userEmail} joined as a player");
});

app.Run();