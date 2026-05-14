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
    throw "Docker Desktop n'est pas disponible dans le PATH."
}

docker info *> $null
if ($LASTEXITCODE -ne 0) {
    throw "Docker Desktop est installe, mais le moteur Docker n'est pas demarre. Ouvre Docker Desktop, attends qu'il soit pret, puis relance ce script."
}

$DbName = Get-SparkVisionSetting -Name "SPARKVISION_POSTGRES_DB" -DefaultValue "sparkvision"
$DbUser = Get-SparkVisionSetting -Name "SPARKVISION_POSTGRES_USER" -DefaultValue "sparkvision_user"

Push-Location $Root
try {
    docker compose -f $ComposeFile exec -T db psql -U $DbUser -d $DbName -f /demo/demo_queries.sql
    if ($LASTEXITCODE -ne 0) {
        throw "Impossible d'executer les requetes SQL de demonstration."
    }
}
finally {
    Pop-Location
}
