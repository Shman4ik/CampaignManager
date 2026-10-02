# Образ CampaignManager 2.0: сервер (API + хост WebAssembly) вместе с клиентом. Собирается из корня
# репозитория — нужны Directory.*.props, global.json и NuGet.config:
#
#   wslc build -f src/Dockerfile -t campaign-manager-v2 .
#
# Выкатывается на beta-стенд воркфлоу .github/workflows/v2-beta-deploy.yml (src/CLAUDE.md,
# «Beta-стенд»). Корневой Dockerfile — это v1, его не трогать.

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

# Node — для Tailwind: MSBuild-таргет в CampaignManager.UI.csproj сам ставит пакеты (npm ci по
# package-lock.json) и собирает styles.css. Версия — та же, что в CI.
FROM node:26-slim AS node

FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
COPY --from=node /usr/local/bin/node /usr/local/bin/node
COPY --from=node /usr/local/lib/node_modules /usr/local/lib/node_modules
RUN ln -s /usr/local/lib/node_modules/npm/bin/npm-cli.js /usr/local/bin/npm \
    && ln -s /usr/local/lib/node_modules/npm/bin/npx-cli.js /usr/local/bin/npx

ARG BUILD_CONFIGURATION=Release
ENV DOTNET_NOLOGO=true DOTNET_CLI_TELEMETRY_OPTOUT=true
WORKDIR /repo

# Восстановление пакетов — отдельным слоем, пока не поменялись csproj и центральные версии.
# Новый проект в src/ сюда дописывать не обязательно: publish ниже всё равно делает restore.
COPY Directory.Build.props Directory.Packages.props global.json NuGet.config ./
COPY src/CampaignManager.Core/CampaignManager.Core.csproj src/CampaignManager.Core/
COPY src/CampaignManager.Contracts/CampaignManager.Contracts.csproj src/CampaignManager.Contracts/
COPY src/CampaignManager.ApiClient/CampaignManager.ApiClient.csproj src/CampaignManager.ApiClient/
COPY src/CampaignManager.Data/CampaignManager.Data.csproj src/CampaignManager.Data/
COPY src/CampaignManager.UI/CampaignManager.UI.csproj src/CampaignManager.UI/
COPY src/CampaignManager.Web.Client/CampaignManager.Web.Client.csproj src/CampaignManager.Web.Client/
COPY src/CampaignManager.Server/CampaignManager.Server.csproj src/CampaignManager.Server/
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore src/CampaignManager.Server/CampaignManager.Server.csproj

# Только src/: v1, тесты и инструменты в образ не нужны. bin/obj/node_modules отсекает .dockerignore.
COPY src/ src/
RUN --mount=type=cache,target=/root/.nuget/packages \
    --mount=type=cache,target=/root/.npm \
    dotnet publish src/CampaignManager.Server/CampaignManager.Server.csproj \
        -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
LABEL org.opencontainers.image.source="https://github.com/Shman4ik/CampaignManager" \
      org.opencontainers.image.description="CampaignManager 2.0 (beta)"
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "CampaignManager.Server.dll"]
