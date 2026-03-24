# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

# Install Node.js for Tailwind CSS compilation via multi-stage copy
FROM node:24-slim AS node

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

# This stage is used in production
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "CampaignManager.Web.dll"]
