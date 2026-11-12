function Get-JackstaffPsqlPath {
    $command = Get-Command 'psql.exe' -ErrorAction SilentlyContinue
    if (-not $command) {
        $command = Get-Command 'psql' -ErrorAction SilentlyContinue
    }
    if ($command) {
        return $command.Source
    }

    $postgresRoot = Join-Path $env:ProgramFiles 'PostgreSQL'
    $preferred = Join-Path $postgresRoot '17\bin\psql.exe'
    if (Test-Path $preferred) {
        return $preferred
    }

    if (Test-Path $postgresRoot) {
        $candidates = Get-ChildItem -Path $postgresRoot -Directory -ErrorAction SilentlyContinue |
            ForEach-Object { Join-Path $_.FullName 'bin\psql.exe' } |
            Where-Object { Test-Path $_ }
        if ($candidates) {
            return ($candidates | Select-Object -First 1)
        }
    }

    throw 'No se encontró psql.exe. Instala PostgreSQL 17 con las herramientas de línea de comandos o agrega su carpeta bin al PATH.'
}

function ConvertFrom-SecureStringToPlainText {
    param([Parameter(Mandatory = $true)][Security.SecureString]$SecureValue)

    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureValue)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    }
}
function Get-JackstaffAppDbPassword {
    $settingsPath = Join-Path $PSScriptRoot '..\api\Jackstaff.Api\appsettings.Development.json'

    if (-not (Test-Path $settingsPath)) {
        throw "No se encontró la configuración local: $settingsPath"
    }

    try {
        $settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
        $connectionString = [string]$settings.Database.ConnectionString

        $builder = New-Object System.Data.Common.DbConnectionStringBuilder
        $property = $builder.GetType().GetProperty("ConnectionString")
        $property.SetValue($builder, $connectionString)

        $passwordKey = @($builder.Keys | Where-Object {
            $_.ToString() -in @('Password', 'Pwd')
        }) | Select-Object -First 1

        if (-not $passwordKey) {
            throw 'No se encontró Password en la cadena de conexión.'
        }

        $password = [string]$builder[$passwordKey]

        if ([string]::IsNullOrWhiteSpace($password)) {
            throw 'La contraseña de la base de datos está vacía.'
        }

        return $password
    }
    catch {
        throw "No se pudo recuperar la contraseña local de JACKSTAFF: $($_.Exception.Message)"
    }
}