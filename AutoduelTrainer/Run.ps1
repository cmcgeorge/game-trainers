#Requires -Version 5.1
<#
.SYNOPSIS
    Builds and runs the Autoduel Trainer WPF application.

.DESCRIPTION
    Restores, builds, and launches the trainer found in the Trainer\ subfolder.
    Attach it to a running DOSBox-X instance with AUTODUEL loaded (past the
    title screen) to read and edit the live game state.

.PARAMETER Configuration
    Build configuration: Debug or Release (default Release).

.PARAMETER NoRun
    Build only; do not launch the application.

.PARAMETER Clean
    Remove bin\ and obj\ before building.

.EXAMPLE
    .\Run.ps1
    Builds in Release and launches the trainer.

.EXAMPLE
    .\Run.ps1 -Configuration Debug
    Builds in Debug and launches the trainer.

.EXAMPLE
    .\Run.ps1 -NoRun
    Builds only, without launching.
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

$root        = $PSScriptRoot
$projectDir  = Join-Path $root 'Trainer'
$projectFile = Join-Path $projectDir 'AutoduelTrainer.csproj'
$tfm         = 'net9.0-windows'
$exePath     = Join-Path $projectDir "bin\$Configuration\$tfm\AutoduelTrainer.exe"

function Write-Step($msg) { Write-Host "==> $msg" -ForegroundColor Cyan }

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "The .NET SDK ('dotnet') was not found on PATH. Install .NET 9 SDK from https://dotnet.microsoft.com/download"
}

if (-not (Test-Path -LiteralPath $projectFile)) {
    throw "Project not found at '$projectFile'. Run this script from the solution root."
}

if ($Clean) {
    Write-Step "Cleaning bin\ and obj\"
    foreach ($dir in @('bin', 'obj')) {
        $path = Join-Path $projectDir $dir
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Recurse -Force
        }
    }
}

Write-Step "Building ($Configuration)"
dotnet build $projectFile -c $Configuration -v minimal
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
Write-Host "A UAC prompt will appear — the trainer needs admin rights to access the game's memory." -ForegroundColor Yellow
Start-Process -FilePath $exePath
Write-Step "Started. (Launch AUTODUEL in DOSBox-X past the title screen, then Attach in the trainer.)"
