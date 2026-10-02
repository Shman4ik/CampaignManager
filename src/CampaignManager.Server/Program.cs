using CampaignManager.Server.Access;
using CampaignManager.Server.Files;
using CampaignManager.Server.Identity;
using CampaignManager.Server.Platform;

var builder = WebApplication.CreateBuilder(args);

builder.AddPlatformModule();
builder.AddIdentityModule();
builder.AddAccessModule();
builder.AddFilesModule();

var app = builder.Build();

app.UsePlatform();
app.UseIdentity();
app.MapPlatformApi();
app.MapIdentityApi();
app.MapFilesApi();
app.MapClientApp();

app.Run();
