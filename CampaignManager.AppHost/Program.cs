using Projects;

IDistributedApplicationBuilder? builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<CampaignManager_Web>("webfrontend")
    .WithExternalHttpEndpoints();

builder.Build().Run();