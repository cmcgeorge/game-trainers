#Requires -Version 5.1
<#
.SYNOPSIS
    Builds and launches the Syndicate Plus Trainer (WPF).

.DESCRIPTION
    Compiles Trainer\SyndicateTrainer\SyndicateTrainer.csproj in Release/x64 and then
    starts the resulting GUI. Can be run from any working directory.

    Start Syndicate Plus (in DOSBox-X) first, then run this script; in the trainer,
    pick the DOSBox process and click Attach.

.PARAMETER Configuration
    Build configuration: Release (default) or Debug.

.PARAMETER NoRun
    Build only; do not launch the trainer.

.EXAMPLE
    .\Run.ps1
    Builds in Release and launches the trainer.

.EXAMPLE
    .\Run.ps1 -Configuration Debug -NoRun
    Builds a Debug binary without launching.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [switch]$NoRun
)

$ErrorActionPreference = 'Stop'

$root    = $PSScriptRoot
$project = Join-Path $root 'Trainer\SyndicateTrainer\SyndicateTrainer.csproj'

if (-not (Test-Path -LiteralPath $project)) {
    throw "Project not found: $project"
}

# --- Locate the .NET SDK ---
$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet) {
    $fallback = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
    if (Test-Path -LiteralPath $fallback) {
        $dotnet = $fallback
    }
    else {
        throw "The .NET SDK ('dotnet') was not found on PATH. Install it from https://dotnet.microsoft.com/download"
    }
}

# --- Build ---
Write-Host "Building SyndicateTrainer ($Configuration, x64)..." -ForegroundColor Cyan
& $dotnet build $project -c $Configuration -p:Platform=x64 --nologo
if ($LASTEXITCODE -ne 0) {
    throw "Build failed (exit code $LASTEXITCODE)."
}

if ($NoRun) {
    Write-Host "Build complete (-NoRun specified; not launching)." -ForegroundColor Green
    return
}

# --- Locate the freshly built executable ---
$binDir   = Join-Path $root 'Trainer\SyndicateTrainer\bin'
$expected = Join-Path $binDir "x64\$Configuration\net8.0-windows\SyndicateTrainer.exe"

if (Test-Path -LiteralPath $expected) {
    $exePath = $expected
}
else {
    # Fall back to the newest matching exe under the project's bin folder.
    $exe = Get-ChildItem -Path $binDir -Recurse -Filter 'SyndicateTrainer.exe' -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -like "*\$Configuration\*" } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if (-not $exe) {
        throw "Could not find SyndicateTrainer.exe under $binDir after the build."
    }
    $exePath = $exe.FullName
}

# --- Launch ---
Write-Host "Launching $exePath" -ForegroundColor Green
Start-Process -FilePath $exePath
Write-Host "Trainer started. If you hit 'Access denied' attaching, re-run PowerShell as Administrator." -ForegroundColor DarkGray
