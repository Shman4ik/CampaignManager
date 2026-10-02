using System.Globalization;
using CampaignManager.ApiClient;
using CampaignManager.Contracts.Identity;
using CampaignManager.Core.Identity;
using CampaignManager.UI;
using CampaignManager.Web.Client.Identity;
using CampaignManager.Web.Client;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Интерфейс русский (docs/v2/README.md, «Принципы»): даты и числа — по-русски при любом языке браузера.
var russian = CultureInfo.GetCultureInfo("ru-RU");
CultureInfo.DefaultThreadCurrentCulture = russian;
CultureInfo.DefaultThreadCurrentUICulture = russian;

builder.Services.AddCampaignManagerUi(builder.HostEnvironment.IsDevelopment());

// Индикатор связи и сохранения видит каждый запрос к API (UI/Platform/ApiActivity).
builder.Services.AddTransient<ApiActivityHandler>();
builder.Services.ConfigureHttpClientDefaults(http => http.AddHttpMessageHandler<ApiActivityHandler>());
builder.Services.AddCampaignManagerApi(new Uri(builder.HostEnvironment.BaseAddress));

// Те же имена политик, что на сервере; здесь они только прячут то, что сервер всё равно не даст.
builder.Services.AddAuthorizationCore(options =>
{
    options.AddPolicy(Policies.Keeper, policy => policy.RequireRole(nameof(UserRole.Keeper)));
    options.AddPolicy(Policies.Admin, policy => policy.RequireRole(nameof(UserRole.Admin)));
});
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, MeAuthenticationStateProvider>();

await builder.Build().RunAsync();
