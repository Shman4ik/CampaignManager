using CampaignManager.Server.Access;
using CampaignManager.Server.Admin;
using CampaignManager.Server.Campaigns;
using CampaignManager.Server.Catalogs;
using CampaignManager.Server.Characters;
using CampaignManager.Server.Encounters;
using CampaignManager.Server.Files;
using CampaignManager.Server.Identity;
using CampaignManager.Server.Music;
using CampaignManager.Server.Platform;
using CampaignManager.Server.Profile;
using CampaignManager.Server.Scenarios;

var builder = WebApplication.CreateBuilder(args);

builder.AddPlatformModule();
builder.AddIdentityModule();
builder.AddAccessModule();
builder.AddFilesModule();
builder.AddCampaignsModule();
builder.AddCharactersModule();
builder.AddEncountersModule();
builder.AddCatalogsModule();
builder.AddProfileModule();
builder.AddAdminModule();
builder.AddMusicModule();
builder.AddScenariosModule();

var app = builder.Build();

app.UsePlatform();
app.UseIdentity();
app.MapPlatformApi();
app.MapIdentityApi();
app.MapFilesApi();
app.MapCampaignsApi();
app.MapCharactersApi();
app.MapEncountersApi();
app.MapCatalogsApi();
app.MapProfileApi();
app.MapAdminApi();
app.MapMusicApi();
app.MapScenariosApi();
app.MapClientApp();

app.Run();
