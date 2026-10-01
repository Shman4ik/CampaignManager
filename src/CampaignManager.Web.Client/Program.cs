using System.Globalization;
using CampaignManager.ApiClient;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Интерфейс русский (docs/v2/README.md, «Принципы»): даты и числа — по-русски при любом языке браузера.
var russian = CultureInfo.GetCultureInfo("ru-RU");
CultureInfo.DefaultThreadCurrentCulture = russian;
CultureInfo.DefaultThreadCurrentUICulture = russian;

builder.Services.AddCampaignManagerApi(new Uri(builder.HostEnvironment.BaseAddress));

await builder.Build().RunAsync();
