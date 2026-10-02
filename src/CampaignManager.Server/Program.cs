using CampaignManager.Server.Files;
using CampaignManager.Server.Platform;

var builder = WebApplication.CreateBuilder(args);

builder.AddPlatformModule();
builder.AddFilesModule();

var app = builder.Build();

app.UsePlatform();
app.MapPlatformApi();
app.MapFilesApi();
app.MapClientApp();

app.Run();
