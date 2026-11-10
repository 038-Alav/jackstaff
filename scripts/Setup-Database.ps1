[CmdletBinding()]
param(
    [string]$HostName = '127.0.0.1',
    [int]$Port = 5432,
    [string]$Database = 'jackstaff',
    [string]$AdminUser = 'postgres',
    [switch]$Recreate
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')

if ($Database -cne 'jackstaff') {
    throw "Esta distribución local usa el nombre de base de datos fijo 'jackstaff' para coincidir con la configuración de la API."
}

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$schemaFile = Join-Path $root 'db\init\001_schema_and_demo.sql'
if (-not (Test-Path $schemaFile)) {
    throw "No se encontró el archivo de inicialización: $schemaFile"
}

$psql = Get-JackstaffPsqlPath
Write-Host "Herramienta PostgreSQL: $psql" -ForegroundColor Cyan
Write-Host "Servidor: ${HostName}:$Port | Base de datos: $Database | Usuario administrador: $AdminUser"

if ($Recreate) {
    Write-Host ''
    Write-Host 'ATENCIÓN: esto eliminará de forma permanente la base de datos JACKSTAFF y todos sus datos.' -ForegroundColor Yellow
    $confirmation = Read-Host "Escribe RESET para eliminar y reconstruir '$Database'"
    if ($confirmation -cne 'RESET') {
        Write-Host 'Operación cancelada. No se modificó la base de datos.' -ForegroundColor Yellow
        exit 1
    }
}

$securePassword = Read-Host "Contraseña del usuario PostgreSQL '$AdminUser'" -AsSecureString
$adminPassword = ConvertFrom-SecureStringToPlainText -SecureValue $securePassword
$env:PGPASSWORD = $adminPassword

function Invoke-JackstaffPsql {
    param(
        [Parameter(Mandatory = $true)][string]$DatabaseName,
        [Parameter(Mandatory = $true)][string]$Sql
    )

    $result = & $psql -X -v ON_ERROR_STOP=1 -h $HostName -p $Port -U $AdminUser -d $DatabaseName -tA -c $Sql 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Falló la consulta PostgreSQL en '$DatabaseName':`n$($result | Out-String)"
    }
    return (($result | Out-String).Trim())
}

try {
    $connectionTest = Invoke-JackstaffPsql -DatabaseName 'postgres' -Sql 'SELECT 1;'
    if ($connectionTest -ne '1') {
        throw 'No se pudo validar la conexión con PostgreSQL.'
    }

    $databaseExists = Invoke-JackstaffPsql -DatabaseName 'postgres' -Sql "SELECT CASE WHEN EXISTS (SELECT 1 FROM pg_database WHERE datname = '$Database') THEN 'true' ELSE 'false' END;"

    if ($Recreate -and $databaseExists -eq 'true') {
        [void](Invoke-JackstaffPsql -DatabaseName 'postgres' -Sql "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '$Database' AND pid <> pg_backend_pid();")
        [void](Invoke-JackstaffPsql -DatabaseName 'postgres' -Sql "DROP DATABASE IF EXISTS `"$Database`";")
        $databaseExists = 'false'
    }

    if ($databaseExists -ne 'true') {
        Write-Host "Creando base de datos '$Database'..." -ForegroundColor Cyan
        [void](Invoke-JackstaffPsql -DatabaseName 'postgres' -Sql "CREATE DATABASE `"$Database`";")
    }

    $hasMigrationTable = Invoke-JackstaffPsql -DatabaseName $Database -Sql "SELECT CASE WHEN to_regclass('public.schema_migrations') IS NULL THEN 'false' ELSE 'true' END;"

    if ($hasMigrationTable -eq 'true') {
        $migrationApplied = Invoke-JackstaffPsql -DatabaseName $Database -Sql "SELECT CASE WHEN EXISTS (SELECT 1 FROM schema_migrations WHERE version = '001_initial_v0_1') THEN 'true' ELSE 'false' END;"
        if ($migrationApplied -eq 'true') {
            Write-Host 'El esquema inicial ya está aplicado. Se conservarán los datos existentes.' -ForegroundColor Green
        }
        else {
            throw "La base '$Database' contiene schema_migrations, pero no registra la migración inicial. No se modificó para evitar sobrescribir un estado parcial. Para reconstruir la demo usa scripts\reset-demo.ps1."
        }
    }
    else {
        Write-Host 'Aplicando esquema, políticas de seguridad y datos de demostración...' -ForegroundColor Cyan
        & $psql -X -v ON_ERROR_STOP=1 -h $HostName -p $Port -U $AdminUser -d $Database -f $schemaFile
        if ($LASTEXITCODE -ne 0) {
            throw 'No se pudo aplicar el esquema inicial. Revisa el error de PostgreSQL mostrado arriba y confirma que PostGIS esté instalado para PostgreSQL 17.'
        }
    }

    # El rol de aplicación es exclusivo de esta demo local y usa la clave definida en appsettings.json.
    $appDbPassword = Get-JackstaffAppDbPassword
    $escapedAppDbPassword = $appDbPassword.Replace("'", "''")

    [void](Invoke-JackstaffPsql -DatabaseName $Database -Sql "ALTER ROLE jackstaff_app WITH LOGIN PASSWORD '$escapedAppDbPassword' NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOBYPASSRLS;")

    $postgisVersion = Invoke-JackstaffPsql -DatabaseName $Database -Sql "SELECT extversion FROM pg_extension WHERE extname = 'postgis';"
    if (-not $postgisVersion) {
        throw 'La extensión PostGIS no aparece instalada en la base de datos. Instala el paquete PostGIS correspondiente a PostgreSQL 17 y vuelve a ejecutar este script.'
    }

    Write-Host ''
    Write-Host 'Base de datos JACKSTAFF preparada correctamente.' -ForegroundColor Green
    Write-Host "PostGIS: $postgisVersion"
    Write-Host 'La contraseña de administrador no se guardó en los archivos del proyecto.'
}
finally {
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    $adminPassword = $null
    $securePassword = $null
    $appDbPassword = $null
    $escapedAppDbPassword = $null
}
