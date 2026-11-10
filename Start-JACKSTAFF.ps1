[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path $PSScriptRoot).Path
. (Join-Path $root 'scripts\common.ps1')
Set-Location $root

Write-Host ''
Write-Host 'JACKSTAFF v0.1 - Inicio nativo para Windows' -ForegroundColor Cyan
Write-Host '------------------------------------------' -ForegroundColor Cyan

# Verificar las versiones declaradas por este proyecto.
$nodeVersionText = (& node -p 'process.versions.node' 2>&1 | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or -not $nodeVersionText) {
    throw 'No se encontró Node.js. Instala Node.js 22.12.0 o superior y vuelve a abrir PowerShell.'
}
$nodeVersion = [version]$nodeVersionText
if ($nodeVersion -lt [version]'22.12.0') {
    throw "Este proyecto requiere Node.js 22.12.0 o superior. Detectado: $nodeVersionText. Descarga la versión LTS desde https://nodejs.org/en/download."
}

$npmVersionText = (& npm --version 2>&1 | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or [version]$npmVersionText -lt [version]'10.0.0') {
    throw "Este proyecto requiere npm 10 o superior. Detectado: $npmVersionText."
}

# Consultar los SDK instalados desde una carpeta temporal para evitar que
# global.json interfiera con el inventario. Capturar stdout/stderr con Process
# evita que Windows PowerShell 5 convierta stderr de dotnet.exe en una excepción.
function Invoke-NativeCapture {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string]$Arguments,
        [Parameter(Mandatory = $true)][string]$WorkingDirectory
    )

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $FilePath
    $startInfo.Arguments = $Arguments
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    [void]$process.Start()
    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()

    return [pscustomobject]@{
        ExitCode = $process.ExitCode
        StdOut = $stdout
        StdErr = $stderr
    }
}

$dotnetCommand = Get-Command 'dotnet.exe' -ErrorAction SilentlyContinue
if (-not $dotnetCommand) {
    $dotnetCommand = Get-Command 'dotnet' -ErrorAction SilentlyContinue
}
if (-not $dotnetCommand) {
    throw 'No se encontró dotnet.exe. Instala .NET 10 SDK x64 y vuelve a abrir PowerShell: https://dotnet.microsoft.com/en-us/download/dotnet/10.0.'
}

$tempDirectory = [System.IO.Path]::GetTempPath()
$sdkInventory = Invoke-NativeCapture -FilePath $dotnetCommand.Source -Arguments '--list-sdks' -WorkingDirectory $tempDirectory
if ($sdkInventory.ExitCode -ne 0) {
    $detail = (($sdkInventory.StdOut + "`n" + $sdkInventory.StdErr).Trim())
    throw "No se pudo consultar los SDK de .NET instalados. Salida de dotnet --list-sdks:`n$detail"
}

$installedSdkVersions = @()
foreach ($line in ($sdkInventory.StdOut -split "`r?`n")) {
    if ($line -match '^\s*(?<version>\d+\.\d+\.\d+)\s+\[') {
        $installedSdkVersions += [version]$Matches['version']
    }
}
$supportedSdkVersions = @($installedSdkVersions | Where-Object { $_.Major -eq 10 -and $_ -ge [version]'10.0.100' } | Sort-Object -Descending)
if ($supportedSdkVersions.Count -eq 0) {
    if ($installedSdkVersions.Count -gt 0) {
        $detectedSdks = (($installedSdkVersions | Sort-Object -Descending | ForEach-Object { $_.ToString() }) -join ', ')
    }
    else {
        $detectedSdks = 'No se detectó ningún SDK (puede que solo esté instalado el Runtime).'
    }
    throw "JACKSTAFF necesita .NET SDK 10.0.100 o superior. SDK detectados: $detectedSdks.`nInstala el SDK x64, no solo el Runtime. Puedes ejecutar: winget install Microsoft.DotNet.SDK.10`nDescarga oficial: https://dotnet.microsoft.com/en-us/download/dotnet/10.0`nDespués cierra y vuelve a abrir PowerShell."
}

# Ahora que sabemos que existe un SDK compatible, comprobar que global.json
# puede seleccionarlo desde la raíz del proyecto.
$dotnetProbe = Invoke-NativeCapture -FilePath $dotnetCommand.Source -Arguments '--version' -WorkingDirectory $root
$dotnetVersionText = $dotnetProbe.StdOut.Trim()
if ($dotnetProbe.ExitCode -ne 0 -or -not $dotnetVersionText) {
    $detail = (($dotnetProbe.StdOut + "`n" + $dotnetProbe.StdErr).Trim())
    throw "Se detectó .NET SDK $($supportedSdkVersions[0]), pero dotnet no pudo seleccionarlo dentro del proyecto. Revisa global.json. Detalle:`n$detail"
}
try {
    $dotnetVersion = [version]$dotnetVersionText
}
catch {
    throw "dotnet --version devolvió un valor inesperado: '$dotnetVersionText'."
}
if ($dotnetVersion.Major -ne 10 -or $dotnetVersion -lt [version]'10.0.100') {
    throw "El proyecto requiere .NET SDK 10.0.100 o superior dentro de la versión 10. Detectado: $dotnetVersionText. SDK 10 detectados: $(($supportedSdkVersions -join ', '))."
}

Write-Host "Node.js $nodeVersionText | npm $npmVersionText | .NET SDK $dotnetVersionText" -ForegroundColor Green

# Verificar PostgreSQL y el esquema demo usando las credenciales locales de desarrollo.
$psql = Get-JackstaffPsqlPath
$env:PGPASSWORD = Get-JackstaffAppDbPassword
try {
    $databaseCheck = & $psql -X -h 127.0.0.1 -p 5432 -U jackstaff_app -d jackstaff -tA -c "SELECT CASE WHEN EXISTS (SELECT 1 FROM schema_migrations WHERE version = '001_initial_v0_1') THEN 'ready' ELSE 'missing' END;" 2>&1
    if ($LASTEXITCODE -ne 0 -or (($databaseCheck | Out-String).Trim() -ne 'ready')) {
        throw 'La base de datos JACKSTAFF no está lista.'
    }
}
catch {
    Write-Host ''
    Write-Host 'No se pudo validar la base de datos JACKSTAFF.' -ForegroundColor Yellow
    Write-Host 'Asegúrate de que el servicio PostgreSQL esté iniciado y ejecuta primero:' -ForegroundColor Yellow
    Write-Host '  .\scripts\Setup-Database.ps1' -ForegroundColor White
    throw $_
}
finally {
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
}

# Evitar lanzar procesos duplicados o chocar con otros servicios locales.
foreach ($port in @(8080, 5173)) {
    $listener = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($listener) {
        throw "El puerto $port ya está ocupado (PID $($listener.OwningProcess)). Cierra el proceso que lo utiliza y vuelve a intentarlo."
    }
}

Write-Host 'Restaurando paquetes de la API...' -ForegroundColor Cyan
Push-Location (Join-Path $root 'api')
try {
    dotnet restore Jackstaff.sln
    if ($LASTEXITCODE -ne 0) { throw 'Falló dotnet restore.' }
}
finally { Pop-Location }

$webPath = Join-Path $root 'web'
if (-not (Test-Path (Join-Path $webPath 'node_modules'))) {
    Write-Host 'Instalando paquetes del frontend (primera ejecución)...' -ForegroundColor Cyan
    Push-Location $webPath
    try {
        npm install
        if ($LASTEXITCODE -ne 0) { throw 'Falló npm install.' }
    }
    finally { Pop-Location }
}

$apiRunner = Join-Path $root 'scripts\Run-Api.ps1'
$webRunner = Join-Path $root 'scripts\Run-Web.ps1'
$currentShell = (Get-Process -Id $PID).Path
if (-not $currentShell) { $currentShell = (Get-Command powershell.exe).Source }

$apiArguments = "-NoExit -ExecutionPolicy Bypass -File `"$apiRunner`""
$webArguments = "-NoExit -ExecutionPolicy Bypass -File `"$webRunner`""
Start-Process -FilePath $currentShell -ArgumentList $apiArguments -WorkingDirectory $root
Start-Sleep -Seconds 2
Start-Process -FilePath $currentShell -ArgumentList $webArguments -WorkingDirectory $root

Write-Host ''
Write-Host 'JACKSTAFF se está iniciando en dos ventanas de PowerShell.' -ForegroundColor Green
Write-Host 'Aplicación: http://localhost:5173'
Write-Host 'API health: http://localhost:8080/api/health'
Write-Host 'OpenAPI:    http://localhost:8080/openapi/v1.json'
Write-Host ''
Write-Host 'Para detener cada proceso, usa Ctrl+C en su ventana de PowerShell.'
