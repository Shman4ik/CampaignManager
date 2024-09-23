var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.CampaignManager_Web>("webfrontend")
    .WithExternalHttpEndpoints();

builder.Build().Run();
