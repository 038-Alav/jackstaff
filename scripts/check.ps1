$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..")

Push-Location (Join-Path $Root "api")
try {
    dotnet restore Jackstaff.sln
    dotnet test Jackstaff.sln --no-restore
}
finally { Pop-Location }

Push-Location (Join-Path $Root "web")
try {
    npm install
    npm run build
}
finally { Pop-Location }

Write-Host "JACKSTAFF v0.1 checks completed."
