$ErrorActionPreference = 'Stop'
$setupScript = Join-Path $PSScriptRoot 'Setup-Database.ps1'
& $setupScript -Recreate
if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
