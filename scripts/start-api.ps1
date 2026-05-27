[CmdletBinding()]
param(
    [int]$Port = 5085
)

$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
$Project = Join-Path $Root "SparkVision.Api\SparkVision.Api.csproj"

Push-Location $Root
try {
    $env:ASPNETCORE_URLS = "http://127.0.0.1:$Port"
    $env:DOTNET_ROLL_FORWARD = "Major"
    dotnet run --project $Project
}
finally {
    Pop-Location
}
