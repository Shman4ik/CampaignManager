using CampaignManager.Server.Access;
using CampaignManager.Server.Campaigns;
using CampaignManager.Server.Catalogs;
using CampaignManager.Server.Files;
using CampaignManager.Server.Identity;
using CampaignManager.Server.Platform;

var builder = WebApplication.CreateBuilder(args);

builder.AddPlatformModule();
builder.AddIdentityModule();
builder.AddAccessModule();
builder.AddFilesModule();
builder.AddCampaignsModule();
builder.AddCatalogsModule();

var app = builder.Build();

app.UsePlatform();
app.UseIdentity();
app.MapPlatformApi();
app.MapIdentityApi();
app.MapFilesApi();
app.MapCampaignsApi();
app.MapCatalogsApi();
app.MapClientApp();

app.Run();
