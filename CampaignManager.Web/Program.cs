using Blazorise;
using Blazorise.Icons.FontAwesome;
using Blazorise.Tailwind;
using CampaignManager.ServiceDefaults;
using CampaignManager.Web.Companies.Services;
using CampaignManager.Web.Components;
using CampaignManager.Web.Model;
using CampaignManager.Web.Services;
using CampaignManager.Web.Utilities.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Npgsql;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
// Add service defaults & Aspire components.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
NpgsqlConnection.GlobalTypeMapper.EnableDynamicJson();

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDbContextFactory<AppIdentityDbContext>(options =>
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
        options.SaveTokens = true;
        options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
        options.CorrelationCookie.SameSite = SameSiteMode.None;
        options.Events = new OAuthEvents
        {
            //OnTicketReceived = context =>
            //{
            //    // Redirect to our processing endpoint after successful authentication
            //    context.Response.Redirect("/api/account/process-login");
            //    context.HandleResponse();
            //    return Task.CompletedTask;
            //},
            OnRedirectToAuthorizationEndpoint = context =>
            {
                if (context.RedirectUri.StartsWith("http:"))
                {
                    context.RedirectUri = context.RedirectUri.Replace("http:", "https:");
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            },


        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdministratorRole", policy => policy.RequireRole("Administrator"));
});
builder.Services
    .AddScoped<AuthenticationStateProvider, RevalidatingIdentityAuthenticationStateProvider<ApplicationUser>>();
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
builder.Services.AddScoped<CampaignCharacterService>();
builder.Services.AddScoped<UserRegistrationService>();
builder.Services.AddScoped<UserInformationService>();
builder.Services.AddScoped<IUserService, UserService>();

builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
WebApplication app = builder.Build();

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
// Enable middleware to serve generated Swagger as a JSON endpoint.
app.UseSwagger();

// Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.),
// specifying the Swagger JSON endpoint.
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "CampaignManager API v1");
});
app.MapAccountEndpoints();


app.MapPost("api/join-as-user", async (string userEmail, [FromServices] AppIdentityDbContext dbContext) =>
{
    ApplicationUser? user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
    if (user == null)
    {
        user = new ApplicationUser { Email = userEmail, UserName = userEmail, Role = PlayerRole.Player };
        dbContext.Users.Add(user);
    }

    await dbContext.SaveChangesAsync();
    return Results.Ok($"User {userEmail} joined as a player");
});

app.Run();