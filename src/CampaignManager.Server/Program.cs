using CampaignManager.Server.Platform;

var builder = WebApplication.CreateBuilder(args);

builder.AddPlatformModule();

var app = builder.Build();

app.UsePlatform();
app.MapPlatformApi();
app.MapClientApp();

app.Run();
