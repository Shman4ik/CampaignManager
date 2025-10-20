# .NET 10 Upgrade Plan

## Execution Steps

Execute steps below sequentially one by one in the order they are listed.

1. Validate that a .NET 10 SDK required for this upgrade is installed on the machine and if not, help to get it installed.
2. Ensure that the SDK version specified in global.json files is compatible with the .NET 10 upgrade.
3. Upgrade CampaignManager.ServiceDefaults\CampaignManager.ServiceDefaults.csproj
4. Upgrade CampaignManager.Web\CampaignManager.Web.csproj
5. Upgrade CampaignManager.AppHost\CampaignManager.AppHost.csproj

## Settings

This section contains settings and data used by execution steps.

### Aggregate NuGet packages modifications across all projects

NuGet packages used across all selected projects or their dependencies that need version update in projects that reference them.

| Package Name                                                 | Current Version | New Version              | Description                                                    |
|:------------------------------------------------------------|:---------------:|:------------------------:|:--------------------------------------------------------------|
| Aspire.Hosting.AppHost                                       | 9.3.1           | 9.5.1                    | Recommended for .NET 10                                       |
| Aspire.Hosting.PostgreSQL                                    | 9.3.1           | 9.5.1                    | Recommended for .NET 10                                       |
| Microsoft.AspNetCore.Authentication.Google                   | 9.0.7           | 10.0.0-rc.2.25502.107    | Recommended for .NET 10                                       |
| Microsoft.AspNetCore.Components.QuickGrid                    | 9.0.7           | 10.0.0-rc.2.25502.107    | Recommended for .NET 10                                       |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore            | 9.0.7           | 10.0.0-rc.2.25502.107    | Recommended for .NET 10                                       |
| Microsoft.EntityFrameworkCore.Design                         | 9.0.7           | 10.0.0-rc.2.25502.107    | Recommended for .NET 10                                       |
| Microsoft.Extensions.Http.Resilience                         | 9.7.0           | 9.10.0                   | Recommended for .NET 10                                       |
| Microsoft.Extensions.ServiceDiscovery                        | 9.3.1           | 9.5.1                    | Recommended for .NET 10                                       |
| Microsoft.VisualStudio.Azure.Containers.Tools.Targets       | 1.22.1          |                          | No supported version found for .NET 10                        |

### Project upgrade details

#### CampaignManager.ServiceDefaults modifications

Project properties changes:
- Target framework should be changed from `net9.0` to `net10.0`

NuGet packages changes:
- Microsoft.Extensions.Http.Resilience should be updated from `9.7.0` to `9.10.0` (*recommended for .NET 10*)
- Microsoft.Extensions.ServiceDiscovery should be updated from `9.3.1` to `9.5.1` (*recommended for .NET 10*)

#### CampaignManager.Web modifications

Project properties changes:
- Target framework should be changed from `net9.0` to `net10.0`

NuGet packages changes:
- Microsoft.AspNetCore.Authentication.Google should be updated from `9.0.7` to `10.0.0-rc.2.25502.107` (*recommended for .NET 10*)
- Microsoft.AspNetCore.Components.QuickGrid should be updated from `9.0.7` to `10.0.0-rc.2.25502.107` (*recommended for .NET 10*)
- Microsoft.AspNetCore.Identity.EntityFrameworkCore should be updated from `9.0.7` to `10.0.0-rc.2.25502.107` (*recommended for .NET 10*)
- Microsoft.EntityFrameworkCore.Design should be updated from `9.0.7` to `10.0.0-rc.2.25502.107` (*recommended for .NET 10*)
- Microsoft.VisualStudio.Azure.Containers.Tools.Targets should be removed (*no supported version found for .NET 10*)

#### CampaignManager.AppHost modifications

Project properties changes:
- Target framework should be changed from `net9.0` to `net10.0`

NuGet packages changes:
- Aspire.Hosting.AppHost should be updated from `9.3.1` to `9.5.1` (*recommended for .NET 10*)
- Aspire.Hosting.PostgreSQL should be updated from `9.3.1` to `9.5.1` (*recommended for .NET 10*)