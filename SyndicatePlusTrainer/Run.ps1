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

.PARAMETER Clean
    Delete bin/obj for the project before building.

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

    [switch]$NoRun,

    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root    = $PSScriptRoot
$project = Join-Path $root 'Trainer\SyndicateTrainer\SyndicateTrainer.csproj'
$tfm     = 'net8.0-windows'
$exePath = Join-Path $root "Trainer\SyndicateTrainer\bin\x64\$Configuration\$tfm\SyndicateTrainer.exe"

function Write-Step($msg) { Write-Host "==> $msg" -ForegroundColor Cyan }

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "The .NET SDK ('dotnet') was not found on PATH. Install it from https://dotnet.microsoft.com/download"
}

if (-not (Test-Path -LiteralPath $project)) {
    throw "Project not found at '$project'. Run this script from the repository root."
}

if ($Clean) {
    Write-Step "Cleaning bin/obj"
    Get-ChildItem $root -Recurse -Directory -Include bin, obj |
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Step "Building ($Configuration, x64)"
dotnet build $project -c $Configuration -p:Platform=x64 -v minimal
if ($LASTEXITCODE -ne 0) { throw "Build failed (exit code $LASTEXITCODE)." }

if ($NoRun) {
    Write-Step "Build complete. Skipping launch (-NoRun)."
    Write-Host "Executable: $exePath"
    return
}

if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Build succeeded but the executable was not found at '$exePath'."
}

Write-Step "Launching the trainer"
Write-Host "A UAC prompt may appear if DOSBox-X is running as Administrator." -ForegroundColor Yellow
Start-Process -FilePath $exePath
Write-Step "Started. (Select the DOSBox process and click Attach in the trainer.)"
