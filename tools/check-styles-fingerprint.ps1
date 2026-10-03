<#
.SYNOPSIS
    Проверяет, что одной сборки хватает, чтобы сервер отдал свежий styles.css Tailwind.

.DESCRIPTION
    Сценарий разработчика целиком: собрать сервер, запомнить, какой styles.css он отдаёт; добавить
    в UI .razor с новым классом; собрать ОДИН раз; снова поднять сервер и убедиться, что
    - адрес в <link> из App.razor получил новый отпечаток (fingerprint),
    - по этому адресу (и с gzip, как ходит браузер) приходит CSS с новым классом,
    - ETag сменился.
    Пробный файл удаляется, и решение пересобирается, даже если проверка упала.

    Сервер поднимается в Development (только там UseStaticWebAssets отдаёт ассеты UI из
    исходников) с пустышками Auth0 и недоступной базой — странице /login база не нужна.

.EXAMPLE
    pwsh tools/check-styles-fingerprint.ps1
    pwsh tools/check-styles-fingerprint.ps1 -Configuration Release
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$serverProject = Join-Path $root 'src/CampaignManager.Server'
$serverDll = Join-Path $serverProject "bin/$Configuration/net10.0/CampaignManager.Server.dll"
$probeFile = Join-Path $root 'src/CampaignManager.UI/Pages/Dev/StylesFingerprintProbe.razor'
$stylesRoute = '_content/CampaignManager.UI/styles'

function Invoke-Build {
    & dotnet build $serverProject -c $Configuration -nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "dotnet build завершился с кодом $LASTEXITCODE" }
}

function Get-FreePort {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    $listener.Start()
    try { return $listener.LocalEndpoint.Port } finally { $listener.Stop() }
}

# Поднимает собранный сервер, читает /login и styles.css по адресу из неё, гасит сервер. Не главную: она под
# [Authorize] и без сессии уводит в Auth0 (здесь — пустышка, ответ 500); /login открыта и рисует ту же оболочку.
function Get-ServedStyles {
    $port = Get-FreePort
    $base = "http://127.0.0.1:$port"
    $psi = [System.Diagnostics.ProcessStartInfo]::new('dotnet')
    $psi.ArgumentList.Add($serverDll)
    $psi.ArgumentList.Add('--urls')
    $psi.ArgumentList.Add($base)
    $psi.WorkingDirectory = $serverProject
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.Environment['ASPNETCORE_ENVIRONMENT'] = 'Development'
    $psi.Environment['ConnectionStrings__DefaultConnection'] = 'Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none'
    $psi.Environment['Authentication__Auth0__Domain'] = 'auth0.example.test'
    $psi.Environment['Authentication__Auth0__ClientId'] = 'check'
    $psi.Environment['Authentication__Auth0__ClientSecret'] = 'check'
    $server = [System.Diagnostics.Process]::Start($psi)
    # Вывод читаем асинхронно, иначе заполненный буфер канала остановит сервер.
    $null = $server.StandardOutput.ReadToEndAsync()
    $null = $server.StandardError.ReadToEndAsync()

    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.AutomaticDecompression = [System.Net.DecompressionMethods]::GZip -bor [System.Net.DecompressionMethods]::Brotli
    $http = [System.Net.Http.HttpClient]::new($handler)
    try {
        $deadline = [DateTime]::UtcNow.AddSeconds(60)
        while ($true) {
            if ($server.HasExited) { throw "Сервер завершился с кодом $($server.ExitCode)" }
            try {
                $html = $http.GetStringAsync("$base/login").GetAwaiter().GetResult()
                break
            }
            catch {
                if ([DateTime]::UtcNow -gt $deadline) { throw "Сервер не ответил за 60 секунд: $_" }
                Start-Sleep -Milliseconds 500
            }
        }

        $match = [regex]::Match($html, "$([regex]::Escape($stylesRoute))[^`"]*\.css")
        if (-not $match.Success) { throw "На /login нет <link> на $stylesRoute*.css" }

        $request = [System.Net.Http.HttpRequestMessage]::new('GET', "$base/$($match.Value)")
        $request.Headers.AcceptEncoding.ParseAdd('gzip, br')
        $response = $http.SendAsync($request).GetAwaiter().GetResult()
        $null = $response.EnsureSuccessStatusCode()
        [pscustomobject]@{
            Href = $match.Value
            ETag = "$($response.Headers.ETag)"
            Css  = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        }
    }
    finally {
        $http.Dispose()
        if (-not $server.HasExited) { $server.Kill($true) }
        $server.WaitForExit()
    }
}

if (Test-Path $probeFile) { Remove-Item $probeFile }

Write-Host "Сборка ($Configuration) и исходное состояние…"
Invoke-Build
$before = Get-ServedStyles
Write-Host "  до:    $($before.Href)  ETag $($before.ETag)"

# Произвольное значение даёт класс, которого в сборке точно нет.
$value = "$(Get-Random -Minimum 100000 -Maximum 999999)px"
$failed = $false
try {
    Set-Content -Path $probeFile -Encoding utf8 -Value @(
        '@* Временный файл tools/check-styles-fingerprint.ps1: удаляется в конце проверки. *@'
        "<div class=`"mt-[$value]`"></div>"
    )

    Write-Host "Добавлен класс mt-[$value]; одна сборка…"
    Invoke-Build
    $after = Get-ServedStyles
    Write-Host "  после: $($after.Href)  ETag $($after.ETag)"

    $problems = @()
    if (-not $after.Css.Contains("margin-top:$value") -and -not $after.Css.Contains("margin-top: $value")) {
        $problems += "в отдаваемом styles.css нет нового класса mt-[$value]"
    }
    if ($after.Href -eq $before.Href) { $problems += "отпечаток в адресе не сменился ($($after.Href))" }
    if ($after.ETag -eq $before.ETag) { $problems += "ETag не сменился ($($after.ETag))" }

    if ($problems) {
        $failed = $true
        $problems | ForEach-Object { Write-Host "ОШИБКА: $_" -ForegroundColor Red }
    }
    else {
        Write-Host 'OK: одной сборки хватило — новый класс отдаётся по новому отпечатку.' -ForegroundColor Green
    }
}
finally {
    Remove-Item $probeFile -ErrorAction SilentlyContinue
    Write-Host 'Пробный файл удалён; пересборка без него…'
    Invoke-Build
}

if ($failed) { exit 1 }
