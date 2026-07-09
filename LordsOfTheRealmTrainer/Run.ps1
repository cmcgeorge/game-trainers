#Requires -Version 5.1
<#
.SYNOPSIS
    Build and run the Lords of the Realm trainer (WPF / .NET 9).

.DESCRIPTION
    Restores, builds, and launches Trainer\LordsTrainer.csproj.
    Attach to a running DOSBox-X instance from within the app once it opens.

.PARAMETER Configuration
    Build configuration: Release (default) or Debug.

.PARAMETER NoBuild
    Skip building; launch the most recent build directly.

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
Set-StrictMode -Version Latest

$root    = $PSScriptRoot
$project = Join-Path $root 'Trainer\LordsTrainer.csproj'
$tfm     = 'net9.0-windows'
$exePath = Join-Path $root "Trainer\bin\$Configuration\$tfm\LordsOfTheRealmTrainer.exe"

function Write-Step($msg) { Write-Host "==> $msg" -ForegroundColor Cyan }

if (-not (Test-Path $project)) {
    throw "Cannot find project at '$project'. Run this script from the repository root."
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "The .NET SDK ('dotnet') was not found on PATH. Install .NET 8 or 9 from https://dotnet.microsoft.com/download"
}

if ($Clean) {
    Write-Step 'Cleaning bin/ and obj/'
    foreach ($d in @('bin', 'obj')) {
        $p = Join-Path $root "Trainer\$d"
        if (Test-Path $p) { Remove-Item $p -Recurse -Force }
    }
}

if (-not $NoBuild) {
    Write-Step "Building ($Configuration)"
    dotnet build $project -c $Configuration -v minimal
    if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE." }
}

if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Executable not found at '$exePath'. Build the trainer first (drop -NoBuild)."
}

Write-Step 'Launching trainer'
Write-Host "A UAC prompt will appear — the trainer needs admin rights to access the game's memory." -ForegroundColor Yellow
Start-Process -FilePath $exePath
Write-Step 'Started. (In the app: press "Attach to DOSBox-X" with the game running.)'
