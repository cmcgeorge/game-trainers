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

# Always operate relative to this script's own location, not the caller's cwd.
$root       = $PSScriptRoot
$projectDir = Join-Path $root 'Trainer'
$projectFile = Join-Path $projectDir 'AutoduelTrainer.csproj'

Write-Host 'Autoduel Trainer - build & run' -ForegroundColor Yellow
Write-Host "  Configuration : $Configuration"
Write-Host "  Project       : $projectFile"
Write-Host ''

# --- prerequisites -----------------------------------------------------------
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    throw "The .NET SDK ('dotnet') was not found on PATH. Install .NET 9 SDK from https://dotnet.microsoft.com/download"
}

if (-not (Test-Path -LiteralPath $projectFile)) {
    throw "Project not found at '$projectFile'. Run this script from the solution root."
}

# --- optional clean ----------------------------------------------------------
if ($Clean) {
    Write-Host 'Cleaning bin\ and obj\ ...' -ForegroundColor Cyan
    foreach ($dir in @('bin', 'obj')) {
        $path = Join-Path $projectDir $dir
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Recurse -Force
        }
    }
}

# --- build -------------------------------------------------------------------
Write-Host 'Building ...' -ForegroundColor Cyan
& dotnet build $projectFile -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) {
    throw "Build failed (dotnet exit code $LASTEXITCODE)."
}
Write-Host 'Build succeeded.' -ForegroundColor Green

if ($NoRun) {
    Write-Host 'NoRun specified - skipping launch.'
    return
}

# --- locate and launch the built executable ----------------------------------
# Prefer the produced .exe so the app runs detached from this shell.
$exe = Get-ChildItem -LiteralPath $projectDir -Recurse -Filter 'AutoduelTrainer.exe' -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match [regex]::Escape("\bin\$Configuration\") } |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

Write-Host ''
if ($exe) {
    Write-Host "Launching $($exe.FullName)" -ForegroundColor Green
    Start-Process -FilePath $exe.FullName
} else {
    # Fall back to 'dotnet run' if the exe could not be located. Launch it
    # detached (Start-Process) so this behaves like the primary path and does
    # not block the calling shell for the app's lifetime.
    Write-Host 'Executable not found; falling back to dotnet run ...' -ForegroundColor Yellow
    Start-Process -FilePath 'dotnet' `
        -ArgumentList @('run', '--project', $projectFile, '-c', $Configuration, '--no-build')
}
