var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.CampaignManager_Web>("campaignmanager-web")
    .WithEndpoint("https", e => e.IsProxied = false);

builder.Build().Run();
