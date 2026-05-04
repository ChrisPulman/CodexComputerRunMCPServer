param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $ProjectRoot 'src\CodexComputerRunMCPServer\CodexComputerRunMCPServer.csproj'

if (-not $IsWindows) {
    throw 'CodexComputerRunMCPServer is Windows-only. Run this from Windows PowerShell.'
}

dotnet run --project $Project --configuration $Configuration --no-launch-profile
