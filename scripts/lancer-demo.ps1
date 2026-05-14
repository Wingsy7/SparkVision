[CmdletBinding()]
param(
    [switch]$WithSqlDocker
)

$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
$Project = Join-Path $Root "SparkVision.WinForms\SparkVision.WinForms.csproj"

Push-Location $Root
try {
    if ($WithSqlDocker) {
        & (Join-Path $PSScriptRoot "start-docker-sql.ps1")
    }

    $env:DOTNET_ROLL_FORWARD = "Major"
    dotnet restore $Project
    if ($LASTEXITCODE -ne 0) {
        throw "La restauration NuGet a echoue."
    }

    dotnet build $Project
    if ($LASTEXITCODE -ne 0) {
        throw "Le build a echoue."
    }

    dotnet run --project $Project
}
finally {
    Pop-Location
}
