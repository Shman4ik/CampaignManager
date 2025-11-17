
var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.CampaignManager_Web>("campaignmanager-web");

builder.Build().Run();