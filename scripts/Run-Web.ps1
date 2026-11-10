$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location (Join-Path $root 'web')

Write-Host 'Iniciando JACKSTAFF Web en http://localhost:5173' -ForegroundColor Cyan
npm run dev
