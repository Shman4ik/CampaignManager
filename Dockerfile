# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "CampaignManager.Web.dll"]

# Install Node.js for Tailwind CSS compilation via multi-stage copy
FROM node:26-slim AS node

# This stage is used to build and publish the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
COPY --from=node /usr/local/bin/node /usr/local/bin/node
COPY --from=node /usr/local/lib/node_modules /usr/local/lib/node_modules
RUN ln -s /usr/local/lib/node_modules/npm/bin/npm-cli.js /usr/local/bin/npm \
    && ln -s /usr/local/lib/node_modules/npm/bin/npx-cli.js /usr/local/bin/npx

ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Restore dependencies (cached unless csproj files change)
COPY ["CampaignManager.Web/CampaignManager.Web.csproj", "CampaignManager.Web/"]
COPY ["CampaignManager.ServiceDefaults/CampaignManager.ServiceDefaults.csproj", "CampaignManager.ServiceDefaults/"]
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore "./CampaignManager.Web/CampaignManager.Web.csproj"

# Publish directly (no separate build step needed)
COPY . .
WORKDIR "/src/CampaignManager.Web"
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish "./CampaignManager.Web.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Production deploys publish on the GitHub runner into artifacts/v1 and only package the result
# (`--target prebuilt`, see .github/workflows/docker-build-deploy.yml): building inside the image
# re-pulled the SDK image every time, spent ~45 s importing/exporting the GitHub Actions layer
# cache, and still re-downloaded NuGet packages, since cache mounts don't survive between runners.
FROM base AS prebuilt
COPY artifacts/v1/ .

# Default target (the last stage): a local `docker build` still builds everything itself.
FROM base AS final
COPY --from=build /app/publish .
