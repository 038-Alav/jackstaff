$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $root

Write-Host 'Iniciando JACKSTAFF API en http://localhost:8080' -ForegroundColor Cyan
dotnet run --no-restore --project (Join-Path $root 'api\Jackstaff.Api\Jackstaff.Api.csproj') --launch-profile Jackstaff.Api
