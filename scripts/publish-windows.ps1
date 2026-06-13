param(
    [ValidateSet('win-x64', 'win-arm64', 'linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64')]
    [string]$Runtime = 'win-x64',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $ProjectRoot 'src\CodexComputerRunMCPServer\CodexComputerRunMCPServer.csproj'
$Output = Join-Path $ProjectRoot "artifacts\publish\$Runtime"

dotnet publish $Project --configuration $Configuration --runtime $Runtime --self-contained false --output $Output
Write-Host "Published to $Output"
