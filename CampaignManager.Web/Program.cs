using Blazorise;
using Blazorise.Icons.FontAwesome;
using Blazorise.Tailwind;
using CampaignManager.ServiceDefaults;
using CampaignManager.Web.Components;
using CampaignManager.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire components.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

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
            }
        };
    });
builder.Services.AddHttpsRedirection(options => { options.HttpsPort = 443; });
builder.Services.AddOutputCache();
builder.Services
    .AddBlazorise()
    .AddTailwindProviders()
    .AddFontAwesomeIcons();
builder.Services.AddScoped<CharacterService>();
builder.Services.AddScoped<CharacterGenerationService>();
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}


app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseStaticFiles();
app.UseAntiforgery();

app.UseOutputCache();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

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

app.Run();
