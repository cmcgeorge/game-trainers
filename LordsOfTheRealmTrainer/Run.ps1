#requires -Version 5.1
<#
.SYNOPSIS
    Build and run the Lords of the Realm trainer (WPF / .NET 9).

.DESCRIPTION
    Restores, builds, and launches Trainer\LordsTrainer.csproj.
    Attach to a running DOSBox-X instance from within the app once it opens.

.PARAMETER Configuration
    Build configuration: Release (default) or Debug.

.PARAMETER NoBuild
    Skip building; just run the most recent build.

.PARAMETER Clean
    Remove bin/ and obj/ before building.

.EXAMPLE
    .\Run.ps1
.EXAMPLE
    .\Run.ps1 -Configuration Debug
.EXAMPLE
    .\Run.ps1 -NoBuild
#>
[CmdletBinding()]
param(
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',
    [switch]$NoBuild,
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
$root    = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root 'Trainer\LordsTrainer.csproj'

function Write-Step($msg) { Write-Host "==> $msg" -ForegroundColor Cyan }

if (-not (Test-Path $project)) {
    throw "Cannot find project at '$project'. Run this script from the repository root."
}

# --- Verify the .NET SDK is available -------------------------------------------------
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    throw "The .NET SDK ('dotnet') was not found on PATH. Install .NET 8 or 9 from https://dotnet.microsoft.com/download"
}
$sdkVersion = (& dotnet --version).Trim()
Write-Step "Using .NET SDK $sdkVersion"

# --- Optional clean -------------------------------------------------------------------
if ($Clean) {
    Write-Step 'Cleaning bin/ and obj/'
    foreach ($d in @('bin', 'obj')) {
        $p = Join-Path $root "Trainer\$d"
        if (Test-Path $p) { Remove-Item $p -Recurse -Force }
    }
}

# --- Build ----------------------------------------------------------------------------
if (-not $NoBuild) {
    Write-Step "Building ($Configuration)"
    & dotnet build $project -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE." }
}

# --- Run ------------------------------------------------------------------------------
Write-Step 'Launching trainer'
Write-Host '    (In the app: press "Attach to DOSBox-X" with the game running.)' -ForegroundColor DarkGray
& dotnet run --project $project -c $Configuration --no-build
exit $LASTEXITCODE
