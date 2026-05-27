[CmdletBinding()]
param(
    [switch]$Reset
)

$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
$ComposeFile = Join-Path $Root "docker-compose.sql.yml"
$EnvFile = Join-Path $Root ".env"

function Get-SparkVisionSetting {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string]$DefaultValue
    )

    $processValue = [Environment]::GetEnvironmentVariable($Name)
    if (-not [string]::IsNullOrWhiteSpace($processValue)) {
        return $processValue
    }

    if (Test-Path $EnvFile) {
        $line = Get-Content $EnvFile | Where-Object { $_ -match "^\s*$Name\s*=" } | Select-Object -First 1
        if ($line) {
            return ($line -replace "^\s*$Name\s*=\s*", "").Trim().Trim('"').Trim("'")
        }
    }

    return $DefaultValue
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Docker Desktop n'est pas disponible dans le PATH. Lance Docker Desktop puis relance ce script."
}

docker info *> $null
if ($LASTEXITCODE -ne 0) {
    throw "Docker Desktop est installe, mais le moteur Docker n'est pas demarre. Ouvre Docker Desktop, attends qu'il soit pret, puis relance ce script."
}

$DbName = Get-SparkVisionSetting -Name "SPARKVISION_POSTGRES_DB" -DefaultValue "sparkvision"
$DbUser = Get-SparkVisionSetting -Name "SPARKVISION_POSTGRES_USER" -DefaultValue "sparkvision_user"
$DbPassword = Get-SparkVisionSetting -Name "SPARKVISION_POSTGRES_PASSWORD" -DefaultValue "SparkVision123!"
$DbPort = Get-SparkVisionSetting -Name "SPARKVISION_POSTGRES_PORT" -DefaultValue "5433"
$ApiPort = Get-SparkVisionSetting -Name "SPARKVISION_API_PORT" -DefaultValue "8080"

Push-Location $Root
try {
    if ($Reset) {
        docker compose -f $ComposeFile down -v
        if ($LASTEXITCODE -ne 0) {
            throw "Impossible de reinitialiser les conteneurs Docker."
        }
    }

    docker compose -f $ComposeFile up -d
    if ($LASTEXITCODE -ne 0) {
        throw "Impossible de demarrer la base SQL Docker."
    }

    Write-Host "Attente de PostgreSQL..."
    $ready = $false
    for ($i = 1; $i -le 30; $i++) {
        docker compose -f $ComposeFile exec -T db pg_isready -U $DbUser -d $DbName *> $null
        if ($LASTEXITCODE -eq 0) {
            $ready = $true
            break
        }

        Start-Sleep -Seconds 2
    }

    if (-not $ready) {
        docker compose -f $ComposeFile logs db
        throw "PostgreSQL n'est pas pret apres 60 secondes."
    }

    docker compose -f $ComposeFile exec -T db psql -U $DbUser -d $DbName -c "SELECT target_table, rows_imported FROM import_runs ORDER BY id;"
    if ($LASTEXITCODE -ne 0) {
        throw "La base est lancee, mais la verification SQL a echoue."
    }

    Write-Host ""
    Write-Host "Base SQL prete."
    Write-Host "Connexion: Host=localhost;Port=$DbPort;Database=$DbName;Username=$DbUser;Password=$DbPassword"
    Write-Host "API Swagger: http://127.0.0.1:$ApiPort/swagger"
    Write-Host "Requetes demo: .\scripts\show-sql-demo.ps1"
}
finally {
    Pop-Location
}
