using Projects;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<CampaignManager_Web>("webfrontend");

builder.Build().Run();