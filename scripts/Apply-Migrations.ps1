#Requires -Version 7.0
<#
.SYNOPSIS
    Применяет EF Core миграции CampaignManager к базе данных.

.DESCRIPTION
    Обёртка над `dotnet ef database update` для двух контекстов приложения.
    Закрывает три места, где команда падает не по делу:

    1. Design-time поднимает Program.cs целиком, поэтому без Google OAuth каждый запуск
       падает с «The value cannot be an empty string. (Parameter 'ClientId')».
       Скрипт подставляет пустышки — на миграции они никак не влияют.
    2. Без ASPNETCORE_ENVIRONMENT=Development не читается appsettings.Development.json,
       и строка подключения оказывается пустой: «Value cannot be null. (Parameter 'Host')».
    3. Запущенное приложение держит bin\Debug\net10.0, и сборка падает с MSB3027,
       а сообщение об ошибке ничего не говорит о том, что надо закрыть приложение.

.PARAMETER Context
    Какие контексты обновлять: App (данные игры, схема games), Identity (схема identity)
    или All. По умолчанию All.

.PARAMETER ListOnly
    Ничего не применять — только показать, какие миграции уже в базе, а какие ждут.

.PARAMETER ConnectionString
    Переопределить строку подключения. По умолчанию берётся из appsettings.Development.json.

.PARAMETER SkipBuild
    Не пересобирать проект перед запуском (`--no-build`). Быстрее, но требует,
    чтобы сборка уже была актуальной.

.EXAMPLE
    .\scripts\Apply-Migrations.ps1 -ListOnly
    Показать список миграций и что из него ещё не применено.

.EXAMPLE
    .\scripts\Apply-Migrations.ps1
    Применить всё, что не применено, к обоим контекстам.

.EXAMPLE
    .\scripts\Apply-Migrations.ps1 -Context App -ConnectionString "Host=localhost;Port=5432;Database=campaignmanager;Username=postgres;Password=postgres"
    Прогнать миграции игровых данных на локальном PostgreSQL.
#>
[CmdletBinding()]
param(
    [ValidateSet('All', 'App', 'Identity')]
    [string] $Context = 'All',

    [switch] $ListOnly,

    [string] $ConnectionString,

    [switch] $SkipBuild
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'CampaignManager.Web'

if (-not (Test-Path $project)) {
    throw "Не найден проект CampaignManager.Web по пути $project"
}

# ── Приложение держит выходной каталог ───────────────────────────────────────
# `dotnet ef` собирает проект, и запущенный экземпляр не даёт перезаписать dll.
$running = Get-Process -Name 'CampaignManager.Web' -ErrorAction SilentlyContinue
if ($running) {
    $ids = ($running.Id -join ', ')
    throw "Приложение запущено (PID $ids) и держит bin\Debug\net10.0 — сборка упадёт с MSB3027. Останови его и повтори."
}

# ── Design-time конфигурация ─────────────────────────────────────────────────
$env:ASPNETCORE_ENVIRONMENT = 'Development'
if (-not $env:Authentication__Google__ClientId)     { $env:Authentication__Google__ClientId = 'dummy-client-id' }
if (-not $env:Authentication__Google__ClientSecret) { $env:Authentication__Google__ClientSecret = 'dummy-client-secret' }
if ($ConnectionString) { $env:ConnectionStrings__DefaultConnection = $ConnectionString }

# ── dotnet-ef ────────────────────────────────────────────────────────────────
if (-not (Get-Command 'dotnet-ef' -ErrorAction SilentlyContinue)) {
    Write-Host 'dotnet-ef не найден, ставлю глобально...' -ForegroundColor Yellow
    dotnet tool install --global dotnet-ef
    if ($LASTEXITCODE -ne 0) { throw 'Не удалось установить dotnet-ef' }
    $env:PATH = "$env:PATH;$HOME\.dotnet\tools"
}

# ── Сборка ───────────────────────────────────────────────────────────────────
# Собираем один раз здесь, а команды ef запускаем с --no-build: иначе проект
# пересобирается на каждый контекст.
if (-not $SkipBuild) {
    Write-Host 'Сборка проекта...' -ForegroundColor Cyan
    dotnet build $project --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw 'Сборка не прошла — миграции не запускались' }
}

$contexts = switch ($Context) {
    'App'      { , 'AppDbContext' }
    'Identity' { , 'AppIdentityDbContext' }
    'All'      { 'AppDbContext', 'AppIdentityDbContext' }
}

$failed = @()

foreach ($ctx in $contexts) {
    Write-Host ''
    Write-Host "── $ctx " -NoNewline -ForegroundColor Cyan
    Write-Host ('─' * [Math]::Max(0, 60 - $ctx.Length)) -ForegroundColor DarkGray

    $efArgs = @('--project', $project, '--context', $ctx, '--no-build')

    if ($ListOnly) {
        dotnet ef migrations list @efArgs
    }
    else {
        dotnet ef database update @efArgs
    }

    if ($LASTEXITCODE -ne 0) {
        $failed += $ctx
        Write-Host "$ctx — ошибка (код $LASTEXITCODE)" -ForegroundColor Red
    }
    else {
        $verb = if ($ListOnly) { 'прочитан' } else { 'обновлён' }
        Write-Host "$ctx — $verb" -ForegroundColor Green
    }
}

Write-Host ''
if ($failed.Count -gt 0) {
    throw "Не удалось обработать: $($failed -join ', ')"
}

if (-not $ListOnly) {
    Write-Host 'Готово. Все миграции применены.' -ForegroundColor Green
}
